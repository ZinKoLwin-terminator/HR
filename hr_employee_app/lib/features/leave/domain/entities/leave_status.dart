/// Where a leave request stands.
///
/// Pure Dart, like every domain enum here — the wire spelling is mapped in
/// the data layer.
enum LeaveStatus {
  pending,
  approved,
  rejected,
  cancelled,

  /// A status this build does not recognise. Keeps a server-side addition
  /// from breaking the list.
  unknown;

  /// Whether the employee can still withdraw the request.
  bool get isCancellable => this == LeaveStatus.pending;

  /// Whether a decision has been made either way.
  bool get isDecided =>
      this == LeaveStatus.approved || this == LeaveStatus.rejected;
}

/// How much of a day a leave request covers.
enum LeaveDayPortion {
  fullDay,
  firstHalf,
  secondHalf;

  /// Days consumed per calendar day covered.
  double get dayFraction => this == LeaveDayPortion.fullDay ? 1.0 : 0.5;
}
