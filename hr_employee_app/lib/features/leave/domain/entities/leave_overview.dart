import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';

/// The headline figures on the leave screen: one donut for the year.
///
/// Adds up only the everyday types (the summary codes). Summing Maternity's
/// 98 days into the same number as Annual leave would make it meaningless,
/// so the screen names the types it covers under the chart.
class LeaveSummary {
  const LeaveSummary({
    required this.entitled,
    required this.used,
    required this.pending,
    required this.left,
    required this.includedTypeNames,
  });

  /// Builds the summary from a year's balances.
  ///
  /// Falls back to every type with an entitlement when none of the balances
  /// carry one of [summaryCodes] (an older server that sends no codes), so
  /// the chart is never empty for want of a code.
  factory LeaveSummary.fromBalances(
    List<LeaveBalance> balances, {
    Set<String> summaryCodes = defaultSummaryCodes,
  }) {
    List<LeaveBalance> included = balances
        .where(
          (LeaveBalance b) =>
              b.code != null && summaryCodes.contains(b.code!.toUpperCase()),
        )
        .toList();
    if (included.isEmpty) {
      included = balances
          .where((LeaveBalance b) => b.entitledDays > 0)
          .toList();
    }

    double sum(double Function(LeaveBalance b) pick) =>
        included.fold(0, (double total, LeaveBalance b) => total + pick(b));

    return LeaveSummary(
      entitled: sum((LeaveBalance b) => b.entitledDays),
      used: sum((LeaveBalance b) => b.usedDays),
      pending: sum((LeaveBalance b) => b.pendingDays),
      left: sum((LeaveBalance b) => b.remainingDays),
      includedTypeNames: included
          .map((LeaveBalance b) => shortLeaveTypeName(b.leaveTypeName))
          .toList(growable: false),
    );
  }

  /// Annual, Medical and Casual.
  ///
  /// Hardcoded until HR can flag "counts towards the headline" per type;
  /// the codes match the HR system's leave type codes.
  static const Set<String> defaultSummaryCodes = <String>{'AL', 'ML', 'CL'};

  final double entitled;
  final double used;
  final double pending;
  final double left;

  /// Short names of the types added up, e.g. `Annual`, `Medical`.
  final List<String> includedTypeNames;

  bool get isEmpty => entitled <= 0 && used <= 0 && pending <= 0 && left <= 0;
}

/// How a single leave type's balance reads at a glance, which sets its tile
/// colour.
enum LeaveBalanceState {
  /// Plenty left and nothing waiting.
  available,

  /// Some of it is locked up in requests awaiting a decision.
  pending,

  /// A day or less, or a tenth of the entitlement or less, is left.
  almostUsedUp,

  /// Entitled to some, none left.
  usedUp,

  /// No entitlement at all, e.g. unpaid leave.
  noBalance;

  static LeaveBalanceState of(LeaveBalance balance) {
    if (balance.entitledDays <= 0) {
      return LeaveBalanceState.noBalance;
    }
    if (balance.remainingDays <= 0) {
      return LeaveBalanceState.usedUp;
    }
    if (balance.remainingDays <= 1 ||
        balance.remainingDays / balance.entitledDays <= 0.1) {
      return LeaveBalanceState.almostUsedUp;
    }
    if (balance.pendingDays > 0) {
      return LeaveBalanceState.pending;
    }
    return LeaveBalanceState.available;
  }
}

/// `Annual Leave` → `Annual`, for tiles and captions where the word "leave"
/// is already implied.
String shortLeaveTypeName(String name) {
  final String trimmed = name.trim();
  final RegExp suffix = RegExp(r'\s+leave$', caseSensitive: false);
  final String short = trimmed.replaceFirst(suffix, '');
  return short.isEmpty ? trimmed : short;
}

/// `Annual Leave` → `Annual leave`, for sentence-style row titles.
String sentenceLeaveTypeName(String name) {
  final String short = shortLeaveTypeName(name);
  return short == name.trim() ? short : '$short leave';
}

/// Whether a request's days count towards a total.
///
/// Rejected and cancelled requests never consumed any leave, so a month's
/// total leaves them out even though they are still listed.
bool countsTowardsTotals(LeaveStatus status) =>
    status == LeaveStatus.pending || status == LeaveStatus.approved;

/// One month of requests on the all-requests screen.
class LeaveMonthGroup {
  const LeaveMonthGroup({required this.month, required this.requests});

  /// The first day of the month.
  final DateTime month;

  /// Newest first.
  final List<LeaveRequest> requests;

  /// Days taken or asked for in this month, leaving out rejected and
  /// cancelled requests.
  double get countedDays => requests
      .where((LeaveRequest r) => countsTowardsTotals(r.status))
      .fold(0, (double total, LeaveRequest r) => total + r.totalDays);
}

/// The employee's requests for one year, filterable by status.
///
/// Status counts are worked out over the whole year, not the filtered list,
/// so every chip shows how many it would reveal.
class LeaveRequestYear {
  LeaveRequestYear({required this.year, required Iterable<LeaveRequest> all})
    : requests =
          all.where((LeaveRequest r) => r.startDate.year == year).toList()
            ..sort(
              (LeaveRequest a, LeaveRequest b) =>
                  b.startDate.compareTo(a.startDate),
            );

  final int year;

  /// Requests starting in [year], newest first.
  final List<LeaveRequest> requests;

  /// How many requests a status filter would show. Null is "All".
  int countFor(LeaveStatus? status) => status == null
      ? requests.length
      : requests.where((LeaveRequest r) => r.status == status).length;

  /// Requests matching [status], or every request when it is null.
  List<LeaveRequest> filtered(LeaveStatus? status) => status == null
      ? requests
      : requests.where((LeaveRequest r) => r.status == status).toList();

  /// [filtered] split into months, newest month first.
  List<LeaveMonthGroup> monthGroups(LeaveStatus? status) {
    final List<LeaveMonthGroup> groups = <LeaveMonthGroup>[];
    for (final LeaveRequest request in filtered(status)) {
      final DateTime month = DateTime(
        request.startDate.year,
        request.startDate.month,
      );
      if (groups.isNotEmpty && groups.last.month == month) {
        groups.last.requests.add(request);
      } else {
        groups.add(
          LeaveMonthGroup(month: month, requests: <LeaveRequest>[request]),
        );
      }
    }
    return groups;
  }
}
