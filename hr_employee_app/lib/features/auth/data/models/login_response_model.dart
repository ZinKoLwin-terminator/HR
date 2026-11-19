import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/storage/token_storage.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_user_model.dart';

part 'login_response_model.freezed.dart';
part 'login_response_model.g.dart';

/// The payload returned by `/auth/login` and `/auth/refresh`.
///
/// Shape: `docs/api_contracts.md` §2.
@freezed
abstract class LoginResponseModel with _$LoginResponseModel {
  const factory LoginResponseModel({
    required String accessToken,
    required String refreshToken,
    required DateTime accessTokenExpiresAt,
    required DateTime refreshTokenExpiresAt,
    @Default('Bearer') String tokenType,
    AuthUserModel? user,
  }) = _LoginResponseModel;

  const LoginResponseModel._();

  factory LoginResponseModel.fromJson(Map<String, dynamic> json) =>
      _$LoginResponseModelFromJson(json);

  /// The credential half, for [TokenStorage].
  AuthTokens toTokens() => AuthTokens(
    accessToken: accessToken,
    refreshToken: refreshToken,
    accessTokenExpiresAt: accessTokenExpiresAt,
    refreshTokenExpiresAt: refreshTokenExpiresAt,
  );

  /// Omits the tokens so they cannot reach a log line.
  @override
  String toString() =>
      'LoginResponseModel(user: ${user?.employeeId}, '
      'accessTokenExpiresAt: $accessTokenExpiresAt)';
}
