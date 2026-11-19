import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/features/attendance/presentation/screens/attendance_detail_screen.dart';
import 'package:hr_employee_app/features/attendance/presentation/screens/attendance_screen.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_state.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/splash_gate.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/change_password_screen.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/forgot_password_screen.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/login_screen.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/reset_password_screen.dart';
import 'package:hr_employee_app/features/auth/presentation/screens/splash_screen.dart';
import 'package:hr_employee_app/features/dashboard/presentation/screens/dashboard_screen.dart';
import 'package:hr_employee_app/features/grievance/presentation/screens/grievance_detail_screen.dart';
import 'package:hr_employee_app/features/grievance/presentation/screens/grievance_form_screen.dart';
import 'package:hr_employee_app/features/grievance/presentation/screens/my_grievances_screen.dart';
import 'package:hr_employee_app/features/leave/presentation/screens/apply_leave_screen.dart';
import 'package:hr_employee_app/features/leave/presentation/screens/leave_detail_screen.dart';
import 'package:hr_employee_app/features/leave/presentation/screens/leave_requests_screen.dart';
import 'package:hr_employee_app/features/leave/presentation/screens/leave_screen.dart';
import 'package:hr_employee_app/features/notifications/presentation/screens/notification_detail_screen.dart';
import 'package:hr_employee_app/features/notifications/presentation/screens/notifications_screen.dart';
import 'package:hr_employee_app/features/payslip/presentation/screens/payslip_detail_screen.dart';
import 'package:hr_employee_app/features/payslip/presentation/screens/payslip_screen.dart';
import 'package:hr_employee_app/features/profile/presentation/screens/edit_profile_screen.dart';
import 'package:hr_employee_app/features/profile/presentation/screens/profile_screen.dart';
import 'package:hr_employee_app/shared/widgets/main_shell.dart';
import 'package:material_ui/material_ui.dart';

/// Navigator that owns the full-screen routes stacked above the tab shell.
final GlobalKey<NavigatorState> _rootNavigatorKey = GlobalKey<NavigatorState>(
  debugLabel: 'root',
);

/// The application router.
///
/// Access control lives in one `GoRouter.redirect` rather than in each
/// screen. That is the whole point: a screen cannot forget to check, and
/// there is a single place to read to know who can reach what.
///
/// The provider itself does **not** `watch` the auth state. Watching would
/// rebuild the `GoRouter` on every sign-in and throw away the navigation
/// stack. Instead a [ValueNotifier] is fed by `ref.listen` and handed to
/// `refreshListenable`, which re-runs only the redirect.
final Provider<GoRouter> routerProvider = Provider<GoRouter>((Ref ref) {
  final _AuthRefreshNotifier refreshNotifier = _AuthRefreshNotifier();
  ref.onDispose(refreshNotifier.dispose);

  ref.listen<AsyncValue<AuthState>>(authControllerProvider, (
    AsyncValue<AuthState>? previous,
    AsyncValue<AuthState> next,
  ) {
    refreshNotifier.update(next.value);
  }, fireImmediately: true);

  ref.listen<bool>(splashGateProvider, (bool? previous, bool next) {
    refreshNotifier.updateSplash(isFinished: next);
  }, fireImmediately: true);

  return GoRouter(
    navigatorKey: _rootNavigatorKey,
    initialLocation: RoutePaths.splash,
    refreshListenable: refreshNotifier,
    debugLogDiagnostics: false,
    redirect: (BuildContext context, GoRouterState state) => resolveRedirect(
      location: state.matchedLocation,
      authState: refreshNotifier.authState,
      isSplashFinished: refreshNotifier.isSplashFinished,
    ),
    routes: <RouteBase>[
      GoRoute(
        path: RoutePaths.splash,
        name: RouteNames.splash,
        builder: (BuildContext context, GoRouterState state) =>
            const SplashScreen(),
      ),
      GoRoute(
        path: RoutePaths.login,
        name: RouteNames.login,
        builder: (BuildContext context, GoRouterState state) =>
            const LoginScreen(),
      ),
      GoRoute(
        path: RoutePaths.forgotPassword,
        name: RouteNames.forgotPassword,
        builder: (BuildContext context, GoRouterState state) =>
            const ForgotPasswordScreen(),
      ),
      GoRoute(
        path: RoutePaths.resetPassword,
        name: RouteNames.resetPassword,
        builder: (BuildContext context, GoRouterState state) {
          // Arrives from the emailed deep link.
          return ResetPasswordScreen(
            email: state.uri.queryParameters[RouteParams.email] ?? '',
            token: state.uri.queryParameters[RouteParams.token] ?? '',
          );
        },
      ),

      // Notifications sit above the shell so the inbox is full-screen, the
      // way an inbox opened from a bell icon is expected to behave.
      GoRoute(
        path: RoutePaths.notifications,
        name: RouteNames.notifications,
        parentNavigatorKey: _rootNavigatorKey,
        builder: (BuildContext context, GoRouterState state) =>
            const NotificationsScreen(),
        routes: <RouteBase>[
          GoRoute(
            path: RoutePaths.notificationDetail,
            name: RouteNames.notificationDetail,
            parentNavigatorKey: _rootNavigatorKey,
            builder: (BuildContext context, GoRouterState state) =>
                NotificationDetailScreen(
                  notificationId:
                      state.pathParameters[RouteParams.notificationId]!,
                ),
          ),
        ],
      ),

      _mainShellRoute,
    ],
  );
});

