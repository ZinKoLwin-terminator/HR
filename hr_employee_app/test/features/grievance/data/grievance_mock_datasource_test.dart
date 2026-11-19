import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_store.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

import '../grievance_test_helpers.dart';

/// The fixture data source mirrors the API's `MyGrievanceService` rules, so
/// these tests pin the behaviour the screens rely on in mock mode.
void main() {
  late GrievanceMockDataSource source;
  final GrievanceMockStore store = GrievanceMockStore.instance;

  setUp(() => source = freshMockDataSource());

  Matcher validationOn(String field, String message) =>
      isA<ValidationException>().having(
        (ValidationException e) => e.fieldErrors[field]?.first,
        field,
        message,
      );

  Future<MyGrievanceDetailModel> newDraft({
    int categoryId = 1,
    String subject = 'Repeated shouting in meetings',
    String description =
        'A team lead shouts at junior staff in the weekly meeting.',
    bool isAnonymous = false,
  }) => source.createDraft(
    GrievanceTestData.input(
      categoryId: categoryId,
      subject: subject,
      description: description,
      isAnonymous: isAnonymous,
    ),
  );

  group('options', () {
    test(
      'offers active categories in order and active departments by name',
      () async {
        final GrievanceOptionsModel options = await source.getOptions();

        expect(
          options.categories.map((GrievanceCategoryOptionModel c) => c.id),
          <int>[1, 2, 3, 4, 5],
        );
        expect(
          options.categories.firstWhere((c) => c.id == 4).allowsAnonymous,
          isFalse,
        );
        final List<String> names = options.departments
            .map((GrievanceDepartmentOptionModel d) => d.name)
            .toList();
        expect(names, isNot(contains('Old Warehouse')));
        expect(names, List<String>.of(names)..sort());
      },
    );
  });

  group('list', () {
    test(
      'returns the newest activity first, drafts as "no case number"',
      () async {
        final PaginatedResponse<MyGrievanceModel> page = await source
            .getMyGrievances(page: const PageRequest());

        expect(page.totalCount, 5);
        final List<DateTime> times = page.items
            .map((MyGrievanceModel g) => g.lastUpdateAtUtc!)
            .toList();
        for (int i = 1; i < times.length; i++) {
          expect(times[i].isAfter(times[i - 1]), isFalse);
        }
        final MyGrievanceModel draft = page.items.firstWhere(
          (MyGrievanceModel g) => g.status == 'Draft',
        );
        expect(draft.caseNo, isNull);
      },
    );

    test('filters by status and searches case number and subject', () async {
      final PaginatedResponse<MyGrievanceModel> awaiting = await source
          .getMyGrievances(
            page: const PageRequest(),
            status: GrievanceStatus.awaitingEmployeeResponse,
          );
      expect(awaiting.items.single.id, 103);

      final PaginatedResponse<MyGrievanceModel> bySubject = await source
          .getMyGrievances(page: const PageRequest(), search: 'ROSTER');
      expect(bySubject.items.single.id, 104);

      final String caseNo = store.caseById(105)!.caseNo!;
      final PaginatedResponse<MyGrievanceModel> byCaseNo = await source
          .getMyGrievances(page: const PageRequest(), search: caseNo);
      expect(byCaseNo.items.single.id, 105);
    });

    test('pages the way the API does', () async {
      final PaginatedResponse<MyGrievanceModel> first = await source
          .getMyGrievances(page: const PageRequest(pageSize: 2));
      final PaginatedResponse<MyGrievanceModel> last = await source
          .getMyGrievances(page: const PageRequest(pageNumber: 3, pageSize: 2));

      expect(first.items, hasLength(2));
      expect(first.hasNextPage, isTrue);
      expect(last.items, hasLength(1));
      expect(last.hasNextPage, isFalse);
    });

    test('includes a case HR recorded on the employee\'s behalf', () async {
      final MyGrievanceDetailModel recorded = await source.getById(102);

      expect(recorded.status, 'Submitted');
      expect(recorded.caseNo, isNotNull);
      expect(recorded.timeline.first.actor, 'HR team');
      expect(
        recorded.timeline.first.reason,
        'Recorded by HR on behalf of the employee (received by phone).',
      );
    });

    test('an unknown id is not found', () {
      expect(source.getById(999), throwsA(isA<NotFoundException>()));
    });
  });

  group('drafts', () {
    test('a new draft has no case number and nothing is sent', () async {
      final MyGrievanceDetailModel draft = await newDraft(subject: 'Hi');

      expect(draft.status, 'Draft');
      expect(draft.caseNo, isNull);
      expect(draft.timeline, isEmpty);
      expect(draft.rowVersion, isNotEmpty);
    });

    test('needs a subject', () {
      expect(
        newDraft(subject: '   '),
        throwsA(validationOn('subject', 'Subject is required.')),
      );
    });

    test('allows at most five open drafts', () async {
      // The seed holds one draft.
      for (int i = 0; i < 4; i++) {
        await newDraft(subject: 'Draft $i');
      }

      await expectLater(
        newDraft(subject: 'One too many'),
        throwsA(
          isA<ValidationException>().having(
            (ValidationException e) => e.message,
            'message',
            GrievanceRules.tooManyDrafts,
          ),
        ),
      );
    });

    test('refuses anonymity where the category does not allow it', () {
      expect(
        newDraft(categoryId: 4, isAnonymous: true),
        throwsA(
          validationOn('isAnonymous', GrievanceRules.anonymousNotAllowed),
        ),
      );
    });

    test('refuses an incident date in the future', () {
      expect(
        source.createDraft(
          GrievanceTestData.input(
            incidentDate: DateTime.now().add(const Duration(days: 1)),
          ),
        ),
        throwsA(
          validationOn(
            'incidentDate',
            'The incident date cannot be in the future.',
          ),
        ),
      );
    });

    test('refuses a retired category and an inactive department', () async {
      await expectLater(
        newDraft(categoryId: 9),
        throwsA(validationOn('categoryId', GrievanceRules.inactiveCategory)),
      );
      await expectLater(
        source.createDraft(
          const GrievanceDraftInput(
            categoryId: 1,
            subject: 'Something',
            involvedDepartmentId: 16,
          ),
        ),
        throwsA(
          validationOn('involvedDepartmentId', 'Choose an active department.'),
        ),
      );
    });

    test('keeps anonymity fixed once the draft exists', () async {
      final MyGrievanceDetailModel draft = await newDraft(isAnonymous: true);

      await expectLater(
        source.updateDraft(
          draft.id,
          rowVersion: draft.rowVersion,
          input: GrievanceTestData.input(isAnonymous: false),
        ),
        throwsA(validationOn('isAnonymous', GrievanceRules.anonymityFixed)),
      );
    });

    test(
      'updates with the current row version and refuses a stale one',
      () async {
        final MyGrievanceDetailModel draft = await newDraft();
        final MyGrievanceDetailModel updated = await source.updateDraft(
          draft.id,
          rowVersion: draft.rowVersion,
          input: GrievanceTestData.input(subject: 'A clearer subject'),
        );

        expect(updated.subject, 'A clearer subject');
        expect(updated.rowVersion, isNot(draft.rowVersion));
        await expectLater(
          source.updateDraft(
            draft.id,
            rowVersion: draft.rowVersion,
            input: GrievanceTestData.input(),
          ),
          throwsA(
            isA<ConflictException>().having(
              (ConflictException e) => e.message,
              'message',
              GrievanceRules.staleRecord,
            ),
          ),
        );
      },
    );

    test('a submitted grievance can no longer be changed or deleted', () async {
      final MyGrievanceDetailModel submitted = store.caseById(102)!;

      await expectLater(
        source.updateDraft(
          102,
          rowVersion: submitted.rowVersion,
          input: GrievanceTestData.input(),
        ),
        throwsA(
          isA<ConflictException>().having(
            (ConflictException e) => e.message,
            'message',
            'The case is Submitted; only a draft can be changed or deleted.',
          ),
        ),
      );
      await expectLater(
        source.deleteDraft(102, rowVersion: submitted.rowVersion),
        throwsA(isA<ConflictException>()),
      );
    });

    test('deletes a draft for good', () async {
      final MyGrievanceDetailModel draft = store.caseById(101)!;

      await source.deleteDraft(101, rowVersion: draft.rowVersion);

      expect(store.caseById(101), isNull);
    });
  });

  group('submit', () {
    test('needs the submission minimums and the declaration', () async {
      // The seeded draft's description is too short to submit.
      final MyGrievanceDetailModel draft = store.caseById(101)!;
      await expectLater(
        source.submit(
          101,
          rowVersion: draft.rowVersion,
          declarationAccepted: true,
        ),
        throwsA(
          validationOn(
            'description',
            'The description must be 20 to 4000 characters.',
          ),
        ),
      );

      final MyGrievanceDetailModel fresh = await newDraft(subject: 'Late');
      await expectLater(
        source.submit(
          fresh.id,
          rowVersion: fresh.rowVersion,
          declarationAccepted: false,
        ),
        throwsA(
          isA<ValidationException>()
              .having(
                (ValidationException e) => e.fieldErrors['subject']?.first,
                'subject',
                'The subject must be 5 to 200 characters.',
              )
              .having(
                (ValidationException e) =>
                    e.fieldErrors['declarationAccepted']?.first,
                'declaration',
                GrievanceRules.declarationRequired,
              ),
        ),
      );
    });

    test('gives the case its number and records who submitted it', () async {
      final MyGrievanceDetailModel draft = await newDraft();

      final MyGrievanceDetailModel submitted = await source.submit(
        draft.id,
        rowVersion: draft.rowVersion,
        declarationAccepted: true,
      );

      expect(submitted.status, 'Submitted');
      expect(submitted.caseNo, matches(RegExp(r'^GRV-\d{4}-\d{5}$')));
      expect(submitted.declarationAcceptedAtUtc, isNotNull);
      expect(submitted.timeline.single.action, 'Submit');
      expect(submitted.timeline.single.actor, 'You');
    });

    test('only a draft can be submitted', () {
      final MyGrievanceDetailModel submitted = store.caseById(102)!;
      expect(
        source.submit(
          102,
          rowVersion: submitted.rowVersion,
          declarationAccepted: true,
        ),
        throwsA(isA<ConflictException>()),
      );
    });
  });

  group('messages', () {
    test(
      'while HR waits, the message is the response and moves the case on',
      () async {
        final MyGrievanceDetailModel awaiting = store.caseById(103)!;
        expect(awaiting.status, 'AwaitingEmployeeResponse');

        final MyGrievanceDetailModel answered = await source.sendMessage(
          103,
          rowVersion: awaiting.rowVersion,
          body: 'The meetings were on Mondays in May. Two colleagues saw it.',
        );

        expect(answered.status, 'UnderReview');
        expect(answered.rowVersion, isNot(awaiting.rowVersion));
        final MyGrievanceMessageModel response = answered.messages.last;
        expect(response.kind, 'InfoResponse');
        expect(response.direction, 'FromSubmitter');
        expect(response.inReplyToId, 801);
        expect(answered.timeline.last.action, 'Respond');
        expect(answered.timeline.last.toStatus, 'UnderReview');
      },
    );

    test('a response with a stale row version is refused', () {
      expect(
        source.sendMessage(103, rowVersion: 'c3RhbGU=', body: 'Answer'),
        throwsA(isA<ConflictException>()),
      );
    });

    test(
      'a plain message changes neither the status nor the row version',
      () async {
        final MyGrievanceDetailModel before = store.caseById(104)!;

        final MyGrievanceDetailModel after = await source.sendMessage(
          104,
          rowVersion: before.rowVersion,
          body: 'Thank you for sorting this out.',
        );

        expect(after.status, before.status);
        expect(after.rowVersion, before.rowVersion);
        expect(after.messages.last.kind, 'Message');
      },
    );

    test('no messages on a draft, and never an empty one', () async {
      final MyGrievanceDetailModel draft = store.caseById(101)!;

      await expectLater(
        source.sendMessage(101, rowVersion: draft.rowVersion, body: 'Hello'),
        throwsA(isA<ConflictException>()),
      );
      await expectLater(
        source.sendMessage(104, rowVersion: draft.rowVersion, body: '  '),
        throwsA(validationOn('body', 'Message is required.')),
      );
    });

    test('opening a case clears its unread badge', () async {
      expect(store.caseById(102)!.hasUnreadUpdate, isTrue);

      await source.markSeen(102);

      expect(store.caseById(102)!.hasUnreadUpdate, isFalse);
    });
  });
}
