import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';

part 'attendance_record.freezed.dart';

/// One day of attendance.
///
/// The late/early flags and minute counts are sent by the server rather than
/// computed here. That is deliberate: lateness depends on the shift roster,
/// grace periods and public holidays, none of which the app knows. Recomputing
/// them client-side would eventually disagree with payroll, and payroll is
/// the authority.
@freezed
abstract class AttendanceRecord with _$AttendanceRecord {
  const factory AttendanceRecord({
    required String id,

    /// The calendar day this record belongs to, with no time component.
    required DateTime attendanceDate,
    required AttendanceStatus status,
    DateTime? checkInAt,
    DateTime? checkOutAt,
    GeoLocation? checkInLocation,
    GeoLocation? checkOutLocation,

    /// Rostered shift boundaries, when the server supplies them.
    DateTime? shiftStartAt,
    DateTime? shiftEndAt,
    @Default(false) bool isLateArrival,
    @Default(0) int lateByMinutes,
    @Default(false) bool isEarlyDeparture,
    @Default(0) int earlyByMinutes,

    /// Minutes actually worked, as calculated by payroll.
    int? workedMinutes,
    @Default(0) int overtimeMinutes,
    String? remark,
  }) = _AttendanceRecord;

  const AttendanceRecord._();

  bool get hasCheckedIn => checkInAt != null;

  bool get hasCheckedOut => checkOutAt != null;

  /// Whether the day is still open, i.e. checked in but not out.
  bool get isInProgress => hasCheckedIn && !hasCheckedOut;

  /// What the user may do next for this day.
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

  /// Minutes elapsed since check-in for a day still in progress.
  ///
  /// Used only as a live "time on site" readout. [workedMinutes] from the
  /// server remains the figure of record once the day is closed.
  int? elapsedMinutes({DateTime? now}) {
    final DateTime? start = checkInAt;
    if (start == null) {
      return null;
    }
    final DateTime end = checkOutAt ?? (now ?? DateTime.now());
    final int minutes = end.difference(start).inMinutes;
    return minutes < 0 ? 0 : minutes;
  }
}

/// A month of attendance, aggregated by the server.
@freezed
abstract class AttendanceSummary with _$AttendanceSummary {
  const factory AttendanceSummary({
    required int year,
    required int month,
    @Default(0) int totalWorkingDays,
    @Default(0) int presentDays,
    @Default(0) int absentDays,
    @Default(0) int leaveDays,
    @Default(0) int holidayDays,
    @Default(0) int lateArrivals,
    @Default(0) int earlyDepartures,
    @Default(0) int totalWorkedMinutes,
    @Default(0) int totalOvertimeMinutes,
  }) = _AttendanceSummary;

  const AttendanceSummary._();

  /// Attendance rate as a fraction from 0 to 1, or null when the month has no
  /// working days (so the UI shows nothing rather than a misleading 0%).
  double? get attendanceRate {
    if (totalWorkingDays <= 0) {
      return null;
    }
    return (presentDays / totalWorkingDays).clamp(0.0, 1.0);
  }

  /// First day of the month this summary describes.
  DateTime get period => DateTime(year, month);
}

/// What the client submits for a check-in or check-out.
@freezed
abstract class AttendancePunch with _$AttendancePunch {
  const factory AttendancePunch({
    /// When the device recorded the action.
    required DateTime capturedAt,

    /// Null when location is unavailable or the user declined it, and the
    /// tenant permits that. The server decides whether to accept it.
    GeoLocation? location,
    String? remark,
  }) = _AttendancePunch;
}
