/// How a given day was recorded by payroll.
///
/// A pure Dart enum with no serialization annotations: the domain must not
/// know that the API spells these in PascalCase. The data layer owns that
/// mapping (`AttendanceStatusMapper`), which is what lets the wire format
/// change without touching domain or UI code.
enum AttendanceStatus {
  present,
  absent,
  onLeave,
  holiday,
  weeklyOff,
  halfDay,

  /// The server sent a value this build does not know about.
  ///
  /// Present so that a new status added server-side degrades to "unknown"
  /// instead of throwing and breaking the whole screen.
  unknown;

  /// Whether the employee was expected at work on this day.
  bool get isWorkingDay =>
      this == AttendanceStatus.present ||
      this == AttendanceStatus.absent ||
      this == AttendanceStatus.halfDay;

  /// Whether attendance was actually recorded.
  bool get isAttended =>
      this == AttendanceStatus.present || this == AttendanceStatus.halfDay;
}

/// What the employee can do right now, derived from today's record.
///
/// Kept in the domain because it is a business rule, not a UI detail: the
/// same rule decides whether the button is shown and whether the check-in
/// use case will accept the request.
enum CheckInAvailability {
  /// Not checked in yet.
  canCheckIn,

  /// Checked in, not yet out.
  canCheckOut,

  /// Both recorded; the day is finished.
  completed,

  /// A holiday, weekly off or approved leave — nothing to record.
  notRequired,
}
