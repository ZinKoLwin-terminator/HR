import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads leave entitlements and manages applications.
abstract interface class LeaveRepository {
  /// The leave types the employer offers.
  Future<Result<List<LeaveType>>> getLeaveTypes();

  /// Balances for a year, defaulting to the current one.
  Future<Result<List<LeaveBalance>>> getBalances({int? year});

  /// Submits an application, uploading any attachments.
  ///
  /// [onProgress] reports upload progress from 0 to 1 so the form can show a
  /// determinate bar; attachments make this slow enough to matter.
  Future<Result<LeaveRequest>> apply(
    LeaveApplication application, {
    void Function(double progress)? onProgress,
  });

  /// One page of the employee's own requests, newest first.
  Future<Result<PaginatedResponse<LeaveRequest>>> getRequests({
    required PageRequest page,
    LeaveStatus? status,
  });

  /// A single request, for the detail screen.
  Future<Result<LeaveRequest>> getRequestById(String id);

  /// Withdraws a request that is still pending.
  Future<Result<LeaveRequest>> cancelRequest(String id);
}
