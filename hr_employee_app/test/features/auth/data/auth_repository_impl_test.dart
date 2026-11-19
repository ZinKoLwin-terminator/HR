import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/core/storage/secure_storage_service.dart';
import 'package:hr_employee_app/core/storage/token_storage.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_remote_datasource.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_request_models.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_user_model.dart';
import 'package:hr_employee_app/features/auth/data/models/login_response_model.dart';
import 'package:hr_employee_app/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/test_doubles.dart';

class MockAuthRemoteDataSource extends Mock implements AuthRemoteDataSource {}

/// Covers the repository's real job: coordinating the API, the token store
/// and the identity cache, and translating exceptions into failures.
void main() {
  late MockAuthRemoteDataSource remote;
  late AuthLocalDataSource local;
  late SecureStorageService storage;
  late TokenStorage tokenStorage;
  late AuthRepositoryImpl repository;

  setUpAll(() {
    registerCommonFallbacks();
    registerFallbackValue(
      const LoginRequestModel(
        userName: 'fallback',
        password: 'fallback',
        deviceId: 'fallback',
        deviceName: 'fallback',
      ),
    );
    registerFallbackValue(const LogoutRequestModel(refreshToken: 'fallback'));
    registerFallbackValue(const ForgotPasswordRequestModel(email: 'fallback'));
  });

  setUp(() {
    remote = MockAuthRemoteDataSource();
    // In-memory storage rather than a mock: the test exercises real
    // read-after-write behaviour, which is the point of these cases.
    storage = InMemorySecureStorageService();
    local = AuthLocalDataSourceImpl(storage);
    tokenStorage = TokenStorage(storage);

    repository = AuthRepositoryImpl(
      remote: remote,
      local: local,
      tokenStorage: tokenStorage,
      logger: silentLogger(),
    );
  });

  const AuthUserModel userModel = AuthUserModel(
    userId: 'u-1',
    employeeId: 'EMP-00421',
    fullName: 'Aye Chan Moe',
    email: 'aye.chan@example.com',
  );

  LoginResponseModel loginResponse({
    Duration accessTtl = const Duration(hours: 1),
    Duration refreshTtl = const Duration(days: 30),
    AuthUserModel? user = userModel,
  }) {
    final DateTime now = DateTime.now().toUtc();
    return LoginResponseModel(
      accessToken: 'access-token',
      refreshToken: 'refresh-token',
      accessTokenExpiresAt: now.add(accessTtl),
      refreshTokenExpiresAt: now.add(refreshTtl),
      user: user,
    );
  }

  const LoginCredentials credentials = LoginCredentials(
    userName: 'EMP-00421',
    password: 'Password1',
    deviceId: 'device-1',
    deviceName: 'Test Device',
  );

  group('login', () {
    test('persists tokens and the cached identity', () async {
      when(() => remote.login(any())).thenAnswer((_) async => loginResponse());

      final Result<AuthenticatedUser> result = await repository.login(
        credentials,
      );

      expect(result.isSuccess, isTrue);
      expect(result.requireValue.employeeId, 'EMP-00421');

      final AuthTokens? stored = await tokenStorage.read();
      expect(stored, isNotNull);
      expect(stored!.accessToken, 'access-token');
      expect(stored.refreshToken, 'refresh-token');
      expect(await local.readUser(), isNotNull);
    });

    test('converts a rejection into a ValidationFailure', () async {
      when(() => remote.login(any())).thenThrow(
        const ValidationException('Incorrect employee ID or password.'),
      );

      final Result<AuthenticatedUser> result = await repository.login(
        credentials,
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
      // Nothing may be persisted from a failed sign-in.
      expect(await tokenStorage.read(), isNull);
    });

    test('converts a network drop into a NetworkFailure', () async {
      when(() => remote.login(any())).thenThrow(const NetworkException());

      final Result<AuthenticatedUser> result = await repository.login(
        credentials,
      );

      expect(result.failureOrNull, isA<NetworkFailure>());
    });

    test('rolls back tokens when the response carries no user', () async {
      // A session with no identity cannot render the shell, so it must not
      // be left half-created.
      when(() => remote.login(any()))
          .thenAnswer((_) async => loginResponse(user: null));

      final Result<AuthenticatedUser> result = await repository.login(
        credentials,
      );

      expect(result.isFailure, isTrue);
      expect(await tokenStorage.read(), isNull);
    });
  });

  group('restoreSession', () {
    test('returns null when nothing is stored', () async {
      final Result<AuthenticatedUser?> result = await repository
          .restoreSession();

      expect(result.isSuccess, isTrue);
      expect(result.valueOrNull, isNull);
    });

    test('restores the cached user when the refresh token is valid', () async {
      when(() => remote.login(any())).thenAnswer((_) async => loginResponse());
      await repository.login(credentials);

      final Result<AuthenticatedUser?> result = await repository
          .restoreSession();

      expect(result.valueOrNull?.employeeId, 'EMP-00421');
    });

    test('restores even when the access token has expired', () async {
      // An expired access token is recoverable: the interceptor exchanges it
      // on the first request, so the user must not be signed out here.
      when(() => remote.login(any())).thenAnswer(
        (_) async => loginResponse(accessTtl: const Duration(seconds: -60)),
      );
      await repository.login(credentials);

      final Result<AuthenticatedUser?> result = await repository
          .restoreSession();

      expect(result.valueOrNull, isNotNull);
    });

    test('clears the session when the refresh token has lapsed', () async {
      when(() => remote.login(any())).thenAnswer(
        (_) async => loginResponse(refreshTtl: const Duration(days: -1)),
      );
      await repository.login(credentials);

      final Result<AuthenticatedUser?> result = await repository
          .restoreSession();

      expect(result.valueOrNull, isNull);
      expect(await tokenStorage.read(), isNull);
      expect(await local.readUser(), isNull);
    });

    test('clears tokens that have no cached identity', () async {
      await tokenStorage.save(
        AuthTokens(
          accessToken: 'a',
          refreshToken: 'r',
          accessTokenExpiresAt: DateTime.now().toUtc().add(
            const Duration(hours: 1),
          ),
          refreshTokenExpiresAt: DateTime.now().toUtc().add(
            const Duration(days: 30),
          ),
        ),
      );

      final Result<AuthenticatedUser?> result = await repository
          .restoreSession();

      expect(result.valueOrNull, isNull);
      expect(await tokenStorage.read(), isNull);
    });
  });

  group('logout', () {
    setUp(() async {
      when(() => remote.login(any())).thenAnswer((_) async => loginResponse());
      await repository.login(credentials);
    });

    test('clears local state after a successful server call', () async {
      when(() => remote.logout(any())).thenAnswer((_) async {});

      final Result<void> result = await repository.logout();

      expect(result.isSuccess, isTrue);
      expect(await tokenStorage.read(), isNull);
      expect(await local.readUser(), isNull);
    });

    test('clears local state even when the server call fails', () async {
      // Signing out must work offline; trapping the user in the app because
      // the network is down would be worse than a stale server session.
      when(() => remote.logout(any())).thenThrow(const NetworkException());

      final Result<void> result = await repository.logout();

      expect(result.isSuccess, isTrue);
      expect(await tokenStorage.read(), isNull);
      expect(await local.readUser(), isNull);
    });
  });

  group('AuthTokens expiry', () {
    AuthTokens tokens({
      required Duration accessTtl,
      required Duration refreshTtl,
    }) {
      final DateTime now = DateTime.now().toUtc();
      return AuthTokens(
        accessToken: 'a',
        refreshToken: 'r',
        accessTokenExpiresAt: now.add(accessTtl),
        refreshTokenExpiresAt: now.add(refreshTtl),
      );
    }

    test('treats a token inside the leeway window as expired', () {
      // The leeway exists so a request cannot fail because the token lapsed
      // between the check and the server receiving it.
      final AuthTokens almostExpired = tokens(
        accessTtl: const Duration(seconds: 5),
        refreshTtl: const Duration(days: 30),
      );
      expect(almostExpired.isAccessTokenExpired, isTrue);
    });

    test('treats a comfortably valid token as live', () {
      final AuthTokens valid = tokens(
        accessTtl: const Duration(minutes: 30),
        refreshTtl: const Duration(days: 30),
      );
      expect(valid.isAccessTokenExpired, isFalse);
      expect(valid.canBeRestored, isTrue);
    });

    test('a lapsed refresh token cannot be restored', () {
      final AuthTokens dead = tokens(
        accessTtl: const Duration(minutes: 30),
        refreshTtl: const Duration(seconds: -1),
      );
      expect(dead.canBeRestored, isFalse);
    });

    test('toString never reveals a token', () {
      // Tokens reach log lines through interpolated objects if toString is
      // left to the default, so this is a real leak path, not a style point.
      final DateTime now = DateTime.now().toUtc();
      final AuthTokens secret = AuthTokens(
        accessToken: 'SECRET-ACCESS-VALUE',
        refreshToken: 'SECRET-REFRESH-VALUE',
        accessTokenExpiresAt: now.add(const Duration(hours: 1)),
        refreshTokenExpiresAt: now.add(const Duration(days: 30)),
      );

      final String rendered = secret.toString();
      expect(rendered, isNot(contains('SECRET-ACCESS-VALUE')));
      expect(rendered, isNot(contains('SECRET-REFRESH-VALUE')));
      expect(rendered, contains('accessTokenExpiresAt'));
    });
  });
}
