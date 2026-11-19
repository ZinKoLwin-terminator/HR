import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/domain/repositories/leave_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// The selectable leave types.
class GetLeaveTypesUseCase {
  const GetLeaveTypesUseCase(this._repository);

  final LeaveRepository _repository;

  /// Returns only active types, in display order.
  ///
  /// Filtering here rather than in the dropdown means a retired leave type
  /// cannot be selected from anywhere.
  Future<Result<List<LeaveType>>> call() async {
    final Result<List<LeaveType>> result = await _repository.getLeaveTypes();
    return result.map(
      (List<LeaveType> types) =>
          types.where((LeaveType type) => type.isActive).toList(growable: false)
            ..sort((LeaveType a, LeaveType b) => a.name.compareTo(b.name)),
    );
  }
}

/// Leave balances for a year.
class GetLeaveBalancesUseCase {
  const GetLeaveBalancesUseCase(this._repository);

  final LeaveRepository _repository;

  Future<Result<List<LeaveBalance>>> call({int? year}) =>
      _repository.getBalances(year: year);
}

/// One page of the employee's leave requests.
class GetLeaveRequestsUseCase {
  const GetLeaveRequestsUseCase(this._repository);

  final LeaveRepository _repository;

  Future<Result<PaginatedResponse<LeaveRequest>>> call({
    required PageRequest page,
    LeaveStatus? status,
  }) => _repository.getRequests(page: page, status: status);
}

/// A single leave request.
class GetLeaveRequestDetailUseCase {
  const GetLeaveRequestDetailUseCase(this._repository);

  final LeaveRepository _repository;

  Future<Result<LeaveRequest>> call(String id) =>
      _repository.getRequestById(id);
}

/// Withdraws a pending leave request.
class CancelLeaveRequestUseCase {
  const CancelLeaveRequestUseCase(this._repository);

  final LeaveRepository _repository;

  /// [current] is the request as last loaded. Supplying it lets an
  /// already-decided request be rejected without a pointless round trip, and
  /// gives a clearer message than the server's generic conflict.
  Future<Result<LeaveRequest>> call(String id, {LeaveRequest? current}) async {
    if (current != null && !current.isCancellable) {
      return Result<LeaveRequest>.failure(
        ConflictFailure(
          'This request has already been '
          '${current.status == LeaveStatus.cancelled ? 'cancelled' : 'decided'}'
          ' and can no longer be withdrawn.',
        ),
      );
    }
    return _repository.cancelRequest(id);
  }
}
