import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_models.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_models.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_models.dart';
import 'package:hr_employee_app/features/payslip/data/models/payslip_models.dart';
import 'package:hr_employee_app/features/profile/data/models/employee_profile_model.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';

/// The single store behind every mock data source.
///
/// ---------------------------------------------------------------------------
/// EVERY VALUE HERE IS INVENTED. None of it comes from an HR system.
/// ---------------------------------------------------------------------------
///
/// One store rather than per-feature fixtures, for two reasons:
///
///   * **Consistency.** The dashboard, the attendance screen and the leave
///     screen read the same records, so they agree with each other. Separate
///     fixtures drift and produce a UI that contradicts itself during review.
///   * **Mutability.** Checking in has to change what the next screen shows,
///     otherwise the flow cannot be exercised at all. Writes are held in
///     memory for the session and are lost on restart, which is the right
///     trade-off for a fixture.
///
/// Delete this file along with the `*MockDataSource` classes once the real
/// API is available.
class MockFixtures {
  MockFixtures._();

  static final MockFixtures instance = MockFixtures._();

  // --- Attendance -----------------------------------------------------

  /// Today's record. Null until the user checks in during this session.
  AttendanceRecordModel? _todayRecord;

  AttendanceRecordModel? get todayRecord => _todayRecord;

  /// Records a check-in. Returns the new record.
  ///
  /// Late arrival is derived from the real clock against a 09:00 shift, so
  /// the "late" badge behaves differently depending on when the fixture is
  /// exercised rather than being hardcoded.
  AttendanceRecordModel checkIn({GeoLocationModel? location}) {
    final DateTime now = DateTime.now();
    const int shiftStartHour = 9;
    final DateTime shiftStart = DateTime(
      now.year,
      now.month,
      now.day,
      shiftStartHour,
    );
    final bool isLate = now.isAfter(shiftStart);

    _todayRecord = AttendanceRecordModel(
      id: 'mock-att-today',
      attendanceDate: DateFormatter.toApiDate(now),
      status: 'Present',
      checkInAt: now.toUtc(),
      checkInLocation: location,
      shiftStartAt: shiftStart.toUtc(),
      shiftEndAt: DateTime(now.year, now.month, now.day, 18).toUtc(),
      isLateArrival: isLate,
      lateByMinutes: isLate ? now.difference(shiftStart).inMinutes : 0,
    );
    return _todayRecord!;
  }

  /// Records a check-out against today's existing record.
  AttendanceRecordModel? checkOut({GeoLocationModel? location}) {
    final AttendanceRecordModel? current = _todayRecord;
    if (current == null) {
      return null;
    }
    final DateTime now = DateTime.now();
    final DateTime shiftEnd = DateTime(now.year, now.month, now.day, 18);
    final bool isEarly = now.isBefore(shiftEnd);

    _todayRecord = current.copyWith(
      checkOutAt: now.toUtc(),
      checkOutLocation: location,
      isEarlyDeparture: isEarly,
      earlyByMinutes: isEarly ? shiftEnd.difference(now).inMinutes : 0,
      workedMinutes: current.checkInAt == null
          ? null
          : now.toUtc().difference(current.checkInAt!).inMinutes,
    );
    return _todayRecord;
  }

  /// Where the seeded history was punched from: the office.
  static const GeoLocationModel _officeLocation = GeoLocationModel(
    latitude: 16.8259,
    longitude: 96.1259,
    accuracyMeters: 12,
    address: 'Yangon HQ',
  );

