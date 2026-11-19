import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_store.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_remote_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// A fixture-backed stand-in for [GrievanceRemoteDataSource].
///
/// NOT A BACKEND — see [MockSupport].
///
/// Mirrors the rules of the API's `MyGrievanceService` and `GrievanceCase`
/// in the order the API checks them, with the API's messages, so every
/// refusal the form and the detail screen handle can be exercised offline:
/// the five-draft limit, the draft and submission rules, anonymity fixed once
/// a draft exists, stale row versions, and the respond flow that returns a
/// case to the status it had before HR asked a question.
class GrievanceMockDataSource implements GrievanceRemoteDataSource {
  GrievanceMockDataSource({
    GrievanceMockStore? store,
    this.simulateLatency = true,
    DateTime Function()? clock,
  }) : _store = store ?? GrievanceMockStore.instance,
       _clock = clock ?? DateTime.now;

  final GrievanceMockStore _store;

  /// False in tests, so no timers are left pending.
  final bool simulateLatency;

  final DateTime Function() _clock;

  DateTime get _nowUtc => _clock().toUtc();

  DateTime get _today {
    final DateTime now = _clock();
    return DateTime(now.year, now.month, now.day);
  }

  Future<void> _delay([int milliseconds = 350]) async {
    if (simulateLatency) {
      await MockSupport.delay(Duration(milliseconds: milliseconds));
    }
  }

  @override
  Future<GrievanceOptionsModel> getOptions() async {
    await _delay(250);
    final List<MockGrievanceDepartment> departments =
        _store.departments
            .where((MockGrievanceDepartment d) => d.isActive)
            .toList()
          ..sort(
            (MockGrievanceDepartment a, MockGrievanceDepartment b) =>
                a.name.toLowerCase().compareTo(b.name.toLowerCase()),
          );
    return GrievanceOptionsModel(
      categories: _store.categories
          .where((MockGrievanceCategory c) => c.isActive)
          .map((MockGrievanceCategory c) => c.toOption())
          .toList(growable: false),
      departments: departments
          .map((MockGrievanceDepartment d) => d.toOption())
          .toList(growable: false),
    );
  }

  @override
  Future<PaginatedResponse<MyGrievanceModel>> getMyGrievances({
    required PageRequest page,
    GrievanceStatus? status,
    String? search,
  }) async {
    await _delay();

    Iterable<MyGrievanceDetailModel> rows = _store.cases;
    final String? wireStatus = status == null
        ? null
        : GrievanceStatusMapper.toApi(status);
    if (wireStatus != null) {
      rows = rows.where((MyGrievanceDetailModel c) => c.status == wireStatus);
    }
    final String query = (search ?? '').trim().toLowerCase();
    if (query.isNotEmpty) {
      rows = rows.where(
        (MyGrievanceDetailModel c) =>
            c.subject.toLowerCase().contains(query) ||
            (c.caseNo?.toLowerCase().contains(query) ?? false),
      );
    }

    final List<MyGrievanceModel> all =
        rows.map((MyGrievanceDetailModel c) => c.toSummaryModel()).toList()
          ..sort((MyGrievanceModel a, MyGrievanceModel b) {
            final DateTime aAt =
                a.lastUpdateAtUtc ?? a.submittedAtUtc ?? a.createdAtUtc;
            final DateTime bAt =
                b.lastUpdateAtUtc ?? b.submittedAtUtc ?? b.createdAtUtc;
            final int byTime = bAt.compareTo(aAt);
            return byTime != 0 ? byTime : b.id.compareTo(a.id);
          });

    final int totalPages = all.isEmpty
        ? 1
        : (all.length / page.pageSize).ceil();
    return PaginatedResponse<MyGrievanceModel>(
      items: MockSupport.page(all, page.pageNumber, page.pageSize),
      pageNumber: page.pageNumber,
      pageSize: page.pageSize,
      totalCount: all.length,
      totalPages: totalPages,
      hasNextPage: page.pageNumber < totalPages,
      hasPreviousPage: page.pageNumber > 1,
    );
  }

  @override
  Future<MyGrievanceDetailModel> getById(int id) async {
    await _delay(250);
    return _find(id);
  }

