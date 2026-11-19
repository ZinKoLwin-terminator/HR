import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/leave/data/datasources/leave_remote_datasource.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_models.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/domain/repositories/leave_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Maps leave payloads to entities.
class LeaveRepositoryImpl with RepositoryGuard implements LeaveRepository {
  LeaveRepositoryImpl({required this._remote, required this.logger});

  final LeaveRemoteDataSource _remote;

  @override
  final AppLogger logger;

  @override
  Future<Result<List<LeaveType>>> getLeaveTypes() {
    return guard(operation: 'getLeaveTypes', () async {
      final List<LeaveTypeModel> models = await _remote.getLeaveTypes();
      return models
          .map((LeaveTypeModel model) => model.toEntity())
          .toList(growable: false);
    });
  }

  @override
  Future<Result<List<LeaveBalance>>> getBalances({int? year}) {
    return guard(operation: 'getLeaveBalances', () async {
      final int effectiveYear = year ?? DateTime.now().year;
      final List<LeaveBalanceModel> models = await _remote.getBalances(
        year: effectiveYear,
      );
      // The dashboard payload omits `year`; passing it through keeps the
      // entity complete regardless of which endpoint supplied the model.
      return models
          .map(
            (LeaveBalanceModel model) =>
                model.toEntity(fallbackYear: effectiveYear),
          )
          .toList(growable: false);
    });
  }

  @override
  Future<Result<LeaveRequest>> apply(
    LeaveApplication application, {
    void Function(double progress)? onProgress,
  }) {
    return guard(operation: 'applyLeave', () async {
      final LeaveRequestModel model = await _remote.apply(
        application,
        onProgress: onProgress,
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<PaginatedResponse<LeaveRequest>>> getRequests({
    required PageRequest page,
    LeaveStatus? status,
  }) {
    return guard(operation: 'getLeaveRequests', () async {
      final PaginatedResponse<LeaveRequestModel> response = await _remote
          .getRequests(page: page, status: status);
      return response.mapItems((LeaveRequestModel model) => model.toEntity());
    });
  }

  @override
  Future<Result<LeaveRequest>> getRequestById(String id) {
    return guard(operation: 'getLeaveRequest', () async {
      final LeaveRequestModel model = await _remote.getRequestById(id);
      return model.toEntity();
    });
  }

  @override
  Future<Result<LeaveRequest>> cancelRequest(String id) {
    return guard(operation: 'cancelLeaveRequest', () async {
      final LeaveRequestModel model = await _remote.cancelRequest(id);
      return model.toEntity();
    });
  }
}
