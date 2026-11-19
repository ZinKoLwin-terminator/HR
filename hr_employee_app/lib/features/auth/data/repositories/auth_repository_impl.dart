import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/storage/token_storage.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_remote_datasource.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_request_models.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_user_model.dart';
import 'package:hr_employee_app/features/auth/data/models/login_response_model.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/repositories/auth_repository.dart';

/// Coordinates the remote API, the local cache and secure token storage.
///
/// This is the only class that sees all three. The domain above it sees a
/// user or a failure; the data sources below it each see one concern.
class AuthRepositoryImpl with RepositoryGuard implements AuthRepository {
  AuthRepositoryImpl({
    required this._remote,
    required this._local,
    required this._tokenStorage,
    required this.logger,
  });

  final AuthRemoteDataSource _remote;
  final AuthLocalDataSource _local;
  final TokenStorage _tokenStorage;

  @override
  final AppLogger logger;

  @override
  Future<Result<AuthenticatedUser>> login(LoginCredentials credentials) {
    return guard(operation: 'login', () async {
      final LoginResponseModel response = await _remote.login(
        LoginRequestModel(
          userName: credentials.userName,
          password: credentials.password,
          deviceId: credentials.deviceId,
          deviceName: credentials.deviceName,
        ),
      );

      // Tokens first: if persisting them fails, the session is unusable and
      // the caller should see a failure rather than a half-signed-in state.
      await _tokenStorage.save(response.toTokens());

      final AuthUserModel? user = response.user;
      if (user != null) {
        await _local.saveUser(user);
        return user.toEntity();
      }

      // The contract says `user` is present on login. If a server ever
      // omits it, fall back to whatever identity was cached rather than
      // failing a login that actually succeeded.
      final AuthUserModel? cached = await _local.readUser();
      if (cached != null) {
        return cached.toEntity();
      }

      // Nothing to identify the session with: undo it and report a
      // contract mismatch rather than leaving dangling tokens.
      await _tokenStorage.clear();
      throw StateError('Login response did not include a user.');
    });
  }

  @override
  Future<Result<AuthenticatedUser?>> restoreSession() {
    return guard(operation: 'restoreSession', () async {
      final AuthTokens? tokens = await _tokenStorage.read();

      // No stored session. Not an error — the user simply has to sign in.
      if (tokens == null) {
        return null;
      }

      // The refresh token has lapsed, so nothing can be recovered.
      if (!tokens.canBeRestored) {
        logger.info('Stored session can no longer be refreshed; clearing');
        await _clearLocalSession();
        return null;
      }

      final AuthUserModel? cached = await _local.readUser();
      if (cached == null) {
        // Tokens without an identity cannot render the shell. Rather than
        // guess, start a clean sign-in.
        await _clearLocalSession();
        return null;
      }

      // An expired *access* token is fine: the interceptor exchanges it on
      // the first real request, so the user is not made to wait here.
      return cached.toEntity();
    });
  }

  @override
  Future<Result<void>> logout() {
    return guard(operation: 'logout', () async {
      final AuthTokens? tokens = await _tokenStorage.read();

      if (tokens != null) {
        try {
          await _remote.logout(
            LogoutRequestModel(refreshToken: tokens.refreshToken),
          );
        } on Object catch (_) {
          // Best effort. Signing out must work offline, and a server that
          // cannot be reached must not trap the user in the app.
          logger.warning('Server sign-out failed; clearing locally anyway');
        }
      }

      await _clearLocalSession();
    });
  }

  @override
  Future<Result<void>> requestPasswordReset(String email) {
    return guard(
      operation: 'requestPasswordReset',
      () => _remote.requestPasswordReset(
        ForgotPasswordRequestModel(email: email),
      ),
    );
  }

  @override
  Future<Result<void>> resetPassword(PasswordResetRequest request) {
    return guard(
      operation: 'resetPassword',
      () => _remote.resetPassword(
        ResetPasswordRequestModel(
          email: request.email,
          token: request.token,
          newPassword: request.newPassword,
        ),
      ),
    );
  }

  @override
  Future<Result<void>> changePassword(PasswordChangeRequest request) {
    return guard(
      operation: 'changePassword',
      () => _remote.changePassword(
        ChangePasswordRequestModel(
          currentPassword: request.currentPassword,
          newPassword: request.newPassword,
        ),
      ),
    );
  }

  @override
  Future<AuthenticatedUser?> cachedUser() async {
    final AuthUserModel? cached = await _local.readUser();
    return cached?.toEntity();
  }

  Future<void> _clearLocalSession() async {
    await _tokenStorage.clear();
    await _local.clear();
  }
}
