import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_request_models.dart';
import 'package:hr_employee_app/features/auth/data/models/login_response_model.dart';

/// Talks to the authentication endpoints.
///
/// Throws `AppException` on failure; translating those into `Failure` is the
/// repository's job, not this layer's.
abstract interface class AuthRemoteDataSource {
  Future<LoginResponseModel> login(LoginRequestModel request);

  Future<void> logout(LogoutRequestModel request);

  Future<void> requestPasswordReset(ForgotPasswordRequestModel request);

  Future<void> resetPassword(ResetPasswordRequestModel request);

  Future<void> changePassword(ChangePasswordRequestModel request);
}

/// Calls the real API.
class AuthRemoteDataSourceImpl implements AuthRemoteDataSource {
  const AuthRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<LoginResponseModel> login(LoginRequestModel request) {
    return _client.post<LoginResponseModel>(
      ApiEndpoints.login,
      body: request.toJson(),
      decode: (Object? data) =>
          LoginResponseModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<void> logout(LogoutRequestModel request) {
    return _client.postVoid(ApiEndpoints.logout, body: request.toJson());
  }

  @override
  Future<void> requestPasswordReset(ForgotPasswordRequestModel request) {
    return _client.postVoid(
      ApiEndpoints.forgotPassword,
      body: request.toJson(),
    );
  }

  @override
  Future<void> resetPassword(ResetPasswordRequestModel request) {
    return _client.postVoid(ApiEndpoints.resetPassword, body: request.toJson());
  }

  @override
  Future<void> changePassword(ChangePasswordRequestModel request) {
    return _client.postVoid(
      ApiEndpoints.changePassword,
      body: request.toJson(),
    );
  }
}
