import 'dart:convert';

import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';

/// The in-memory grievances behind `GrievanceMockDataSource`.
///
/// ---------------------------------------------------------------------------
/// EVERY VALUE HERE IS INVENTED. None of it comes from an HR system.
/// ---------------------------------------------------------------------------
///
/// Kept in the grievance feature rather than in `MockFixtures`: grievances
/// share no records with the other screens, and a self-contained store keeps
/// a confidential module out of the shared fixture file. Like `MockFixtures`,
/// writes live for the session only. [reset] restores the seed, for tests.
class GrievanceMockStore {
  GrievanceMockStore._() {
    reset();
  }

  static final GrievanceMockStore instance = GrievanceMockStore._();

  /// Every category, active or not, in HR's sort order.
  late List<MockGrievanceCategory> categories;

  late List<MockGrievanceDepartment> departments;

  /// The employee's own cases.
  late List<MyGrievanceDetailModel> cases;

  /// For a case awaiting the employee: the status their response returns it
  /// to (the API keeps this as `ReturnStatus`, never shown to them).
  late Map<int, String> returnStatuses;

  int _nextCaseId = 200;
  int _nextMessageId = 900;
  int _nextCaseNumber = 30;
  int _version = 1000;

  /// A fresh base64 row version, as the API's `byte[] RowVersion` serialises.
  String nextRowVersion() {
    _version++;
    final List<int> bytes = List<int>.generate(
      8,
      (int i) => (_version >> (8 * (7 - i))) & 0xFF,
    );
    return base64Encode(bytes);
  }

  int nextCaseId() => _nextCaseId++;

  int nextMessageId() => _nextMessageId++;

  /// `GRV-yyyy-nnnnn` for the current UTC year.
  String nextCaseNo() {
    final int year = DateTime.now().toUtc().year;
    return 'GRV-$year-${(_nextCaseNumber++).toString().padLeft(5, '0')}';
  }

  MockGrievanceCategory? categoryById(int id) =>
      categories.where((MockGrievanceCategory c) => c.id == id).firstOrNull;

  MockGrievanceDepartment? departmentById(int id) =>
      departments.where((MockGrievanceDepartment d) => d.id == id).firstOrNull;

  MyGrievanceDetailModel? caseById(int id) =>
      cases.where((MyGrievanceDetailModel c) => c.id == id).firstOrNull;

  void replace(MyGrievanceDetailModel updated) {
    final int index = cases.indexWhere(
      (MyGrievanceDetailModel c) => c.id == updated.id,
    );
    if (index >= 0) {
      cases[index] = updated;
    } else {
      cases.add(updated);
    }
  }

  void remove(int id) {
    cases.removeWhere((MyGrievanceDetailModel c) => c.id == id);
    returnStatuses.remove(id);
  }

  /// Restores the seeded fixtures.
  void reset() {
    _nextCaseId = 200;
    _nextMessageId = 900;
    _nextCaseNumber = 30;
    _version = 1000;
    categories = _seedCategories();
    departments = _seedDepartments();
    returnStatuses = <int, String>{103: 'UnderReview'};
    cases = _seedCases();
  }

