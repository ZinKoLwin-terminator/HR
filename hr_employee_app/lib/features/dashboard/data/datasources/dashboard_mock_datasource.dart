import 'package:hr_employee_app/features/attendance/data/models/attendance_models.dart';
import 'package:hr_employee_app/features/dashboard/data/datasources/dashboard_remote_datasource.dart';
import 'package:hr_employee_app/features/dashboard/data/models/dashboard_summary_model.dart';
import 'package:hr_employee_app/shared/helpers/mock_fixtures.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';

/// A fixture-backed stand-in for [DashboardRemoteDataSource].
///
/// NOT A BACKEND — see [MockSupport]. It assembles the dashboard payload from
/// [MockFixtures], which is also what the attendance, leave and notification
/// mocks read, so the dashboard agrees with the screens it links to.
class DashboardMockDataSource implements DashboardRemoteDataSource {
  const DashboardMockDataSource();

  @override
  Future<DashboardSummaryModel> getSummary() async {
    await MockSupport.delay(const Duration(milliseconds: 600));

    final MockFixtures fixtures = MockFixtures.instance;
    final AttendanceRecordModel? today = fixtures.todayRecord;

    return DashboardSummaryModel(
      profile: const EmployeeHeadlineModel(
        employeeId: MockSupport.employeeId,
        fullName: MockSupport.fullName,
        department: MockSupport.department,
        designation: MockSupport.designation,
        profilePhotoUrl: MockSupport.profilePhotoUrl,
      ),
      // Null until a check-in happens in this session, which is what the real
      // endpoint does before the first punch of the day.
      todayAttendance: today == null
          ? null
          : TodayAttendanceModel(
              attendanceDate: today.attendanceDate,
              status: today.status,
              checkInAt: today.checkInAt,
              checkOutAt: today.checkOutAt,
              isLateArrival: today.isLateArrival,
              isEarlyDeparture: today.isEarlyDeparture,
              workedMinutes: today.workedMinutes,
            ),
      leaveBalances: fixtures.leaveBalances,
      // The dashboard shows only the most recent few.
      recentNotifications: fixtures.notifications.take(3).toList(),
      unreadNotificationCount: fixtures.unreadNotificationCount,
    );
  }
}
