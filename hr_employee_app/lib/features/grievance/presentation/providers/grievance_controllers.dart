import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/paginated_list_notifier.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_providers.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// The categories and departments the form offers.
final FutureProvider<GrievanceOptions> grievanceOptionsProvider =
    FutureProvider<GrievanceOptions>((Ref ref) async {
      final Result<GrievanceOptions> result = await ref.watch(
        getGrievanceOptionsUseCaseProvider,
      )();
      return result.valueOrThrow();
    });

/// The status chip selected on the list. Null is "All".
class GrievanceStatusFilter extends Notifier<GrievanceStatus?> {
  @override
  GrievanceStatus? build() => null;

  void select(GrievanceStatus? status) => state = status;
}

final NotifierProvider<GrievanceStatusFilter, GrievanceStatus?>
grievanceStatusFilterProvider =
    NotifierProvider<GrievanceStatusFilter, GrievanceStatus?>(
      GrievanceStatusFilter.new,
    );

/// The employee's grievances, most recently updated first.
class MyGrievancesController extends PaginatedListNotifier<GrievanceSummary> {
  @override
  Future<Result<PaginatedResponse<GrievanceSummary>>> fetchPage(
    PageRequest page,
  ) {
    // Watched so changing the filter reloads from page 1.
    final GrievanceStatus? status = ref.watch(grievanceStatusFilterProvider);
    return ref.read(getMyGrievancesUseCaseProvider)(page: page, status: status);
  }

  @override
  Object identityOf(GrievanceSummary item) => item.id;

  /// Drops the unread dot of one row, after its detail was opened.
  void markSeenLocally(int id) {
    final GrievanceSummary? row = state.value?.items
        .where((GrievanceSummary g) => g.id == id)
        .firstOrNull;
    if (row != null && row.hasUnreadUpdate) {
      replaceItem(row.copyWith(hasUnreadUpdate: false));
    }
  }
}

final AsyncNotifierProvider<
  MyGrievancesController,
  PaginatedState<GrievanceSummary>
>
myGrievancesControllerProvider =
    AsyncNotifierProvider<
      MyGrievancesController,
      PaginatedState<GrievanceSummary>
    >(MyGrievancesController.new);

/// A single grievance, for the detail screen and the edit form.
final grievanceDetailProvider = FutureProvider.family<GrievanceDetail, int>((
  Ref ref,
  int id,
) async {
  final Result<GrievanceDetail> result = await ref.watch(
    getGrievanceDetailUseCaseProvider,
  )(id);
  return result.valueOrThrow();
});

/// Saves and submits the new / edit draft form.
///
/// Keeps the draft as last saved, so a second save updates it (with its
/// fresh row version) instead of creating another, and so submitting a form
/// with unsaved edits saves them first, as the web form does.
class GrievanceFormController extends Notifier<GrievanceFormState> {
  @override
  GrievanceFormState build() => const GrievanceFormState();

  /// Starts editing [draft], or a new grievance when it is null.
  void start(GrievanceDetail? draft) =>
      state = GrievanceFormState(saved: draft);

  /// Creates or updates the draft. Returns it, or null on failure.
  Future<GrievanceDetail?> saveDraft(
    GrievanceDraftInput input, {
    GrievanceCategory? category,
  }) async {
    if (state.submission.isSubmitting) {
      return null;
    }
    state = state.copyWith(submission: const SubmissionState.submitting());
    final Result<GrievanceDetail> result = await _persist(input, category);
    return result.fold(
      onSuccess: (GrievanceDetail saved) {
        _onChanged(saved.id);
        state = GrievanceFormState(
          saved: saved,
          submission: const SubmissionState.success(
            message: 'Draft saved. Nothing has been sent to HR yet.',
          ),
        );
        return saved;
      },
      onFailure: _fail,
    );
  }

