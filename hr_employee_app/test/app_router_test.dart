import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/app_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_providers.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_state.dart';
import 'package:mocktail/mocktail.dart';

import 'helpers/test_doubles.dart';

/// Exercises the router.
///
/// This file also serves as the project's compile check: `app_router.dart`
/// imports every screen in the app, so a type error anywhere in the
/// presentation layer fails here. `flutter analyze` alone would not catch a
/// problem inside a generated `.freezed.dart`, because those are excluded
/// from analysis but still compiled.
void main() {
  late MockAuthRepository repository;

  setUpAll(registerCommonFallbacks);

  setUp(() {
    repository = MockAuthRepository();
  });

  ProviderContainer containerWith({AuthenticatedUser? user}) {
    when(repository.restoreSession)
        .thenAnswer((_) async => Result<AuthenticatedUser?>.success(user));

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
    return container;
  }

  group('construction', () {
    test('builds without a duplicate route name or path', () {
      // GoRouter asserts on duplicate names and malformed paths when the
      // route tree is built, so simply constructing it is a real check.
      final ProviderContainer container = containerWith();
      final GoRouter router = container.read(routerProvider);

      expect(router, isNotNull);
      expect(router.configuration.routes, isNotEmpty);
    });

    test('every named route resolves to a path', () {
      final ProviderContainer container = containerWith();
      final GoRouter router = container.read(routerProvider);

      // Routes with no path parameters.
      for (final String name in <String>[
        RouteNames.splash,
        RouteNames.login,
        RouteNames.forgotPassword,
        RouteNames.resetPassword,
        RouteNames.dashboard,
        RouteNames.attendance,
        RouteNames.leave,
        RouteNames.applyLeave,
        RouteNames.payslip,
        RouteNames.profile,
        RouteNames.editProfile,
        RouteNames.changePassword,
        RouteNames.notifications,
      ]) {
        expect(
          () => router.namedLocation(name),
          returnsNormally,
          reason: '$name should resolve',
        );
      }
    });

    test('parameterised routes build the expected location', () {
      final ProviderContainer container = containerWith();
      final GoRouter router = container.read(routerProvider);

      expect(
        router.namedLocation(
          RouteNames.leaveDetail,
          pathParameters: <String, String>{
            RouteParams.leaveRequestId: 'lr-5501',
          },
        ),
        '/leave/request/lr-5501',
      );
      expect(
        router.namedLocation(
          RouteNames.attendanceDetail,
          pathParameters: <String, String>{
            RouteParams.attendanceId: 'att-9001',
          },
        ),
        '/attendance/att-9001',
      );
      expect(
        router.namedLocation(
          RouteNames.payslipDetail,
          pathParameters: <String, String>{RouteParams.payslipId: 'ps-2026-03'},
        ),
        '/payslip/ps-2026-03',
      );
      expect(
        router.namedLocation(
          RouteNames.notificationDetail,
          pathParameters: <String, String>{RouteParams.notificationId: 'n-1'},
        ),
        '/notifications/n-1',
      );
    });
  });

  /// The access-control policy, tested as the pure function it is.
  ///
  /// Every protected screen in the app depends on exactly these rules, so
  /// they are worth covering case by case rather than through one happy-path
  /// navigation test.
  group('resolveRedirect', () {
    const AuthState unknown = AuthState.unknown();
    const AuthState signedOut = AuthState.unauthenticated();
    const AuthState signedIn = AuthState.authenticated(TestData.user);
    final AuthState mustChangePassword = AuthState.authenticated(
      TestData.user.copyWith(mustChangePassword: true),
    );

    group('session not yet resolved', () {
      test('holds on the splash screen', () {
        // The user must not be flashed the login screen only to be moved
        // again a moment later.
        expect(
          resolveRedirect(location: RoutePaths.splash, authState: unknown),
          isNull,
        );
      });

      test('sends every other location to the splash screen', () {
        expect(
          resolveRedirect(location: RoutePaths.dashboard, authState: unknown),
          RoutePaths.splash,
        );
        expect(
          resolveRedirect(location: RoutePaths.payslip, authState: null),
          RoutePaths.splash,
        );
      });
    });

    group('splash animation still playing', () {
      test('holds on the splash even once the session is known', () {
        for (final AuthState state in <AuthState>[signedOut, signedIn]) {
          expect(
            resolveRedirect(
              location: RoutePaths.splash,
              authState: state,
              isSplashFinished: false,
            ),
            isNull,
          );
        }
      });

      test('moves on once it finishes, whichever comes later', () {
        expect(
          resolveRedirect(
            location: RoutePaths.splash,
            authState: signedOut,
            isSplashFinished: true,
          ),
          RoutePaths.login,
        );
        expect(
          resolveRedirect(
            location: RoutePaths.splash,
            authState: unknown,
            isSplashFinished: true,
          ),
          isNull,
        );
      });

      test('does not affect any other location', () {
        expect(
          resolveRedirect(
            location: RoutePaths.dashboard,
            authState: signedOut,
            isSplashFinished: false,
          ),
          RoutePaths.login,
        );
      });
    });

    group('signed out', () {
      test('protected screens redirect to login', () {
        for (final String path in <String>[
          RoutePaths.dashboard,
          RoutePaths.attendance,
          RoutePaths.leave,
          RoutePaths.payslip,
          RoutePaths.profile,
          RoutePaths.notifications,
          RoutePaths.splash,
        ]) {
          expect(
            resolveRedirect(location: path, authState: signedOut),
            RoutePaths.login,
            reason: '$path must not be reachable signed out',
          );
        }
      });

      test('the public auth screens stay reachable', () {
        for (final String path in <String>[
          RoutePaths.login,
          RoutePaths.forgotPassword,
          RoutePaths.resetPassword,
        ]) {
          expect(
            resolveRedirect(location: path, authState: signedOut),
            isNull,
            reason: '$path must be reachable signed out',
          );
        }
      });
    });

    group('signed in', () {
      test('is moved off the login screen', () {
        expect(
          resolveRedirect(location: RoutePaths.login, authState: signedIn),
          RoutePaths.dashboard,
        );
      });

      test('is moved off the splash screen', () {
        expect(
          resolveRedirect(location: RoutePaths.splash, authState: signedIn),
          RoutePaths.dashboard,
        );
      });

      test('stays put on any app screen', () {
        for (final String path in <String>[
          RoutePaths.dashboard,
          RoutePaths.attendance,
          RoutePaths.leave,
          RoutePaths.payslip,
          RoutePaths.profile,
          '/leave/request/lr-1',
          '/payslip/ps-1',
        ]) {
          expect(
            resolveRedirect(location: path, authState: signedIn),
            isNull,
            reason: '$path should not redirect',
          );
        }
      });
    });

    group('temporary password', () {
      test('every screen is forced to change-password', () {
        for (final String path in <String>[
          RoutePaths.dashboard,
          RoutePaths.leave,
          RoutePaths.payslip,
          RoutePaths.profile,
        ]) {
          final String? destination = resolveRedirect(
            location: path,
            authState: mustChangePassword,
          );
          expect(destination, isNotNull);
          expect(destination, contains(RoutePaths.changePassword));
          // The flag hides the back button, so the screen cannot be skipped.
          expect(destination, contains('forced=true'));
        }
      });

      test('the change-password screen itself does not loop', () {
        // Redirecting a screen to itself is the classic router hang.
        expect(
          resolveRedirect(
            location: '${RoutePaths.profile}/${RoutePaths.changePassword}',
            authState: mustChangePassword,
          ),
          isNull,
        );
      });
    });
  });
}
