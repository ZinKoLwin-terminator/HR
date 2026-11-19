import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/paginated_list_notifier.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_controller.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_providers.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// The selectable leave types, for the apply form.
final leaveTypesProvider = FutureProvider<List<LeaveType>>((Ref ref) async {
  final Result<List<LeaveType>> result = await ref.watch(
    getLeaveTypesUseCaseProvider,
  )();
  return result.valueOrThrow();
});

/// Leave balances for a year. Defaults to the current one.
final leaveBalancesProvider = FutureProvider.family<List<LeaveBalance>, int>((
  Ref ref,
  int year,
) async {
  final Result<List<LeaveBalance>> result = await ref.watch(
    getLeaveBalancesUseCaseProvider,
  )(year: year);
  return result.valueOrThrow();
});

/// The status filter applied to the request list.
///
/// A `Notifier` rather than local widget state so the list controller can
/// watch it and reload itself when it changes.
class LeaveStatusFilter extends Notifier<LeaveStatus?> {
  @override
  LeaveStatus? build() => null;

  /// Null clears the filter and shows everything.
  void set(LeaveStatus? status) => state = status;
}

final NotifierProvider<LeaveStatusFilter, LeaveStatus?>
leaveStatusFilterProvider = NotifierProvider<LeaveStatusFilter, LeaveStatus?>(
  LeaveStatusFilter.new,
);

/// Paginated leave requests, newest first, honouring the status filter.
class LeaveRequestsController extends PaginatedListNotifier<LeaveRequest> {
  @override
  Future<Result<PaginatedResponse<LeaveRequest>>> fetchPage(PageRequest page) {
    // Watched, not read: changing the filter rebuilds this provider, which
    // reloads from page 1 — exactly the right behaviour for a filter change.
    final LeaveStatus? status = ref.watch(leaveStatusFilterProvider);
    return ref.read(getLeaveRequestsUseCaseProvider)(
      page: page,
      status: status,
    );
  }

  @override
  Object identityOf(LeaveRequest item) => item.id;
}

final AsyncNotifierProvider<
  LeaveRequestsController,
  PaginatedState<LeaveRequest>
>
leaveRequestsControllerProvider =
    AsyncNotifierProvider<
      LeaveRequestsController,
      PaginatedState<LeaveRequest>
    >(LeaveRequestsController.new);

/// A single leave request, for the detail screen.
final leaveRequestDetailProvider = FutureProvider.family<LeaveRequest, String>((
  Ref ref,
  String id,
) async {
  final Result<LeaveRequest> result = await ref.watch(
    getLeaveRequestDetailUseCaseProvider,
  )(id);
  return result.valueOrThrow();
});

/// Submits a leave application.
///
/// Holds upload progress as well as the submission state, because an
/// application with attachments is slow enough that a determinate bar is
/// worth showing.
class ApplyLeaveController extends Notifier<ApplyLeaveUiState> {
  @override
  ApplyLeaveUiState build() => const ApplyLeaveUiState();

  Future<LeaveRequest?> submit(
    LeaveApplication application, {
    LeaveType? leaveType,
    LeaveBalance? balance,
  }) async {
    if (state.submission.isSubmitting) {
      return null;
    }
    state = const ApplyLeaveUiState(submission: SubmissionState.submitting());

    final Result<LeaveRequest> result =
        await ref.read(applyLeaveUseCaseProvider)(
          application,
          leaveType: leaveType,
          balance: balance,
          onProgress: (double progress) {
            // Only meaningful while submitting; a late callback after a failure
            // must not resurrect the progress bar.
            if (state.submission.isSubmitting) {
              state = state.copyWith(uploadProgress: progress);
            }
          },
        );

    return result.fold(
      onSuccess: (LeaveRequest request) {
        _onApplied(request);
        state = const ApplyLeaveUiState(
          submission: SubmissionState.success(
            message: 'Leave request submitted.',
          ),
          uploadProgress: 1,
        );
        return request;
      },
      onFailure: (failure) {
        state = ApplyLeaveUiState(submission: SubmissionState.failure(failure));
        return null;
      },
    );
  }

  void reset() => state = const ApplyLeaveUiState();

  /// Pushes the new request into the list and refreshes the balances it
  /// consumed, so returning to the leave screen shows it immediately.
  void _onApplied(LeaveRequest request) {
    ref.read(leaveRequestsControllerProvider.notifier).prependItem(request);

    // Pending days have moved, and the server owns that arithmetic.
    ref.invalidate(leaveBalancesProvider);
    ref.invalidate(dashboardControllerProvider);
  }
}

/// Submission state plus upload progress for the apply form.
class ApplyLeaveUiState {
  const ApplyLeaveUiState({
    this.submission = const SubmissionState.idle(),
    this.uploadProgress = 0,
  });

  final SubmissionState submission;

  /// 0 to 1. Only meaningful while [submission] is submitting and the
  /// application carries attachments.
  final double uploadProgress;

  ApplyLeaveUiState copyWith({
    SubmissionState? submission,
    double? uploadProgress,
  }) {
    return ApplyLeaveUiState(
      submission: submission ?? this.submission,
      uploadProgress: uploadProgress ?? this.uploadProgress,
    );
  }
}

final NotifierProvider<ApplyLeaveController, ApplyLeaveUiState>
applyLeaveControllerProvider =
    NotifierProvider<ApplyLeaveController, ApplyLeaveUiState>(
      ApplyLeaveController.new,
    );

/// Withdraws a pending leave request.
class CancelLeaveController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  Future<bool> cancel(String id, {LeaveRequest? current}) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final Result<LeaveRequest> result = await ref.read(
      cancelLeaveRequestUseCaseProvider,
    )(id, current: current);

    return result.fold(
      onSuccess: (LeaveRequest updated) {
        // Patch the row in place, then let the detail screen and balances
        // reconcile with the server.
        ref.read(leaveRequestsControllerProvider.notifier).replaceItem(updated);
        ref.invalidate(leaveRequestDetailProvider(id));
        ref.invalidate(leaveBalancesProvider);
        ref.invalidate(dashboardControllerProvider);

        state = const SubmissionState.success(
          message: 'Leave request withdrawn.',
        );
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  void reset() => state = const SubmissionState.idle();
}

final NotifierProvider<CancelLeaveController, SubmissionState>
cancelLeaveControllerProvider =
    NotifierProvider<CancelLeaveController, SubmissionState>(
      CancelLeaveController.new,
    );
