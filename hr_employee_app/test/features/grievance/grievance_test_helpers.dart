import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_screenutil/flutter_screenutil.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_mock_store.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/domain/repositories/grievance_repository.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_providers.dart';
import 'package:hr_employee_app/features/grievance/presentation/screens/grievance_detail_screen.dart';
import 'package:hr_employee_app/features/grievance/presentation/screens/grievance_form_screen.dart';
import 'package:hr_employee_app/features/grievance/presentation/screens/my_grievances_screen.dart';
import 'package:material_ui/material_ui.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_doubles.dart';

/// Test doubles and fixtures for the grievance feature, kept beside its
/// tests so the shared `test_doubles.dart` stays untouched.

class MockGrievanceRepository extends Mock implements GrievanceRepository {}

/// The seeded store, restored, and a data source over it without latency.
GrievanceMockDataSource freshMockDataSource({DateTime Function()? clock}) {
  GrievanceMockStore.instance.reset();
  return GrievanceMockDataSource(simulateLatency: false, clock: clock);
}

void registerGrievanceFallbacks() {
  registerFallbackValue(
    const GrievanceDraftInput(categoryId: 1, subject: 'fallback'),
  );
}

abstract final class GrievanceTestData {
  static const GrievanceCategory harassment = GrievanceCategory(
    id: 1,
    name: 'Harassment or bullying',
    description: 'Unwelcome behaviour.',
    allowsAnonymous: true,
  );

  static const GrievanceCategory pay = GrievanceCategory(
    id: 4,
    name: 'Pay and benefits',
    description: 'Salary and allowances.',
  );

  static GrievanceDraftInput input({
    int categoryId = 1,
    String subject = 'Repeated shouting in meetings',
    String description =
        'A team lead shouts at junior staff in the weekly meeting.',
    DateTime? incidentDate,
    bool isAnonymous = false,
  }) {
    return GrievanceDraftInput(
      categoryId: categoryId,
      subject: subject,
      description: description,
      incidentDate: incidentDate,
      isAnonymous: isAnonymous,
    );
  }

  static GrievanceDetail detail({
    int id = 7,
    GrievanceStatus status = GrievanceStatus.draft,
    String subject = 'Repeated shouting in meetings',
    String description =
        'A team lead shouts at junior staff in the weekly meeting.',
    bool isAnonymous = false,
    String rowVersion = 'AAAAAAAAB9E=',
    List<GrievanceMessage> messages = const <GrievanceMessage>[],
  }) {
    return GrievanceDetail(
      id: id,
      categoryId: 1,
      categoryName: 'Harassment or bullying',
      subject: subject,
      description: description,
      status: status,
      isAnonymous: isAnonymous,
      createdAt: DateTime.utc(2026, 6),
      rowVersion: rowVersion,
      caseNo: status == GrievanceStatus.draft ? null : 'GRV-2026-00001',
      messages: messages,
    );
  }
}

/// Test configuration with mock mode on, for the "Sample data" banner.
const AppConfig mockModeConfig = AppConfig(
  environment: AppEnvironment.development,
  apiBaseUrl: 'https://test.example.com/api/v1',
  useMockData: true,
  enableVerboseLogging: false,
  connectTimeout: Duration(seconds: 5),
  receiveTimeout: Duration(seconds: 5),
  sendTimeout: Duration(seconds: 5),
  defaultPageSize: 20,
);

/// Pumps the grievance screens behind a small router that uses the real
/// route names, so navigation between them works as in the app.
///
/// The data source is the fixture one without latency, over a freshly
/// reset store: widget tests exercise the same rules the app runs in mock
/// mode.
Future<ProviderContainer> pumpGrievanceApp(
  WidgetTester tester, {
  String initialLocation = '/grievances',
  AppConfig config = testConfig,
}) async {
  tester.view
    ..physicalSize = const Size(390 * 3, 844 * 3)
    ..devicePixelRatio = 3.0;
  addTearDown(tester.view.reset);

  final ProviderContainer container = ProviderContainer(
    overrides: [
      appConfigProvider.overrideWithValue(config),
      grievanceRemoteDataSourceProvider.overrideWithValue(
        freshMockDataSource(),
      ),
    ],
  );
  addTearDown(container.dispose);

  final GoRouter router = GoRouter(
    initialLocation: initialLocation,
    routes: <RouteBase>[
      GoRoute(
        path: '/grievances',
        name: RouteNames.grievances,
        builder: (BuildContext context, GoRouterState state) =>
            const MyGrievancesScreen(),
        routes: <RouteBase>[
          GoRoute(
            path: RoutePaths.newGrievance,
            name: RouteNames.newGrievance,
            builder: (BuildContext context, GoRouterState state) =>
                const GrievanceFormScreen(),
          ),
          GoRoute(
            path: RoutePaths.grievanceDetail,
            name: RouteNames.grievanceDetail,
            builder: (BuildContext context, GoRouterState state) =>
                GrievanceDetailScreen(
                  grievanceId: int.parse(
                    state.pathParameters[RouteParams.grievanceId]!,
                  ),
                ),
            routes: <RouteBase>[
              GoRoute(
                path: RoutePaths.editGrievance,
                name: RouteNames.editGrievance,
                builder: (BuildContext context, GoRouterState state) =>
                    GrievanceFormScreen(
                      grievanceId: int.parse(
                        state.pathParameters[RouteParams.grievanceId]!,
                      ),
                    ),
              ),
            ],
          ),
        ],
      ),
    ],
  );
  addTearDown(router.dispose);

  await tester.pumpWidget(
    UncontrolledProviderScope(
      container: container,
      child: ScreenUtilInit(
        designSize: AppSizes.designSize,
        builder: (BuildContext context, Widget? child) =>
            MaterialApp.router(theme: AppTheme.light, routerConfig: router),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return container;
}
