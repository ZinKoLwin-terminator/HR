@Tags(<String>['screenshots'])
library;

import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_screenutil/flutter_screenutil.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/app.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/router/app_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/services/location_service.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/core/storage/secure_storage_service.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/punch_attendance_usecase.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_providers.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/login_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/splash_screen.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_controllers.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_providers.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// Renders every screen to a PNG.
///
/// Run with:
/// ```
/// flutter test test/screenshots --update-goldens
/// ```
///
/// This is tooling, not an assertion suite — it is tagged `screenshots` and
/// excluded from the default run by `dart_test.yaml`, because a UI tweak
/// should not fail the build.
///
/// It drives the **real** app: the actual `HrEmployeeApp`, the actual router,
/// and the mock data sources behind `useMockData: true`. Only the three
/// things a headless test cannot provide are substituted — platform storage,
/// device info and the GPS — so what you see is the app as it renders, not a
/// mock-up of it. The "Sample data" banner is visible in every shot because
/// that is genuinely what the app shows in this mode.
void main() {
  /// Output directory, relative to the project root.
  const String outputDirectory = 'screenshots';

  /// A phone-sized surface at 3x, giving 1170x2532 images.
  const Size logicalSize = Size(390, 844);
  const double pixelRatio = 3;

  setUpAll(_loadRealFonts);

  testWidgets('capture the splash screen', (WidgetTester tester) async {
    tester.view
      ..physicalSize = Size(
        logicalSize.width * pixelRatio,
        logicalSize.height * pixelRatio,
      )
      ..devicePixelRatio = pixelRatio;
    addTearDown(tester.view.reset);

    // Captured on its own rather than from the running app: the splash is
    // visible only while the stored session is being read, and with
    // in-memory storage that finishes within the first frame. Pumping the
    // real app would always land on the screen *after* it.
    await tester.pumpWidget(
      ScreenUtilInit(
        designSize: logicalSize,
        builder: (BuildContext context, Widget? child) => const MaterialApp(
          debugShowCheckedModeBanner: false,
          home: SplashScreen(),
        ),
      ),
    );
    await tester.pump(const Duration(milliseconds: 300));

    await _capture(tester, outputDirectory, '01-splash');
  });

  testWidgets('capture every screen', (WidgetTester tester) async {
    tester.view
      ..physicalSize = Size(
        logicalSize.width * pixelRatio,
        logicalSize.height * pixelRatio,
      )
      ..devicePixelRatio = pixelRatio;
    addTearDown(tester.view.reset);

    final ProviderContainer container = ProviderContainer(
      // Element type inferred: Riverpod 3 does not export `Override`.
      overrides: [
        appConfigProvider.overrideWithValue(_screenshotConfig),
        preferencesServiceProvider.overrideWithValue(
          InMemoryPreferencesService(),
        ),
        secureStorageServiceProvider.overrideWithValue(
          InMemorySecureStorageService(),
        ),
        deviceInfoProvider.overrideWithValue(
          const DeviceInfo(
            deviceId: 'screenshot-device',
            deviceName: 'Pixel 8',
            appVersion: '1.0.0+1',
            platform: 'android',
          ),
        ),
        locationServiceProvider.overrideWithValue(
          const _FixedLocationService(),
        ),
      ],
    );
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: const HrEmployeeApp(),
      ),
    );

    await _settle(tester);
    await _capture(tester, outputDirectory, '02-login');

    final GoRouter router = container.read(routerProvider);

    // --- Signed-out screens -------------------------------------------
    await _goto(tester, router, RoutePaths.forgotPassword);
    await _capture(tester, outputDirectory, '03-forgot-password');

    await _goto(
      tester,
      router,
      '${RoutePaths.resetPassword}'
      '?email=aye.chan@example.com&token=sample-token',
    );
    await _capture(tester, outputDirectory, '04-reset-password');

    await _goto(tester, router, RoutePaths.login);

    // --- Sign in ------------------------------------------------------
    // Started without awaiting: the mock data sources use `Future.delayed`,
    // and in a widget test those timers only fire while the tester pumps.
    // Awaiting here would deadlock the fake clock.
    final Future<bool> signIn = container
        .read(loginControllerProvider.notifier)
        .submit(
          userName: MockSupport.employeeId,
          password: MockSupport.acceptedPassword,
          rememberMe: true,
        );
    await _settle(tester);
    expect(await signIn, isTrue, reason: 'the fixture sign-in should succeed');

    await _settle(tester);
    await _capture(tester, outputDirectory, '05-dashboard');

    // Record a check-in so the attendance screens show a populated day
    // rather than an empty one.
    final Future<bool> punch = container
        .read(punchControllerProvider.notifier)
        .punch(PunchDirection.checkIn);
    await _settle(tester);
    expect(await punch, isTrue, reason: 'the fixture check-in should succeed');

    await _settle(tester);
    await _capture(tester, outputDirectory, '06-dashboard-checked-in');

    // --- Attendance ---------------------------------------------------
    await _goto(tester, router, RoutePaths.attendance);
    await _capture(tester, outputDirectory, '07-attendance');

    final String? attendanceId = container
        .read(attendanceHistoryControllerProvider)
        .value
        ?.items
        .firstOrNull
        ?.id;
    if (attendanceId != null) {
      await _goto(tester, router, '${RoutePaths.attendance}/$attendanceId');
      await _capture(tester, outputDirectory, '08-attendance-detail');
    }

    // --- Leave --------------------------------------------------------
    await _goto(tester, router, RoutePaths.leave);
    await _capture(tester, outputDirectory, '09-leave-balances');

    await tester.tap(find.text('Requests'));
    await _settle(tester);
    await _capture(tester, outputDirectory, '10-leave-requests');

    await _goto(tester, router, '${RoutePaths.leave}/apply');
    await _capture(tester, outputDirectory, '11-apply-leave');

    await _goto(tester, router, '${RoutePaths.leave}/request/mock-lr-1');
    await _capture(tester, outputDirectory, '12-leave-detail-pending');

    await _goto(tester, router, '${RoutePaths.leave}/request/mock-lr-2');
    await _capture(tester, outputDirectory, '13-leave-detail-approved');

    // --- Payslip ------------------------------------------------------
    await _goto(tester, router, RoutePaths.payslip);
    await _capture(tester, outputDirectory, '14-payslip-masked');

    // Reveal the amounts, which is the deliberate second state of the
    // screen rather than a different screen.
    container.read(salaryMaskedProvider.notifier).toggle();
    await _settle(tester);
    await _capture(tester, outputDirectory, '15-payslip-revealed');

    final PaginatedState<PayslipSummary>? payslips = container
        .read(payslipsControllerProvider)
        .value;
    final String? payslipId = payslips?.items.firstOrNull?.id;
    if (payslipId != null) {
      await _goto(tester, router, '${RoutePaths.payslip}/$payslipId');
      await _capture(tester, outputDirectory, '16-payslip-detail');
    }

    // --- Profile ------------------------------------------------------
    await _goto(tester, router, RoutePaths.profile);
    await _capture(tester, outputDirectory, '17-profile');

    await _goto(tester, router, '${RoutePaths.profile}/edit');
    await _capture(tester, outputDirectory, '18-edit-profile');

    await _goto(tester, router, '${RoutePaths.profile}/change-password');
    await _capture(tester, outputDirectory, '19-change-password');

    // --- Notifications ------------------------------------------------
    await _goto(tester, router, RoutePaths.notifications);
    await _capture(tester, outputDirectory, '20-notifications');

    await _goto(tester, router, '${RoutePaths.notifications}/mock-n-1');
    await _capture(tester, outputDirectory, '21-notification-detail');
  });
}

