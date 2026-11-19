import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/dashboard/data/datasources/dashboard_remote_datasource.dart';
import 'package:hr_employee_app/features/dashboard/data/models/dashboard_summary_model.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:hr_employee_app/features/dashboard/domain/repositories/dashboard_repository.dart';

/// Maps the dashboard payload to its entity.
///
/// Nothing is cached: the dashboard is a snapshot of attendance and leave as
/// they stand right now, and a stale version of it would mislead rather than
/// help.
class DashboardRepositoryImpl
    with RepositoryGuard
    implements DashboardRepository {
  DashboardRepositoryImpl({required this._remote, required this.logger});

  final DashboardRemoteDataSource _remote;

  @override
  final AppLogger logger;

  @override
  Future<Result<DashboardSummary>> getSummary() {
    return guard(operation: 'getDashboardSummary', () async {
      final DashboardSummaryModel model = await _remote.getSummary();
      return model.toEntity();
    });
  }
}
