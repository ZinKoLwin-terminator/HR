import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_store.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_controllers.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_providers.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

import '../../../helpers/test_doubles.dart';
import '../grievance_test_helpers.dart';

/// The controllers over the fixture data source: what the screens see after
/// each action.
void main() {
  ProviderContainer makeContainer() {
    final ProviderContainer container = ProviderContainer(
      overrides: [
        appConfigProvider.overrideWithValue(testConfig),
        grievanceRemoteDataSourceProvider.overrideWithValue(
          freshMockDataSource(),
        ),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  group('MyGrievancesController', () {
    test('loads the list and reloads it when the filter changes', () async {
      final ProviderContainer container = makeContainer();
      final ProviderSubscription<AsyncValue<PaginatedState<GrievanceSummary>>>
      sub = container.listen(myGrievancesControllerProvider, (_, _) {});
      addTearDown(sub.close);

      final PaginatedState<GrievanceSummary> all = await container.read(
        myGrievancesControllerProvider.future,
      );
      expect(all.items, hasLength(5));

      container
          .read(grievanceStatusFilterProvider.notifier)
          .select(GrievanceStatus.draft);
      final PaginatedState<GrievanceSummary> drafts = await container.read(
        myGrievancesControllerProvider.future,
      );
      expect(drafts.items.single.id, 101);
    });
  });

  group('GrievanceFormController', () {
    test('saves a new draft, then updates the same one', () async {
      final ProviderContainer container = makeContainer();
      final GrievanceFormController form = container.read(
        grievanceFormControllerProvider.notifier,
      )..start(null);

      final GrievanceDetail? first = await form.saveDraft(
        GrievanceTestData.input(subject: 'First'),
      );
      final GrievanceDetail? second = await form.saveDraft(
        GrievanceTestData.input(subject: 'Second'),
      );

      expect(first, isNotNull);
      expect(second!.id, first!.id);
      expect(second.subject, 'Second');
      expect(
        container.read(grievanceFormControllerProvider).submission,
        isA<SubmissionSuccess>(),
      );
    });

    test('submits after saving, and the case gets its number', () async {
      final ProviderContainer container = makeContainer();
      final GrievanceFormController form = container.read(
        grievanceFormControllerProvider.notifier,
      )..start(null);

      final GrievanceDetail? submitted = await form.submit(
        GrievanceTestData.input(),
        declarationAccepted: true,
      );

      expect(submitted!.status, GrievanceStatus.submitted);
      expect(submitted.caseNo, isNotNull);
      final SubmissionState state = container
          .read(grievanceFormControllerProvider)
          .submission;
      expect(
        (state as SubmissionSuccess).message,
        'Grievance ${submitted.caseNo} submitted.',
      );
    });

    test('keeps the saved draft when the submission is refused', () async {
      final ProviderContainer container = makeContainer();
      final GrievanceFormController form = container.read(
        grievanceFormControllerProvider.notifier,
      )..start(null);

      final GrievanceDetail? submitted = await form.submit(
        GrievanceTestData.input(description: 'Too short'),
        declarationAccepted: true,
      );

      final GrievanceFormState state = container.read(
        grievanceFormControllerProvider,
      );
      expect(submitted, isNull);
      expect(state.saved?.status, GrievanceStatus.draft);
      expect(
        (state.submission.failureOrNull! as ValidationFailure).errorFor(
          'description',
        ),
        'The description must be 20 to 4000 characters.',
      );
    });

    test('refuses a sixth draft with the API message', () async {
      final ProviderContainer container = makeContainer();
      final GrievanceFormController form = container.read(
        grievanceFormControllerProvider.notifier,
      );
      for (int i = 0; i < 4; i++) {
        form.start(null);
        await form.saveDraft(GrievanceTestData.input(subject: 'Draft $i'));
      }

      form.start(null);
      final GrievanceDetail? sixth = await form.saveDraft(
        GrievanceTestData.input(subject: 'Sixth'),
      );

      expect(sixth, isNull);
      expect(
        container
            .read(grievanceFormControllerProvider)
            .submission
            .failureOrNull
            ?.message,
        GrievanceRules.tooManyDrafts,
      );
    });
  });

  group('GrievanceActionsController', () {
    test('responding moves the case on and refreshes the detail', () async {
      final ProviderContainer container = makeContainer();
      final GrievanceDetail awaiting = await container.read(
        grievanceDetailProvider(103).future,
      );

      final GrievanceDetail? updated = await container
          .read(grievanceActionsControllerProvider.notifier)
          .send(awaiting, 'Mondays in May; two colleagues were there.');

      expect(updated!.status, GrievanceStatus.underReview);
      expect(
        (container.read(
          grievanceActionsControllerProvider,
        ) as SubmissionSuccess).message,
        'Your answer was sent to HR.',
      );
      final GrievanceDetail reloaded = await container.read(
        grievanceDetailProvider(103).future,
      );
      expect(reloaded.status, GrievanceStatus.underReview);
    });

    test('deletes a draft', () async {
      final ProviderContainer container = makeContainer();
      final GrievanceDetail draft = await container.read(
        grievanceDetailProvider(101).future,
      );

      final bool deleted = await container
          .read(grievanceActionsControllerProvider.notifier)
          .deleteDraft(draft);

      expect(deleted, isTrue);
      expect(GrievanceMockStore.instance.caseById(101), isNull);
    });

    test('marking seen clears the dot in the loaded list', () async {
      final ProviderContainer container = makeContainer();
      final ProviderSubscription<AsyncValue<PaginatedState<GrievanceSummary>>>
      sub = container.listen(myGrievancesControllerProvider, (_, _) {});
      addTearDown(sub.close);
      await container.read(myGrievancesControllerProvider.future);
      final GrievanceDetail recorded = await container.read(
        grievanceDetailProvider(102).future,
      );

      await container
          .read(grievanceActionsControllerProvider.notifier)
          .markSeen(recorded);

      final GrievanceSummary row = container
          .read(myGrievancesControllerProvider)
          .value!
          .items
          .firstWhere((GrievanceSummary g) => g.id == 102);
      expect(row.hasUnreadUpdate, isFalse);
    });
  });
}