/// Mock mode on, so the screens have data without a backend.
const AppConfig _screenshotConfig = AppConfig(
  environment: AppEnvironment.development,
  apiBaseUrl: 'https://screenshots.example.com/api/v1',
  useMockData: true,
  enableVerboseLogging: false,
  connectTimeout: Duration(seconds: 5),
  receiveTimeout: Duration(seconds: 5),
  sendTimeout: Duration(seconds: 5),
  defaultPageSize: 20,
);

/// Navigates and waits for the screen to settle.
Future<void> _goto(
  WidgetTester tester,
  GoRouter router,
  String location,
) async {
  router.go(location);
  await _settle(tester);
}

/// Pumps until the screen has finished loading.
///
/// `pumpAndSettle` alone is not enough here. The mock data sources simulate
/// latency with `Future.delayed`, and those timers only fire when the tester
/// advances its fake clock — which `pumpAndSettle` does not do while nothing
/// is animating. Pumping an explicit duration first lets the pending request
/// complete, after which settling finishes any resulting animation.
Future<void> _settle(WidgetTester tester) async {
  await tester.pump();
  await tester.pump(const Duration(seconds: 2));
  await tester.pumpAndSettle();
}

/// Writes the current frame to `<outputDirectory>/<name>.png`.
Future<void> _capture(
  WidgetTester tester,
  String outputDirectory,
  String name,
) async {
  await expectLater(
    find.byType(MaterialApp),
    matchesGoldenFile('../../$outputDirectory/$name.png'),
  );
}

