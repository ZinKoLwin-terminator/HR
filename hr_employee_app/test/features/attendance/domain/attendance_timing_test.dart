import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_timing.dart';

AttendanceRecord _day({
  required int inHour,
  required int inMinute,
  int? outHour,
  int outMinute = 0,
}) {
  final DateTime d = DateTime(2026, 11, 17);
  final DateTime checkIn = d.add(Duration(hours: inHour, minutes: inMinute));
  final DateTime? checkOut = outHour == null
      ? null
      : d.add(Duration(hours: outHour, minutes: outMinute));
  return AttendanceRecord(
    id: 'x',
    attendanceDate: d,
    status: AttendanceStatus.present,
    shiftStartAt: d.add(const Duration(hours: 9)),
    shiftEndAt: d.add(const Duration(hours: 18)),
    checkInAt: checkIn,
    checkOutAt: checkOut,
    workedMinutes: checkOut?.difference(checkIn).inMinutes,
  );
}

void main() {
  test('the design day: 9:02 to 6:10', () {
    final AttendanceRecord r = _day(
      inHour: 9,
      inMinute: 2,
      outHour: 18,
      outMinute: 10,
    );
    expect(AttendanceTiming.versusShiftStart(r), '2 min after shift start');
    expect(AttendanceTiming.versusShiftEnd(r), '10 min after shift end');
    expect(AttendanceTiming.versusShiftLength(r), '+8m over 9h shift');
  });

  test('early in, early out, short day', () {
    final AttendanceRecord r = _day(
      inHour: 8,
      inMinute: 55,
      outHour: 16,
      outMinute: 10,
    );
    expect(AttendanceTiming.versusShiftStart(r), '5 min before shift start');
    expect(AttendanceTiming.versusShiftEnd(r), '1h 50m before shift end');
    expect(AttendanceTiming.versusShiftLength(r), '-1h 45m under 9h shift');
  });

  test('on time, and an open day has no length comparison', () {
    final AttendanceRecord r = _day(inHour: 9, inMinute: 0);
    expect(AttendanceTiming.versusShiftStart(r), 'On time');
    expect(AttendanceTiming.versusShiftEnd(r), isNull);
    expect(AttendanceTiming.versusShiftLength(r), isNull);
  });
}