  /// Sixty days of history, newest first, excluding today.
  ///
  /// Generated rather than hand-written so weekends fall on real weekends
  /// and the list exercises every status.
  List<AttendanceRecordModel> get attendanceHistory {
    final List<AttendanceRecordModel> records = <AttendanceRecordModel>[];

    for (int offset = 1; offset <= 60; offset++) {
      final DateTime day = MockSupport.daysAgo(offset);
      final bool isWeekend =
          day.weekday == DateTime.saturday || day.weekday == DateTime.sunday;

      if (isWeekend) {
        records.add(
          AttendanceRecordModel(
            id: 'mock-att-$offset',
            attendanceDate: DateFormatter.toApiDate(day),
            status: 'WeeklyOff',
          ),
        );
        continue;
      }

      // A deterministic sprinkle of exceptions, so the list is not uniform.
      if (offset % 17 == 0) {
        records.add(
          AttendanceRecordModel(
            id: 'mock-att-$offset',
            attendanceDate: DateFormatter.toApiDate(day),
            status: 'OnLeave',
          ),
        );
        continue;
      }
      if (offset % 23 == 0) {
        records.add(
          AttendanceRecordModel(
            id: 'mock-att-$offset',
            attendanceDate: DateFormatter.toApiDate(day),
            status: 'Absent',
          ),
        );
        continue;
      }

      final bool isLate = offset % 7 == 0;
      final bool isEarly = offset % 11 == 0;
      final int inMinute = isLate ? 14 : 2;
      final int outHour = isEarly ? 16 : 18;

      records.add(
        AttendanceRecordModel(
          id: 'mock-att-$offset',
          attendanceDate: DateFormatter.toApiDate(day),
          status: 'Present',
          checkInAt: MockSupport.at(day, 9, inMinute),
          checkOutAt: MockSupport.at(day, outHour, 10),
          shiftStartAt: MockSupport.at(day, 9, 0),
          shiftEndAt: MockSupport.at(day, 18, 0),
          checkInLocation: _officeLocation,
          checkOutLocation: _officeLocation,
          isLateArrival: isLate,
          lateByMinutes: isLate ? inMinute : 0,
          isEarlyDeparture: isEarly,
          earlyByMinutes: isEarly ? 110 : 0,
          workedMinutes: (outHour - 9) * 60 + (10 - inMinute),
          overtimeMinutes: offset % 13 == 0 ? 45 : 0,
        ),
      );
    }

    return records;
  }

  /// A monthly summary computed from [attendanceHistory], so the numbers
  /// agree with the list the user can scroll through.
  AttendanceSummaryModel attendanceSummary({
    required int year,
    required int month,
  }) {
    final List<AttendanceRecordModel> inMonth = attendanceHistory.where((
      AttendanceRecordModel record,
    ) {
      final DateTime? date = DateFormatter.parseApiDate(record.attendanceDate);
      return date != null && date.year == year && date.month == month;
    }).toList();

    int count(String status) =>
        inMonth.where((AttendanceRecordModel r) => r.status == status).length;

    final int present = count('Present');
    final int absent = count('Absent');
    final int leave = count('OnLeave');

    return AttendanceSummaryModel(
      year: year,
      month: month,
      totalWorkingDays: present + absent + leave,
      presentDays: present,
      absentDays: absent,
      leaveDays: leave,
      lateArrivals: inMonth
          .where((AttendanceRecordModel r) => r.isLateArrival)
          .length,
      earlyDepartures: inMonth
          .where((AttendanceRecordModel r) => r.isEarlyDeparture)
          .length,
      totalWorkedMinutes: inMonth.fold<int>(
        0,
        (int sum, AttendanceRecordModel r) => sum + (r.workedMinutes ?? 0),
      ),
      totalOvertimeMinutes: inMonth.fold<int>(
        0,
        (int sum, AttendanceRecordModel r) => sum + r.overtimeMinutes,
      ),
    );
  }

  // --- Leave ----------------------------------------------------------

