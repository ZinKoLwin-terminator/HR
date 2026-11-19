import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/file_picker_service.dart';
import 'package:hr_employee_app/features/leave/data/datasources/leave_mock_datasource.dart';
import 'package:hr_employee_app/features/leave/data/datasources/leave_remote_datasource.dart';
import 'package:hr_employee_app/features/leave/data/repositories/leave_repository_impl.dart';
import 'package:hr_employee_app/features/leave/domain/repositories/leave_repository.dart';
import 'package:hr_employee_app/features/leave/domain/usecases/apply_leave_usecase.dart';
import 'package:hr_employee_app/features/leave/domain/usecases/leave_usecases.dart';

/// Dependency injection for the leave feature.

/// Shared with the profile feature, which also uploads a file.
final Provider<FilePickerService> filePickerServiceProvider =
    Provider<FilePickerService>((Ref ref) => const FilePickerServiceImpl());

final Provider<LeaveRemoteDataSource> leaveRemoteDataSourceProvider =
    Provider<LeaveRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return const LeaveMockDataSource();
      }
      return LeaveRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<LeaveRepository> leaveRepositoryProvider =
    Provider<LeaveRepository>(
      (Ref ref) => LeaveRepositoryImpl(
        remote: ref.watch(leaveRemoteDataSourceProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<GetLeaveTypesUseCase> getLeaveTypesUseCaseProvider =
    Provider<GetLeaveTypesUseCase>(
      (Ref ref) => GetLeaveTypesUseCase(ref.watch(leaveRepositoryProvider)),
    );

final Provider<GetLeaveBalancesUseCase> getLeaveBalancesUseCaseProvider =
    Provider<GetLeaveBalancesUseCase>(
      (Ref ref) => GetLeaveBalancesUseCase(ref.watch(leaveRepositoryProvider)),
    );

final Provider<ApplyLeaveUseCase> applyLeaveUseCaseProvider =
    Provider<ApplyLeaveUseCase>(
      (Ref ref) => ApplyLeaveUseCase(ref.watch(leaveRepositoryProvider)),
    );

final Provider<GetLeaveRequestsUseCase> getLeaveRequestsUseCaseProvider =
    Provider<GetLeaveRequestsUseCase>(
      (Ref ref) => GetLeaveRequestsUseCase(ref.watch(leaveRepositoryProvider)),
    );

final Provider<GetLeaveRequestDetailUseCase>
getLeaveRequestDetailUseCaseProvider = Provider<GetLeaveRequestDetailUseCase>(
  (Ref ref) => GetLeaveRequestDetailUseCase(ref.watch(leaveRepositoryProvider)),
);

final Provider<CancelLeaveRequestUseCase> cancelLeaveRequestUseCaseProvider =
    Provider<CancelLeaveRequestUseCase>(
      (Ref ref) =>
          CancelLeaveRequestUseCase(ref.watch(leaveRepositoryProvider)),
    );
