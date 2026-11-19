import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/services/session_event_bus.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_providers.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_state.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/login_controller.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/test_doubles.dart';

/// Verifies the separation that `LoginController` exists to provide: a failed
/// sign-in must stay local to the form and must not disturb the session
/// provider the router watches.
void main() {
  late MockAuthRepository repository;
  late InMemoryPreferencesService preferences;

  setUpAll(registerCommonFallbacks);

  setUp(() {
    repository = MockAuthRepository();
    preferences = InMemoryPreferencesService();

    // No stored session, so AuthController settles on unauthenticated.
    when(
      repository.restoreSession,
    ).thenAnswer((_) async => const Result<AuthenticatedUser?>.success(null));
  });

  ProviderContainer makeContainer() {
    final ProviderContainer container = ProviderContainer(
      overrides: [
        appConfigProvider.overrideWithValue(testConfig),
        preferencesServiceProvider.overrideWithValue(preferences),
        deviceInfoProvider.overrideWithValue(
          const DeviceInfo(
            deviceId: 'device-1',
            deviceName: 'Test Device',
            appVersion: '1.0.0+1',
            platform: 'android',
          ),
        ),
        authRepositoryProvider.overrideWithValue(repository),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  Future<ProviderContainer> settledContainer() async {
    final ProviderContainer container = makeContainer();
    // Let AuthController.build() finish so the session starts resolved.
    await container.read(authControllerProvider.future);
    return container;
  }

  group('submit', () {
    test('starts idle', () async {
      final ProviderContainer container = await settledContainer();

      expect(container.read(loginControllerProvider), isA<SubmissionIdle>());
    });

    test('promotes a successful sign-in to the session', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.success(TestData.user),
      );

      final ProviderContainer container = await settledContainer();

      final bool succeeded = await container
          .read(loginControllerProvider.notifier)
          .submit(
            userName: 'EMP-00421',
            password: 'Password1',
            rememberMe: false,
          );

      expect(succeeded, isTrue);
      expect(container.read(loginControllerProvider).isSuccess, isTrue);

      final AuthState? auth = container.read(authControllerProvider).value;
      expect(auth, isA<AuthAuthenticated>());
      expect(auth!.userOrNull?.employeeId, 'EMP-00421');
    });

    test('keeps a failure local to the form', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.failure(
          AuthenticationFailure('Incorrect ID or password.'),
        ),
      );

      final ProviderContainer container = await settledContainer();

      final bool succeeded = await container
          .read(loginControllerProvider.notifier)
          .submit(userName: 'EMP-00421', password: 'wrong', rememberMe: false);

      expect(succeeded, isFalse);
      expect(
        container.read(loginControllerProvider).failureOrNull,
        isA<AuthenticationFailure>(),
      );

      // The point of the split: the session provider is untouched, so the
      // router does not move the user off the login screen.
      final AsyncValue<AuthState> session = container.read(
        authControllerProvider,
      );
      expect(session.hasError, isFalse);
      expect(session.value, isA<AuthUnauthenticated>());
    });

    test('surfaces server field errors for the form to render', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.failure(
          ValidationFailure(
            'Check your details.',
            fieldErrors: <String, List<String>>{
              'userName': <String>['No account with that employee ID.'],
            },
          ),
        ),
      );

      final ProviderContainer container = await settledContainer();

      await container
          .read(loginControllerProvider.notifier)
          .submit(
            userName: 'EMP-99999',
            password: 'Password1',
            rememberMe: false,
          );

      expect(
        container.read(loginControllerProvider).errorFor('userName'),
        'No account with that employee ID.',
      );
    });

    test('ignores a second submit while one is in flight', () async {
      // Guards against a double tap recording two sign-in attempts.
      final Completer<Result<AuthenticatedUser>> gate =
          Completer<Result<AuthenticatedUser>>();
      when(() => repository.login(any())).thenAnswer((_) => gate.future);

      final ProviderContainer container = await settledContainer();
      final LoginController controller = container.read(
        loginControllerProvider.notifier,
      );

      final Future<bool> first = controller.submit(
        userName: 'EMP-00421',
        password: 'Password1',
        rememberMe: false,
      );
      final bool second = await controller.submit(
        userName: 'EMP-00421',
        password: 'Password1',
        rememberMe: false,
      );

      expect(second, isFalse);

      gate.complete(const Result<AuthenticatedUser>.success(TestData.user));
      expect(await first, isTrue);

      verify(() => repository.login(any())).called(1);
    });

    test('reset clears a previous error', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.failure(
          AuthenticationFailure('nope'),
        ),
      );

      final ProviderContainer container = await settledContainer();
      final LoginController controller = container.read(
        loginControllerProvider.notifier,
      );

      await controller.submit(
        userName: 'EMP-00421',
        password: 'wrong',
        rememberMe: false,
      );
      expect(container.read(loginControllerProvider).failureOrNull, isNotNull);

      controller.reset();
      expect(container.read(loginControllerProvider), isA<SubmissionIdle>());
    });
  });

  group('remember me', () {
    test('stores the identifier when ticked', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.success(TestData.user),
      );

      final ProviderContainer container = await settledContainer();

      await container
          .read(loginControllerProvider.notifier)
          .submit(
            userName: '  EMP-00421 ',
            password: 'Password1',
            rememberMe: true,
          );

      expect(
        container.read(rememberedUserNameProvider),
        'EMP-00421',
        reason: 'the identifier should be trimmed before storing',
      );
    });

    test('removes a stored identifier when unticked', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.success(TestData.user),
      );

      final ProviderContainer container = await settledContainer();
      final LoginController controller = container.read(
        loginControllerProvider.notifier,
      );

      await controller.submit(
        userName: 'EMP-00421',
        password: 'Password1',
        rememberMe: true,
      );
      controller.reset();
      await controller.submit(
        userName: 'EMP-00421',
        password: 'Password1',
        rememberMe: false,
      );

      // Re-read through a fresh container so the provider is not memoised
      // from the earlier value.
      expect(preferences.getString(PreferenceKeys.rememberedUserName), isNull);
    });

    test('never stores the password', () async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.success(TestData.user),
      );

      final ProviderContainer container = await settledContainer();

      await container
          .read(loginControllerProvider.notifier)
          .submit(
            userName: 'EMP-00421',
            password: 'Password1',
            rememberMe: true,
          );

      // Preferences are unencrypted; a credential must never reach them.
      expect(
        preferences.getString(PreferenceKeys.rememberedUserName),
        isNot(contains('Password1')),
      );
    });
  });

  group('session expiry', () {
    test('an expired session is flagged so login can explain why', () async {
      final ProviderContainer container = await settledContainer();

      container.read(sessionEventBusProvider).emit(SessionEvent.expired);

      // Let the subscription deliver the event.
      await Future<void>.delayed(Duration.zero);

      final AuthState? auth = container.read(authControllerProvider).value;
      expect(auth, isA<AuthUnauthenticated>());
      expect((auth! as AuthUnauthenticated).wasSessionExpired, isTrue);
    });
  });
}
