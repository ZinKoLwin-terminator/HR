import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_remote_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/repositories/grievance_repository_impl.dart';
import 'package:hr_employee_app/features/grievance/domain/repositories/grievance_repository.dart';
import 'package:hr_employee_app/features/grievance/domain/usecases/grievance_usecases.dart';

/// Dependency injection for the grievance feature.

final Provider<GrievanceRemoteDataSource> grievanceRemoteDataSourceProvider =
    Provider<GrievanceRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return GrievanceMockDataSource();
      }
      return GrievanceRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<GrievanceRepository> grievanceRepositoryProvider =
    Provider<GrievanceRepository>(
      (Ref ref) => GrievanceRepositoryImpl(
        remote: ref.watch(grievanceRemoteDataSourceProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<GetGrievanceOptionsUseCase> getGrievanceOptionsUseCaseProvider =
    Provider<GetGrievanceOptionsUseCase>(
      (Ref ref) =>
          GetGrievanceOptionsUseCase(ref.watch(grievanceRepositoryProvider)),
    );

final Provider<GetMyGrievancesUseCase> getMyGrievancesUseCaseProvider =
    Provider<GetMyGrievancesUseCase>(
      (Ref ref) =>
          GetMyGrievancesUseCase(ref.watch(grievanceRepositoryProvider)),
    );

final Provider<GetGrievanceDetailUseCase> getGrievanceDetailUseCaseProvider =
    Provider<GetGrievanceDetailUseCase>(
      (Ref ref) =>
          GetGrievanceDetailUseCase(ref.watch(grievanceRepositoryProvider)),
    );

final Provider<SaveGrievanceDraftUseCase> saveGrievanceDraftUseCaseProvider =
    Provider<SaveGrievanceDraftUseCase>(
      (Ref ref) =>
          SaveGrievanceDraftUseCase(ref.watch(grievanceRepositoryProvider)),
    );

final Provider<SubmitGrievanceUseCase> submitGrievanceUseCaseProvider =
    Provider<SubmitGrievanceUseCase>(
      (Ref ref) =>
          SubmitGrievanceUseCase(ref.watch(grievanceRepositoryProvider)),
    );

final Provider<DeleteGrievanceDraftUseCase>
deleteGrievanceDraftUseCaseProvider = Provider<DeleteGrievanceDraftUseCase>(
  (Ref ref) =>
      DeleteGrievanceDraftUseCase(ref.watch(grievanceRepositoryProvider)),
);

final Provider<SendGrievanceMessageUseCase>
sendGrievanceMessageUseCaseProvider = Provider<SendGrievanceMessageUseCase>(
  (Ref ref) =>
      SendGrievanceMessageUseCase(ref.watch(grievanceRepositoryProvider)),
);

final Provider<MarkGrievanceSeenUseCase> markGrievanceSeenUseCaseProvider =
    Provider<MarkGrievanceSeenUseCase>(
      (Ref ref) =>
          MarkGrievanceSeenUseCase(ref.watch(grievanceRepositoryProvider)),
    );
