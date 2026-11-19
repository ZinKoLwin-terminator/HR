import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/features/dashboard/data/datasources/dashboard_mock_datasource.dart';
import 'package:hr_employee_app/features/dashboard/data/datasources/dashboard_remote_datasource.dart';
import 'package:hr_employee_app/features/dashboard/data/repositories/dashboard_repository_impl.dart';
import 'package:hr_employee_app/features/dashboard/domain/repositories/dashboard_repository.dart';
import 'package:hr_employee_app/features/dashboard/domain/usecases/get_dashboard_summary_usecase.dart';

/// Dependency injection for the dashboard feature.
final Provider<DashboardRemoteDataSource> dashboardRemoteDataSourceProvider =
    Provider<DashboardRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return const DashboardMockDataSource();
      }
      return DashboardRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<DashboardRepository> dashboardRepositoryProvider =
    Provider<DashboardRepository>(
      (Ref ref) => DashboardRepositoryImpl(
        remote: ref.watch(dashboardRemoteDataSourceProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<GetDashboardSummaryUseCase> getDashboardSummaryUseCaseProvider =
    Provider<GetDashboardSummaryUseCase>(
      (Ref ref) =>
          GetDashboardSummaryUseCase(ref.watch(dashboardRepositoryProvider)),
    );
