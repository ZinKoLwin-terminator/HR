import 'package:freezed_annotation/freezed_annotation.dart';

part 'login_credentials.freezed.dart';

/// What the user types on the login screen, plus the device identity the
/// server records against the session.
///
/// [toString] is overridden so a credential cannot reach a log line through
/// an interpolated object.
@freezed
abstract class LoginCredentials with _$LoginCredentials {
  const factory LoginCredentials({
    /// Employee ID or work email — the server accepts either.
    required String userName,
    required String password,
    required String deviceId,
    required String deviceName,
  }) = _LoginCredentials;

  const LoginCredentials._();

  @override
  String toString() => 'LoginCredentials(userName: $userName, password: ***)';
}

/// Input for setting a new password from an emailed reset link.
@freezed
abstract class PasswordResetRequest with _$PasswordResetRequest {
  const factory PasswordResetRequest({
    required String email,
    required String token,
    required String newPassword,
  }) = _PasswordResetRequest;

  const PasswordResetRequest._();

  @override
  String toString() => 'PasswordResetRequest(email: $email, token: ***)';
}

/// Input for changing the password while signed in.
@freezed
abstract class PasswordChangeRequest with _$PasswordChangeRequest {
  const factory PasswordChangeRequest({
    required String currentPassword,
    required String newPassword,
  }) = _PasswordChangeRequest;

  const PasswordChangeRequest._();

  @override
  String toString() => 'PasswordChangeRequest(***)';
}
