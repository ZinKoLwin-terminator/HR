import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_controller.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_overview.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_providers.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
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

/// Every request the employee has made, newest first.
///
/// Loaded whole rather than a page at a time: the leave screen needs the
/// pending ones wherever they fall, and the all-requests screen groups by
/// month and counts every status, neither of which works on one page. An
/// employee files a few dozen requests a year, so this stays small. The
/// loop is capped so a server that never reports a last page cannot spin.
final leaveRequestHistoryProvider = FutureProvider<List<LeaveRequest>>((
  Ref ref,
) async {
  const int pageSize = 50;
  const int maxPages = 20;
  final List<LeaveRequest> all = <LeaveRequest>[];
  PageRequest page = const PageRequest(pageSize: pageSize);

  for (int i = 0; i < maxPages; i++) {
    final Result<PaginatedResponse<LeaveRequest>> result = await ref.read(
      getLeaveRequestsUseCaseProvider,
    )(page: page);
    final PaginatedResponse<LeaveRequest> response = result.valueOrThrow();
    all.addAll(response.items);
    if (!response.hasNextPage) {
      break;
    }
    page = page.next();
  }
  return all;
});

/// The requests for one year, with status counts and month groups.
final leaveRequestYearProvider = FutureProvider.family<LeaveRequestYear, int>((
  Ref ref,
  int year,
) async {
  final List<LeaveRequest> all = await ref.watch(
    leaveRequestHistoryProvider.future,
  );
  return LeaveRequestYear(year: year, all: all);
});

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

  /// Reloads the request history and the balances the new request
  /// consumed, so returning to the leave screen shows it immediately.
  void _onApplied(LeaveRequest request) {
    ref.invalidate(leaveRequestHistoryProvider);

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
      onSuccess: (LeaveRequest _) {
        // Reconcile the lists, the detail screen and the balances with the
        // server.
        ref.invalidate(leaveRequestHistoryProvider);
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
