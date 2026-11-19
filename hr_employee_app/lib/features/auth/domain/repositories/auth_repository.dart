import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';

/// The contract the domain layer depends on.
///
/// Declared here, implemented in `data/` — the dependency inversion that lets
/// use cases be tested with a fake and lets the data layer be swapped for a
/// mock without the domain noticing.
///
/// Every method returns a `Result` instead of throwing, so a caller cannot
/// forget that it might fail.
abstract interface class AuthRepository {
  /// Exchanges credentials for a session and persists the tokens.
  Future<Result<AuthenticatedUser>> login(LoginCredentials credentials);

  /// Restores a session saved by a previous run.
  ///
  /// Returns a success with `null` when there is no stored session or it can
  /// no longer be refreshed — "not signed in" is an expected state, not a
  /// failure.
  Future<Result<AuthenticatedUser?>> restoreSession();

  /// Revokes the session server-side and clears local credentials.
  ///
  /// Succeeds even when the server call fails: local sign-out must always be
  /// possible, including offline.
  Future<Result<void>> logout();

  /// Starts the reset flow. Succeeds even for an unknown address, so the
  /// response cannot be used to discover which emails have accounts.
  Future<Result<void>> requestPasswordReset(String email);

  /// Completes the reset flow with the emailed token.
  Future<Result<void>> resetPassword(PasswordResetRequest request);

  /// Changes the password of the signed-in user.
  Future<Result<void>> changePassword(PasswordChangeRequest request);

  /// The cached user, without a network call.
  ///
  /// Used to paint the shell immediately at start-up while the real profile
  /// request is still in flight.
  Future<AuthenticatedUser?> cachedUser();
}
