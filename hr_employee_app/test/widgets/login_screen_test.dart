import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_screenutil/flutter_screenutil.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_providers.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_state.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/login_screen.dart';
import 'package:material_ui/material_ui.dart';
import 'package:mocktail/mocktail.dart';

import '../helpers/test_doubles.dart';

void main() {
  late MockAuthRepository repository;

  setUpAll(registerCommonFallbacks);

  setUp(() {
    repository = MockAuthRepository();
    when(
      repository.restoreSession,
    ).thenAnswer((_) async => const Result<AuthenticatedUser?>.success(null));
  });

  /// Pumps the login screen inside the same scaffolding `app.dart` provides:
  /// a provider scope, the real theme and the screen-util scale.
  ///
  /// The surface is set to the design size first. The default 800x600 test
  /// window would make `flutter_screenutil` scale every dimension by ~2x,
  /// which is not a configuration any phone has and produces overflow
  /// failures that say nothing about the screen.
  Future<ProviderContainer> pumpLoginScreen(WidgetTester tester) async {
    tester.view
      ..physicalSize = const Size(390 * 3, 844 * 3)
      ..devicePixelRatio = 3.0;
    addTearDown(tester.view.reset);

    final ProviderContainer container = ProviderContainer(
      overrides: [
        appConfigProvider.overrideWithValue(testConfig),
        preferencesServiceProvider.overrideWithValue(
          InMemoryPreferencesService(),
        ),
        deviceInfoProvider.overrideWithValue(
          const DeviceInfo(
            deviceId: 'device-1',
            deviceName: 'Test Device',
            appVersion: '1.0.0+1',
            platform: 'android',
          ),
        ),
        authRepositoryProvider.overrideWithValue(repository),
      ],
    );
    addTearDown(container.dispose);

    // Resolve the session before the first frame, so the screen does not
    // rebuild mid-test.
    await container.read(authControllerProvider.future);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: ScreenUtilInit(
          designSize: AppSizes.designSize,
          builder: (BuildContext context, Widget? child) =>
              MaterialApp(theme: AppTheme.light, home: const LoginScreen()),
        ),
      ),
    );
    await tester.pumpAndSettle();
    return container;
  }

  group('layout', () {
    testWidgets('renders the sign-in form', (WidgetTester tester) async {
      await pumpLoginScreen(tester);

      expect(find.text('Welcome back'), findsOneWidget);
      expect(find.text('Sign in'), findsOneWidget);
      expect(find.text('Forgot password?'), findsOneWidget);
      expect(find.text('Keep me signed in'), findsOneWidget);

      // The required-field labels are built with `Text.rich` so the asterisk
      // can be coloured, so they are matched through rich text rather than
      // with a plain `find.text`.
      expect(
        find.textContaining('Employee ID or email', findRichText: true),
        findsOneWidget,
      );
      expect(find.textContaining('Password', findRichText: true), findsWidgets);

      // One identifier field and one password field.
      expect(find.byType(TextFormField), findsNWidgets(2));
    });

    testWidgets('hides the password by default', (WidgetTester tester) async {
      await pumpLoginScreen(tester);

      final EditableText field = tester.widget<EditableText>(
        find.byType(EditableText).last,
      );
      expect(field.obscureText, isTrue);
    });

    testWidgets('reveals the password when the toggle is tapped', (
      WidgetTester tester,
    ) async {
      await pumpLoginScreen(tester);

      await tester.tap(find.byTooltip('Show password'));
      await tester.pumpAndSettle();

      final EditableText field = tester.widget<EditableText>(
        find.byType(EditableText).last,
      );
      expect(field.obscureText, isFalse);
    });

    testWidgets('does not show the mock hint when mock data is off', (
      WidgetTester tester,
    ) async {
      await pumpLoginScreen(tester);

      expect(find.text('Demo workspace'), findsNothing);
      expect(find.text('Use demo'), findsNothing);
      expect(find.textContaining('Sample data'), findsNothing);
    });
  });

  group('validation', () {
    testWidgets('blocks submission with empty fields', (
      WidgetTester tester,
    ) async {
      await pumpLoginScreen(tester);

      await tester.tap(find.text('Sign in'));
      await tester.pumpAndSettle();

      expect(find.textContaining('is required'), findsWidgets);
      // Nothing may reach the repository from an invalid form.
      verifyNever(() => repository.login(any()));
    });

    testWidgets('rejects a malformed email', (WidgetTester tester) async {
      await pumpLoginScreen(tester);

      await tester.enterText(find.byType(TextFormField).first, 'aye@');
      await tester.enterText(find.byType(TextFormField).last, 'Password1');
      await tester.tap(find.text('Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address.'), findsOneWidget);
      verifyNever(() => repository.login(any()));
    });
  });

  group('submission', () {
    testWidgets('sends valid credentials to the repository', (
      WidgetTester tester,
    ) async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.success(TestData.user),
      );

      final ProviderContainer container = await pumpLoginScreen(tester);

      await tester.enterText(find.byType(TextFormField).first, 'EMP-00421');
      await tester.enterText(find.byType(TextFormField).last, 'Password1');
      await tester.tap(find.text('Sign in'));
      await tester.pumpAndSettle();

      verify(() => repository.login(any())).called(1);
      expect(
        container.read(authControllerProvider).value,
        isA<AuthAuthenticated>(),
      );
    });

    testWidgets('shows the server message when sign-in fails', (
      WidgetTester tester,
    ) async {
      when(() => repository.login(any())).thenAnswer(
        (_) async => const Result<AuthenticatedUser>.failure(
          AuthenticationFailure('Incorrect employee ID or password.'),
        ),
      );

      await pumpLoginScreen(tester);

      await tester.enterText(find.byType(TextFormField).first, 'EMP-00421');
      await tester.enterText(find.byType(TextFormField).last, 'wrong');
      await tester.tap(find.text('Sign in'));
      await tester.pumpAndSettle();

      // Shown in the inline panel as well as the snack bar, so the message
      // survives the snack bar timing out while the user retypes.
      expect(find.text('Incorrect employee ID or password.'), findsWidgets);
    });

    testWidgets('disables the button while the request is in flight', (
      WidgetTester tester,
    ) async {
      final Completer<Result<AuthenticatedUser>> gate =
          Completer<Result<AuthenticatedUser>>();
      when(() => repository.login(any())).thenAnswer((_) => gate.future);

      await pumpLoginScreen(tester);

      await tester.enterText(find.byType(TextFormField).first, 'EMP-00421');
      await tester.enterText(find.byType(TextFormField).last, 'Password1');
      await tester.tap(find.text('Sign in'));
      await tester.pump();

      // The label is replaced by a spinner, which is also what detaches the
      // callback and prevents a double submission.
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Sign in'), findsNothing);

      gate.complete(const Result<AuthenticatedUser>.success(TestData.user));
      await tester.pumpAndSettle();
    });
  });
}
