import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_status_mapper.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_models.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_models.dart';

part 'dashboard_summary_model.freezed.dart';
part 'dashboard_summary_model.g.dart';

/// Wire shape of `/dashboard/summary`. See `docs/api_contracts.md` §3.
///
/// Reuses `LeaveBalanceModel` and `AppNotificationModel` from their own
/// features: those sub-objects are identical to the standalone endpoints, so
/// a second copy would be two things to keep in step.
@freezed
abstract class DashboardSummaryModel with _$DashboardSummaryModel {
  const factory DashboardSummaryModel({
    required EmployeeHeadlineModel profile,
    TodayAttendanceModel? todayAttendance,
    @Default(<LeaveBalanceModel>[]) List<LeaveBalanceModel> leaveBalances,
    @Default(<AppNotificationModel>[])
    List<AppNotificationModel> recentNotifications,
    @Default(0) int unreadNotificationCount,
  }) = _DashboardSummaryModel;

  const DashboardSummaryModel._();

  factory DashboardSummaryModel.fromJson(Map<String, dynamic> json) =>
      _$DashboardSummaryModelFromJson(json);

  DashboardSummary toEntity() => DashboardSummary(
    profile: profile.toEntity(),
    todayAttendance: todayAttendance?.toEntity(),
    leaveBalances: leaveBalances
        .map((LeaveBalanceModel model) => model.toEntity())
        .toList(growable: false),
    recentNotifications: recentNotifications
        .map((AppNotificationModel model) => model.toEntity())
        .toList(growable: false),
    unreadNotificationCount: unreadNotificationCount,
  );
}

/// Wire shape of the dashboard's profile strip.
@freezed
abstract class EmployeeHeadlineModel with _$EmployeeHeadlineModel {
  const factory EmployeeHeadlineModel({
    required String employeeId,
    required String fullName,
    String? department,
    String? designation,
    String? profilePhotoUrl,
  }) = _EmployeeHeadlineModel;

  const EmployeeHeadlineModel._();

  factory EmployeeHeadlineModel.fromJson(Map<String, dynamic> json) =>
      _$EmployeeHeadlineModelFromJson(json);

  EmployeeHeadline toEntity() => EmployeeHeadline(
    employeeId: employeeId,
    fullName: fullName,
    department: department,
    designation: designation,
    profilePhotoUrl: profilePhotoUrl,
  );
}

/// Wire shape of today's attendance on the dashboard.
@freezed
abstract class TodayAttendanceModel with _$TodayAttendanceModel {
  const factory TodayAttendanceModel({
    /// `yyyy-MM-dd`.
    required String attendanceDate,
    required String status,
    DateTime? checkInAt,
    DateTime? checkOutAt,
    @Default(false) bool isLateArrival,
    @Default(false) bool isEarlyDeparture,
    int? workedMinutes,
  }) = _TodayAttendanceModel;

  const TodayAttendanceModel._();

  factory TodayAttendanceModel.fromJson(Map<String, dynamic> json) =>
      _$TodayAttendanceModelFromJson(json);

  TodayAttendance toEntity() => TodayAttendance(
    attendanceDate:
        DateFormatter.parseApiDate(attendanceDate) ??
        DateFormatter.dateOnly(DateTime.now()),
    status: AttendanceStatusMapper.fromApi(status),
    checkInAt: checkInAt,
    checkOutAt: checkOutAt,
    isLateArrival: isLateArrival,
    isEarlyDeparture: isEarlyDeparture,
    workedMinutes: workedMinutes,
  );
}
