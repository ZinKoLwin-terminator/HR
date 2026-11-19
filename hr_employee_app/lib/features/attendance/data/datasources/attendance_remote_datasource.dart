import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_models.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads and records attendance over HTTP.
abstract interface class AttendanceRemoteDataSource {
  Future<AttendanceRecordModel?> getToday();

  Future<AttendanceRecordModel> checkIn(AttendancePunchRequestModel request);

  Future<AttendanceRecordModel> checkOut(AttendancePunchRequestModel request);

  Future<PaginatedResponse<AttendanceRecordModel>> getHistory({
    required PageRequest page,
    DateTime? fromDate,
    DateTime? toDate,
  });

  Future<AttendanceSummaryModel> getMonthlySummary({
    required int year,
    required int month,
  });

  Future<AttendanceRecordModel> getById(String id);
}

/// Calls the real API.
class AttendanceRemoteDataSourceImpl implements AttendanceRemoteDataSource {
  const AttendanceRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<AttendanceRecordModel?> getToday() {
    // The contract allows a null payload before the first punch of the day,
    // which is why this decodes a nullable object rather than requiring one.
    return _client.get<AttendanceRecordModel?>(
      ApiEndpoints.attendanceToday,
      decode: (Object? data) {
        final Map<String, dynamic>? json = Decode.nullableObject(data);
        return json == null ? null : AttendanceRecordModel.fromJson(json);
      },
    );
  }

  @override
  Future<AttendanceRecordModel> checkIn(AttendancePunchRequestModel request) {
    return _client.post<AttendanceRecordModel>(
      ApiEndpoints.attendanceCheckIn,
      body: request.toJson(),
      // Lets the server collapse a retry caused by a dropped response into
      // the original punch, instead of recording a second one.
      headers: <String, dynamic>{
        ApiHeaders.idempotencyKey: _idempotencyKey('in', request),
      },
      decode: (Object? data) =>
          AttendanceRecordModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<AttendanceRecordModel> checkOut(AttendancePunchRequestModel request) {
    return _client.post<AttendanceRecordModel>(
      ApiEndpoints.attendanceCheckOut,
      body: request.toJson(),
      headers: <String, dynamic>{
        ApiHeaders.idempotencyKey: _idempotencyKey('out', request),
      },
      decode: (Object? data) =>
          AttendanceRecordModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<PaginatedResponse<AttendanceRecordModel>> getHistory({
    required PageRequest page,
    DateTime? fromDate,
    DateTime? toDate,
  }) {
    return _client.getPage<AttendanceRecordModel>(
      ApiEndpoints.attendanceHistory,
      page: page,
      fromItem: AttendanceRecordModel.fromJson,
      queryParameters: <String, dynamic>{
        ApiQueryKeys.fromDate: fromDate == null
            ? null
            : DateFormatter.toApiDate(fromDate),
        ApiQueryKeys.toDate: toDate == null
            ? null
            : DateFormatter.toApiDate(toDate),
      },
    );
  }

  @override
  Future<AttendanceSummaryModel> getMonthlySummary({
    required int year,
    required int month,
  }) {
    return _client.get<AttendanceSummaryModel>(
      ApiEndpoints.attendanceSummary,
      queryParameters: <String, dynamic>{
        ApiQueryKeys.year: year,
        ApiQueryKeys.month: month,
      },
      decode: (Object? data) =>
          AttendanceSummaryModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<AttendanceRecordModel> getById(String id) {
    return _client.get<AttendanceRecordModel>(
      ApiEndpoints.attendanceById(id),
      decode: (Object? data) =>
          AttendanceRecordModel.fromJson(Decode.object(data)),
    );
  }

  /// A key that is stable for one punch attempt but differs between days and
  /// between check-in and check-out.
  ///
  /// Derived from the device and the captured timestamp rather than generated
  /// randomly, so an automatic retry of the *same* attempt reuses it.
  String _idempotencyKey(String kind, AttendancePunchRequestModel request) {
    final String day = DateFormatter.toApiDate(request.capturedAt.toUtc());
    return '${request.deviceId}:$kind:$day:'
        '${request.capturedAt.toUtc().millisecondsSinceEpoch}';
  }
}