  @override
  Future<MyGrievanceDetailModel> createDraft(GrievanceDraftInput input) async {
    await _delay(500);
    final GrievanceDraftInput d = input.normalized();

    // 1. The request's shape (lengths, required fields).
    _throwIfAny(GrievanceRules.violations(d, today: _today));

    // 2. The draft limit, before anything is looked up.
    final int drafts = _store.cases
        .where((MyGrievanceDetailModel c) => c.status == 'Draft')
        .length;
    if (drafts >= GrievanceRules.maxOpenDrafts) {
      throw const ValidationException(
        GrievanceRules.tooManyDrafts,
        statusCode: 400,
      );
    }

    // 3. The references, then the domain rules that need the category.
    final MockGrievanceCategory category = _references(d);
    _throwIfAny(_domainViolations(d, category));

    final DateTime now = _nowUtc;
    final MyGrievanceDetailModel draft = _applyDraft(
      MyGrievanceDetailModel(
        id: _store.nextCaseId(),
        categoryId: category.id,
        categoryName: category.name,
        subject: d.subject,
        status: 'Draft',
        createdAtUtc: now,
        rowVersion: _store.nextRowVersion(),
      ),
      d,
      category,
    ).copyWith(lastUpdateAtUtc: now);
    _store.replace(draft);
    return draft;
  }

  @override
  Future<MyGrievanceDetailModel> updateDraft(
    int id, {
    required String rowVersion,
    required GrievanceDraftInput input,
  }) async {
    await _delay(500);
    final MyGrievanceDetailModel current = _openOwn(id, rowVersion);
    final GrievanceDraftInput d = input.normalized();

    _throwIfAny(GrievanceRules.violations(d, today: _today));
    _ensureDraft(current, 'only a draft can be changed or deleted');
    final MockGrievanceCategory category = _references(d);

    // Fixed when the draft was created: switching would link the employee
    // to an anonymous case (security review L5).
    if (d.isAnonymous != current.isAnonymous) {
      throw const ValidationException(
        GrievanceRules.anonymityFixed,
        fieldErrors: <String, List<String>>{
          'isAnonymous': <String>[GrievanceRules.anonymityFixed],
        },
        statusCode: 400,
      );
    }
    _throwIfAny(_domainViolations(d, category));

    final MyGrievanceDetailModel updated = _applyDraft(
      current,
      d,
      category,
    ).copyWith(lastUpdateAtUtc: _nowUtc, rowVersion: _store.nextRowVersion());
    _store.replace(updated);
    return updated;
  }

  @override
  Future<void> deleteDraft(int id, {required String rowVersion}) async {
    await _delay(400);
    final MyGrievanceDetailModel current = _openOwn(id, rowVersion);
    _ensureDraft(current, 'only a draft can be changed or deleted');
    _store.remove(id);
  }

  @override
  Future<MyGrievanceDetailModel> submit(
    int id, {
    required String rowVersion,
    required bool declarationAccepted,
  }) async {
    await _delay(700);
    final MyGrievanceDetailModel current = _openOwn(id, rowVersion);
    _ensureDraft(current, 'only a draft can be submitted');

    final MockGrievanceCategory? category = _store.categoryById(
      current.categoryId,
    );
    if (category == null) {
      throw StateError('The category of grievance case #$id is missing.');
    }
    _throwIfAny(
      GrievanceRules.violations(
        _inputOf(current),
        today: _today,
        category: _entityOf(category),
        isCategoryActive: category.isActive,
        forSubmission: true,
        declarationAccepted: declarationAccepted,
      ),
    );

    final DateTime now = _nowUtc;
    final MyGrievanceDetailModel submitted = current.copyWith(
      caseNo: _store.nextCaseNo(),
      status: 'Submitted',
      submittedAtUtc: now,
      declarationAcceptedAtUtc: now,
      lastUpdateAtUtc: now,
      rowVersion: _store.nextRowVersion(),
      timeline: <MyGrievanceTimelineEntryModel>[
        ...current.timeline,
        MyGrievanceTimelineEntryModel(
          fromStatus: 'Draft',
          toStatus: 'Submitted',
          action: 'Submit',
          actor: 'You',
          occurredAtUtc: now,
        ),
      ],
    );
    _store.replace(submitted);
    return submitted;
  }

