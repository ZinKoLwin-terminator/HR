import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';

part 'grievance.freezed.dart';

/// A kind of grievance HR accepts, e.g. "Harassment or bullying".
@freezed
abstract class GrievanceCategory with _$GrievanceCategory {
  const factory GrievanceCategory({
    required int id,
    required String name,
    @Default('') String code,

    /// Shown under the picker so the employee can tell categories apart.
    @Default('') String description,

    /// Whether a grievance of this category may be raised anonymously.
    /// Pay and management decisions usually cannot: HR has to know whose
    /// records to check.
    @Default(false) bool allowsAnonymous,
    @Default(0) int defaultTargetDays,
  }) = _GrievanceCategory;
}

/// A department the employee can name as involved.
@freezed
abstract class GrievanceDepartment with _$GrievanceDepartment {
  const factory GrievanceDepartment({required int id, required String name}) =
      _GrievanceDepartment;
}

/// What the form offers: the active categories (in HR's order) and the
/// active departments (by name).
@freezed
abstract class GrievanceOptions with _$GrievanceOptions {
  const factory GrievanceOptions({
    @Default(<GrievanceCategory>[]) List<GrievanceCategory> categories,
    @Default(<GrievanceDepartment>[]) List<GrievanceDepartment> departments,
  }) = _GrievanceOptions;

  const GrievanceOptions._();

  GrievanceCategory? categoryById(int? id) =>
      id == null ? null : categories.where((c) => c.id == id).firstOrNull;
}

/// One of the employee's own grievances, as the list shows it.
@freezed
abstract class GrievanceSummary with _$GrievanceSummary {
  const factory GrievanceSummary({
    required int id,
    required int categoryId,
    required String categoryName,
    required String subject,
    required GrievanceStatus status,
    required DateTime createdAt,

    /// Optimistic-concurrency token, base64. Every write sends back the one
    /// it last read.
    required String rowVersion,

    /// `GRV-yyyy-nnnnn`; null while the grievance is a draft.
    String? caseNo,
    @Default(false) bool isAnonymous,
    DateTime? submittedAt,

    /// Calendar day, no time component.
    DateTime? targetResolutionDate,
    DateTime? lastUpdateAt,

    /// HR has changed something the employee has not opened yet.
    @Default(false) bool hasUnreadUpdate,
  }) = _GrievanceSummary;

  const GrievanceSummary._();

  /// The case number, or "Draft" for an unsubmitted grievance.
  String get reference => caseNo ?? 'Draft';

  /// When anything last happened to it, for sorting and the list row.
  DateTime get lastActivity => lastUpdateAt ?? submittedAt ?? createdAt;
}

/// One step in a grievance's status history.
@freezed
abstract class GrievanceTimelineEntry with _$GrievanceTimelineEntry {
  const factory GrievanceTimelineEntry({
    required GrievanceStatus fromStatus,
    required GrievanceStatus toStatus,

    /// The API's action name, e.g. `Submit`, `RequestInfo`.
    required String action,

    /// "You", "HR team" or "System" — never a case-team member's name.
    required String actor,
    required DateTime occurredAt,

    /// Why it happened, when HR gave a reason the employee may see. A
    /// grievance HR recorded on the employee's behalf says so here.
    String? reason,
  }) = _GrievanceTimelineEntry;
}

/// A message between the employee and HR on a case.
@freezed
abstract class GrievanceMessage with _$GrievanceMessage {
  const factory GrievanceMessage({
    required int id,
    required GrievanceMessageDirection direction,
    required GrievanceMessageKind kind,
    required String body,
    required String actor,
    required DateTime createdAt,
    int? inReplyToId,
  }) = _GrievanceMessage;

  const GrievanceMessage._();

  bool get isMine => direction == GrievanceMessageDirection.fromSubmitter;
}

/// A file shared on a case. Listed only; uploads are web-only for now.
@freezed
abstract class GrievanceAttachment with _$GrievanceAttachment {
  const factory GrievanceAttachment({
    required int id,
    required String fileName,
    required String uploadedBy,
    required DateTime uploadedAt,
    @Default('') String contentType,
    @Default(0) int sizeBytes,
  }) = _GrievanceAttachment;
}