  static List<MockGrievanceCategory> _seedCategories() =>
      <MockGrievanceCategory>[
        const MockGrievanceCategory(
          id: 1,
          code: 'HAR',
          name: 'Harassment or bullying',
          description:
              'Unwelcome behaviour, intimidation, shouting or bullying by '
              'anyone at work.',
          allowsAnonymous: true,
          defaultTargetDays: 30,
        ),
        const MockGrievanceCategory(
          id: 2,
          code: 'DIS',
          name: 'Discrimination',
          description:
              'Being treated unfairly because of who you are, for example '
              'your gender, age, religion or ethnicity.',
          allowsAnonymous: true,
          defaultTargetDays: 30,
        ),
        const MockGrievanceCategory(
          id: 3,
          code: 'WRK',
          name: 'Working conditions',
          description:
              'Safety, facilities, equipment, workload or working hours.',
          allowsAnonymous: true,
          defaultTargetDays: 21,
        ),
        const MockGrievanceCategory(
          id: 4,
          code: 'PAY',
          name: 'Pay and benefits',
          description:
              'Salary, overtime, allowances or benefits that look wrong. HR '
              'needs to know who you are to check your records.',
          defaultTargetDays: 14,
        ),
        const MockGrievanceCategory(
          id: 5,
          code: 'MGT',
          name: 'Management decision',
          description:
              'A decision about your role, roster, appraisal or transfer that '
              'you think was unfair.',
          defaultTargetDays: 21,
        ),
        // Retired: not offered, but an old draft may still point at it.
        const MockGrievanceCategory(
          id: 9,
          code: 'OTH',
          name: 'Other (retired)',
          description: 'No longer used.',
          allowsAnonymous: true,
          isActive: false,
        ),
      ];

  static List<MockGrievanceDepartment> _seedDepartments() =>
      const <MockGrievanceDepartment>[
        MockGrievanceDepartment(id: 11, name: 'Finance'),
        MockGrievanceDepartment(id: 12, name: 'Human Resources'),
        MockGrievanceDepartment(id: 13, name: 'Information Technology'),
        MockGrievanceDepartment(id: 14, name: 'Operations'),
        MockGrievanceDepartment(id: 15, name: 'Sales and Marketing'),
        MockGrievanceDepartment(id: 16, name: 'Old Warehouse', isActive: false),
      ];

