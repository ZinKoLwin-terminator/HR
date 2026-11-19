import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_overview.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';

LeaveBalance _balance(
  String code,
  double entitled,
  double used,
  double pending, {
  String? name,
}) => LeaveBalance(
  leaveTypeId: 'lt-$code',
  leaveTypeName: name ?? '$code Leave',
  code: code,
  year: 2026,
  entitledDays: entitled,
  usedDays: used,
  pendingDays: pending,
  remainingDays: entitled - used - pending,
);

LeaveRequest _request(
  String id,
  DateTime start,
  LeaveStatus status,
  double days,
) => LeaveRequest(
  id: id,
  leaveTypeId: 'lt-AL',
  leaveTypeName: 'Annual Leave',
  startDate: start,
  endDate: start,
  status: status,
  appliedAt: start,
  totalDays: days,
);

void main() {
  group('LeaveSummary', () {
    test('adds up only Annual, Medical and Casual', () {
      final LeaveSummary summary = LeaveSummary.fromBalances(<LeaveBalance>[
        _balance('AL', 14, 5, 2, name: 'Annual Leave'),
        _balance('ML', 10, 2, 0, name: 'Medical Leave'),
        _balance('CL', 6, 5.5, 0, name: 'Casual Leave'),
        _balance('MAT', 98, 0, 0, name: 'Maternity Leave'),
        _balance('UL', 0, 0, 0, name: 'Unpaid Leave'),
      ]);

      expect(summary.left, 15.5);
      expect(summary.pending, 2);
      expect(summary.used, 12.5);
      expect(summary.entitled, 30);
      expect(summary.includedTypeNames, <String>[
        'Annual',
        'Medical',
        'Casual',
      ]);
    });

    test('falls back to every entitled type when no codes match', () {
      final LeaveSummary summary = LeaveSummary.fromBalances(<LeaveBalance>[
        _balance('X', 4, 1, 0),
        _balance('Y', 0, 0, 0),
      ]);
      expect(summary.entitled, 4);
      expect(summary.left, 3);
    });
  });

  group('LeaveBalanceState', () {
    test('reads each balance', () {
      expect(
        LeaveBalanceState.of(_balance('UL', 0, 0, 0)),
        LeaveBalanceState.noBalance,
      );
      expect(
        LeaveBalanceState.of(_balance('SL', 5, 5, 0)),
        LeaveBalanceState.usedUp,
      );
      expect(
        LeaveBalanceState.of(_balance('CL', 6, 5.5, 0)),
        LeaveBalanceState.almostUsedUp,
      );
      expect(
        LeaveBalanceState.of(_balance('AL', 14, 5, 2)),
        LeaveBalanceState.pending,
      );
      expect(
        LeaveBalanceState.of(_balance('ML', 10, 2, 0)),
        LeaveBalanceState.available,
      );
    });
  });

  test('type names shorten and read as sentences', () {
    expect(shortLeaveTypeName('Annual Leave'), 'Annual');
    expect(sentenceLeaveTypeName('Medical Leave'), 'Medical leave');
    expect(sentenceLeaveTypeName('Sabbatical'), 'Sabbatical');
  });

  group('LeaveRequestYear', () {
    final List<LeaveRequest> all = <LeaveRequest>[
      _request('a', DateTime(2026, 11, 24), LeaveStatus.pending, 2),
      _request('b', DateTime(2026, 11, 3), LeaveStatus.approved, 2),
      _request('c', DateTime(2026, 10, 21), LeaveStatus.approved, 1),
      _request('d', DateTime(2026, 10, 14), LeaveStatus.cancelled, 0.5),
      _request('e', DateTime(2026, 10, 6), LeaveStatus.approved, 5),
      _request('f', DateTime(2026, 9, 1), LeaveStatus.rejected, 1),
      _request('g', DateTime(2026, 9, 14), LeaveStatus.approved, 2),
      _request('old', DateTime(2025, 12, 30), LeaveStatus.approved, 1),
    ];
    final LeaveRequestYear year = LeaveRequestYear(year: 2026, all: all);

    test('keeps only the year and counts each status', () {
      expect(year.countFor(null), 7);
      expect(year.countFor(LeaveStatus.pending), 1);
      expect(year.countFor(LeaveStatus.approved), 4);
      expect(year.countFor(LeaveStatus.rejected), 1);
      expect(year.countFor(LeaveStatus.cancelled), 1);
    });

    test('groups by month, newest first, leaving out rejected and '
        'cancelled days from the totals', () {
      final List<LeaveMonthGroup> groups = year.monthGroups(null);
      expect(groups.map((LeaveMonthGroup g) => g.month.month), <int>[
        11,
        10,
        9,
      ]);
      expect(groups.map((LeaveMonthGroup g) => g.countedDays), <double>[
        4,
        6,
        2,
      ]);
      expect(groups.last.requests.map((LeaveRequest r) => r.id), <String>[
        'g',
        'f',
      ]);
    });

    test('a filter with no matches yields no groups', () {
      final LeaveRequestYear empty = LeaveRequestYear(
        year: 2026,
        all: all.where((LeaveRequest r) => r.status != LeaveStatus.rejected),
      );
      expect(empty.monthGroups(LeaveStatus.rejected), isEmpty);
    });
  });
}
