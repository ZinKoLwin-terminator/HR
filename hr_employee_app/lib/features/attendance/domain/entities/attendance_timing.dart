import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';

/// How a day's punches compare with its rostered shift, in words.
///
/// Only describes the gaps; whether a gap counts as late or early is the
/// server's call ([AttendanceRecord.isLateArrival] and
/// [AttendanceRecord.isEarlyDeparture]), because grace periods live there.
abstract final class AttendanceTiming {
  /// `2 min`, `1h 50m`, `3h`.
  static String gap(int minutes) {
    final int m = minutes.abs();
    if (m < 60) {
      return '$m min';
    }
    final int h = m ~/ 60;
    final int rest = m % 60;
    return rest == 0 ? '${h}h' : '${h}h ${rest}m';
  }

  /// `2 min after shift start`, `5 min before shift start`, `On time`.
  /// Null when either time is unknown.
  static String? versusShiftStart(AttendanceRecord record) =>
      _versus(record.checkInAt, record.shiftStartAt, 'shift start');

  /// `10 min after shift end`, `1h 50m before shift end`.
  static String? versusShiftEnd(AttendanceRecord record) =>
      _versus(record.checkOutAt, record.shiftEndAt, 'shift end');

  static String? _versus(DateTime? actual, DateTime? planned, String what) {
    if (actual == null || planned == null) {
      return null;
    }
    final int minutes = actual.difference(planned).inMinutes;
    if (minutes == 0) {
      return 'On time';
    }
    return '${gap(minutes)} ${minutes > 0 ? 'after' : 'before'} $what';
  }

  /// Length of the rostered shift in minutes, or null when unknown.
  static int? shiftMinutes(AttendanceRecord record) {
    final DateTime? start = record.shiftStartAt;
    final DateTime? end = record.shiftEndAt;
    if (start == null || end == null || !end.isAfter(start)) {
      return null;
    }
    return end.difference(start).inMinutes;
  }

  /// `+8m over 9h shift`, `-1h 52m under 9h shift`, `Exactly 9h shift`.
  /// Null unless the day is closed and the shift is known.
  static String? versusShiftLength(AttendanceRecord record) {
    final int? shift = shiftMinutes(record);
    final int? worked = record.workedMinutes;
    if (shift == null || worked == null || !record.hasCheckedOut) {
      return null;
    }
    final int diff = worked - shift;
    final String length = gap(shift).replaceAll(' min', 'm');
    if (diff == 0) {
      return 'Exactly $length shift';
    }
    final String amount = gap(diff).replaceAll(' min', 'm');
    return diff > 0
        ? '+$amount over $length shift'
        : '-$amount under $length shift';
  }
}
