import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads and records attendance.
abstract interface class AttendanceRepository {
  /// Today's record, or a success carrying null when nothing is recorded yet.
  Future<Result<AttendanceRecord?>> getToday();

  /// Records a check-in. The server validates the location, not the client.
  Future<Result<AttendanceRecord>> checkIn(AttendancePunch punch);

  /// Records a check-out.
  Future<Result<AttendanceRecord>> checkOut(AttendancePunch punch);

  /// One page of history, newest first.
  Future<Result<PaginatedResponse<AttendanceRecord>>> getHistory({
    required PageRequest page,
    DateTime? fromDate,
    DateTime? toDate,
  });

  /// The monthly aggregate.
  Future<Result<AttendanceSummary>> getMonthlySummary({
    required int year,
    required int month,
  });

  /// A single record, for the detail screen.
  Future<Result<AttendanceRecord>> getById(String id);
}
