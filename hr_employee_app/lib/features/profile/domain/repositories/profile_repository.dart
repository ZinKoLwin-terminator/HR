import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';

/// Reads the employee record and updates the parts the employee owns.
abstract interface class ProfileRepository {
  Future<Result<EmployeeProfile>> getProfile();

  /// Updates the self-editable contact fields.
  Future<Result<EmployeeProfile>> updateContact(ContactUpdate update);

  /// Updates the emergency contact.
  Future<Result<EmployeeProfile>> updateEmergencyContact(
    EmergencyContact contact,
  );

  /// Uploads a new profile photo and returns the updated profile.
  ///
  /// [filePath] is a local path; the data layer builds the multipart part.
  Future<Result<EmployeeProfile>> uploadProfilePhoto(
    String filePath, {
    void Function(double progress)? onProgress,
  });
}
