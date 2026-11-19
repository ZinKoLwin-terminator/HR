import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';

/// Reads the aggregate shown on the home screen.
abstract interface class DashboardRepository {
  Future<Result<DashboardSummary>> getSummary();
}
