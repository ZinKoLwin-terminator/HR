import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/usecases/auth_usecases.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_providers.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// The three password flows, grouped because they are variations of one
/// shape: submit a form, report success or a failure, reset on reopen.
///
/// Each keeps its own provider so one screen's error cannot appear on
/// another.

/// "Forgot password" — sends the reset email.
class ForgotPasswordController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  /// Always reports success when the request reaches the server, including
  /// for an address with no account. The server answers 200 either way so
  /// that the screen cannot be used to discover which emails are registered.
  Future<bool> submit(String email) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final RequestPasswordResetUseCase requestReset = ref.read(
      requestPasswordResetUseCaseProvider,
    );
    final Result<void> result = await requestReset(email);

    return result.fold(
      onSuccess: (_) {
        state = const SubmissionState.success(
          message: 'If that address is on file, a reset link is on its way.',
        );
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  void reset() => state = const SubmissionState.idle();
}

final NotifierProvider<ForgotPasswordController, SubmissionState>
forgotPasswordControllerProvider =
    NotifierProvider<ForgotPasswordController, SubmissionState>(
      ForgotPasswordController.new,
    );

/// "Reset password" — sets a new password using the emailed token.
class ResetPasswordController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  Future<bool> submit({
    required String email,
    required String token,
    required String newPassword,
  }) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final ResetPasswordUseCase resetPassword = ref.read(
      resetPasswordUseCaseProvider,
    );
    final Result<void> result = await resetPassword(
      PasswordResetRequest(
        email: email,
        token: token,
        newPassword: newPassword,
      ),
    );

    return result.fold(
      onSuccess: (_) {
        state = const SubmissionState.success(
          message: 'Password updated. Sign in with your new password.',
        );
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  void reset() => state = const SubmissionState.idle();
}

final NotifierProvider<ResetPasswordController, SubmissionState>
resetPasswordControllerProvider =
    NotifierProvider<ResetPasswordController, SubmissionState>(
      ResetPasswordController.new,
    );

/// "Change password" — for a user who is already signed in.
///
/// Lives in the auth feature even though the screen is reached from Profile:
/// everything that handles a credential belongs in one place.
class ChangePasswordController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  Future<bool> submit({
    required String currentPassword,
    required String newPassword,
  }) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final ChangePasswordUseCase changePassword = ref.read(
      changePasswordUseCaseProvider,
    );
    final Result<void> result = await changePassword(
      PasswordChangeRequest(
        currentPassword: currentPassword,
        newPassword: newPassword,
      ),
    );

    return result.fold(
      onSuccess: (_) {
        state = const SubmissionState.success(
          message: 'Your password has been changed.',
        );
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  void reset() => state = const SubmissionState.idle();
}

final NotifierProvider<ChangePasswordController, SubmissionState>
changePasswordControllerProvider =
    NotifierProvider<ChangePasswordController, SubmissionState>(
      ChangePasswordController.new,
    );
