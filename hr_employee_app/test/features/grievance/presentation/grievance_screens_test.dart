import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_store.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:material_ui/material_ui.dart';

import '../grievance_test_helpers.dart';

void main() {
  final GrievanceMockStore store = GrievanceMockStore.instance;

  /// Scrolls the form or detail list until [finder] is built and visible.
  Future<void> scrollTo(WidgetTester tester, Finder finder) async {
    await tester.scrollUntilVisible(
      finder,
      200,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.pumpAndSettle();
  }

  group('My grievances list', () {
    testWidgets('shows status, case number or Draft, subject and unread dot', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester);

      expect(find.text('My grievances'), findsOneWidget);
      expect(find.text('Draft'), findsWidgets);
      expect(find.text('Overtime for last month not paid'), findsOneWidget);
      expect(find.text(store.caseById(102)!.caseNo!), findsOneWidget);
      // 102 (recorded by HR) and 103 (awaiting a response) are unread.
      expect(
        find.byKey(const ValueKey<String>('grievance-unread-dot')),
        findsNWidgets(2),
      );
      expect(find.text('New grievance'), findsOneWidget);
      expect(find.byType(MockDataBanner), findsNothing);
    });

    testWidgets('shows the Sample data banner in mock mode', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester, config: mockModeConfig);

      expect(find.byType(MockDataBanner), findsOneWidget);
    });

    testWidgets('filters by status and shows an empty state', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester);

      final Finder closed = find.widgetWithText(ChoiceChip, 'Closed');
      await tester.dragUntilVisible(
        closed,
        find.byType(ListView).first,
        const Offset(-200, 0),
      );
      await tester.tap(closed);
      await tester.pumpAndSettle();

      expect(find.text('Nothing with this status'), findsOneWidget);
    });

    testWidgets('opens the detail, with history reasons, and clears the dot', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester);

      await tester.tap(
        find.text('Air conditioning on the 4th floor has been off for weeks'),
      );
      await tester.pumpAndSettle();
      await scrollTo(
        tester,
        find.text(
          'Recorded by HR on behalf of the employee (received by phone).',
        ),
      );

      expect(
        find.text(
          'Recorded by HR on behalf of the employee (received by phone).',
        ),
        findsOneWidget,
      );
      expect(store.caseById(102)!.hasUnreadUpdate, isFalse);
    });

    testWidgets('labels the reply box Respond while HR waits for an answer', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester, initialLocation: '/grievances/103');

      final Finder field = find.byKey(
        const ValueKey<String>('grievance-reply-field'),
      );
      await scrollTo(tester, field);
      await tester.enterText(field, 'The meetings were on Mondays in May.');
      await tester.pumpAndSettle();
      await scrollTo(tester, find.widgetWithText(FilledButton, 'Respond'));
      await tester.tap(find.widgetWithText(FilledButton, 'Respond'));
      await tester.pumpAndSettle();

      expect(store.caseById(103)!.status, 'UnderReview');
      expect(find.text('Your answer was sent to HR.'), findsOneWidget);
    });
  });

  group('New grievance form', () {
    Future<void> chooseCategory(WidgetTester tester, String name) async {
      await tester.tap(
        find.byKey(const ValueKey<String>('grievance-category-field')),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.textContaining(name).last);
      await tester.pumpAndSettle();
    }

    testWidgets('shows the category description and blocks anonymity where '
        'the category refuses it', (WidgetTester tester) async {
      await pumpGrievanceApp(tester, initialLocation: '/grievances/new');

      await chooseCategory(tester, 'Pay and benefits');
      expect(
        find.textContaining('HR needs to know who you are'),
        findsOneWidget,
      );

      final Finder toggle = find.byKey(
        const ValueKey<String>('grievance-anonymous-switch'),
      );
      await scrollTo(tester, toggle);
      final InkWell option = tester.widget<InkWell>(
        find.descendant(of: toggle, matching: find.byType(InkWell)),
      );
      expect(option.onTap, isNull);
      expect(
        find.textContaining('does not accept anonymous grievances'),
        findsOneWidget,
      );
    });

    testWidgets('explains what HR will and will not see when anonymous', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester, initialLocation: '/grievances/new');

      await chooseCategory(tester, 'Harassment or bullying');
      final Finder toggle = find.byKey(
        const ValueKey<String>('grievance-anonymous-switch'),
      );
      await scrollTo(tester, toggle);
      await tester.tap(toggle);
      await tester.pumpAndSettle();

      await scrollTo(tester, find.text('HR will not see'));
      expect(find.text('HR will see'), findsOneWidget);
      expect(find.text('HR will not see'), findsOneWidget);
      expect(
        find.textContaining('Your name, employee code, department or position'),
        findsOneWidget,
      );
    });

    testWidgets('a draft without a subject is refused with the API wording', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester, initialLocation: '/grievances/new');

      await chooseCategory(tester, 'Working conditions');
      await tester.tap(
        find.byKey(const ValueKey<String>('grievance-save-draft')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Subject is required.'), findsOneWidget);
      await scrollTo(
        tester,
        find.byKey(const ValueKey<String>('grievance-form-error')),
      );
      expect(
        find.text('A draft needs a category and a subject.'),
        findsOneWidget,
      );
    });

    testWidgets(
      'saving a draft keeps it out of HR\'s hands and fixes anonymity',
      (WidgetTester tester) async {
        await pumpGrievanceApp(tester, initialLocation: '/grievances/new');
        final int before = store.cases.length;

        await chooseCategory(tester, 'Working conditions');
        await tester.enterText(
          find.byKey(const ValueKey<String>('grievance-subject-field')),
          'Broken chairs in the canteen',
        );
        await tester.tap(
          find.byKey(const ValueKey<String>('grievance-save-draft')),
        );
        await tester.pumpAndSettle();

        expect(store.cases.length, before + 1);
        expect(store.cases.last.status, 'Draft');
        expect(
          find.text('Draft saved. Nothing has been sent to HR yet.'),
          findsOneWidget,
        );
        await scrollTo(
          tester,
          find.byKey(const ValueKey<String>('grievance-anonymity-fixed')),
        );
        expect(
          find.textContaining('Start a new draft to change how you report.'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'submit checks the minimums before asking for the declaration',
      (WidgetTester tester) async {
        await pumpGrievanceApp(tester, initialLocation: '/grievances/new');

        await chooseCategory(tester, 'Working conditions');
        await tester.enterText(
          find.byKey(const ValueKey<String>('grievance-subject-field')),
          'Broken chairs',
        );
        await tester.enterText(
          find.byKey(const ValueKey<String>('grievance-description-field')),
          'Too short',
        );
        await tester.tap(
          find.byKey(const ValueKey<String>('grievance-submit')),
        );
        await tester.pumpAndSettle();

        expect(
          find.text('The description must be 20 to 4000 characters.'),
          findsOneWidget,
        );
        expect(find.text('Submit this grievance?'), findsNothing);
      },
    );

    testWidgets('submits only once the declaration is ticked', (
      WidgetTester tester,
    ) async {
      await pumpGrievanceApp(tester, initialLocation: '/grievances/new');

      await chooseCategory(tester, 'Working conditions');
      await tester.enterText(
        find.byKey(const ValueKey<String>('grievance-subject-field')),
        'Broken chairs in the canteen',
      );
      await tester.enterText(
        find.byKey(const ValueKey<String>('grievance-description-field')),
        'Three chairs in the canteen have broken legs and one collapsed.',
      );
      await tester.tap(find.byKey(const ValueKey<String>('grievance-submit')));
      await tester.pumpAndSettle();

      expect(find.text('Submit this grievance?'), findsOneWidget);
      final Finder confirm = find.byKey(
        const ValueKey<String>('grievance-declaration-submit'),
      );
      expect(tester.widget<FilledButton>(confirm).onPressed, isNull);

      await tester.tap(
        find.byKey(const ValueKey<String>('grievance-declaration-checkbox')),
      );
      await tester.pumpAndSettle();
      await tester.tap(confirm);
      await tester.pumpAndSettle();

      final MyGrievanceDetailModel created = store.cases.last;
      expect(created.status, 'Submitted');
      expect(created.declarationAcceptedAtUtc, isNotNull);
      // Lands on the new case's detail.
      expect(find.text(created.caseNo!), findsWidgets);
    });
  });
}