  // Eight types, so the leave screen's horizontal tiles and its "View all"
  // sheet are exercised, with one of each balance state: plenty left,
  // pending, almost used up, used up, and no balance at all (unpaid).
  static const List<LeaveTypeModel> leaveTypes = <LeaveTypeModel>[
    LeaveTypeModel(
      id: 'lt-annual',
      name: 'Annual Leave',
      code: 'AL',
      maxConsecutiveDays: 14,
      colorHex: '#0D9488',
    ),
    LeaveTypeModel(
      id: 'lt-medical',
      name: 'Medical Leave',
      code: 'ML',
      requiresAttachment: true,
      maxConsecutiveDays: 10,
      colorHex: '#DB2777',
    ),
    LeaveTypeModel(
      id: 'lt-casual',
      name: 'Casual Leave',
      code: 'CL',
      maxConsecutiveDays: 3,
      colorHex: '#2563EB',
    ),
    LeaveTypeModel(
      id: 'lt-maternity',
      name: 'Maternity Leave',
      code: 'MAT',
      requiresAttachment: true,
      maxConsecutiveDays: 98,
      colorHex: '#9333EA',
    ),
    LeaveTypeModel(
      id: 'lt-paternity',
      name: 'Paternity Leave',
      code: 'PAT',
      maxConsecutiveDays: 15,
      colorHex: '#4F46E5',
    ),
    LeaveTypeModel(
      id: 'lt-compassionate',
      name: 'Compassionate Leave',
      code: 'CPL',
      maxConsecutiveDays: 3,
      colorHex: '#B45309',
    ),
    LeaveTypeModel(
      id: 'lt-study',
      name: 'Study Leave',
      code: 'SL',
      maxConsecutiveDays: 5,
      colorHex: '#0891B2',
    ),
    LeaveTypeModel(
      id: 'lt-unpaid',
      name: 'Unpaid Leave',
      code: 'UL',
      colorHex: '#667085',
    ),
  ];

  List<LeaveBalanceModel> get leaveBalances => <LeaveBalanceModel>[
    LeaveBalanceModel(
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      code: 'AL',
      year: DateTime.now().year,
      entitledDays: 14,
      usedDays: 5,
      pendingDays: _pendingDaysFor('lt-annual'),
      remainingDays: 14 - 5 - _pendingDaysFor('lt-annual'),
      colorHex: '#0D9488',
    ),
    LeaveBalanceModel(
      leaveTypeId: 'lt-medical',
      leaveTypeName: 'Medical Leave',
      code: 'ML',
      year: DateTime.now().year,
      entitledDays: 10,
      usedDays: 2,
      pendingDays: _pendingDaysFor('lt-medical'),
      remainingDays: 10 - 2 - _pendingDaysFor('lt-medical'),
      colorHex: '#DB2777',
    ),
    LeaveBalanceModel(
      leaveTypeId: 'lt-casual',
      leaveTypeName: 'Casual Leave',
      code: 'CL',
      year: DateTime.now().year,
      entitledDays: 6,
      usedDays: 5.5,
      pendingDays: _pendingDaysFor('lt-casual'),
      remainingDays: 0.5 - _pendingDaysFor('lt-casual'),
      colorHex: '#2563EB',
    ),
    _untouched('lt-maternity', 'Maternity Leave', 'MAT', 98, '#9333EA'),
    _untouched('lt-paternity', 'Paternity Leave', 'PAT', 15, '#4F46E5'),
    _untouched('lt-compassionate', 'Compassionate Leave', 'CPL', 3, '#B45309'),
    LeaveBalanceModel(
      leaveTypeId: 'lt-study',
      leaveTypeName: 'Study Leave',
      code: 'SL',
      year: DateTime.now().year,
      entitledDays: 5,
      usedDays: 5,
      colorHex: '#0891B2',
    ),
    LeaveBalanceModel(
      leaveTypeId: 'lt-unpaid',
      leaveTypeName: 'Unpaid Leave',
      code: 'UL',
      year: DateTime.now().year,
      colorHex: '#667085',
    ),
  ];

  /// A balance nothing has been taken from yet, apart from anything pending.
  LeaveBalanceModel _untouched(
    String id,
    String name,
    String code,
    num entitled,
    String colorHex,
  ) => LeaveBalanceModel(
    leaveTypeId: id,
    leaveTypeName: name,
    code: code,
    year: DateTime.now().year,
    entitledDays: entitled,
    pendingDays: _pendingDaysFor(id),
    remainingDays: entitled - _pendingDaysFor(id),
    colorHex: colorHex,
  );

  /// Requests submitted during this session, newest first, followed by the
  /// seeded history.
  final List<LeaveRequestModel> _submittedRequests = <LeaveRequestModel>[];

  List<LeaveRequestModel> get leaveRequests => <LeaveRequestModel>[
    ..._submittedRequests,
    ..._seededRequests,
  ];

