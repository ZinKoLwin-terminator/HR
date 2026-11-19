import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/usecases/login_usecase.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_providers.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// Drives the login form.
///
/// Separate from [AuthController] on purpose. If a failed sign-in put the
/// *session* provider into an error state, the router would react to it and
/// the user would be thrown off the login screen mid-attempt. Here the
/// failure stays local to the form, and only a success is promoted to the
/// session.
class LoginController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  /// Attempts a sign-in.
  ///
  /// Returns true on success so the screen can navigate; the state is updated
  /// either way so the form can render its own error.
  Future<bool> submit({
    required String userName,
    required String password,
    required bool rememberMe,
  }) async {
    // Ignore a second tap while the first is still in flight.
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final DeviceInfo device = ref.read(deviceInfoProvider);
    final LoginUseCase login = ref.read(loginUseCaseProvider);

    final Result<AuthenticatedUser> result = await login(
      LoginCredentials(
        userName: userName,
        password: password,
        deviceId: device.deviceId,
        deviceName: device.deviceName,
      ),
    );

    return result.fold(
      onSuccess: (AuthenticatedUser user) {
        _persistRememberMe(userName: userName, rememberMe: rememberMe);
        // Promote to the session only once it has actually succeeded.
        final AuthController auth = ref.read(authControllerProvider.notifier)
          ..onSignedIn(user);

        // Push registration must not delay the sign-in, and a failure to
        // register must not fail it either.
        auth.registerForPushNotifications().ignore();
        state = const SubmissionState.success();
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  /// Clears a previous error so reopening the screen starts clean.
  void reset() => state = const SubmissionState.idle();

  /// Stores the identifier only when the user asked for it.
  ///
  /// An employee ID is not a credential, but it is still personal data, so it
  /// is written only on request and removed as soon as the box is unticked.
  void _persistRememberMe({
    required String userName,
    required bool rememberMe,
  }) {
    final PreferencesService preferences = ref.read(preferencesServiceProvider);
    // `ignore()` rather than `unawaited()`: a preference that fails to write
    // must not surface as an unhandled error on a sign-in that succeeded.
    preferences.setBool(PreferenceKeys.rememberMe, value: rememberMe).ignore();
    if (rememberMe) {
      preferences
          .setString(PreferenceKeys.rememberedUserName, userName.trim())
          .ignore();
    } else {
      preferences.remove(PreferenceKeys.rememberedUserName).ignore();
    }
  }
}

final NotifierProvider<LoginController, SubmissionState>
loginControllerProvider = NotifierProvider<LoginController, SubmissionState>(
  LoginController.new,
);

/// The identifier to pre-fill, when the user previously ticked "remember me".
final Provider<String?> rememberedUserNameProvider = Provider<String?>((
  Ref ref,
) {
  final PreferencesService preferences = ref.watch(preferencesServiceProvider);
  if (!preferences.getBool(PreferenceKeys.rememberMe)) {
    return null;
  }
  return preferences.getString(PreferenceKeys.rememberedUserName);
});
