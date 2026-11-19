import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/domain/usecases/grievance_usecases.dart';
import 'package:mocktail/mocktail.dart';

import '../grievance_test_helpers.dart';

void main() {
  late MockGrievanceRepository repository;

  setUpAll(registerGrievanceFallbacks);

  setUp(() {
    repository = MockGrievanceRepository();
    when(() => repository.createDraft(any())).thenAnswer(
      (_) async => Result<GrievanceDetail>.success(GrievanceTestData.detail()),
    );
    when(
      () => repository.updateDraft(
        any(),
        rowVersion: any(named: 'rowVersion'),
        input: any(named: 'input'),
      ),
    ).thenAnswer(
      (_) async => Result<GrievanceDetail>.success(GrievanceTestData.detail()),
    );
    when(
      () => repository.submit(
        any(),
        rowVersion: any(named: 'rowVersion'),
        declarationAccepted: any(named: 'declarationAccepted'),
      ),
    ).thenAnswer(
      (_) async => Result<GrievanceDetail>.success(
        GrievanceTestData.detail(status: GrievanceStatus.submitted),
      ),
    );
    when(
      () => repository.sendMessage(
        any(),
        rowVersion: any(named: 'rowVersion'),
        body: any(named: 'body'),
        inReplyToId: any(named: 'inReplyToId'),
      ),
    ).thenAnswer(
      (_) async => Result<GrievanceDetail>.success(
        GrievanceTestData.detail(status: GrievanceStatus.underReview),
      ),
    );
  });

  group('GrievanceRules', () {
    final DateTime today = DateTime(2026, 6, 15);

    test('a draft needs only a category and a subject', () {
      expect(
        GrievanceRules.violations(
          const GrievanceDraftInput(categoryId: 1, subject: 'Hi'),
          today: today,
        ),
        isEmpty,
      );
      expect(
        GrievanceRules.violations(
          const GrievanceDraftInput(categoryId: 0, subject: ''),
          today: today,
        ),
        <String, String>{
          'categoryId': 'Choose a category.',
          'subject': 'Subject is required.',
        },
      );
    });

    test('a submission needs the minimums and the declaration', () {
      final Map<String, String> errors = GrievanceRules.violations(
        const GrievanceDraftInput(
          categoryId: 1,
          subject: 'Hi',
          description: 'Too short',
        ),
        today: today,
        forSubmission: true,
      );
      expect(errors['subject'], 'The subject must be 5 to 200 characters.');
      expect(
        errors['description'],
        'The description must be 20 to 4000 characters.',
      );
      expect(errors['declarationAccepted'], GrievanceRules.declarationRequired);
    });

    test('checks the maximum lengths and the incident date', () {
      final Map<String, String> errors = GrievanceRules.violations(
        GrievanceDraftInput(
          categoryId: 1,
          subject: 'x' * 201,
          description: 'y' * 4001,
          location: 'z' * 201,
          involvedPersonText: 'p' * 201,
          incidentDate: DateTime(2026, 6, 16),
        ),
        today: today,
      );
      expect(errors.keys, <String>[
        'subject',
        'description',
        'incidentDate',
        'location',
        'involvedPersonText',
      ]);
      expect(
        GrievanceRules.incidentDateError(
          DateTime(2026, 6, 15, 23, 59),
          today: today,
        ),
        isNull,
      );
    });
  });

  group('SaveGrievanceDraftUseCase', () {
    late SaveGrievanceDraftUseCase useCase;

    setUp(
      () => useCase = SaveGrievanceDraftUseCase(
        repository,
        today: () => DateTime(2026, 6, 15),
      ),
    );

    test('creates a new draft, trimmed', () async {
      await useCase(GrievanceTestData.input(subject: '  Spaced  '));

      final GrievanceDraftInput sent =
          verify(() => repository.createDraft(captureAny())).captured.single
              as GrievanceDraftInput;
      expect(sent.subject, 'Spaced');
    });

    test('updates an existing draft with its row version', () async {
      final GrievanceDetail existing = GrievanceTestData.detail(
        rowVersion: 'djE=',
      );

      await useCase(GrievanceTestData.input(), existing: existing);

      verify(
        () => repository.updateDraft(
          existing.id,
          rowVersion: 'djE=',
          input: any(named: 'input'),
        ),
      ).called(1);
      verifyNever(() => repository.createDraft(any()));
    });

    test('refuses to change how an existing draft reports', () async {
      final Result<GrievanceDetail> result = await useCase(
        GrievanceTestData.input(isAnonymous: true),
        existing: GrievanceTestData.detail(),
      );

      final ValidationFailure failure =
          result.failureOrNull! as ValidationFailure;
      expect(failure.errorFor('isAnonymous'), GrievanceRules.anonymityFixed);
      verifyNever(
        () => repository.updateDraft(
          any(),
          rowVersion: any(named: 'rowVersion'),
          input: any(named: 'input'),
        ),
      );
    });

    test(
      'refuses anonymity for a named-only category before calling',
      () async {
        final Result<GrievanceDetail> result = await useCase(
          GrievanceTestData.input(categoryId: 4, isAnonymous: true),
          category: GrievanceTestData.pay,
        );

        expect(
          (result.failureOrNull! as ValidationFailure).errorFor('isAnonymous'),
          GrievanceRules.anonymousNotAllowed,
        );
        verifyNever(() => repository.createDraft(any()));
      },
    );
  });

  group('SubmitGrievanceUseCase', () {
    late SubmitGrievanceUseCase useCase;

    setUp(
      () => useCase = SubmitGrievanceUseCase(
        repository,
        today: () => DateTime(2026, 6, 15),
      ),
    );

    test('submits a complete draft with the declaration', () async {
      final Result<GrievanceDetail> result = await useCase(
        GrievanceTestData.detail(),
        declarationAccepted: true,
      );

      expect(result.isSuccess, isTrue);
      verify(
        () => repository.submit(
          7,
          rowVersion: 'AAAAAAAAB9E=',
          declarationAccepted: true,
        ),
      ).called(1);
    });

    test('does not call the server without the declaration', () async {
      final Result<GrievanceDetail> result = await useCase(
        GrievanceTestData.detail(),
        declarationAccepted: false,
      );

      expect(
        (result.failureOrNull! as ValidationFailure).errorFor(
          'declarationAccepted',
        ),
        GrievanceRules.declarationRequired,
      );
      verifyNever(
        () => repository.submit(
          any(),
          rowVersion: any(named: 'rowVersion'),
          declarationAccepted: any(named: 'declarationAccepted'),
        ),
      );
    });

    test('refuses a description under the minimum', () async {
      final Result<GrievanceDetail> result = await useCase(
        GrievanceTestData.detail(description: 'Too short'),
        declarationAccepted: true,
      );

      expect(
        (result.failureOrNull! as ValidationFailure).errorFor('description'),
        'The description must be 20 to 4000 characters.',
      );
    });

    test('only a draft can be submitted', () async {
      final Result<GrievanceDetail> result = await useCase(
        GrievanceTestData.detail(status: GrievanceStatus.submitted),
        declarationAccepted: true,
      );

      expect(result.failureOrNull, isA<ConflictFailure>());
    });
  });

  group('SendGrievanceMessageUseCase', () {
    late SendGrievanceMessageUseCase useCase;

    setUp(() => useCase = SendGrievanceMessageUseCase(repository));

    test('answers the open information request while HR waits', () async {
      final GrievanceDetail awaiting = GrievanceTestData.detail(
        status: GrievanceStatus.awaitingEmployeeResponse,
        messages: <GrievanceMessage>[
          GrievanceMessage(
            id: 31,
            direction: GrievanceMessageDirection.toSubmitter,
            kind: GrievanceMessageKind.infoRequest,
            body: 'Which dates?',
            actor: 'HR team',
            createdAt: DateTime.utc(2026, 6, 10),
          ),
        ],
      );

      await useCase(awaiting, '  Mondays in May.  ');

      verify(
        () => repository.sendMessage(
          7,
          rowVersion: 'AAAAAAAAB9E=',
          body: 'Mondays in May.',
          inReplyToId: 31,
        ),
      ).called(1);
    });

    test('refuses an empty message and a draft', () async {
      final Result<GrievanceDetail> empty = await useCase(
        GrievanceTestData.detail(status: GrievanceStatus.underReview),
        '   ',
      );
      final Result<GrievanceDetail> draft = await useCase(
        GrievanceTestData.detail(),
        'Hello',
      );
      final Result<GrievanceDetail> closed = await useCase(
        GrievanceTestData.detail(status: GrievanceStatus.closed),
        'Hello',
      );

      expect(
        (empty.failureOrNull! as ValidationFailure).errorFor('message'),
        'Enter a message.',
      );
      expect(draft.failureOrNull, isA<ConflictFailure>());
      expect(closed.failureOrNull, isA<ConflictFailure>());
    });
  });

  group('GrievanceStatus', () {
    test('open, respond and message rules follow the API', () {
      expect(GrievanceStatus.reopened.isOpen, isTrue);
      expect(GrievanceStatus.resolved.isOpen, isFalse);
      expect(GrievanceStatus.rejected.acceptsMessages, isTrue);
      expect(GrievanceStatus.closed.acceptsMessages, isFalse);
      expect(GrievanceStatus.draft.acceptsMessages, isFalse);
      expect(
        GrievanceStatus.awaitingEmployeeResponse.isAwaitingResponse,
        isTrue,
      );
    });
  });
}