  @override
  Future<MyGrievanceDetailModel> sendMessage(
    int id, {
    required String rowVersion,
    required String body,
    int? inReplyToId,
  }) async {
    await _delay(500);
    if (rowVersion.trim().isEmpty) {
      throw const ValidationException(
        'The row version is missing or not valid.',
        fieldErrors: <String, List<String>>{
          'rowVersion': <String>['The row version is missing or not valid.'],
        },
        statusCode: 400,
      );
    }
    final String text = body.trim();
    if (text.isEmpty || text.length > GrievanceRules.messageMaxLength) {
      final String message = text.isEmpty
          ? 'Message is required.'
          : 'Message must be at most ${GrievanceRules.messageMaxLength} '
                'characters.';
      throw ValidationException(
        message,
        fieldErrors: <String, List<String>>{
          'body': <String>[message],
        },
        statusCode: 400,
      );
    }

    final MyGrievanceDetailModel current = _find(id);
    return current.status == 'AwaitingEmployeeResponse'
        ? _respond(current, rowVersion, text, inReplyToId)
        : _postMessage(current, text);
  }

  /// The answer to HR's request: the row version must be current, and the
  /// case returns to the status it had before HR asked.
  MyGrievanceDetailModel _respond(
    MyGrievanceDetailModel current,
    String rowVersion,
    String body,
    int? inReplyToId,
  ) {
    if (current.rowVersion != rowVersion) {
      throw const ConflictException(
        GrievanceRules.staleRecord,
        statusCode: 409,
      );
    }
    final List<MyGrievanceMessageModel> requests =
        current.messages
            .where((MyGrievanceMessageModel m) => m.kind == 'InfoRequest')
            .toList()
          ..sort(
            (MyGrievanceMessageModel a, MyGrievanceMessageModel b) =>
                b.createdAtUtc.compareTo(a.createdAtUtc),
          );
    int? replyTo;
    if (inReplyToId != null) {
      if (requests.every((MyGrievanceMessageModel m) => m.id != inReplyToId)) {
        throw const ValidationException(
          'Answer an information request of this case.',
          fieldErrors: <String, List<String>>{
            'inReplyToId': <String>[
              'Answer an information request of this case.',
            ],
          },
          statusCode: 400,
        );
      }
      replyTo = inReplyToId;
    } else {
      replyTo = requests.firstOrNull?.id;
    }

    final String target = _store.returnStatuses[current.id] ?? 'UnderReview';
    final DateTime now = _nowUtc;
    final MyGrievanceDetailModel answered = current.copyWith(
      status: target,
      rowVersion: _store.nextRowVersion(),
      timeline: <MyGrievanceTimelineEntryModel>[
        ...current.timeline,
        MyGrievanceTimelineEntryModel(
          fromStatus: 'AwaitingEmployeeResponse',
          toStatus: target,
          action: 'Respond',
          actor: 'You',
          occurredAtUtc: now,
        ),
      ],
      messages: <MyGrievanceMessageModel>[
        ...current.messages,
        MyGrievanceMessageModel(
          id: _store.nextMessageId(),
          direction: 'FromSubmitter',
          kind: 'InfoResponse',
          body: body,
          actor: 'You',
          inReplyToId: replyTo,
          createdAtUtc: now,
        ),
      ],
    );
    _store.returnStatuses.remove(current.id);
    _store.replace(answered);
    return answered;
  }

  /// A plain message: one more row; the case and its row version do not
  /// change.
  MyGrievanceDetailModel _postMessage(
    MyGrievanceDetailModel current,
    String body,
  ) {
    if (current.status == 'Draft' || current.status == 'Closed') {
      throw ConflictException(
        'The case is ${current.status}; messages are exchanged on a submitted '
        'case that is not closed.',
        statusCode: 409,
      );
    }
    final MyGrievanceDetailModel updated = current.copyWith(
      messages: <MyGrievanceMessageModel>[
        ...current.messages,
        MyGrievanceMessageModel(
          id: _store.nextMessageId(),
          direction: 'FromSubmitter',
          kind: 'Message',
          body: body,
          actor: 'You',
          createdAtUtc: _nowUtc,
        ),
      ],
    );
    _store.replace(updated);
    return updated;
  }

