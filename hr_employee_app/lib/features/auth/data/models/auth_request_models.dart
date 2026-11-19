import 'package:freezed_annotation/freezed_annotation.dart';

part 'auth_request_models.freezed.dart';
part 'auth_request_models.g.dart';

/// Request body for `/auth/login`.
@freezed
abstract class LoginRequestModel with _$LoginRequestModel {
  const factory LoginRequestModel({
    required String userName,
    required String password,
    required String deviceId,
    required String deviceName,
  }) = _LoginRequestModel;

  const LoginRequestModel._();

  factory LoginRequestModel.fromJson(Map<String, dynamic> json) =>
      _$LoginRequestModelFromJson(json);

  @override
  String toString() => 'LoginRequestModel(userName: $userName, password: ***)';
}

/// Request body for `/auth/refresh`.
@freezed
abstract class RefreshTokenRequestModel with _$RefreshTokenRequestModel {
  const factory RefreshTokenRequestModel({
    required String refreshToken,
    required String deviceId,
  }) = _RefreshTokenRequestModel;

  const RefreshTokenRequestModel._();

  factory RefreshTokenRequestModel.fromJson(Map<String, dynamic> json) =>
      _$RefreshTokenRequestModelFromJson(json);

  @override
  String toString() => 'RefreshTokenRequestModel(deviceId: $deviceId)';
}

/// Request body for `/auth/forgot-password`.
@freezed
abstract class ForgotPasswordRequestModel with _$ForgotPasswordRequestModel {
  const factory ForgotPasswordRequestModel({required String email}) =
      _ForgotPasswordRequestModel;

  factory ForgotPasswordRequestModel.fromJson(Map<String, dynamic> json) =>
      _$ForgotPasswordRequestModelFromJson(json);
}

/// Request body for `/auth/reset-password`.
@freezed
abstract class ResetPasswordRequestModel with _$ResetPasswordRequestModel {
  const factory ResetPasswordRequestModel({
    required String email,
    required String token,
    required String newPassword,
  }) = _ResetPasswordRequestModel;

  const ResetPasswordRequestModel._();

  factory ResetPasswordRequestModel.fromJson(Map<String, dynamic> json) =>
      _$ResetPasswordRequestModelFromJson(json);

  @override
  String toString() => 'ResetPasswordRequestModel(email: $email, token: ***)';
}

/// Request body for `/auth/change-password`.
@freezed
abstract class ChangePasswordRequestModel with _$ChangePasswordRequestModel {
  const factory ChangePasswordRequestModel({
    required String currentPassword,
    required String newPassword,
  }) = _ChangePasswordRequestModel;

  const ChangePasswordRequestModel._();

  factory ChangePasswordRequestModel.fromJson(Map<String, dynamic> json) =>
      _$ChangePasswordRequestModelFromJson(json);

  @override
  String toString() => 'ChangePasswordRequestModel(***)';
}

/// Request body for `/auth/logout`.
@freezed
abstract class LogoutRequestModel with _$LogoutRequestModel {
  const factory LogoutRequestModel({required String refreshToken}) =
      _LogoutRequestModel;

  const LogoutRequestModel._();

  factory LogoutRequestModel.fromJson(Map<String, dynamic> json) =>
      _$LogoutRequestModelFromJson(json);

  @override
  String toString() => 'LogoutRequestModel(***)';
}
