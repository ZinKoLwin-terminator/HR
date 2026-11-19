import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/features/attendance/data/datasources/attendance_remote_datasource.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_models.dart';
import 'package:hr_employee_app/shared/helpers/mock_fixtures.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// A fixture-backed stand-in for [AttendanceRemoteDataSource].
///
/// NOT A BACKEND — see [MockSupport].
///
/// Two behaviours are modelled faithfully rather than stubbed, because the
/// screens depend on them:
///   * **state changes.** A check-in mutates [MockFixtures], so the dashboard
///     and the attendance screen both reflect it afterwards.
///   * **conflicts.** Checking in twice returns the same `409` the server
///     would, so the error path is exercised instead of assumed.
///
/// What it does *not* do is pretend to verify a location. The punch is
/// recorded with whatever position it is handed, exactly as the real
/// endpoint would, because geofencing is the server's job.
class AttendanceMockDataSource implements AttendanceRemoteDataSource {
  const AttendanceMockDataSource();

  @override
  Future<AttendanceRecordModel?> getToday() async {
    await MockSupport.delay(const Duration(milliseconds: 350));
    return MockFixtures.instance.todayRecord;
  }

  @override
  Future<AttendanceRecordModel> checkIn(
    AttendancePunchRequestModel request,
  ) async {
    await MockSupport.delay(const Duration(milliseconds: 900));

    if (MockFixtures.instance.todayRecord != null) {
      throw const ConflictException(
        'You have already checked in today.',
        statusCode: 409,
      );
    }

    return MockFixtures.instance.checkIn(location: request.location);
  }

  @override
  Future<AttendanceRecordModel> checkOut(
    AttendancePunchRequestModel request,
  ) async {
    await MockSupport.delay(const Duration(milliseconds: 900));

    final AttendanceRecordModel? current = MockFixtures.instance.todayRecord;
    if (current == null) {
      throw const ConflictException(
        'You have not checked in today.',
        statusCode: 409,
      );
    }
    if (current.checkOutAt != null) {
      throw const ConflictException(
        'You have already checked out today.',
        statusCode: 409,
      );
    }

    final AttendanceRecordModel? updated = MockFixtures.instance.checkOut(
      location: request.location,
    );
    if (updated == null) {
      throw const ServerException('Could not record the check-out.');
    }
    return updated;
  }

  @override
  Future<PaginatedResponse<AttendanceRecordModel>> getHistory({
    required PageRequest page,
    DateTime? fromDate,
    DateTime? toDate,
  }) async {
    await MockSupport.delay();

    List<AttendanceRecordModel> all = MockFixtures.instance.attendanceHistory;

    // Apply the same date filter the API would, so paging and filtering can
    // be reviewed together.
    if (fromDate != null || toDate != null) {
      all = all.where((AttendanceRecordModel record) {
        final DateTime? date = DateTime.tryParse(record.attendanceDate);
        if (date == null) {
          return false;
        }
        if (fromDate != null && date.isBefore(fromDate)) {
          return false;
        }
        if (toDate != null && date.isAfter(toDate)) {
          return false;
        }
        return true;
      }).toList();
    }

    final List<AttendanceRecordModel> items = MockSupport.page(
      all,
      page.pageNumber,
      page.pageSize,
    );
    final int totalPages = (all.length / page.pageSize).ceil();

    return PaginatedResponse<AttendanceRecordModel>(
      items: items,
      pageNumber: page.pageNumber,
      pageSize: page.pageSize,
      totalCount: all.length,
      totalPages: totalPages,
      hasNextPage: page.pageNumber < totalPages,
      hasPreviousPage: page.pageNumber > 1,
    );
  }

  @override
  Future<AttendanceSummaryModel> getMonthlySummary({
    required int year,
    required int month,
  }) async {
    await MockSupport.delay(const Duration(milliseconds: 400));
    return MockFixtures.instance.attendanceSummary(year: year, month: month);
  }

  @override
  Future<AttendanceRecordModel> getById(String id) async {
    await MockSupport.delay(const Duration(milliseconds: 300));

    final AttendanceRecordModel? today = MockFixtures.instance.todayRecord;
    if (today != null && today.id == id) {
      return today;
    }

    final AttendanceRecordModel? record = MockFixtures
        .instance
        .attendanceHistory
        .where((AttendanceRecordModel r) => r.id == id)
        .firstOrNull;

    if (record == null) {
      throw const NotFoundException('That attendance record was not found.');
    }
    return record;
  }
}
