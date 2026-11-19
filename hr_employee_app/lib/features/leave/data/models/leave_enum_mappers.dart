import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';

/// Translates the API's `leaveStatus` strings to [LeaveStatus].
///
/// Same contract as `AttendanceStatusMapper`: case- and separator-tolerant,
/// and an unknown value degrades rather than throwing.
abstract final class LeaveStatusMapper {
  static const Map<String, LeaveStatus> _byWireName = <String, LeaveStatus>{
    'pending': LeaveStatus.pending,
    'approved': LeaveStatus.approved,
    'rejected': LeaveStatus.rejected,
    'cancelled': LeaveStatus.cancelled,
    // American spelling, in case the backend uses it.
    'canceled': LeaveStatus.cancelled,
  };

  static const Map<LeaveStatus, String> _toWireName = <LeaveStatus, String>{
    LeaveStatus.pending: 'Pending',
    LeaveStatus.approved: 'Approved',
    LeaveStatus.rejected: 'Rejected',
    LeaveStatus.cancelled: 'Cancelled',
  };

  static LeaveStatus fromApi(Object? value) {
    if (value is! String) {
      return LeaveStatus.unknown;
    }
    return _byWireName[_normalize(value)] ?? LeaveStatus.unknown;
  }

  /// Returns null for [LeaveStatus.unknown], so an unknown filter is omitted
  /// from the query string rather than sent as a value the server will reject.
  static String? toApi(LeaveStatus status) => _toWireName[status];

  static String _normalize(String value) =>
      value.toLowerCase().replaceAll(RegExp(r'[_\-\s]'), '');
}

/// Translates the API's `leaveDayPortion` strings.
abstract final class LeaveDayPortionMapper {
  static const Map<String, LeaveDayPortion> _byWireName =
      <String, LeaveDayPortion>{
        'fullday': LeaveDayPortion.fullDay,
        'firsthalf': LeaveDayPortion.firstHalf,
        'secondhalf': LeaveDayPortion.secondHalf,
      };

  static const Map<LeaveDayPortion, String> _toWireName =
      <LeaveDayPortion, String>{
        LeaveDayPortion.fullDay: 'FullDay',
        LeaveDayPortion.firstHalf: 'FirstHalf',
        LeaveDayPortion.secondHalf: 'SecondHalf',
      };

  /// Defaults to [LeaveDayPortion.fullDay], which is the common case and a
  /// safe assumption when the server omits the field.
  static LeaveDayPortion fromApi(Object? value) {
    if (value is! String) {
      return LeaveDayPortion.fullDay;
    }
    return _byWireName[_normalize(value)] ?? LeaveDayPortion.fullDay;
  }

  static String toApi(LeaveDayPortion portion) =>
      _toWireName[portion] ?? 'FullDay';

  static String _normalize(String value) =>
      value.toLowerCase().replaceAll(RegExp(r'[_\-\s]'), '');
}

/// Display labels.
extension LeaveStatusLabel on LeaveStatus {
  String get label => switch (this) {
    LeaveStatus.pending => 'Pending',
    LeaveStatus.approved => 'Approved',
    LeaveStatus.rejected => 'Rejected',
    LeaveStatus.cancelled => 'Cancelled',
    LeaveStatus.unknown => 'Unknown',
  };
}

extension LeaveDayPortionLabel on LeaveDayPortion {
  String get label => switch (this) {
    LeaveDayPortion.fullDay => 'Full day',
    LeaveDayPortion.firstHalf => 'First half',
    LeaveDayPortion.secondHalf => 'Second half',
  };
}
