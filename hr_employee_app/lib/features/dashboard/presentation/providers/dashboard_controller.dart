import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:hr_employee_app/features/dashboard/domain/usecases/get_dashboard_summary_usecase.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_providers.dart';

/// Loads the dashboard.
///
/// An `AsyncNotifier` because the screen is a straight read with three
/// states. Failures are surfaced with [ResultThrowX.valueOrThrow], the
/// convention every controller in the app follows: the thrown object is the
/// `Failure` itself, so `AsyncValue.error` carries a typed, user-ready value
/// that `AsyncValueView` can render.
class DashboardController extends AsyncNotifier<DashboardSummary> {
  @override
  Future<DashboardSummary> build() async {
    final GetDashboardSummaryUseCase getSummary = ref.watch(
      getDashboardSummaryUseCaseProvider,
    );
    final Result<DashboardSummary> result = await getSummary();
    return result.valueOrThrow();
  }

  /// Pull-to-refresh.
  ///
  /// `invalidateSelf` re-runs [build]. The previous value is retained while
  /// it reloads, so the screen does not blank out — see `AsyncValueView`.
  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }

  /// Replaces today's attendance in place after a check-in or check-out.
  ///
  /// Avoids a full dashboard reload for a change whose outcome the app
  /// already has. The next refresh reconciles with the server.
  void applyTodayAttendance(TodayAttendance attendance) {
    final DashboardSummary? current = state.value;
    if (current == null) {
      return;
    }
    state = AsyncValue<DashboardSummary>.data(
      current.copyWith(todayAttendance: attendance),
    );
  }

  /// Lowers the unread badge when notifications are read on another screen.
  void decrementUnreadCount([int by = 1]) {
    final DashboardSummary? current = state.value;
    if (current == null) {
      return;
    }
    final int next = current.unreadNotificationCount - by;
    state = AsyncValue<DashboardSummary>.data(
      current.copyWith(unreadNotificationCount: next < 0 ? 0 : next),
    );
  }

  /// Clears the unread badge after "mark all as read".
  void clearUnreadCount() {
    final DashboardSummary? current = state.value;
    if (current == null) {
      return;
    }
    state = AsyncValue<DashboardSummary>.data(
      current.copyWith(unreadNotificationCount: 0),
    );
  }
}

final AsyncNotifierProvider<DashboardController, DashboardSummary>
dashboardControllerProvider =
    AsyncNotifierProvider<DashboardController, DashboardSummary>(
      DashboardController.new,
    );
