import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/platform/local_file.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:hr_employee_app/features/profile/domain/repositories/profile_repository.dart';

/// The employee's own record.
class GetProfileUseCase {
  const GetProfileUseCase(this._repository);

  final ProfileRepository _repository;

  Future<Result<EmployeeProfile>> call() => _repository.getProfile();
}

/// Updates the self-editable contact fields.
class UpdateContactUseCase {
  const UpdateContactUseCase(this._repository);

  final ProfileRepository _repository;

  Future<Result<EmployeeProfile>> call(ContactUpdate update) {
    final String mobile = update.mobileNumber.trim();
    if (mobile.isEmpty) {
      return Future<Result<EmployeeProfile>>.value(
        const Result<EmployeeProfile>.failure(
          ValidationFailure('A mobile number is required.'),
        ),
      );
    }
    return _repository.updateContact(
      update.copyWith(mobileNumber: mobile, address: update.address?.trim()),
    );
  }
}

/// Updates the emergency contact.
class UpdateEmergencyContactUseCase {
  const UpdateEmergencyContactUseCase(this._repository);

  final ProfileRepository _repository;

  Future<Result<EmployeeProfile>> call(EmergencyContact contact) {
    if (contact.name.trim().isEmpty) {
      return Future<Result<EmployeeProfile>>.value(
        const Result<EmployeeProfile>.failure(
          ValidationFailure('Enter the name of your emergency contact.'),
        ),
      );
    }
    if (contact.mobileNumber.trim().isEmpty) {
      return Future<Result<EmployeeProfile>>.value(
        const Result<EmployeeProfile>.failure(
          ValidationFailure('Enter a phone number for your emergency contact.'),
        ),
      );
    }
    return _repository.updateEmergencyContact(contact);
  }
}

/// Uploads a profile photo.
///
/// Size and type are checked before the upload starts: on a slow connection,
/// discovering a 10 MB photo is rejected *after* uploading it is a poor
/// experience, and the limits are already known.
class UploadProfilePhotoUseCase {
  const UploadProfilePhotoUseCase(this._repository);

  final ProfileRepository _repository;

  Future<Result<EmployeeProfile>> call(
    String filePath, {
    void Function(double progress)? onProgress,
  }) async {
    // Null means two different things depending on the host: on a device the
    // file is gone, and in a browser a path simply cannot be inspected. Only
    // the first is an error the user can act on.
    final int? sizeBytes = localFileSizeOrNull(filePath);

    if (canInspectLocalFiles && sizeBytes == null) {
      return const Result<EmployeeProfile>.failure(
        ValidationFailure('That file could not be found.'),
      );
    }

    final String extension = filePath.split('.').last.toLowerCase();
    if (!UploadLimits.profilePhotoExtensions.contains(extension)) {
      return Result<EmployeeProfile>.failure(
        ValidationFailure(
          'Use a '
          '${UploadLimits.profilePhotoExtensions.join(', ')} image.',
        ),
      );
    }

    if (sizeBytes != null && sizeBytes > UploadLimits.maxProfilePhotoBytes) {
      const int maxMb = UploadLimits.maxProfilePhotoBytes ~/ (1024 * 1024);
      return const Result<EmployeeProfile>.failure(
        ValidationFailure('Choose an image under $maxMb MB.'),
      );
    }

    return _repository.uploadProfilePhoto(filePath, onProgress: onProgress);
  }
}
