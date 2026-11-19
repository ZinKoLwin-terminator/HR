import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';

/// Translates the API's grievance status texts (`Draft`, `UnderReview`, ...)
/// to [GrievanceStatus].
///
/// Same contract as `LeaveStatusMapper`: case- and separator-tolerant, and an
/// unknown value degrades rather than throwing.
abstract final class GrievanceStatusMapper {
  static const Map<GrievanceStatus, String> _toWireName =
      <GrievanceStatus, String>{
        GrievanceStatus.draft: 'Draft',
        GrievanceStatus.submitted: 'Submitted',
        GrievanceStatus.acknowledged: 'Acknowledged',
        GrievanceStatus.underReview: 'UnderReview',
        GrievanceStatus.assigned: 'Assigned',
        GrievanceStatus.underInvestigation: 'UnderInvestigation',
        GrievanceStatus.awaitingEmployeeResponse: 'AwaitingEmployeeResponse',
        GrievanceStatus.resolutionProposed: 'ResolutionProposed',
        GrievanceStatus.resolved: 'Resolved',
        GrievanceStatus.closed: 'Closed',
        GrievanceStatus.rejected: 'Rejected',
        GrievanceStatus.reopened: 'Reopened',
      };

  static final Map<String, GrievanceStatus> _byWireName =
      <String, GrievanceStatus>{
        for (final MapEntry<GrievanceStatus, String> e in _toWireName.entries)
          _normalize(e.value): e.key,
      };

  static GrievanceStatus fromApi(Object? value) {
    if (value is! String) {
      return GrievanceStatus.unknown;
    }
    return _byWireName[_normalize(value)] ?? GrievanceStatus.unknown;
  }

  /// Null for [GrievanceStatus.unknown], so it is never sent as a filter.
  static String? toApi(GrievanceStatus status) => _toWireName[status];

  static String _normalize(String value) =>
      value.toLowerCase().replaceAll(RegExp(r'[_\-\s]'), '');
}

/// `FromSubmitter` / `ToSubmitter`.
abstract final class GrievanceMessageDirectionMapper {
  static GrievanceMessageDirection fromApi(Object? value) =>
      value is String && value.toLowerCase() == 'fromsubmitter'
      ? GrievanceMessageDirection.fromSubmitter
      : GrievanceMessageDirection.toSubmitter;

  static String toApi(GrievanceMessageDirection direction) =>
      direction == GrievanceMessageDirection.fromSubmitter
      ? 'FromSubmitter'
      : 'ToSubmitter';
}

/// `Message` / `InfoRequest` / `InfoResponse`; anything else reads as a
/// plain message.
abstract final class GrievanceMessageKindMapper {
  static GrievanceMessageKind fromApi(Object? value) {
    final String text = value is String ? value.toLowerCase() : '';
    return switch (text) {
      'inforequest' => GrievanceMessageKind.infoRequest,
      'inforesponse' => GrievanceMessageKind.infoResponse,
      _ => GrievanceMessageKind.message,
    };
  }

  static String toApi(GrievanceMessageKind kind) => switch (kind) {
    GrievanceMessageKind.message => 'Message',
    GrievanceMessageKind.infoRequest => 'InfoRequest',
    GrievanceMessageKind.infoResponse => 'InfoResponse',
  };
}

/// Display labels, matching the web app's.
extension GrievanceStatusLabel on GrievanceStatus {
  String get label => switch (this) {
    GrievanceStatus.draft => 'Draft',
    GrievanceStatus.submitted => 'Submitted',
    GrievanceStatus.acknowledged => 'Acknowledged',
    GrievanceStatus.underReview => 'Under Review',
    GrievanceStatus.assigned => 'Assigned',
    GrievanceStatus.underInvestigation => 'Under Investigation',
    GrievanceStatus.awaitingEmployeeResponse => 'Awaiting Your Response',
    GrievanceStatus.resolutionProposed => 'Resolution Proposed',
    GrievanceStatus.resolved => 'Resolved',
    GrievanceStatus.closed => 'Closed',
    GrievanceStatus.rejected => 'Rejected',
    GrievanceStatus.reopened => 'Reopened',
    GrievanceStatus.unknown => 'Unknown',
  };
}

extension GrievanceMessageKindLabel on GrievanceMessageKind {
  String get label => switch (this) {
    GrievanceMessageKind.message => 'Message',
    GrievanceMessageKind.infoRequest => 'Information request',
    GrievanceMessageKind.infoResponse => 'Information response',
  };
}

/// A history action (`Submit`, `RequestInfo`, ...) as the timeline shows
/// it. An action this build does not know is shown as sent.
String grievanceActionLabel(String action) => switch (action.toLowerCase()) {
  'submit' => 'Submitted',
  'acknowledge' => 'Acknowledged',
  'startreview' => 'Review started',
  'assign' => 'Investigator assigned',
  'unassign' => 'Investigator removed',
  'startinvestigation' => 'Investigation started',
  'requestinfo' => 'Information requested',
  'respond' => 'You responded',
  'proposeresolution' => 'Resolution proposed',
  'approveresolution' => 'Resolution approved',
  'rejectresolution' => 'Resolution sent back',
  'reject' => 'Rejected',
  'close' => 'Closed',
  'reopen' => 'Reopened',
  'escalate' => 'Escalated',
  'archive' => 'Archived',
  _ => action,
};
