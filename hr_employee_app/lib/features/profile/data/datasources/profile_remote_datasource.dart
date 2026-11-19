import 'package:dio/dio.dart';
import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/features/profile/data/models/employee_profile_model.dart';

/// Reads and updates the employee record.
abstract interface class ProfileRemoteDataSource {
  Future<EmployeeProfileModel> getProfile();

  Future<EmployeeProfileModel> updateContact(ContactUpdateModel update);

  Future<EmployeeProfileModel> updateEmergencyContact(
    EmergencyContactModel contact,
  );

  Future<EmployeeProfileModel> uploadProfilePhoto(
    String filePath, {
    void Function(double progress)? onProgress,
  });
}

/// Calls the real API.
class ProfileRemoteDataSourceImpl implements ProfileRemoteDataSource {
  const ProfileRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<EmployeeProfileModel> getProfile() {
    return _client.get<EmployeeProfileModel>(
      ApiEndpoints.myProfile,
      decode: (Object? data) =>
          EmployeeProfileModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<EmployeeProfileModel> updateContact(ContactUpdateModel update) {
    return _client.put<EmployeeProfileModel>(
      ApiEndpoints.myContact,
      body: update.toJson(),
      decode: (Object? data) =>
          EmployeeProfileModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<EmployeeProfileModel> updateEmergencyContact(
    EmergencyContactModel contact,
  ) {
    return _client.put<EmployeeProfileModel>(
      ApiEndpoints.myEmergencyContact,
      body: contact.toJson(),
      decode: (Object? data) =>
          EmployeeProfileModel.fromJson(Decode.object(data)),
    );
  }

  /// Uploads the photo and returns the refreshed profile.
  ///
  /// The photo endpoint returns only the new URL, so the profile is re-read
  /// afterwards. One extra request keeps the caller's contract simple — it
  /// always gets a complete, current profile back — and avoids every call
  /// site having to patch the URL into its own copy.
  @override
  Future<EmployeeProfileModel> uploadProfilePhoto(
    String filePath, {
    void Function(double progress)? onProgress,
  }) async {
    final FormData formData = FormData.fromMap(<String, dynamic>{
      'file': await MultipartFile.fromFile(
        filePath,
        filename: filePath.split(RegExp(r'[/\\]')).last,
      ),
    });

    await _client.upload<ProfilePhotoResponseModel>(
      ApiEndpoints.myPhoto,
      formData: formData,
      onSendProgress: onProgress == null
          ? null
          : (int sent, int total) {
              if (total > 0) {
                onProgress(sent / total);
              }
            },
      decode: (Object? data) =>
          ProfilePhotoResponseModel.fromJson(Decode.object(data)),
    );

    return getProfile();
  }
}