  /// Saves the form, then submits the draft with the declaration.
  Future<GrievanceDetail?> submit(
    GrievanceDraftInput input, {
    required bool declarationAccepted,
    GrievanceCategory? category,
  }) async {
    if (state.submission.isSubmitting) {
      return null;
    }
    state = state.copyWith(submission: const SubmissionState.submitting());

    final Result<GrievanceDetail> saved = await _persist(input, category);
    final GrievanceDetail? draft = saved.valueOrNull;
    if (draft == null) {
      return _fail(saved.failureOrNull!);
    }
    // The draft exists now even if the submission is refused below.
    state = state.copyWith(saved: draft);
    _onChanged(draft.id);

    final Result<GrievanceDetail> result = await ref.read(
      submitGrievanceUseCaseProvider,
    )(draft, declarationAccepted: declarationAccepted, category: category);
    return result.fold(
      onSuccess: (GrievanceDetail submitted) {
        _onChanged(submitted.id);
        state = GrievanceFormState(
          saved: submitted,
          submission: SubmissionState.success(
            message: submitted.caseNo == null
                ? 'Grievance submitted.'
                : 'Grievance ${submitted.caseNo} submitted.',
          ),
        );
        return submitted;
      },
      onFailure: _fail,
    );
  }

  Future<Result<GrievanceDetail>> _persist(
    GrievanceDraftInput input,
    GrievanceCategory? category,
  ) => ref.read(saveGrievanceDraftUseCaseProvider)(
    input,
    existing: state.saved,
    category: category,
  );

  GrievanceDetail? _fail(Failure failure) {
    state = state.copyWith(submission: SubmissionState.failure(failure));
    return null;
  }

  void _onChanged(int id) {
    ref.invalidate(myGrievancesControllerProvider);
    ref.invalidate(grievanceDetailProvider(id));
  }
}

/// The form's submission state plus the draft as last saved.
class GrievanceFormState {
  const GrievanceFormState({
    this.saved,
    this.submission = const SubmissionState.idle(),
  });

  /// Null until the first save of a new grievance.
  final GrievanceDetail? saved;
  final SubmissionState submission;

  GrievanceFormState copyWith({
    GrievanceDetail? saved,
    SubmissionState? submission,
  }) => GrievanceFormState(
    saved: saved ?? this.saved,
    submission: submission ?? this.submission,
  );
}

final NotifierProvider<GrievanceFormController, GrievanceFormState>
grievanceFormControllerProvider =
    NotifierProvider<GrievanceFormController, GrievanceFormState>(
      GrievanceFormController.new,
    );

/// The detail screen's writes: delete a draft, send a message or respond.
class GrievanceActionsController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  Future<bool> deleteDraft(GrievanceDetail draft) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();
    final Result<void> result = await ref.read(
      deleteGrievanceDraftUseCaseProvider,
    )(draft);
    return result.fold(
      onSuccess: (_) {
        ref.invalidate(myGrievancesControllerProvider);
        ref.invalidate(grievanceDetailProvider(draft.id));
        state = const SubmissionState.success(message: 'Draft deleted.');
        return true;
      },
      onFailure: (Failure failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  /// Sends [body]. While HR waits for the employee it is the response.
  Future<GrievanceDetail?> send(GrievanceDetail grievance, String body) async {
    if (state.isSubmitting) {
      return null;
    }
    final bool responding = grievance.isRespondMode;
    state = const SubmissionState.submitting();
    final Result<GrievanceDetail> result = await ref.read(
      sendGrievanceMessageUseCaseProvider,
    )(grievance, body);
    return result.fold(
      onSuccess: (GrievanceDetail updated) {
        ref.invalidate(grievanceDetailProvider(grievance.id));
        if (responding) {
          // The status moved on; the list shows it.
          ref.invalidate(myGrievancesControllerProvider);
        }
        state = SubmissionState.success(
          message: responding ? 'Your answer was sent to HR.' : 'Message sent.',
        );
        return updated;
      },
      onFailure: (Failure failure) {
        state = SubmissionState.failure(failure);
        return null;
      },
    );
  }

  /// Clears the unread badge. Fire-and-forget: a failure only leaves the
  /// dot until the next visit.
  Future<void> markSeen(GrievanceDetail grievance) async {
    if (!grievance.hasUnreadUpdate) {
      return;
    }
    final Result<void> result = await ref.read(
      markGrievanceSeenUseCaseProvider,
    )(grievance.id);
    // Patched in place rather than reloaded: the detail is opened from the
    // list, which keeps its scroll position.
    if (result.isSuccess && ref.mounted) {
      ref
          .read(myGrievancesControllerProvider.notifier)
          .markSeenLocally(grievance.id);
    }
  }

  void reset() => state = const SubmissionState.idle();
}

final NotifierProvider<GrievanceActionsController, SubmissionState>
grievanceActionsControllerProvider =
    NotifierProvider<GrievanceActionsController, SubmissionState>(
      GrievanceActionsController.new,
    );