  /// Appends a request submitted through the apply form, so it appears in the
  /// list immediately afterwards.
  LeaveRequestModel addLeaveRequest({
    required String leaveTypeId,
    required DateTime startDate,
    required DateTime endDate,
    required String reason,
    required String dayPortion,
    required int attachmentCount,
    String? contactDuringLeave,
  }) {
    final LeaveTypeModel type = leaveTypes.firstWhere(
      (LeaveTypeModel t) => t.id == leaveTypeId,
      orElse: () => leaveTypes.first,
    );

    final double days = dayPortion == 'FullDay'
        ? DateFormatter.inclusiveDayCount(startDate, endDate).toDouble()
        : 0.5;

    final LeaveRequestModel request = LeaveRequestModel(
      id: 'mock-lr-${DateTime.now().microsecondsSinceEpoch}',
      leaveTypeId: type.id,
      leaveTypeName: type.name,
      startDate: DateFormatter.toApiDate(startDate),
      endDate: DateFormatter.toApiDate(endDate),
      status: 'Pending',
      appliedAt: DateTime.now().toUtc(),
      dayPortion: dayPortion,
      totalDays: days,
      reason: reason,
      contactDuringLeave: contactDuringLeave,
      colorHex: type.colorHex,
      attachments: List<LeaveAttachmentModel>.generate(
        attachmentCount,
        (int index) => LeaveAttachmentModel(
          id: 'mock-at-$index',
          fileName: 'attachment-${index + 1}.pdf',
          contentType: 'application/pdf',
          sizeBytes: 148221,
        ),
      ),
    );

    _submittedRequests.insert(0, request);
    return request;
  }

  /// Marks a pending request cancelled.
  LeaveRequestModel? cancelLeaveRequest(String id) {
    final int submittedIndex = _submittedRequests.indexWhere(
      (LeaveRequestModel r) => r.id == id,
    );
    if (submittedIndex >= 0) {
      final LeaveRequestModel cancelled = _submittedRequests[submittedIndex]
          .copyWith(status: 'Cancelled', decidedAt: DateTime.now().toUtc());
      _submittedRequests[submittedIndex] = cancelled;
      return cancelled;
    }

    final int seededIndex = _seededRequests.indexWhere(
      (LeaveRequestModel r) => r.id == id,
    );
    if (seededIndex >= 0) {
      final LeaveRequestModel cancelled = _seededRequests[seededIndex].copyWith(
        status: 'Cancelled',
        decidedAt: DateTime.now().toUtc(),
      );
      _seededRequests[seededIndex] = cancelled;
      return cancelled;
    }
    return null;
  }

  double _pendingDaysFor(String leaveTypeId) => leaveRequests
      .where(
        (LeaveRequestModel r) =>
            r.leaveTypeId == leaveTypeId && r.status == 'Pending',
      )
      .fold<double>(
        0,
        (double sum, LeaveRequestModel r) => sum + r.totalDays.toDouble(),
      );

