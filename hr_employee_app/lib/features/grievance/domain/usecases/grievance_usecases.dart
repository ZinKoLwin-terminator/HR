import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/domain/repositories/grievance_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Builds the [ValidationFailure] for a set of field problems: the first
/// one is the headline, all of them land on their fields.
ValidationFailure grievanceValidationFailure(Map<String, String> errors) {
  return ValidationFailure(
    errors.values.first,
    fieldErrors: <String, List<String>>{
      for (final MapEntry<String, String> e in errors.entries)
        e.key: <String>[e.value],
    },
  );
}

DateTime _today() {
  final DateTime now = DateTime.now();
  return DateTime(now.year, now.month, now.day);
}

/// The categories and departments the form offers.
class GetGrievanceOptionsUseCase {
  const GetGrievanceOptionsUseCase(this._repository);

  final GrievanceRepository _repository;

  Future<Result<GrievanceOptions>> call() => _repository.getOptions();
}

/// One page of the employee's grievances.
class GetMyGrievancesUseCase {
  const GetMyGrievancesUseCase(this._repository);

  final GrievanceRepository _repository;

  Future<Result<PaginatedResponse<GrievanceSummary>>> call({
    required PageRequest page,
    GrievanceStatus? status,
    String? search,
  }) {
    final String? query = search?.trim();
    return _repository.getMyGrievances(
      page: page,
      // An unknown status cannot be sent as a filter; treat it as "all".
      status: status == GrievanceStatus.unknown ? null : status,
      search: (query == null || query.isEmpty) ? null : query,
    );
  }
}

/// A single grievance.
class GetGrievanceDetailUseCase {
  const GetGrievanceDetailUseCase(this._repository);

  final GrievanceRepository _repository;

  Future<Result<GrievanceDetail>> call(int id) => _repository.getById(id);
}

/// Creates a draft, or updates the one being edited.
///
/// Checks the draft rules first (a category and a subject, the maximum
/// lengths, the incident date, anonymity only where the category allows it)
/// and refuses to switch an existing draft between named and anonymous, as
/// the API does.
class SaveGrievanceDraftUseCase {
  const SaveGrievanceDraftUseCase(
    this._repository, {
    DateTime Function()? today,
  }) : _todayOf = today ?? _today;

  final GrievanceRepository _repository;
  final DateTime Function() _todayOf;

  Future<Result<GrievanceDetail>> call(
    GrievanceDraftInput input, {

    /// The draft as last loaded; null for a new grievance.
    GrievanceDetail? existing,
    GrievanceCategory? category,
  }) async {
    final GrievanceDraftInput normalized = input.normalized();
    final Map<String, String> errors = GrievanceRules.violations(
      normalized,
      today: _todayOf(),
      category: category,
    );
    if (existing != null && existing.isAnonymous != normalized.isAnonymous) {
      errors.putIfAbsent('isAnonymous', () => GrievanceRules.anonymityFixed);
    }
    if (errors.isNotEmpty) {
      return Result<GrievanceDetail>.failure(
        grievanceValidationFailure(errors),
      );
    }

    if (existing == null) {
      return _repository.createDraft(normalized);
    }
    if (!existing.canEditDraft) {
      return const Result<GrievanceDetail>.failure(
        ConflictFailure('Only a draft can be changed.'),
      );
    }
    return _repository.updateDraft(
      existing.id,
      rowVersion: existing.rowVersion,
      input: normalized,
    );
  }
}

/// Submits a saved draft to HR.
class SubmitGrievanceUseCase {
  const SubmitGrievanceUseCase(this._repository, {DateTime Function()? today})
    : _todayOf = today ?? _today;

  final GrievanceRepository _repository;
  final DateTime Function() _todayOf;

  Future<Result<GrievanceDetail>> call(
    GrievanceDetail draft, {
    required bool declarationAccepted,
    GrievanceCategory? category,
  }) async {
    if (!draft.canEditDraft) {
      return const Result<GrievanceDetail>.failure(
        ConflictFailure('Only a draft can be submitted.'),
      );
    }
    final Map<String, String> errors = GrievanceRules.violations(
      draft.toDraftInput(),
      today: _todayOf(),
      category: category,
      forSubmission: true,
      declarationAccepted: declarationAccepted,
    );
    if (errors.isNotEmpty) {
      return Result<GrievanceDetail>.failure(
        grievanceValidationFailure(errors),
      );
    }
    return _repository.submit(
      draft.id,
      rowVersion: draft.rowVersion,
      declarationAccepted: declarationAccepted,
    );
  }
}

/// Deletes a draft.
class DeleteGrievanceDraftUseCase {
  const DeleteGrievanceDraftUseCase(this._repository);

  final GrievanceRepository _repository;

  Future<Result<void>> call(GrievanceDetail draft) async {
    if (!draft.canEditDraft) {
      return const Result<void>.failure(
        ConflictFailure(
          'Only a draft can be deleted; a submitted grievance stays on record.',
        ),
      );
    }
    return _repository.deleteDraft(draft.id, rowVersion: draft.rowVersion);
  }
}

/// Sends a message to HR, or the response HR asked for.
class SendGrievanceMessageUseCase {
  const SendGrievanceMessageUseCase(this._repository);

  final GrievanceRepository _repository;

  Future<Result<GrievanceDetail>> call(
    GrievanceDetail grievance,
    String body,
  ) async {
    final String text = body.trim();
    final String? problem = GrievanceRules.messageError(text);
    if (problem != null) {
      return Result<GrievanceDetail>.failure(
        grievanceValidationFailure(<String, String>{'message': problem}),
      );
    }
    if (!grievance.canMessage) {
      return Result<GrievanceDetail>.failure(
        ConflictFailure(
          grievance.isDraft
              ? 'Submit the grievance before you write to HR.'
              : 'This grievance is closed; messages can no longer be sent.',
        ),
      );
    }
    return _repository.sendMessage(
      grievance.id,
      rowVersion: grievance.rowVersion,
      body: text,
      inReplyToId: grievance.openInfoRequest?.id,
    );
  }
}

/// Clears the unread badge of a grievance.
class MarkGrievanceSeenUseCase {
  const MarkGrievanceSeenUseCase(this._repository);

  final GrievanceRepository _repository;

  Future<Result<void>> call(int id) => _repository.markSeen(id);
}