/// Registers the real Roboto and MaterialIcons faces.
///
/// Without this the test environment draws every glyph as a filled rectangle,
/// which would make the screenshots useless. The files ship with the Flutter
/// SDK, located through `FLUTTER_ROOT` so the path is not machine-specific.
Future<void> _loadRealFonts() async {
  final String? flutterRoot = Platform.environment['FLUTTER_ROOT'];
  if (flutterRoot == null) {
    fail(
      'FLUTTER_ROOT is not set, so the real fonts cannot be located. '
      'Run this through `flutter test` rather than `dart test`.',
    );
  }

  final Directory fontDirectory = Directory(
    '$flutterRoot/bin/cache/artifacts/material_fonts',
  );
  if (!fontDirectory.existsSync()) {
    fail(
      'Font artifacts are missing at ${fontDirectory.path}. '
      'Run `flutter precache` first.',
    );
  }

  Future<ByteData> load(String fileName) async {
    final File file = File('${fontDirectory.path}/$fileName');
    final Uint8List bytes = await file.readAsBytes();
    return ByteData.view(bytes.buffer);
  }

  // Registered under both the real family name and the empty family, so text
  // that does not name a font (the whole app, which uses the platform
  // default) still resolves to Roboto rather than the box-drawing fallback.
  for (final String family in <String>['Roboto', '']) {
    final FontLoader loader = FontLoader(family)
      ..addFont(load('roboto-regular.ttf'))
      ..addFont(load('roboto-medium.ttf'))
      ..addFont(load('roboto-bold.ttf'));
    await loader.load();
  }

  final FontLoader icons = FontLoader('MaterialIcons')
    ..addFont(load('materialicons-regular.otf'));
  await icons.load();
}

/// A location service that always succeeds, so the attendance screens show a
/// recorded position instead of a permission error.
///
/// Substituted only because a headless test has no GPS; the app's real
/// behaviour around permissions is covered by
/// `test/features/attendance/domain/punch_attendance_usecase_test.dart`.
class _FixedLocationService implements LocationService {
  const _FixedLocationService();

  @override
  Future<LocationAuthorizationState> checkPermission() async =>
      LocationAuthorizationState.granted;

  @override
  Future<LocationAuthorizationState> requestPermission() async =>
      LocationAuthorizationState.granted;

  @override
  Future<bool> isServiceEnabled() async => true;

  @override
  Future<Result<GeoLocation>> getCurrentLocation({Duration? timeout}) async {
    return const Result<GeoLocation>.success(
      GeoLocation(
        latitude: 16.8409,
        longitude: 96.1735,
        accuracyMeters: 12.4,
        address: 'Yangon HQ, Kyauktada',
      ),
    );
  }

  @override
  Future<void> openSettings() async {}
}
