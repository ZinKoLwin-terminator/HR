import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/features/profile/data/datasources/profile_mock_datasource.dart';
import 'package:hr_employee_app/features/profile/data/datasources/profile_remote_datasource.dart';
import 'package:hr_employee_app/features/profile/data/repositories/profile_repository_impl.dart';
import 'package:hr_employee_app/features/profile/domain/repositories/profile_repository.dart';
import 'package:hr_employee_app/features/profile/domain/usecases/profile_usecases.dart';

/// Dependency injection for the profile feature.

final Provider<ProfileRemoteDataSource> profileRemoteDataSourceProvider =
    Provider<ProfileRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return const ProfileMockDataSource();
      }
      return ProfileRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<ProfileRepository> profileRepositoryProvider =
    Provider<ProfileRepository>(
      (Ref ref) => ProfileRepositoryImpl(
        remote: ref.watch(profileRemoteDataSourceProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<GetProfileUseCase> getProfileUseCaseProvider =
    Provider<GetProfileUseCase>(
      (Ref ref) => GetProfileUseCase(ref.watch(profileRepositoryProvider)),
    );

final Provider<UpdateContactUseCase> updateContactUseCaseProvider =
    Provider<UpdateContactUseCase>(
      (Ref ref) => UpdateContactUseCase(ref.watch(profileRepositoryProvider)),
    );

final Provider<UpdateEmergencyContactUseCase>
updateEmergencyContactUseCaseProvider = Provider<UpdateEmergencyContactUseCase>(
  (Ref ref) =>
      UpdateEmergencyContactUseCase(ref.watch(profileRepositoryProvider)),
);

final Provider<UploadProfilePhotoUseCase> uploadProfilePhotoUseCaseProvider =
    Provider<UploadProfilePhotoUseCase>(
      (Ref ref) =>
          UploadProfilePhotoUseCase(ref.watch(profileRepositoryProvider)),
    );