  /// Seeded history: seven requests covering every status, spread over three
  /// months so the all-requests screen's month groups and filters can be
  /// reviewed. Dates are relative to today, so the fixture never goes stale.
  ///
  /// The approved days here do not add up to every balance's used figure:
  /// the balances also stand for leave taken before this history starts.
  final List<LeaveRequestModel> _seededRequests = <LeaveRequestModel>[
    LeaveRequestModel(
      id: 'mock-lr-1',
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      startDate: DateFormatter.toApiDate(
        MockSupport.today.add(const Duration(days: 5)),
      ),
      endDate: DateFormatter.toApiDate(
        MockSupport.today.add(const Duration(days: 6)),
      ),
      status: 'Pending',
      appliedAt: MockSupport.daysAgo(1).toUtc(),
      totalDays: 2,
      reason: 'Family matters out of town, returning on the Sunday.',
      approverName: 'Ma Myat Gyi',
      colorHex: '#0D9488',
      contactDuringLeave: '+95 9 123 456 789',
    ),
    LeaveRequestModel(
      id: 'mock-lr-2',
      leaveTypeId: 'lt-medical',
      leaveTypeName: 'Medical Leave',
      startDate: DateFormatter.toApiDate(MockSupport.daysAgo(16)),
      endDate: DateFormatter.toApiDate(MockSupport.daysAgo(15)),
      status: 'Approved',
      appliedAt: MockSupport.daysAgo(17).toUtc(),
      totalDays: 2,
      reason: 'Seasonal flu, advised two days of rest by the clinic.',
      decidedAt: MockSupport.daysAgo(16).toUtc(),
      approverName: 'Ma Myat Gyi',
      decisionRemark: 'Get well soon.',
      colorHex: '#DB2777',
      attachments: <LeaveAttachmentModel>[
        const LeaveAttachmentModel(
          id: 'mock-at-1',
          fileName: 'medical-certificate.pdf',
          contentType: 'application/pdf',
          sizeBytes: 148221,
        ),
      ],
    ),
    LeaveRequestModel(
      id: 'mock-lr-3',
      leaveTypeId: 'lt-casual',
      leaveTypeName: 'Casual Leave',
      startDate: DateFormatter.toApiDate(MockSupport.daysAgo(29)),
      endDate: DateFormatter.toApiDate(MockSupport.daysAgo(29)),
      status: 'Approved',
      appliedAt: MockSupport.daysAgo(32).toUtc(),
      totalDays: 1,
      reason: 'Moving house; the movers only come on a weekday.',
      decidedAt: MockSupport.daysAgo(31).toUtc(),
      approverName: 'Ma Myat Gyi',
      colorHex: '#2563EB',
    ),
    LeaveRequestModel(
      id: 'mock-lr-4',
      leaveTypeId: 'lt-casual',
      leaveTypeName: 'Casual Leave',
      startDate: DateFormatter.toApiDate(MockSupport.daysAgo(36)),
      endDate: DateFormatter.toApiDate(MockSupport.daysAgo(36)),
      status: 'Cancelled',
      appliedAt: MockSupport.daysAgo(40).toUtc(),
      dayPortion: 'FirstHalf',
      totalDays: 0.5,
      reason: 'Personal appointment in the morning, back by midday.',
      decidedAt: MockSupport.daysAgo(38).toUtc(),
      colorHex: '#2563EB',
    ),
    LeaveRequestModel(
      id: 'mock-lr-5',
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      startDate: DateFormatter.toApiDate(MockSupport.daysAgo(44)),
      endDate: DateFormatter.toApiDate(MockSupport.daysAgo(40)),
      status: 'Approved',
      appliedAt: MockSupport.daysAgo(60).toUtc(),
      totalDays: 5,
      reason: 'Short trip booked with family over the long weekend.',
      decidedAt: MockSupport.daysAgo(58).toUtc(),
      approverName: 'Ma Myat Gyi',
      colorHex: '#0D9488',
    ),
    LeaveRequestModel(
      id: 'mock-lr-6',
      leaveTypeId: 'lt-compassionate',
      leaveTypeName: 'Compassionate Leave',
      startDate: DateFormatter.toApiDate(MockSupport.daysAgo(79)),
      endDate: DateFormatter.toApiDate(MockSupport.daysAgo(79)),
      status: 'Rejected',
      appliedAt: MockSupport.daysAgo(79).toUtc(),
      totalDays: 1,
      reason: "Attending a relative's funeral in the morning.",
      decidedAt: MockSupport.daysAgo(78).toUtc(),
      approverName: 'Ma Myat Gyi',
      decisionRemark: 'Please apply at least 1 day ahead.',
      colorHex: '#B45309',
    ),
    LeaveRequestModel(
      id: 'mock-lr-7',
      leaveTypeId: 'lt-casual',
      leaveTypeName: 'Casual Leave',
      startDate: DateFormatter.toApiDate(MockSupport.daysAgo(72)),
      endDate: DateFormatter.toApiDate(MockSupport.daysAgo(71)),
      status: 'Approved',
      appliedAt: MockSupport.daysAgo(92).toUtc(),
      totalDays: 2,
      reason: 'Renewing my passport at the regional office.',
      decidedAt: MockSupport.daysAgo(90).toUtc(),
      approverName: 'Ma Myat Gyi',
      colorHex: '#2563EB',
    ),
  ];

