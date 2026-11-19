import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/features/leave/data/datasources/leave_remote_datasource.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_models.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/shared/helpers/mock_fixtures.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// A fixture-backed stand-in for [LeaveRemoteDataSource].
///
/// NOT A BACKEND — see [MockSupport].
///
/// Applying leave appends to [MockFixtures], so the new request appears in
/// the list and consumes pending days in the balance, which is what makes the
/// flow reviewable end to end. Upload progress is simulated so the apply
/// form's progress bar is exercised rather than assumed.
class LeaveMockDataSource implements LeaveRemoteDataSource {
  const LeaveMockDataSource();

  @override
  Future<List<LeaveTypeModel>> getLeaveTypes() async {
    await MockSupport.delay(const Duration(milliseconds: 300));
    return MockFixtures.leaveTypes;
  }

  @override
  Future<List<LeaveBalanceModel>> getBalances({int? year}) async {
    await MockSupport.delay(const Duration(milliseconds: 400));
    return MockFixtures.instance.leaveBalances;
  }

  @override
  Future<LeaveRequestModel> apply(
    LeaveApplication application, {
    void Function(double progress)? onProgress,
  }) async {
    // Walk the progress bar so the UI path is real, then "submit".
    if (application.attachmentPaths.isNotEmpty && onProgress != null) {
      for (int step = 1; step <= 5; step++) {
        await MockSupport.delay(const Duration(milliseconds: 160));
        onProgress(step / 5);
      }
    } else {
      await MockSupport.delay(const Duration(milliseconds: 800));
    }

    // Mirror the server's overlap rule so the conflict path is exercised.
    final bool overlaps = MockFixtures.instance.leaveRequests.any((
      LeaveRequestModel existing,
    ) {
      if (existing.status != 'Pending' && existing.status != 'Approved') {
        return false;
      }
      final DateTime? start = DateTime.tryParse(existing.startDate);
      final DateTime? end = DateTime.tryParse(existing.endDate);
      if (start == null || end == null) {
        return false;
      }
      return !application.endDate.isBefore(start) &&
          !application.startDate.isAfter(end);
    });

    if (overlaps) {
      throw const ConflictException(
        'These dates overlap a leave request you have already submitted.',
        statusCode: 422,
      );
    }

    return MockFixtures.instance.addLeaveRequest(
      leaveTypeId: application.leaveTypeId,
      startDate: application.startDate,
      endDate: application.endDate,
      reason: application.reason,
      dayPortion: LeaveDayPortionMapper.toApi(application.dayPortion),
      attachmentCount: application.attachmentPaths.length,
      contactDuringLeave: application.contactDuringLeave,
    );
  }

  @override
  Future<PaginatedResponse<LeaveRequestModel>> getRequests({
    required PageRequest page,
    LeaveStatus? status,
  }) async {
    await MockSupport.delay();

    List<LeaveRequestModel> all = MockFixtures.instance.leaveRequests;
    if (status != null) {
      final String? wireStatus = LeaveStatusMapper.toApi(status);
      all = all.where((LeaveRequestModel r) => r.status == wireStatus).toList();
    }

    final List<LeaveRequestModel> items = MockSupport.page(
      all,
      page.pageNumber,
      page.pageSize,
    );
    final int totalPages = all.isEmpty
        ? 1
        : (all.length / page.pageSize).ceil();

    return PaginatedResponse<LeaveRequestModel>(
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
  Future<LeaveRequestModel> getRequestById(String id) async {
    await MockSupport.delay(const Duration(milliseconds: 300));

    final LeaveRequestModel? request = MockFixtures.instance.leaveRequests
        .where((LeaveRequestModel r) => r.id == id)
        .firstOrNull;

    if (request == null) {
      throw const NotFoundException('That leave request was not found.');
    }
    return request;
  }

  @override
  Future<LeaveRequestModel> cancelRequest(String id) async {
    await MockSupport.delay(const Duration(milliseconds: 600));

    final LeaveRequestModel? existing = MockFixtures.instance.leaveRequests
        .where((LeaveRequestModel r) => r.id == id)
        .firstOrNull;

    if (existing == null) {
      throw const NotFoundException('That leave request was not found.');
    }
    if (existing.status != 'Pending') {
      throw const ConflictException(
        'Only a pending request can be withdrawn.',
        statusCode: 409,
      );
    }

    final LeaveRequestModel? cancelled = MockFixtures.instance
        .cancelLeaveRequest(id);
    if (cancelled == null) {
      throw const ServerException('Could not withdraw the request.');
    }
    return cancelled;
  }
}
