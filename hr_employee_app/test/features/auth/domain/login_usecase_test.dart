import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/usecases/login_usecase.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/test_doubles.dart';

void main() {
  late MockAuthRepository repository;
  late LoginUseCase useCase;

  setUpAll(registerCommonFallbacks);

  setUp(() {
    repository = MockAuthRepository();
    useCase = LoginUseCase(repository);
  });

  LoginCredentials credentials({
    String userName = 'EMP-00421',
    String password = 'Password1',
  }) {
    return LoginCredentials(
      userName: userName,
      password: password,
      deviceId: 'device-1',
      deviceName: 'Test Device',
    );
  }

  void stubSuccess() {
    when(() => repository.login(any())).thenAnswer(
      (_) async => const Result<AuthenticatedUser>.success(TestData.user),
    );
  }

  group('normalisation', () {
    test('trims surrounding whitespace from the identifier', () async {
      stubSuccess();

      await useCase(credentials(userName: '  EMP-00421  '));

      final LoginCredentials sent =
          verify(() => repository.login(captureAny())).captured.single
              as LoginCredentials;
      expect(sent.userName, 'EMP-00421');
    });

    test('lower-cases an email so case cannot split an account', () async {
      stubSuccess();

      await useCase(credentials(userName: 'Aye.Chan@Example.COM'));

      final LoginCredentials sent =
          verify(() => repository.login(captureAny())).captured.single
              as LoginCredentials;
      expect(sent.userName, 'aye.chan@example.com');
    });

    test('preserves the case of an employee ID', () async {
      // The server may treat an employee ID as case-sensitive, and that is
      // not the client's call to make.
      stubSuccess();

      await useCase(credentials(userName: 'eMp-00421'));

      final LoginCredentials sent =
          verify(() => repository.login(captureAny())).captured.single
              as LoginCredentials;
      expect(sent.userName, 'eMp-00421');
    });

    test('never alters the password', () async {
      stubSuccess();

      await useCase(credentials(password: '  Secret 1  '));

      final LoginCredentials sent =
          verify(() => repository.login(captureAny())).captured.single
              as LoginCredentials;
      expect(sent.password, '  Secret 1  ');
    });
  });

  group('guards', () {
    test('rejects a blank identifier without calling the repository', () async {
      final Result<AuthenticatedUser> result = await useCase(
        credentials(userName: '   '),
      );

      expect(result.isFailure, isTrue);
      expect(result.failureOrNull, isA<ValidationFailure>());
      verifyNever(() => repository.login(any()));
    });

    test('rejects a blank password without calling the repository', () async {
      final Result<AuthenticatedUser> result = await useCase(
        credentials(password: ''),
      );

      expect(result.isFailure, isTrue);
      verifyNever(() => repository.login(any()));
    });
  });

  group('results', () {
    test('returns the user on success', () async {
      stubSuccess();

      final Result<AuthenticatedUser> result = await useCase(credentials());

      expect(result.isSuccess, isTrue);
      expect(result.requireValue.employeeId, 'EMP-00421');
    });

    test('passes a repository failure through unchanged', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.failure(
          AuthenticationFailure('Incorrect ID or password.'),
        ),
      );

      final Result<AuthenticatedUser> result = await useCase(credentials());

      expect(result.isFailure, isTrue);
      expect(result.failureOrNull, isA<AuthenticationFailure>());
      expect(result.failureOrNull!.message, 'Incorrect ID or password.');
    });
  });
}
