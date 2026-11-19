import 'package:freezed_annotation/freezed_annotation.dart';

part 'leave_type.freezed.dart';

/// A category of leave the employer offers, e.g. Annual or Medical.
@freezed
abstract class LeaveType with _$LeaveType {
  const factory LeaveType({
    required String id,
    required String name,

    /// Short code used on payslips and reports, e.g. `AL`.
    String? code,

    /// When true the apply form requires at least one attachment, typically a
    /// medical certificate.
    @Default(false) bool requiresAttachment,

    /// Longest run the policy allows in one request, when capped.
    int? maxConsecutiveDays,

    /// Brand colour for this type, as `#RRGGBB`.
    ///
    /// Kept as a string so the domain stays free of Flutter types; the
    /// presentation layer converts it.
    String? colorHex,
    @Default(true) bool isActive,
  }) = _LeaveType;
}

/// How much leave of one type the employee has left this year.
@freezed
abstract class LeaveBalance with _$LeaveBalance {
  const factory LeaveBalance({
    required String leaveTypeId,
    required String leaveTypeName,
    required int year,

    /// Totals are decimals because half-days are allowed.
    @Default(0) double entitledDays,
    @Default(0) double usedDays,

    /// Days locked up by requests still awaiting a decision.
    @Default(0) double pendingDays,
    @Default(0) double remainingDays,
    String? colorHex,
  }) = _LeaveBalance;

  const LeaveBalance._();

  /// Fraction of the entitlement consumed, from 0 to 1.
  ///
  /// Null when there is no entitlement, so the UI omits the bar rather than
  /// drawing an empty one that implies a quota exists.
  double? get usedFraction {
    if (entitledDays <= 0) {
      return null;
    }
    return (usedDays / entitledDays).clamp(0.0, 1.0);
  }

  /// Whether a request of [days] fits within what is left.
  ///
  /// A client-side pre-check only: the server re-validates, because the
  /// balance may have moved since this was fetched.
  bool canAccommodate(double days) => days <= remainingDays;
}
