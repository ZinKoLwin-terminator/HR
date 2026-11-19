import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/repositories/auth_repository.dart';

/// Restores a saved session at start-up.
///
/// A success carrying `null` means "no session" — an ordinary outcome, not an
/// error. Only an actual problem (corrupt storage, a server that rejected a
/// valid-looking refresh) comes back as a failure.
class RestoreSessionUseCase {
  const RestoreSessionUseCase(this._repository);

  final AuthRepository _repository;

  Future<Result<AuthenticatedUser?>> call() => _repository.restoreSession();
}

/// Signs the user out.
class LogoutUseCase {
  const LogoutUseCase(this._repository);

  final AuthRepository _repository;

  Future<Result<void>> call() => _repository.logout();
}

/// Sends a password reset email.
class RequestPasswordResetUseCase {
  const RequestPasswordResetUseCase(this._repository);

  final AuthRepository _repository;

  Future<Result<void>> call(String email) {
    final String normalized = email.trim().toLowerCase();
    if (normalized.isEmpty) {
      return Future<Result<void>>.value(
        const Result<void>.failure(
          ValidationFailure('Enter the email address on your HR record.'),
        ),
      );
    }
    return _repository.requestPasswordReset(normalized);
  }
}

/// Sets a new password using the token from the reset email.
class ResetPasswordUseCase {
  const ResetPasswordUseCase(this._repository);

  final AuthRepository _repository;

  Future<Result<void>> call(PasswordResetRequest request) {
    if (request.token.isEmpty) {
      return Future<Result<void>>.value(
        const Result<void>.failure(
          ValidationFailure(
            'This reset link is incomplete. Request a new one.',
          ),
        ),
      );
    }
    return _repository.resetPassword(
      request.copyWith(email: request.email.trim().toLowerCase()),
    );
  }
}

/// Changes the password of the signed-in user.
class ChangePasswordUseCase {
  const ChangePasswordUseCase(this._repository);

  final AuthRepository _repository;

  Future<Result<void>> call(PasswordChangeRequest request) {
    // Checked here rather than only in the form, so any future caller gets
    // the same guarantee.
    if (request.currentPassword == request.newPassword) {
      return Future<Result<void>>.value(
        const Result<void>.failure(
          ValidationFailure(
            'The new password must be different from the current one.',
          ),
        ),
      );
    }
    return _repository.changePassword(request);
  }
}