/// Everything the employee may see of one of their grievances.
///
/// Never carries HR's notes, the priority, assignments or a case-team
/// member's name: the API leaves them out.
@freezed
abstract class GrievanceDetail with _$GrievanceDetail {
  const factory GrievanceDetail({
    required int id,
    required int categoryId,
    required String categoryName,
    required String subject,
    required GrievanceStatus status,
    required DateTime createdAt,
    required String rowVersion,
    String? caseNo,
    @Default(false) bool isAnonymous,
    DateTime? submittedAt,
    DateTime? targetResolutionDate,
    DateTime? lastUpdateAt,
    @Default(false) bool hasUnreadUpdate,
    @Default('') String description,
    DateTime? incidentDate,
    String? location,
    String? involvedPersonText,
    int? involvedDepartmentId,
    String? involvedDepartmentName,
    DateTime? declarationAcceptedAt,
    DateTime? acknowledgedAt,
    DateTime? resolvedAt,
    DateTime? closedAt,

    /// Only when Rejected.
    String? rejectionReason,

    /// Only when Resolved or Closed and HR chose to share it.
    String? resolutionSummary,
    @Default(<GrievanceTimelineEntry>[]) List<GrievanceTimelineEntry> timeline,
    @Default(<GrievanceMessage>[]) List<GrievanceMessage> messages,
    @Default(<GrievanceAttachment>[]) List<GrievanceAttachment> attachments,
  }) = _GrievanceDetail;

  const GrievanceDetail._();

  String get reference => caseNo ?? 'Draft';

  bool get isDraft => status.isDraft;

  /// A draft can be edited and deleted; anything submitted stays on record.
  bool get canEditDraft => status.isDraft;

  bool get canMessage => status.acceptsMessages;

  /// The reply box answers HR's question rather than sending a plain
  /// message.
  bool get isRespondMode => status.isAwaitingResponse;

  /// The newest unanswered question from HR, when the case waits for one.
  GrievanceMessage? get openInfoRequest {
    if (!isRespondMode) {
      return null;
    }
    final List<GrievanceMessage> requests =
        messages
            .where(
              (GrievanceMessage m) =>
                  m.kind == GrievanceMessageKind.infoRequest,
            )
            .toList()
          ..sort(
            (GrievanceMessage a, GrievanceMessage b) =>
                b.createdAt.compareTo(a.createdAt),
          );
    return requests.firstOrNull;
  }

  /// The draft's fields, to fill the edit form.
  GrievanceDraftInput toDraftInput() => GrievanceDraftInput(
    categoryId: categoryId,
    subject: subject,
    description: description,
    incidentDate: incidentDate,
    location: location,
    involvedPersonText: involvedPersonText,
    involvedDepartmentId: involvedDepartmentId,
    isAnonymous: isAnonymous,
  );
}

/// What the new / edit draft form saves.
///
/// A draft may be unfinished (only a category and a subject are required);
/// submitting needs the full minimums. See `GrievanceRules`.
@freezed
abstract class GrievanceDraftInput with _$GrievanceDraftInput {
  const factory GrievanceDraftInput({
    required int categoryId,
    required String subject,
    @Default('') String description,

    /// Calendar day, not after today.
    DateTime? incidentDate,
    String? location,
    String? involvedPersonText,
    int? involvedDepartmentId,

    /// Fixed once the draft exists: "Start a new draft to change how you
    /// report."
    @Default(false) bool isAnonymous,
  }) = _GrievanceDraftInput;

  const GrievanceDraftInput._();

  /// Trims text and turns blank optional text into null, as the API does
  /// before it checks anything.
  GrievanceDraftInput normalized() => GrievanceDraftInput(
    categoryId: categoryId,
    subject: subject.trim(),
    description: description.trim(),
    incidentDate: incidentDate,
    location: _blankToNull(location),
    involvedPersonText: _blankToNull(involvedPersonText),
    involvedDepartmentId: involvedDepartmentId,
    isAnonymous: isAnonymous,
  );

  static String? _blankToNull(String? text) {
    final String? trimmed = text?.trim();
    return (trimmed == null || trimmed.isEmpty) ? null : trimmed;
  }
}
