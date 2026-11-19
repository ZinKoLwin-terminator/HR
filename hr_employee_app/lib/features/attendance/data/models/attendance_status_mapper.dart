import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';

/// Translates the API's `attendanceStatus` strings to [AttendanceStatus].
///
/// Lives in the data layer so the domain enum stays free of wire details.
/// Owned by the attendance feature; the dashboard feature imports it rather
/// than keeping a second copy, so there is one definition of what `"OnLeave"`
/// means.
///
/// Matching is case-insensitive and tolerant of separators, so `OnLeave`,
/// `on_leave` and `ON-LEAVE` all resolve. An unrecognised value becomes
/// [AttendanceStatus.unknown] instead of throwing — a status added
/// server-side should degrade one row, not break the screen.
abstract final class AttendanceStatusMapper {
  static const Map<String, AttendanceStatus> _byWireName =
      <String, AttendanceStatus>{
        'present': AttendanceStatus.present,
        'absent': AttendanceStatus.absent,
        'onleave': AttendanceStatus.onLeave,
        'holiday': AttendanceStatus.holiday,
        'weeklyoff': AttendanceStatus.weeklyOff,
        'halfday': AttendanceStatus.halfDay,
      };

  static const Map<AttendanceStatus, String> _toWireName =
      <AttendanceStatus, String>{
        AttendanceStatus.present: 'Present',
        AttendanceStatus.absent: 'Absent',
        AttendanceStatus.onLeave: 'OnLeave',
        AttendanceStatus.holiday: 'Holiday',
        AttendanceStatus.weeklyOff: 'WeeklyOff',
        AttendanceStatus.halfDay: 'HalfDay',
      };

  static AttendanceStatus fromApi(Object? value) {
    if (value is! String) {
      return AttendanceStatus.unknown;
    }
    return _byWireName[_normalize(value)] ?? AttendanceStatus.unknown;
  }

  static String toApi(AttendanceStatus status) =>
      _toWireName[status] ?? 'Present';

  static String _normalize(String value) =>
      value.toLowerCase().replaceAll(RegExp(r'[_\-\s]'), '');
}

/// Labels for [AttendanceStatus], for display.
///
/// Kept beside the mapper rather than derived from the enum name so the
/// wording can be edited without touching the domain.
extension AttendanceStatusLabel on AttendanceStatus {
  String get label => switch (this) {
    AttendanceStatus.present => 'Present',
    AttendanceStatus.absent => 'Absent',
    AttendanceStatus.onLeave => 'On leave',
    AttendanceStatus.holiday => 'Holiday',
    AttendanceStatus.weeklyOff => 'Weekly off',
    AttendanceStatus.halfDay => 'Half day',
    AttendanceStatus.unknown => 'Unknown',
  };
}
