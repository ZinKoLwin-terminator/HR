import 'package:freezed_annotation/freezed_annotation.dart';

part 'employee_profile.freezed.dart';

/// Who to contact in an emergency.
@freezed
abstract class EmergencyContact with _$EmergencyContact {
  const factory EmergencyContact({
    required String name,
    required String mobileNumber,
    String? relationship,
    String? alternateNumber,
    String? address,
  }) = _EmergencyContact;

  const EmergencyContact._();

  /// `Mother - Daw Khin Mar`, or just the name when the relationship is
  /// unknown.
  String get displayName {
    final String? relation = relationship;
    return (relation == null || relation.isEmpty) ? name : '$relation - $name';
  }
}

/// The complete employee record.
///
/// Most of this is HR-controlled and read-only in the app. Only the fields
/// listed in [isSelfEditable] can be changed by the employee — everything
/// else (department, designation, joining date, salary-affecting data) is
/// changed through HR, so the UI shows it without an edit affordance rather
/// than offering an action the server would reject.
@freezed
abstract class EmployeeProfile with _$EmployeeProfile {
  const factory EmployeeProfile({
    required String userId,
    required String employeeId,
    required String fullName,
    required String email,

    // --- Self-editable -------------------------------------------------
    String? mobileNumber,
    String? address,
    EmergencyContact? emergencyContact,
    String? profilePhotoUrl,

    // --- Personal, HR-controlled ---------------------------------------
    DateTime? dateOfBirth,
    String? gender,

    /// National identity number. Sensitive: masked in the UI by default and
    /// never written to logs.
    String? nationalId,
    String? maritalStatus,
    String? bloodGroup,

    // --- Employment, HR-controlled -------------------------------------
    String? department,
    String? designation,
    String? employmentType,
    String? reportingManagerName,
    String? workLocation,
    DateTime? dateOfJoining,
    DateTime? confirmationDate,
  }) = _EmployeeProfile;

  const EmployeeProfile._();

  /// `Senior Accountant - Finance`, omitting either part when unknown.
  String get roleLine {
    final List<String> parts = <String>[
      if (designation != null && designation!.isNotEmpty) designation!,
      if (department != null && department!.isNotEmpty) department!,
    ];
    return parts.join(' - ');
  }

  /// Whole years of service, or null when the joining date is unknown.
  int? get yearsOfService {
    final DateTime? joined = dateOfJoining;
    if (joined == null) {
      return null;
    }
    final DateTime now = DateTime.now();
    int years = now.year - joined.year;
    final bool anniversaryPassed =
        now.month > joined.month ||
        (now.month == joined.month && now.day >= joined.day);
    if (!anniversaryPassed) {
      years -= 1;
    }
    return years < 0 ? 0 : years;
  }

  /// A national ID with all but the last four characters replaced.
  ///
  /// The full value is only ever shown when the user explicitly reveals it.
  String? get maskedNationalId {
    final String? value = nationalId;
    if (value == null || value.isEmpty) {
      return null;
    }
    if (value.length <= 4) {
      return '****';
    }
    return '${'*' * (value.length - 4)}${value.substring(value.length - 4)}';
  }

  /// The fields the employee is allowed to change themselves.
  ///
  /// Named so it reads as documentation at the call site; the edit form uses
  /// it to decide what to render.
  static const Set<String> isSelfEditable = <String>{
    'mobileNumber',
    'address',
    'emergencyContact',
    'profilePhotoUrl',
  };
}

/// The self-editable contact fields, as submitted by the edit form.
@freezed
abstract class ContactUpdate with _$ContactUpdate {
  const factory ContactUpdate({required String mobileNumber, String? address}) =
      _ContactUpdate;
}
