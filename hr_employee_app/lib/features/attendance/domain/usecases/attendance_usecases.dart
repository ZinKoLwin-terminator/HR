import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/repositories/attendance_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Today's attendance record.
class GetTodayAttendanceUseCase {
  const GetTodayAttendanceUseCase(this._repository);

  final AttendanceRepository _repository;

  Future<Result<AttendanceRecord?>> call() => _repository.getToday();
}

/// One page of attendance history.
class GetAttendanceHistoryUseCase {
  const GetAttendanceHistoryUseCase(this._repository);

  final AttendanceRepository _repository;

  Future<Result<PaginatedResponse<AttendanceRecord>>> call({
    required PageRequest page,
    DateTime? fromDate,
    DateTime? toDate,
  }) {
    return _repository.getHistory(
      page: page,
      fromDate: fromDate,
      toDate: toDate,
    );
  }
}

/// The monthly aggregate.
class GetMonthlyAttendanceSummaryUseCase {
  const GetMonthlyAttendanceSummaryUseCase(this._repository);

  final AttendanceRepository _repository;

  Future<Result<AttendanceSummary>> call({
    required int year,
    required int month,
  }) => _repository.getMonthlySummary(year: year, month: month);
}

/// A single record, for the detail screen.
class GetAttendanceDetailUseCase {
  const GetAttendanceDetailUseCase(this._repository);

  final AttendanceRepository _repository;

  Future<Result<AttendanceRecord>> call(String id) => _repository.getById(id);
}
