import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// The employee's own grievances (the API's `/grievances/options` and
/// `/grievances/my...`). A case of somebody else is simply not found.
abstract interface class GrievanceRepository {
  /// The active categories and departments the form offers.
  Future<Result<GrievanceOptions>> getOptions();

  /// One page of the employee's grievances, most recently updated first.
  Future<Result<PaginatedResponse<GrievanceSummary>>> getMyGrievances({
    required PageRequest page,
    GrievanceStatus? status,
    String? search,
  });

  /// A single grievance, for the detail screen and the edit form.
  Future<Result<GrievanceDetail>> getById(int id);

  /// Starts a new draft. Nothing is sent to HR.
  Future<Result<GrievanceDetail>> createDraft(GrievanceDraftInput input);

  /// Changes a draft. [rowVersion] is the one last read.
  Future<Result<GrievanceDetail>> updateDraft(
    int id, {
    required String rowVersion,
    required GrievanceDraftInput input,
  });

  /// Deletes a draft for good.
  Future<Result<void>> deleteDraft(int id, {required String rowVersion});

  /// Sends a draft to HR; it gets its case number.
  Future<Result<GrievanceDetail>> submit(
    int id, {
    required String rowVersion,
    required bool declarationAccepted,
  });

  /// Writes to HR. While the case awaits the employee this is the response
  /// to HR's request and moves the case on.
  Future<Result<GrievanceDetail>> sendMessage(
    int id, {
    required String rowVersion,
    required String body,
    int? inReplyToId,
  });

  /// Clears the unread badge. Idempotent.
  Future<Result<void>> markSeen(int id);
}