/// The five-tab shell.
///
/// `StatefulShellRoute.indexedStack` keeps one navigator per branch, so each
/// tab remembers its own scroll position and pushed pages — switching tabs
/// does not reset the others.
///
/// Detail and form screens are declared as children of their branch but carry
/// `parentNavigatorKey: _rootNavigatorKey`, which renders them above the
/// shell. That keeps the URL hierarchical (`/leave/request/lr-1`) while
/// hiding the navigation bar on a focused screen.
final StatefulShellRoute _mainShellRoute = StatefulShellRoute.indexedStack(
  builder: (
    BuildContext context,
    GoRouterState state,
    StatefulNavigationShell shell,
  ) => MainShell(shell: shell),
  branches: <StatefulShellBranch>[
    StatefulShellBranch(
      routes: <RouteBase>[
        GoRoute(
          path: RoutePaths.dashboard,
          name: RouteNames.dashboard,
          builder: (BuildContext context, GoRouterState state) =>
              const DashboardScreen(),
        ),
      ],
    ),
    StatefulShellBranch(
      routes: <RouteBase>[
        GoRoute(
          path: RoutePaths.attendance,
          name: RouteNames.attendance,
          builder: (BuildContext context, GoRouterState state) =>
              const AttendanceScreen(),
          routes: <RouteBase>[
            GoRoute(
              path: RoutePaths.attendanceDetail,
              name: RouteNames.attendanceDetail,
              parentNavigatorKey: _rootNavigatorKey,
              builder: (BuildContext context, GoRouterState state) =>
                  AttendanceDetailScreen(
                    attendanceId:
                        state.pathParameters[RouteParams.attendanceId]!,
                  ),
            ),
          ],
        ),
      ],
    ),
    StatefulShellBranch(
      routes: <RouteBase>[
        GoRoute(
          path: RoutePaths.leave,
          name: RouteNames.leave,
          builder: (BuildContext context, GoRouterState state) =>
              const LeaveScreen(),
          routes: <RouteBase>[
            GoRoute(
              path: RoutePaths.applyLeave,
              name: RouteNames.applyLeave,
              parentNavigatorKey: _rootNavigatorKey,
              builder: (BuildContext context, GoRouterState state) =>
                  ApplyLeaveScreen(
                    initialLeaveTypeId:
                        state.uri.queryParameters[RouteParams.leaveTypeId],
                  ),
            ),
            GoRoute(
              path: RoutePaths.leaveRequests,
              name: RouteNames.leaveRequests,
              builder: (BuildContext context, GoRouterState state) =>
                  const LeaveRequestsScreen(),
            ),
            GoRoute(
              path: RoutePaths.leaveDetail,
              name: RouteNames.leaveDetail,
              parentNavigatorKey: _rootNavigatorKey,
              builder: (BuildContext context, GoRouterState state) =>
                  LeaveDetailScreen(
                    leaveRequestId:
                        state.pathParameters[RouteParams.leaveRequestId]!,
                  ),
            ),
          ],
        ),
      ],
    ),
    StatefulShellBranch(
      routes: <RouteBase>[
        GoRoute(
          path: RoutePaths.payslip,
          name: RouteNames.payslip,
          builder: (BuildContext context, GoRouterState state) =>
              const PayslipScreen(),
          routes: <RouteBase>[
            GoRoute(
              path: RoutePaths.payslipDetail,
              name: RouteNames.payslipDetail,
              parentNavigatorKey: _rootNavigatorKey,
              builder: (BuildContext context, GoRouterState state) =>
                  PayslipDetailScreen(
                    payslipId: state.pathParameters[RouteParams.payslipId]!,
                  ),
            ),
          ],
        ),
      ],
    ),
    StatefulShellBranch(
      routes: <RouteBase>[
        GoRoute(
          path: RoutePaths.profile,
          name: RouteNames.profile,
          builder: (BuildContext context, GoRouterState state) =>
              const ProfileScreen(),
          routes: <RouteBase>[
            GoRoute(
              path: RoutePaths.editProfile,
              name: RouteNames.editProfile,
              parentNavigatorKey: _rootNavigatorKey,
              builder: (BuildContext context, GoRouterState state) =>
                  const EditProfileScreen(),
            ),
            GoRoute(
              path: RoutePaths.changePassword,
              name: RouteNames.changePassword,
              parentNavigatorKey: _rootNavigatorKey,
              builder: (BuildContext context, GoRouterState state) {
                // `forced` is set by the redirect when the server has
                // flagged the account; it hides the back button.
                final bool isForced =
                    state.uri.queryParameters['forced'] == 'true';
                return ChangePasswordScreen(isForced: isForced);
              },
            ),
            GoRoute(
              path: RoutePaths.grievances,
              name: RouteNames.grievances,
              builder: (BuildContext context, GoRouterState state) =>
                  const MyGrievancesScreen(),
              routes: <RouteBase>[
                // Before the id route, so `new` is never read as an id.
                GoRoute(
                  path: RoutePaths.newGrievance,
                  name: RouteNames.newGrievance,
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (BuildContext context, GoRouterState state) =>
                      const GrievanceFormScreen(),
                ),
                GoRoute(
                  path: RoutePaths.grievanceDetail,
                  name: RouteNames.grievanceDetail,
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (BuildContext context, GoRouterState state) =>
                      GrievanceDetailScreen(
                        grievanceId: int.tryParse(
                          state.pathParameters[RouteParams.grievanceId] ?? '',
                        ) ?? 0,
                      ),
                  routes: <RouteBase>[
                    GoRoute(
                      path: RoutePaths.editGrievance,
                      name: RouteNames.editGrievance,
                      parentNavigatorKey: _rootNavigatorKey,
                      builder: (BuildContext context, GoRouterState state) =>
                          GrievanceFormScreen(
                            grievanceId: int.tryParse(
                              state.pathParameters[RouteParams.grievanceId] ??
                                  '',
                            ),
                          ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ],
    ),
  ],
);

/// Paths reachable without a session.
const Set<String> _publicPaths = <String>{
  RoutePaths.login,
  RoutePaths.forgotPassword,
  RoutePaths.resetPassword,
};

/// The forced change-password destination, including the flag that hides the
/// back button.
const String _forcedPasswordChangePath =
    '${RoutePaths.profile}/${RoutePaths.changePassword}?forced=true';

/// Decides where the user is allowed to be.
///
/// Returning null means "stay here". Returning the current location would
/// loop, so every branch checks before redirecting.
///
/// Takes a plain location string rather than a `GoRouterState` so it is a
/// pure function of (where you are, who you are). That is what makes the
/// app's entire access-control policy unit-testable without a widget tree —
/// see `test/app_router_test.dart`.
String? resolveRedirect({
  required String location,
  required AuthState? authState,
  bool isSplashFinished = true,
}) {
  // The logo reveal is still playing. Stay on the splash until it has
  // finished, even if the session is already known; anywhere else, carry on
  // as normal.
  if (location == RoutePaths.splash && !isSplashFinished) {
    return null;
  }

  // The session has not been resolved yet. Hold on the splash screen; the
  // refresh listener re-runs this the moment it is known.
  if (authState == null || authState.isUnknown) {
    return location == RoutePaths.splash ? null : RoutePaths.splash;
  }

  final bool isPublic = _publicPaths.contains(location);

  if (!authState.isAuthenticated) {
    // Signed out: only the public screens are reachable.
    return isPublic ? null : RoutePaths.login;
  }

  // Signed in, but on a temporary password: nothing else is reachable until
  // it has been changed.
  if (authState.requiresPasswordChange) {
    final bool isOnChangePassword = location.endsWith(
      RoutePaths.changePassword,
    );
    return isOnChangePassword ? null : _forcedPasswordChangePath;
  }

  // Signed in on the splash or a public screen: move on to the app.
  if (isPublic || location == RoutePaths.splash) {
    return RoutePaths.dashboard;
  }

  return null;
}

/// Notifies [GoRouter] when the auth state changes, without rebuilding it.
///
/// Only genuine transitions notify. Without that guard, every unrelated
/// rebuild of the auth provider would trigger a redirect pass.
class _AuthRefreshNotifier extends ChangeNotifier {
  AuthState? _authState;

  AuthState? get authState => _authState;

  bool _isSplashFinished = false;

  bool get isSplashFinished => _isSplashFinished;

  void updateSplash({required bool isFinished}) {
    if (_isSplashFinished == isFinished) {
      return;
    }
    _isSplashFinished = isFinished;
    notifyListeners();
  }

  void update(AuthState? next) {
    if (_authState == next) {
      return;
    }
    _authState = next;
    notifyListeners();
  }
}
