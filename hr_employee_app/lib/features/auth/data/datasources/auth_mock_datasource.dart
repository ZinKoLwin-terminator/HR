import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_remote_datasource.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_request_models.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_user_model.dart';
import 'package:hr_employee_app/features/auth/data/models/login_response_model.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';

/// A fixture-backed stand-in for [AuthRemoteDataSource].
///
/// NOT A BACKEND. It returns invented data so the auth screens can be built
/// before the API exists. See [MockSupport] for the full caveat.
///
/// It is still worth being faithful about *failure*: a wrong password returns
/// the same `ValidationException` the server would, so the login screen's
/// error path is exercised rather than assumed.
class AuthMockDataSource implements AuthRemoteDataSource {
  const AuthMockDataSource();

  static const AuthUserModel _user = AuthUserModel(
    userId: MockSupport.userId,
    employeeId: MockSupport.employeeId,
    fullName: MockSupport.fullName,
    email: MockSupport.email,
    department: MockSupport.department,
    designation: MockSupport.designation,
    profilePhotoUrl: MockSupport.profilePhotoUrl,
  );

  @override
  Future<LoginResponseModel> login(LoginRequestModel request) async {
    await MockSupport.delay(const Duration(milliseconds: 800));

    final String identifier = request.userName.trim().toLowerCase();
    final bool knownUser =
        identifier == MockSupport.employeeId.toLowerCase() ||
        identifier == MockSupport.email;

    if (!knownUser || request.password != MockSupport.acceptedPassword) {
      throw const ValidationException(
        'Incorrect employee ID or password.',
        statusCode: 400,
      );
    }

    final DateTime now = DateTime.now().toUtc();
    return LoginResponseModel(
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      accessTokenExpiresAt: now.add(const Duration(hours: 1)),
      refreshTokenExpiresAt: now.add(const Duration(days: 30)),
      user: _user,
    );
  }

  @override
  Future<void> logout(LogoutRequestModel request) => MockSupport.delay();

  @override
  Future<void> requestPasswordReset(ForgotPasswordRequestModel request) =>
      MockSupport.delay(const Duration(milliseconds: 700));

  @override
  Future<void> resetPassword(ResetPasswordRequestModel request) =>
      MockSupport.delay(const Duration(milliseconds: 700));

  @override
  Future<void> changePassword(ChangePasswordRequestModel request) async {
    await MockSupport.delay(const Duration(milliseconds: 700));
    if (request.currentPassword != MockSupport.acceptedPassword) {
      throw const ValidationException(
        'Your current password is incorrect.',
        fieldErrors: <String, List<String>>{
          'currentPassword': <String>['Your current password is incorrect.'],
        },
        statusCode: 400,
      );
    }
  }
}
