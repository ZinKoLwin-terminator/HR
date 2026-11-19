import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/session_event_bus.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/usecases/auth_usecases.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_providers.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_state.dart';
import 'package:hr_employee_app/features/notifications/presentation/providers/notification_providers.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_providers.dart';

/// Owns the session for the lifetime of the app.
///
/// Scope is deliberately narrow: it answers "who is signed in" and nothing
/// else. The login form has its own controller so that a failed sign-in
/// attempt cannot flip this provider into a loading or error state and bounce
/// the router around.
///
/// [build] does two things — restores any saved session, and subscribes to
/// [SessionEventBus] so an unrecoverable 401 discovered deep in the Dio
/// interceptor ends the session here, where the router can see it.
class AuthController extends AsyncNotifier<AuthState> {
  @override
  Future<AuthState> build() async {
    final AppLogger logger = ref.watch(appLoggerProvider);

    // The interceptor cannot navigate and must not know about features, so it
    // publishes an event and this listener turns it into state.
    final StreamSubscription<SessionEvent> subscription = ref
        .watch(sessionEventBusProvider)
        .stream
        .listen((SessionEvent event) {
          if (event == SessionEvent.expired) {
            logger.info('Session expired; signing out');
            state = const AsyncValue<AuthState>.data(
              AuthState.unauthenticated(wasSessionExpired: true),
            );
          }
        });
    ref.onDispose(subscription.cancel);

    final RestoreSessionUseCase restoreSession = ref.watch(
      restoreSessionUseCaseProvider,
    );
    final Result<AuthenticatedUser?> result = await restoreSession();

    return result.fold(
      onSuccess: (AuthenticatedUser? user) => user == null
          ? const AuthState.unauthenticated()
          : AuthState.authenticated(user),
      // A restore that fails is treated as "not signed in" rather than as an
      // error screen: the user can always recover by signing in, and showing
      // them a failure at launch would be a dead end.
      onFailure: (_) => const AuthState.unauthenticated(),
    );
  }

  /// Records a successful sign-in. Called by `LoginController`.
  void onSignedIn(AuthenticatedUser user) {
    state = AsyncValue<AuthState>.data(AuthState.authenticated(user));
  }

  /// Updates the cached identity after a profile edit, so the shell's name
  /// and avatar stay in step without a full session reload.
  void onUserUpdated(AuthenticatedUser user) {
    if (state.value?.isAuthenticated ?? false) {
      state = AsyncValue<AuthState>.data(AuthState.authenticated(user));
    }
  }

  /// Signs out and clears every trace of the session from the device.
  ///
  /// The state is set to unauthenticated regardless of the outcome: sign-out
  /// must work offline, and the repository clears local storage either way.
  ///
  /// Credentials are not the only thing to remove. Downloaded payslip PDFs
  /// and the push token both outlive the session otherwise, and on a shared
  /// device that is a data leak. Each cleanup is best-effort and cannot
  /// block the sign-out.
  Future<void> signOut() async {
    final LogoutUseCase logout = ref.read(logoutUseCaseProvider);
    await logout();

    await _clearDeviceArtifacts();

    state = const AsyncValue<AuthState>.data(AuthState.unauthenticated());
  }

  /// Removes personal data this user left on the device.
  Future<void> _clearDeviceArtifacts() async {
    final AppLogger logger = ref.read(appLoggerProvider);

    try {
      await ref.read(payslipRepositoryProvider).clearDownloadedPayslips();
    } on Object catch (error) {
      logger.warning('Could not clear downloaded payslips: $error');
    }

    try {
      // Stops this device receiving pushes addressed to the user who left.
      await ref.read(pushNotificationServiceProvider).clearToken();
    } on Object catch (error) {
      logger.warning('Could not clear the push token: $error');
    }
  }

  /// Registers the device for push delivery.
  ///
  /// Called after a successful sign-in. With the no-op push service shipped
  /// today the token is null and this returns immediately — the wiring is in
  /// place so that configuring a provider is the only remaining step.
  Future<void> registerForPushNotifications() async {
    final String? token = await ref
        .read(pushNotificationServiceProvider)
        .token();

    if (token == null || token.isEmpty) {
      return;
    }
    await ref.read(registerPushDeviceUseCaseProvider)(token);
  }
}

final AsyncNotifierProvider<AuthController, AuthState> authControllerProvider =
    AsyncNotifierProvider<AuthController, AuthState>(AuthController.new);

/// The signed-in user, or null.
///
/// A derived provider so widgets that only need the user do not rebuild when
/// an unrelated part of the auth state changes.
final Provider<AuthenticatedUser?> currentUserProvider =
    Provider<AuthenticatedUser?>((Ref ref) {
      return ref.watch(
        authControllerProvider.select(
          (AsyncValue<AuthState> value) => value.value?.userOrNull,
        ),
      );
    });
