import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_loading_indicator.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/punch_attendance_usecase.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:hr_employee_app/features/dashboard/domain/usecases/get_dashboard_summary_usecase.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_controller.dart';
import 'package:hr_employee_app/features/dashboard/presentation/widgets/leave_balance_card.dart';
import 'package:hr_employee_app/features/dashboard/presentation/widgets/profile_summary_card.dart';
import 'package:hr_employee_app/features/dashboard/presentation/widgets/quick_actions_grid.dart';
import 'package:hr_employee_app/features/dashboard/presentation/widgets/recent_notifications_list.dart';
import 'package:hr_employee_app/features/dashboard/presentation/widgets/today_attendance_card.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:material_ui/material_ui.dart';

/// The home screen.
///
/// One aggregate request fills it (see `GetDashboardSummaryUseCase`), so the
/// first screen after sign-in costs a single round trip.
class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  /// How far the body is pulled up into the header.
  ///
  /// Shared with the loading placeholder so the cards do not jump when the
  /// data arrives.
  static double get bodyOverlap => AppSpacing.xxxl;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<DashboardSummary> summary = ref.watch(
      dashboardControllerProvider,
    );

    // The scaffold keeps the neutral background: the header paints its own
    // status-bar area, so there is no coloured strip left for the scaffold
    // to match.
    return Scaffold(
      body: AsyncValueView<DashboardSummary>(
        value: summary,
        loading: const _DashboardSkeleton(),
        onRetry: () => ref.invalidate(dashboardControllerProvider),
        data: (DashboardSummary data) => _DashboardBody(summary: data),
      ),
    );
  }
}

class _DashboardBody extends ConsumerStatefulWidget {
  const _DashboardBody({required this.summary});

  final DashboardSummary summary;

  @override
  ConsumerState<_DashboardBody> createState() => _DashboardBodyState();
}

class _DashboardBodyState extends ConsumerState<_DashboardBody> {
  Future<void> _punch(PunchDirection direction) async {
    final bool succeeded = await ref
        .read(punchControllerProvider.notifier)
        .punch(direction);

    if (!mounted) {
      return;
    }
    if (succeeded) {
      AppSnackBar.success(
        context,
        direction == PunchDirection.checkIn ? 'Checked in.' : 'Checked out.',
      );
      return;
    }

    final Failure? failure = ref.read(punchControllerProvider).failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  void _openNotification(AppNotification notification) {
    context.pushNamed(
      RouteNames.notificationDetail,
      pathParameters: <String, String>{
        RouteParams.notificationId: notification.id,
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final DashboardSummary summary = widget.summary;
    final AppPalette palette = context.palette;
    final ColorScheme colors = Theme.of(context).colorScheme;
    final bool isPunching = ref.watch(punchControllerProvider).isSubmitting;

    return RefreshIndicator(
      onRefresh: () => ref.read(dashboardControllerProvider.notifier).refresh(),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: <Widget>[
          // Header and body are one sliver, not two, because the body is
          // pulled up into the header: a viewport paints its slivers back to
          // front, so as separate slivers the header would paint over the
          // card that is meant to overlap it.
          SliverToBoxAdapter(
            child: Column(
              children: <Widget>[
                ProfileSummaryCard(
                  profile: summary.profile,
                  unreadCount: summary.unreadNotificationCount,
                  onNotificationsTap: () =>
                      context.pushNamed(RouteNames.notifications),
                  onProfileTap: () => context.goNamed(RouteNames.profile),
                ),

                // A transform rather than a negative margin: it moves the
                // painted cards without taking the gap out of the scroll
                // extent, so the last card still clears the bottom bar.
                Transform.translate(
                  offset: Offset(0, -DashboardScreen.bodyOverlap),
                  child: Padding(
                    padding: EdgeInsets.fromLTRB(
                      AppSpacing.lg,
                      0,
                      AppSpacing.lg,
                      AppSpacing.xxxl,
                    ),
                    child: Column(
                      children: <Widget>[
                        TodayAttendanceCard(
                          attendance: summary.todayAttendance,
                          isPunching: isPunching,
                          onCheckIn: () => _punch(PunchDirection.checkIn),
                          onCheckOut: () => _punch(PunchDirection.checkOut),
                          onViewAttendance: () =>
                              context.goNamed(RouteNames.attendance),
                        ),
                        SizedBox(height: AppSpacing.cardGap),

                        // Each shortcut carries its own tint, taken from the
                        // theme: four identical squares have to be read,
                        // whereas four colours can be aimed at once the grid
                        // is familiar.
                        QuickActionsGrid(
                          actions: <QuickAction>[
                            QuickAction(
                              label: 'Apply leave',
                              icon: Icons.post_add_rounded,
                              onTap: () =>
                                  context.pushNamed(RouteNames.applyLeave),
                            ),
                            QuickAction(
                              label: 'My leave',
                              icon: Icons.event_available_rounded,
                              tint: colors.secondary,
                              onTap: () => context.goNamed(RouteNames.leave),
                            ),
                            QuickAction(
                              label: 'Payslips',
                              icon: Icons.receipt_long_rounded,
                              tint: palette.success,
                              onTap: () => context.goNamed(RouteNames.payslip),
                            ),
                            QuickAction(
                              label: 'Attendance',
                              icon: Icons.schedule_rounded,
                              tint: palette.warning,
                              onTap: () =>
                                  context.goNamed(RouteNames.attendance),
                            ),
                          ],
                        ),
                        SizedBox(height: AppSpacing.cardGap),

                        LeaveBalanceCard(
                          balances: summary.leaveBalances,
                          totalRemainingDays: summary.totalRemainingLeaveDays,
                          maxVisible:
                              GetDashboardSummaryUseCase.visibleLeaveBalances,
                          onSeeAll: () => context.goNamed(RouteNames.leave),
                        ),
                        SizedBox(height: AppSpacing.cardGap),

                        RecentNotificationsList(
                          notifications: summary.recentNotifications,
                          onSeeAll: () =>
                              context.pushNamed(RouteNames.notifications),
                          onNotificationTap: _openNotification,
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// First-load placeholder.
///
/// Mirrors the real layout — gradient header, then overlapping cards — so the
/// page does not jump when the data arrives.
class _DashboardSkeleton extends StatelessWidget {
  const _DashboardSkeleton();

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Container(
          width: double.infinity,
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: context.palette.brandGradient,
            ),
            borderRadius: BorderRadius.vertical(
              bottom: Radius.circular(AppRadius.xl),
            ),
          ),
          child: SafeArea(
            bottom: false,
            // The real header's height, expressed in the tokens that build
            // it: the avatar, the padding above and below it, and the
            // clearance the overlapping card sits in.
            child: SizedBox(
              height:
                  AppSizes.avatarMd +
                  AppSpacing.lg * 2 +
                  ProfileSummaryCard.bottomPadding,
            ),
          ),
        ),
        Expanded(
          child: Transform.translate(
            offset: Offset(0, -DashboardScreen.bodyOverlap),
            child: ListView(
              // Not scrollable: it is a placeholder, and a skeleton that
              // scrolls invites the user to drag at nothing.
              physics: const NeverScrollableScrollPhysics(),
              padding: EdgeInsets.fromLTRB(
                AppSpacing.lg,
                0,
                AppSpacing.lg,
                AppSpacing.xxxl,
              ),
              children: <Widget>[
                const AppSkeletonCard(lines: 4),
                SizedBox(height: AppSpacing.cardGap),
                const AppSkeletonCard(lines: 3),
                SizedBox(height: AppSpacing.cardGap),
                const AppSkeletonCard(lines: 4),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