  List<MyGrievanceDetailModel> _seedCases() {
    final int year = DateTime.now().toUtc().year;
    String no(int n) => 'GRV-$year-${n.toString().padLeft(5, '0')}';
    DateTime at(int daysAgo, int hour, [int minute = 0]) =>
        MockSupport.at(MockSupport.daysAgo(daysAgo), hour, minute);
    String day(int daysAgo) =>
        DateFormatter.toApiDate(MockSupport.daysAgo(daysAgo));
    String dayAhead(int days) =>
        DateFormatter.toApiDate(MockSupport.today.add(Duration(days: days)));

    return <MyGrievanceDetailModel>[
      // An unfinished draft: too short to submit yet.
      MyGrievanceDetailModel(
        id: 101,
        categoryId: 4,
        categoryName: 'Pay and benefits',
        subject: 'Overtime for last month not paid',
        status: 'Draft',
        createdAtUtc: at(2, 18, 40),
        lastUpdateAtUtc: at(1, 20, 5),
        rowVersion: nextRowVersion(),
        description: 'Six hours unpaid',
        incidentDate: day(20),
        involvedDepartmentId: 11,
        involvedDepartmentName: 'Finance',
      ),

      // Recorded by HR from a phone call: it reads like a submitted case.
      MyGrievanceDetailModel(
        id: 102,
        caseNo: no(24),
        categoryId: 3,
        categoryName: 'Working conditions',
        subject: 'Air conditioning on the 4th floor has been off for weeks',
        status: 'Submitted',
        createdAtUtc: at(3, 10, 15),
        submittedAtUtc: at(3, 10, 15),
        lastUpdateAtUtc: at(3, 10, 15),
        declarationAcceptedAtUtc: at(3, 10, 15),
        hasUnreadUpdate: true,
        rowVersion: nextRowVersion(),
        description:
            'The air conditioning in the finance room on the 4th floor has '
            'not worked for about three weeks. Afternoons reach 34 °C and two '
            'colleagues have felt unwell. Facilities were told twice by email.',
        incidentDate: day(21),
        location: 'Head office, 4th floor, finance room',
        involvedDepartmentId: 14,
        involvedDepartmentName: 'Operations',
        timeline: <MyGrievanceTimelineEntryModel>[
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Draft',
            toStatus: 'Submitted',
            action: 'Submit',
            actor: 'HR team',
            reason:
                'Recorded by HR on behalf of the employee (received by phone).',
            occurredAtUtc: at(3, 10, 15),
          ),
        ],
      ),

      // Anonymous, waiting for the employee to answer HR's question.
      MyGrievanceDetailModel(
        id: 103,
        caseNo: no(17),
        categoryId: 1,
        categoryName: 'Harassment or bullying',
        subject: 'Repeated shouting during team meetings',
        status: 'AwaitingEmployeeResponse',
        isAnonymous: true,
        createdAtUtc: at(16, 21, 2),
        submittedAtUtc: at(15, 8, 30),
        targetResolutionDate: dayAhead(15),
        lastUpdateAtUtc: at(1, 14, 20),
        acknowledgedAtUtc: at(14, 9, 45),
        declarationAcceptedAtUtc: at(15, 8, 30),
        hasUnreadUpdate: true,
        rowVersion: nextRowVersion(),
        description:
            'In the weekly planning meeting a team lead regularly shouts at '
            'junior staff and calls their work useless in front of everyone. '
            'It has happened at least four times in the last two months.',
        location: 'Meeting room B',
        involvedPersonText: 'A team lead in the planning meeting',
        timeline: <MyGrievanceTimelineEntryModel>[
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Draft',
            toStatus: 'Submitted',
            action: 'Submit',
            actor: 'You',
            occurredAtUtc: at(15, 8, 30),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Submitted',
            toStatus: 'Acknowledged',
            action: 'Acknowledge',
            actor: 'HR team',
            reason: 'Thank you. We have received your grievance.',
            occurredAtUtc: at(14, 9, 45),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Acknowledged',
            toStatus: 'UnderReview',
            action: 'StartReview',
            actor: 'HR team',
            occurredAtUtc: at(10, 11),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'UnderReview',
            toStatus: 'AwaitingEmployeeResponse',
            action: 'RequestInfo',
            actor: 'HR team',
            reason:
                'Can you tell us the dates of the meetings, roughly, and '
                'whether anyone else raised it at the time?',
            occurredAtUtc: at(1, 14, 20),
          ),
        ],
        messages: <MyGrievanceMessageModel>[
          MyGrievanceMessageModel(
            id: 801,
            direction: 'ToSubmitter',
            kind: 'InfoRequest',
            body:
                'Can you tell us the dates of the meetings, roughly, and '
                'whether anyone else raised it at the time?',
            actor: 'HR team',
            createdAtUtc: at(1, 14, 20),
          ),
        ],
      ),