  @override
  Future<void> markSeen(int id) async {
    await _delay(150);
    final MyGrievanceDetailModel current = _find(id);
    if (current.hasUnreadUpdate) {
      // The case row and its row version do not change.
      _store.replace(current.copyWith(hasUnreadUpdate: false));
    }
  }

  // ---- Helpers ------------------------------------------------------------

  MyGrievanceDetailModel _find(int id) {
    final MyGrievanceDetailModel? found = _store.caseById(id);
    if (found == null) {
      throw NotFoundException('Grievance case #$id was not found.');
    }
    return found;
  }

  /// Own case for a change: scope 404, then stale 409.
  MyGrievanceDetailModel _openOwn(int id, String rowVersion) {
    final MyGrievanceDetailModel current = _find(id);
    if (current.rowVersion != rowVersion) {
      throw const ConflictException(
        GrievanceRules.staleRecord,
        statusCode: 409,
      );
    }
    return current;
  }

  void _ensureDraft(MyGrievanceDetailModel current, String what) {
    if (current.status != 'Draft') {
      throw ConflictException(
        'The case is ${current.status}; $what.',
        statusCode: 409,
      );
    }
  }

  /// The category must exist (the domain then checks it is active) and an
  /// involved department must exist and be active.
  MockGrievanceCategory _references(GrievanceDraftInput d) {
    final MockGrievanceCategory? category = _store.categoryById(d.categoryId);
    final Map<String, String> errors = <String, String>{
      if (category == null) 'categoryId': GrievanceRules.inactiveCategory,
    };
    final int? departmentId = d.involvedDepartmentId;
    if (departmentId != null) {
      final MockGrievanceDepartment? department = _store.departmentById(
        departmentId,
      );
      if (department == null || !department.isActive) {
        errors['involvedDepartmentId'] = 'Choose an active department.';
      }
    }
    _throwIfAny(errors);
    return category!;
  }

  Map<String, String> _domainViolations(
    GrievanceDraftInput d,
    MockGrievanceCategory category,
  ) => GrievanceRules.violations(
    d,
    today: _today,
    category: _entityOf(category),
    isCategoryActive: category.isActive,
  );

  MyGrievanceDetailModel _applyDraft(
    MyGrievanceDetailModel target,
    GrievanceDraftInput d,
    MockGrievanceCategory category,
  ) {
    final int? departmentId = d.involvedDepartmentId;
    return target.copyWith(
      categoryId: category.id,
      categoryName: category.name,
      subject: d.subject,
      description: d.description,
      incidentDate: d.incidentDate == null
          ? null
          : DateFormatter.toApiDate(d.incidentDate!),
      location: d.location,
      involvedPersonText: d.involvedPersonText,
      involvedDepartmentId: departmentId,
      involvedDepartmentName: departmentId == null
          ? null
          : _store.departmentById(departmentId)?.name,
      isAnonymous: d.isAnonymous,
    );
  }

  static GrievanceDraftInput _inputOf(MyGrievanceDetailModel c) =>
      GrievanceDraftInput(
        categoryId: c.categoryId,
        subject: c.subject,
        description: c.description,
        incidentDate: DateFormatter.parseApiDate(c.incidentDate),
        location: c.location,
        involvedPersonText: c.involvedPersonText,
        involvedDepartmentId: c.involvedDepartmentId,
        isAnonymous: c.isAnonymous,
      );

  static GrievanceCategory _entityOf(MockGrievanceCategory c) =>
      c.toOption().toEntity();

  static void _throwIfAny(Map<String, String> errors) {
    if (errors.isEmpty) {
      return;
    }
    throw ValidationException(
      errors.values.first,
      fieldErrors: <String, List<String>>{
        for (final MapEntry<String, String> e in errors.entries)
          e.key: <String>[e.value],
      },
      statusCode: 400,
    );
  }
}
