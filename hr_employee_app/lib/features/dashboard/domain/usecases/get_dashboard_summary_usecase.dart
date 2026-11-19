import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:hr_employee_app/features/dashboard/domain/repositories/dashboard_repository.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';

/// Loads the dashboard, applying the ordering the screen relies on.
///
/// Sorting here rather than in the widget keeps a presentation-free rule in
/// the domain, and means the order is covered by a unit test instead of a
/// golden test.
class GetDashboardSummaryUseCase {
  const GetDashboardSummaryUseCase(this._repository);

  final DashboardRepository _repository;

  /// How many leave balances the dashboard card shows before "See all".
  static const int visibleLeaveBalances = 4;

  Future<Result<DashboardSummary>> call() async {
    final Result<DashboardSummary> result = await _repository.getSummary();

    return result.map((DashboardSummary summary) {
      // Most-depleted entitlements first: a balance with little left is what
      // the employee needs to know about.
      final List<LeaveBalance> balances =
          List<LeaveBalance>.of(summary.leaveBalances)..sort((
            LeaveBalance a,
            LeaveBalance b,
          ) {
            final int byRemaining = a.remainingDays.compareTo(b.remainingDays);
            // Stable tie-break so the order does not shuffle between loads.
            return byRemaining != 0
                ? byRemaining
                : a.leaveTypeName.compareTo(b.leaveTypeName);
          });

      return summary.copyWith(leaveBalances: balances);
    });
  }
}