      // Resolved, with a shared resolution and a short conversation.
      MyGrievanceDetailModel(
        id: 104,
        caseNo: no(9),
        categoryId: 5,
        categoryName: 'Management decision',
        subject: 'Weekend roster always given to the same three people',
        status: 'Resolved',
        createdAtUtc: at(48, 9),
        submittedAtUtc: at(48, 9, 20),
        targetResolutionDate: day(27),
        lastUpdateAtUtc: at(6, 16, 10),
        acknowledgedAtUtc: at(47, 10),
        resolvedAtUtc: at(6, 16, 10),
        declarationAcceptedAtUtc: at(48, 9, 20),
        rowVersion: nextRowVersion(),
        description:
            'For the last three months the Saturday roster has gone to the '
            'same three people, including me, while others have none. The '
            'rota is not shared in advance.',
        involvedDepartmentId: 11,
        involvedDepartmentName: 'Finance',
        resolutionSummary:
            'The weekend roster now rotates across the whole team and is '
            'published four weeks ahead. Your two extra Saturdays will be '
            'given back as days off in lieu.',
        timeline: <MyGrievanceTimelineEntryModel>[
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Draft',
            toStatus: 'Submitted',
            action: 'Submit',
            actor: 'You',
            occurredAtUtc: at(48, 9, 20),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Submitted',
            toStatus: 'Acknowledged',
            action: 'Acknowledge',
            actor: 'HR team',
            occurredAtUtc: at(47, 10),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Acknowledged',
            toStatus: 'UnderInvestigation',
            action: 'StartInvestigation',
            actor: 'HR team',
            occurredAtUtc: at(40, 13),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'UnderInvestigation',
            toStatus: 'ResolutionProposed',
            action: 'ProposeResolution',
            actor: 'HR team',
            occurredAtUtc: at(9, 15),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'ResolutionProposed',
            toStatus: 'Resolved',
            action: 'ApproveResolution',
            actor: 'HR team',
            occurredAtUtc: at(6, 16, 10),
          ),
        ],
        messages: <MyGrievanceMessageModel>[
          MyGrievanceMessageModel(
            id: 811,
            direction: 'ToSubmitter',
            kind: 'Message',
            body:
                'We are speaking to the finance manager this week and will '
                'update you.',
            actor: 'HR team',
            createdAtUtc: at(38, 11),
          ),
          MyGrievanceMessageModel(
            id: 812,
            direction: 'FromSubmitter',
            kind: 'Message',
            body: 'Thank you. The next roster is due on Friday.',
            actor: 'You',
            createdAtUtc: at(38, 12, 5),
          ),
        ],
      ),

      // Rejected, with the reason HR gave.
      MyGrievanceDetailModel(
        id: 105,
        caseNo: no(4),
        categoryId: 4,
        categoryName: 'Pay and benefits',
        subject: 'Transport allowance rate is lower than last year',
        status: 'Rejected',
        createdAtUtc: at(70, 17),
        submittedAtUtc: at(70, 17, 30),
        lastUpdateAtUtc: at(60, 10),
        acknowledgedAtUtc: at(68, 9),
        declarationAcceptedAtUtc: at(70, 17, 30),
        rowVersion: nextRowVersion(),
        description:
            'My transport allowance dropped this year although my route and '
            'grade have not changed. I would like it checked.',
        rejectionReason:
            'The allowance rate changed for every employee under the new '
            'policy announced in January; it is not a grievance about your '
            'treatment. Payroll can explain the calculation.',
        timeline: <MyGrievanceTimelineEntryModel>[
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Draft',
            toStatus: 'Submitted',
            action: 'Submit',
            actor: 'You',
            occurredAtUtc: at(70, 17, 30),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Submitted',
            toStatus: 'Acknowledged',
            action: 'Acknowledge',
            actor: 'HR team',
            occurredAtUtc: at(68, 9),
          ),
          MyGrievanceTimelineEntryModel(
            fromStatus: 'Acknowledged',
            toStatus: 'Rejected',
            action: 'Reject',
            actor: 'HR team',
            reason: 'Policy change applying to all employees.',
            occurredAtUtc: at(60, 10),
          ),
        ],
      ),
    ];
  }
}

/// A category as the store keeps it: the option plus whether it is active.
class MockGrievanceCategory {
  const MockGrievanceCategory({
    required this.id,
    required this.code,
    required this.name,
    required this.description,
    this.allowsAnonymous = false,
    this.defaultTargetDays = 30,
    this.isActive = true,
  });

  final int id;
  final String code;
  final String name;
  final String description;
  final bool allowsAnonymous;
  final int defaultTargetDays;
  final bool isActive;

  GrievanceCategoryOptionModel toOption() => GrievanceCategoryOptionModel(
    id: id,
    code: code,
    name: name,
    description: description,
    allowsAnonymous: allowsAnonymous,
    defaultTargetDays: defaultTargetDays,
  );
}

/// A department as the store keeps it.
class MockGrievanceDepartment {
  const MockGrievanceDepartment({
    required this.id,
    required this.name,
    this.isActive = true,
  });

  final int id;
  final String name;
  final bool isActive;

  GrievanceDepartmentOptionModel toOption() =>
      GrievanceDepartmentOptionModel(id: id, name: name);
}
