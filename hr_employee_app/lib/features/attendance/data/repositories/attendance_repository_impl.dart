import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/attendance/data/datasources/attendance_remote_datasource.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_models.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/repositories/attendance_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Maps attendance payloads to entities.
class AttendanceRepositoryImpl
    with RepositoryGuard
    implements AttendanceRepository {
  AttendanceRepositoryImpl({
    required this._remote,
    required this._deviceId,
    required this.logger,
  });

  final AttendanceRemoteDataSource _remote;

  /// Sent with every punch so HR can see which device recorded it.
  final String _deviceId;

  @override
  final AppLogger logger;

  @override
  Future<Result<AttendanceRecord?>> getToday() {
    return guard(operation: 'getTodayAttendance', () async {
      final AttendanceRecordModel? model = await _remote.getToday();
      return model?.toEntity();
    });
  }

  @override
  Future<Result<AttendanceRecord>> checkIn(AttendancePunch punch) {
    return guard(operation: 'checkIn', () async {
      final AttendanceRecordModel model = await _remote.checkIn(
        AttendancePunchRequestModel.fromEntity(punch, deviceId: _deviceId),
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<AttendanceRecord>> checkOut(AttendancePunch punch) {
    return guard(operation: 'checkOut', () async {
      final AttendanceRecordModel model = await _remote.checkOut(
        AttendancePunchRequestModel.fromEntity(punch, deviceId: _deviceId),
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<PaginatedResponse<AttendanceRecord>>> getHistory({
    required PageRequest page,
    DateTime? fromDate,
    DateTime? toDate,
  }) {
    return guard(operation: 'getAttendanceHistory', () async {
      final PaginatedResponse<AttendanceRecordModel> response = await _remote
          .getHistory(page: page, fromDate: fromDate, toDate: toDate);
      // `mapItems` keeps the page metadata, so pagination survives the
      // model-to-entity conversion.
      return response.mapItems(
        (AttendanceRecordModel model) => model.toEntity(),
      );
    });
  }

  @override
  Future<Result<AttendanceSummary>> getMonthlySummary({
    required int year,
    required int month,
  }) {
    return guard(operation: 'getAttendanceSummary', () async {
      final AttendanceSummaryModel model = await _remote.getMonthlySummary(
        year: year,
        month: month,
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<AttendanceRecord>> getById(String id) {
    return guard(operation: 'getAttendanceById', () async {
      final AttendanceRecordModel model = await _remote.getById(id);
      return model.toEntity();
    });
  }
}
