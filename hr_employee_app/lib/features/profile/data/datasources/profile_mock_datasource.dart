import 'package:hr_employee_app/features/profile/data/datasources/profile_remote_datasource.dart';
import 'package:hr_employee_app/features/profile/data/models/employee_profile_model.dart';
import 'package:hr_employee_app/shared/helpers/mock_fixtures.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';

/// A fixture-backed stand-in for [ProfileRemoteDataSource].
///
/// NOT A BACKEND — see [MockSupport]. The employee record is invented.
///
/// Edits are written back into [MockFixtures], so changing a phone number and
/// returning to the profile screen shows the new value, as it would against a
/// real API.
class ProfileMockDataSource implements ProfileRemoteDataSource {
  const ProfileMockDataSource();

  @override
  Future<EmployeeProfileModel> getProfile() async {
    await MockSupport.delay(const Duration(milliseconds: 450));
    return MockFixtures.instance.profile;
  }

  @override
  Future<EmployeeProfileModel> updateContact(ContactUpdateModel update) async {
    await MockSupport.delay(const Duration(milliseconds: 700));
    return MockFixtures.instance.updateContact(
      mobileNumber: update.mobileNumber,
      address: update.address,
    );
  }

  @override
  Future<EmployeeProfileModel> updateEmergencyContact(
    EmergencyContactModel contact,
  ) async {
    await MockSupport.delay(const Duration(milliseconds: 700));
    return MockFixtures.instance.updateEmergencyContact(contact);
  }

  @override
  Future<EmployeeProfileModel> uploadProfilePhoto(
    String filePath, {
    void Function(double progress)? onProgress,
  }) async {
    for (int step = 1; step <= 4; step++) {
      await MockSupport.delay(const Duration(milliseconds: 180));
      onProgress?.call(step / 4);
    }

    // The fixture deliberately keeps `profilePhotoUrl` null: inventing a URL
    // would make the avatar try to fetch a host that does not exist, and the
    // initials fallback is the honest result of "no photo on file".
    return MockFixtures.instance.profile;
  }
}
