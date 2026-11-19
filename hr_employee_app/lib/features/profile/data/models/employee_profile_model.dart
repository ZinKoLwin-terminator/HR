import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';

part 'employee_profile_model.freezed.dart';
part 'employee_profile_model.g.dart';

/// Wire shape of the emergency contact. See `docs/api_contracts.md` §7.
@freezed
abstract class EmergencyContactModel with _$EmergencyContactModel {
  const factory EmergencyContactModel({
    required String name,
    @Default('') String mobileNumber,
    String? relationship,
    String? alternateNumber,
    String? address,
  }) = _EmergencyContactModel;

  const EmergencyContactModel._();

  factory EmergencyContactModel.fromJson(Map<String, dynamic> json) =>
      _$EmergencyContactModelFromJson(json);

  factory EmergencyContactModel.fromEntity(EmergencyContact contact) =>
      EmergencyContactModel(
        name: contact.name,
        mobileNumber: contact.mobileNumber,
        relationship: contact.relationship,
        alternateNumber: contact.alternateNumber,
        address: contact.address,
      );

  EmergencyContact toEntity() => EmergencyContact(
    name: name,
    mobileNumber: mobileNumber,
    relationship: relationship,
    alternateNumber: alternateNumber,
    address: address,
  );
}

/// Wire shape of the full employee record.
///
/// Date-only fields are `String` on the wire and parsed as calendar days, so
/// a birthday is not shifted by a timezone.
@freezed
abstract class EmployeeProfileModel with _$EmployeeProfileModel {
  const factory EmployeeProfileModel({
    required String userId,
    required String employeeId,
    required String fullName,
    @Default('') String email,
    String? mobileNumber,
    String? address,
    String? profilePhotoUrl,
    EmergencyContactModel? emergencyContact,

    /// `yyyy-MM-dd`.
    String? dateOfBirth,
    String? gender,
    String? nationalId,
    String? maritalStatus,
    String? bloodGroup,
    String? department,
    String? designation,
    String? employmentType,
    String? reportingManagerName,
    String? workLocation,
    String? dateOfJoining,
    String? confirmationDate,
  }) = _EmployeeProfileModel;

  const EmployeeProfileModel._();

  factory EmployeeProfileModel.fromJson(Map<String, dynamic> json) =>
      _$EmployeeProfileModelFromJson(json);

  EmployeeProfile toEntity() => EmployeeProfile(
    userId: userId,
    employeeId: employeeId,
    fullName: fullName,
    email: email,
    mobileNumber: mobileNumber,
    address: address,
    profilePhotoUrl: profilePhotoUrl,
    emergencyContact: emergencyContact?.toEntity(),
    dateOfBirth: DateFormatter.parseApiDate(dateOfBirth),
    gender: gender,
    nationalId: nationalId,
    maritalStatus: maritalStatus,
    bloodGroup: bloodGroup,
    department: department,
    designation: designation,
    employmentType: employmentType,
    reportingManagerName: reportingManagerName,
    workLocation: workLocation,
    dateOfJoining: DateFormatter.parseApiDate(dateOfJoining),
    confirmationDate: DateFormatter.parseApiDate(confirmationDate),
  );

  /// Withholds the identity fields; the profile carries personal data that
  /// must not be logged.
  @override
  String toString() => 'EmployeeProfileModel(employeeId: $employeeId)';
}

/// Request body for `PUT /employees/me/contact`.
@freezed
abstract class ContactUpdateModel with _$ContactUpdateModel {
  const factory ContactUpdateModel({
    required String mobileNumber,
    String? address,
  }) = _ContactUpdateModel;

  const ContactUpdateModel._();

  factory ContactUpdateModel.fromJson(Map<String, dynamic> json) =>
      _$ContactUpdateModelFromJson(json);

  factory ContactUpdateModel.fromEntity(ContactUpdate update) =>
      ContactUpdateModel(
        mobileNumber: update.mobileNumber,
        address: update.address,
      );
}

/// Response body of `POST /employees/me/photo`.
@freezed
abstract class ProfilePhotoResponseModel with _$ProfilePhotoResponseModel {
  const factory ProfilePhotoResponseModel({required String profilePhotoUrl}) =
      _ProfilePhotoResponseModel;

  factory ProfilePhotoResponseModel.fromJson(Map<String, dynamic> json) =>
      _$ProfilePhotoResponseModelFromJson(json);
}