  // --- Payslips -------------------------------------------------------

  /// Twelve months of payslips, newest first.
  List<PayslipSummaryModel> get payslips {
    final DateTime now = DateTime.now();
    return List<PayslipSummaryModel>.generate(12, (int index) {
      // Start from last month: the current month is not yet paid.
      final DateTime period = DateTime(now.year, now.month - index - 1);
      return PayslipSummaryModel(
        id: 'mock-ps-${period.year}-${period.month}',
        year: period.year,
        month: period.month,
        netPay: 1842500 + (index % 3) * 25000,
        periodLabel: DateFormatter.monthYear(period),
        payDate: DateFormatter.toApiDate(
          DateTime(period.year, period.month, 28),
        ),
        isPdfAvailable: true,
      );
    });
  }

  PayslipDetailModel? payslipDetail(String id) {
    final PayslipSummaryModel? summary = payslips
        .where((PayslipSummaryModel p) => p.id == id)
        .firstOrNull;
    if (summary == null) {
      return null;
    }

    const num basic = 1500000;
    const num allowanceHousing = 350000;
    const num allowanceTransport = 150000;
    final num overtime = summary.netPay > 1860000 ? 50000 : 0;
    final num gross = basic + allowanceHousing + allowanceTransport + overtime;

    const num ssb = 6000;
    const num incomeTax = 195000;
    final num advance = gross - ssb - incomeTax - summary.netPay;
    final num totalDeductions = ssb + incomeTax + (advance > 0 ? advance : 0);

    return PayslipDetailModel(
      id: summary.id,
      year: summary.year,
      month: summary.month,
      currencyCode: MockSupport.currencyCode,
      basicSalary: basic,
      grossEarnings: gross,
      totalDeductions: totalDeductions,
      netPay: gross - totalDeductions,
      periodLabel: summary.periodLabel,
      payDate: summary.payDate,
      paidDays: 30,
      lopDays: 0,
      earnings: <PayslipComponentModel>[
        const PayslipComponentModel(
          label: 'Basic Salary',
          code: 'BASIC',
          amount: basic,
        ),
        const PayslipComponentModel(
          label: 'Housing Allowance',
          code: 'HRA',
          amount: allowanceHousing,
        ),
        const PayslipComponentModel(
          label: 'Transport Allowance',
          code: 'TRA',
          amount: allowanceTransport,
        ),
        if (overtime > 0)
          PayslipComponentModel(
            label: 'Overtime',
            code: 'OT',
            amount: overtime,
          ),
      ],
      deductions: <PayslipComponentModel>[
        const PayslipComponentModel(
          label: 'Social Security',
          code: 'SSB',
          amount: ssb,
        ),
        const PayslipComponentModel(
          label: 'Income Tax',
          code: 'PIT',
          amount: incomeTax,
        ),
        if (advance > 0)
          PayslipComponentModel(
            label: 'Salary Advance',
            code: 'ADV',
            amount: advance,
          ),
      ],
      isPdfAvailable: true,
    );
  }

  // --- Profile --------------------------------------------------------

  EmployeeProfileModel _profile = const EmployeeProfileModel(
    userId: MockSupport.userId,
    employeeId: MockSupport.employeeId,
    fullName: MockSupport.fullName,
    email: MockSupport.email,
    mobileNumber: '+95 9 123 456 789',
    address: 'No. 24, Bo Aung Kyaw Street, Kyauktada, Yangon',
    dateOfBirth: '1994-07-21',
    gender: 'Female',
    nationalId: '12/ABC(N)123456',
    maritalStatus: 'Single',
    bloodGroup: 'O+',
    department: MockSupport.department,
    designation: MockSupport.designation,
    employmentType: 'Permanent',
    reportingManagerName: 'U Myint Soe',
    workLocation: 'Yangon HQ',
    dateOfJoining: '2021-02-01',
    confirmationDate: '2021-08-01',
    emergencyContact: EmergencyContactModel(
      name: 'Daw Khin Mar',
      relationship: 'Mother',
      mobileNumber: '+95 9 987 654 321',
      address: 'No. 24, Bo Aung Kyaw Street, Kyauktada, Yangon',
    ),
  );

