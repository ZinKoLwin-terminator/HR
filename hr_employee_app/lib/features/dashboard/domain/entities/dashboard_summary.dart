import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';

part 'dashboard_summary.freezed.dart';

/// Everything the home screen renders, from one request.
///
/// A single aggregate endpoint rather than five parallel calls: the dashboard
/// is the first thing a user sees, and one round trip on a slow connection
/// beats five.
///
/// It reuses [LeaveBalance] and [AppNotification] from their own features
/// instead of redeclaring near-identical types. The contract for those parts
/// of the payload is genuinely the same, so duplicating them would mean two
/// places to update and two chances to drift. The two types declared below
/// are *not* reused, because they are reduced forms that exist only here.
@freezed
abstract class DashboardSummary with _$DashboardSummary {
  const factory DashboardSummary({
    required EmployeeHeadline profile,

    /// Null when nothing has been recorded for today yet.
    TodayAttendance? todayAttendance,
    @Default(<LeaveBalance>[]) List<LeaveBalance> leaveBalances,
    @Default(<AppNotification>[]) List<AppNotification> recentNotifications,
    @Default(0) int unreadNotificationCount,
  }) = _DashboardSummary;

  const DashboardSummary._();

  /// Total days left across every leave type, for the headline figure.
  double get totalRemainingLeaveDays => leaveBalances.fold<double>(
    0,
    (double sum, LeaveBalance balance) => sum + balance.remainingDays,
  );

  bool get hasUnreadNotifications => unreadNotificationCount > 0;
}

/// The identity strip at the top of the dashboard.
///
/// A reduced form of the full employee profile: just enough to greet the user
/// and show their role, without pulling the whole record.
@freezed
abstract class EmployeeHeadline with _$EmployeeHeadline {
  const factory EmployeeHeadline({
    required String employeeId,
    required String fullName,
    String? department,
    String? designation,
    String? profilePhotoUrl,
  }) = _EmployeeHeadline;

  const EmployeeHeadline._();

  /// `Senior Accountant - Finance`, omitting either part when unknown.
  String get roleLine {
    final List<String> parts = <String>[
      if (designation != null && designation!.isNotEmpty) designation!,
      if (department != null && department!.isNotEmpty) department!,
    ];
    return parts.join(' - ');
  }

  /// First name only, for the greeting.
  String get firstName {
    final List<String> parts = fullName.trim().split(RegExp(r'\s+'));
    return parts.isEmpty ? fullName : parts.first;
  }
}

/// Today's attendance, reduced to what the dashboard card needs.
///
/// Separate from `AttendanceRecord` because the dashboard payload omits
/// locations, shift times and the record id. Modelling it as the full record
/// would mean inventing nulls for fields the endpoint never sends.
@freezed
abstract class TodayAttendance with _$TodayAttendance {
  const factory TodayAttendance({
    required DateTime attendanceDate,
    required AttendanceStatus status,
    DateTime? checkInAt,
    DateTime? checkOutAt,
    @Default(false) bool isLateArrival,
    @Default(false) bool isEarlyDeparture,
    int? workedMinutes,
  }) = _TodayAttendance;

  const TodayAttendance._();

  bool get hasCheckedIn => checkInAt != null;

  bool get hasCheckedOut => checkOutAt != null;

  /// What the quick-action button on the dashboard should offer.
  CheckInAvailability get availability {
    if (!status.isWorkingDay) {
      return CheckInAvailability.notRequired;
    }
    if (!hasCheckedIn) {
      return CheckInAvailability.canCheckIn;
    }
    if (!hasCheckedOut) {
      return CheckInAvailability.canCheckOut;
    }
    return CheckInAvailability.completed;
  }
}
