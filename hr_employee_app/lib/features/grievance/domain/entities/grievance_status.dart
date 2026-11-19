/// Where one of the employee's grievances stands.
///
/// Pure Dart, like every domain enum here — the wire spelling (`Draft`,
/// `UnderReview`, ...) is mapped in the data layer.
enum GrievanceStatus {
  draft,
  submitted,
  acknowledged,
  underReview,
  assigned,
  underInvestigation,
  awaitingEmployeeResponse,
  resolutionProposed,
  resolved,
  closed,
  rejected,
  reopened,

  /// A status this build does not recognise. Keeps a server-side addition
  /// from breaking the list.
  unknown;

  /// Still being written; nothing has been sent to HR.
  bool get isDraft => this == GrievanceStatus.draft;

  /// Submitted and not yet resolved, closed or rejected (the API's
  /// `GrievanceCase.OpenStatuses`).
  bool get isOpen => switch (this) {
    GrievanceStatus.submitted ||
    GrievanceStatus.acknowledged ||
    GrievanceStatus.underReview ||
    GrievanceStatus.assigned ||
    GrievanceStatus.underInvestigation ||
    GrievanceStatus.awaitingEmployeeResponse ||
    GrievanceStatus.resolutionProposed ||
    GrievanceStatus.reopened => true,
    _ => false,
  };

  /// HR has asked the employee a question; their next message is the
  /// answer, and it moves the case on again.
  bool get isAwaitingResponse =>
      this == GrievanceStatus.awaitingEmployeeResponse;

  /// Whether the employee may still write to HR: every submitted status
  /// except Closed (the API's `EnsureMessagesOpen`).
  bool get acceptsMessages =>
      this != GrievanceStatus.draft &&
      this != GrievanceStatus.closed &&
      this != GrievanceStatus.unknown;
}

/// Which way a message on a case went.
enum GrievanceMessageDirection { fromSubmitter, toSubmitter }

/// What a message on a case is.
enum GrievanceMessageKind {
  /// An ordinary message, either way.
  message,

  /// HR asking the employee for more information.
  infoRequest,

  /// The employee's answer to an [infoRequest].
  infoResponse,
}