  EmployeeProfileModel get profile => _profile;

  EmployeeProfileModel updateContact({
    required String mobileNumber,
    String? address,
  }) {
    _profile = _profile.copyWith(mobileNumber: mobileNumber, address: address);
    return _profile;
  }

  EmployeeProfileModel updateEmergencyContact(EmergencyContactModel contact) {
    _profile = _profile.copyWith(emergencyContact: contact);
    return _profile;
  }

  // --- Notifications --------------------------------------------------

  /// The newest payslip's month: last month, as [payslips] starts there.
  static DateTime get _lastPayPeriod {
    final DateTime now = DateTime.now();
    return DateTime(now.year, now.month - 1);
  }

  late final List<AppNotificationModel> _notifications = <AppNotificationModel>[
    AppNotificationModel(
      id: 'mock-n-1',
      title: 'Leave approved',
      // The dates and approver of mock-lr-2, so the message and the request
      // it links to agree.
      body:
          'Your medical leave for '
          '${DateFormatter.dateRange(MockSupport.daysAgo(16), MockSupport.daysAgo(15))} '
          'has been approved by Ma Myat Gyi. Get well soon.',
      type: 'LeaveApproval',
      createdAt: DateTime.now().toUtc().subtract(const Duration(hours: 3)),
      deepLink: 'hrapp://leave/mock-lr-2',
    ),
    AppNotificationModel(
      id: 'mock-n-2',
      title: 'Payslip available',
      // Names the month, and links to that month's payslip.
      body:
          'Your ${DateFormatter.monthYear(_lastPayPeriod)} payslip is ready '
          'to view.',
      type: 'Payslip',
      deepLink:
          'hrapp://payslip/mock-ps-${_lastPayPeriod.year}-${_lastPayPeriod.month}',
      createdAt: DateTime.now().toUtc().subtract(
        const Duration(days: 1, hours: 2),
      ),
    ),
    AppNotificationModel(
      id: 'mock-n-3',
      title: 'Office closed for Thingyan',
      body:
          'The office will be closed from 13 to 16 April. Submit any '
          'claims before the 11th so payroll can process them on time.',
      type: 'Announcement',
      createdAt: DateTime.now().toUtc().subtract(const Duration(days: 2)),
    ),
    AppNotificationModel(
      id: 'mock-n-4',
      title: 'Missed check-out',
      body:
          'No check-out was recorded on your last working day. Contact '
          'HR if this is incorrect.',
      type: 'Attendance',
      deepLink: 'hrapp://attendance/mock-att-4',
      createdAt: DateTime.now().toUtc().subtract(const Duration(days: 4)),
      isRead: true,
      readAt: DateTime.now().toUtc().subtract(const Duration(days: 3)),
    ),
    AppNotificationModel(
      id: 'mock-n-5',
      title: 'Leave rejected',
      body:
          'Your casual leave request was not approved. Reason: '
          'month-end close; please reschedule.',
      type: 'LeaveRejection',
      createdAt: DateTime.now().toUtc().subtract(const Duration(days: 37)),
      isRead: true,
      readAt: DateTime.now().toUtc().subtract(const Duration(days: 36)),
      deepLink: 'hrapp://leave/mock-lr-3',
    ),
  ];

  List<AppNotificationModel> get notifications =>
      List<AppNotificationModel>.unmodifiable(_notifications);

  int get unreadNotificationCount =>
      _notifications.where((AppNotificationModel n) => !n.isRead).length;

  void markNotificationRead(String id) {
    final int index = _notifications.indexWhere(
      (AppNotificationModel n) => n.id == id,
    );
    if (index >= 0 && !_notifications[index].isRead) {
      _notifications[index] = _notifications[index].copyWith(
        isRead: true,
        readAt: DateTime.now().toUtc(),
      );
    }
  }

  void markAllNotificationsRead() {
    for (int index = 0; index < _notifications.length; index++) {
      if (!_notifications[index].isRead) {
        _notifications[index] = _notifications[index].copyWith(
          isRead: true,
          readAt: DateTime.now().toUtc(),
        );
      }
    }
  }
}
