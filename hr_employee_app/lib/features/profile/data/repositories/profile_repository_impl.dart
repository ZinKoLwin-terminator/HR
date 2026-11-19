import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/profile/data/datasources/profile_remote_datasource.dart';
import 'package:hr_employee_app/features/profile/data/models/employee_profile_model.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:hr_employee_app/features/profile/domain/repositories/profile_repository.dart';

/// Maps profile payloads to entities.
class ProfileRepositoryImpl with RepositoryGuard implements ProfileRepository {
  ProfileRepositoryImpl({required this._remote, required this.logger});

  final ProfileRemoteDataSource _remote;

  @override
  final AppLogger logger;

  @override
  Future<Result<EmployeeProfile>> getProfile() {
    return guard(operation: 'getProfile', () async {
      final EmployeeProfileModel model = await _remote.getProfile();
      return model.toEntity();
    });
  }

  @override
  Future<Result<EmployeeProfile>> updateContact(ContactUpdate update) {
    return guard(operation: 'updateContact', () async {
      final EmployeeProfileModel model = await _remote.updateContact(
        ContactUpdateModel.fromEntity(update),
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<EmployeeProfile>> updateEmergencyContact(
    EmergencyContact contact,
  ) {
    return guard(operation: 'updateEmergencyContact', () async {
      final EmployeeProfileModel model = await _remote.updateEmergencyContact(
        EmergencyContactModel.fromEntity(contact),
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<EmployeeProfile>> uploadProfilePhoto(
    String filePath, {
    void Function(double progress)? onProgress,
  }) {
    return guard(operation: 'uploadProfilePhoto', () async {
      final EmployeeProfileModel model = await _remote.uploadProfilePhoto(
        filePath,
        onProgress: onProgress,
      );
      return model.toEntity();
    });
  }
}
