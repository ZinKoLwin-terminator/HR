import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';

part 'auth_user_model.freezed.dart';
part 'auth_user_model.g.dart';

/// Wire representation of the signed-in user.
///
/// Models mirror the JSON exactly and know how to become an entity; entities
/// never know about JSON. That separation is what lets the API change shape
/// without the domain or the UI changing with it.
///
/// Shape: `docs/api_contracts.md` §2.
@freezed
abstract class AuthUserModel with _$AuthUserModel {
  const factory AuthUserModel({
    required String userId,
    required String employeeId,
    required String fullName,
    required String email,
    String? department,
    String? designation,
    String? profilePhotoUrl,
    @Default(false) bool mustChangePassword,
  }) = _AuthUserModel;

  const AuthUserModel._();

  factory AuthUserModel.fromJson(Map<String, dynamic> json) =>
      _$AuthUserModelFromJson(json);

  AuthenticatedUser toEntity() => AuthenticatedUser(
    userId: userId,
    employeeId: employeeId,
    fullName: fullName,
    email: email,
    department: department,
    designation: designation,
    profilePhotoUrl: profilePhotoUrl,
    mustChangePassword: mustChangePassword,
  );

  /// Used to cache the identity for the next cold start.
  static AuthUserModel fromEntity(AuthenticatedUser user) => AuthUserModel(
    userId: user.userId,
    employeeId: user.employeeId,
    fullName: user.fullName,
    email: user.email,
    department: user.department,
    designation: user.designation,
    profilePhotoUrl: user.profilePhotoUrl,
    mustChangePassword: user.mustChangePassword,
  );
}
