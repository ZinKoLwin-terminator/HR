import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/punch_attendance_usecase.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_providers.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_history_tile.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/check_in_out_panel.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/monthly_summary_card.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Attendance: today's punch panel, the monthly summary, and history.
///
/// The three sections read three providers rather than one aggregate, so a
/// slow history page does not hold up today's check-in button.
class AttendanceScreen extends ConsumerStatefulWidget {
  const AttendanceScreen({super.key});

  @override
  ConsumerState<AttendanceScreen> createState() => _AttendanceScreenState();
}

class _AttendanceScreenState extends ConsumerState<AttendanceScreen> {
  /// Which month the summary shows. Local because it is pure view state.
  AttendancePeriod _period = AttendancePeriod.current();

  Future<void> _punch(PunchDirection direction) async {
    final bool succeeded = await ref
        .read(punchControllerProvider.notifier)
        .punch(direction);

    if (!mounted) {
      return;
    }

    final SubmissionState state = ref.read(punchControllerProvider);
    if (succeeded) {
      AppSnackBar.success(
        context,
        direction == PunchDirection.checkIn
            ? 'Checked in. Have a good day.'
            : 'Checked out. See you tomorrow.',
      );
      return;
    }

    final Failure? failure = state.failureOrNull;
    if (failure == null) {
      return;
    }

    // A permanently denied location permission cannot be fixed by retrying,
    // so offer the settings route instead of a bare error.
    if (failure is DevicePermissionFailure && failure.isPermanentlyDenied) {
      _offerSettings(failure);
      return;
    }
    AppSnackBar.failure(context, failure);
  }

  void _offerSettings(DevicePermissionFailure failure) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          duration: const Duration(seconds: 6),
          content: Text(failure.message),
          action: SnackBarAction(
            label: 'Settings',
            onPressed: () {
              ref.read(locationServiceProvider).openSettings();
            },
          ),
        ),
      );
  }

  /// Pull-to-refresh reloads all three sections, which is what the gesture
  /// implies for the screen as a whole.
  Future<void> _refreshAll() async {
    ref.invalidate(todayAttendanceControllerProvider);
    ref.invalidate(attendanceSummaryProvider);
    await ref.read(attendanceHistoryControllerProvider.notifier).refresh();
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<PaginatedState<AttendanceRecord>> history = ref.watch(
      attendanceHistoryControllerProvider,
    );

    return Scaffold(
      appBar: const BrandAppBar(title: Text('Attendance')),
      body: SafeArea(
        top: false,
        child: AsyncValueView<PaginatedState<AttendanceRecord>>(
          value: history,
          loading: const PaginatedListSkeleton(lines: 3),
          onRetry: () => ref.invalidate(attendanceHistoryControllerProvider),
          data: (PaginatedState<AttendanceRecord> state) =>
              PaginatedListView<AttendanceRecord>(
                items: state.items,
                hasMore: state.hasMore,
                isLoadingMore: state.isLoadingMore,
                loadMoreFailure: state.loadMoreFailure,
                onRefresh: _refreshAll,
                onLoadMore: () => ref
                    .read(attendanceHistoryControllerProvider.notifier)
                    .loadMore(),
                emptyTitle: 'No attendance yet',
                emptyMessage:
                    'Your check-ins will appear here once you start '
                    'recording them.',
                emptyIcon: Icons.schedule_outlined,
                header: _Header(
                  period: _period,
                  onPreviousMonth: () =>
                      setState(() => _period = _period.previous()),
                  onNextMonth: () => setState(() => _period = _period.next()),
                  onCheckIn: () => _punch(PunchDirection.checkIn),
                  onCheckOut: () => _punch(PunchDirection.checkOut),
                ),
                itemBuilder:
                    (
                      BuildContext context,
                      AttendanceRecord record,
                      int index,
                    ) => AttendanceHistoryTile(
                      record: record,
                      onTap: () => context.pushNamed(
                        RouteNames.attendanceDetail,
                        pathParameters: <String, String>{
                          RouteParams.attendanceId: record.id,
                        },
                      ),
                    ),
              ),
        ),
      ),
    );
  }
}

/// The punch panel and monthly summary, scrolling above the history list.
///
/// A `ConsumerWidget` of its own so a punch rebuilds only this header, not
/// the entire history list below it.
class _Header extends ConsumerWidget {
  const _Header({
    required this.period,
    required this.onPreviousMonth,
    required this.onNextMonth,
    required this.onCheckIn,
    required this.onCheckOut,
  });

  final AttendancePeriod period;
  final VoidCallback onPreviousMonth;
  final VoidCallback onNextMonth;
  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<AttendanceRecord?> today = ref.watch(
      todayAttendanceControllerProvider,
    );
    final AsyncValue<AttendanceSummary> summary = ref.watch(
      attendanceSummaryProvider(period),
    );
    final bool isPunching = ref.watch(punchControllerProvider).isSubmitting;

    return Column(
      children: <Widget>[
        CheckInOutPanel(
          record: today.value,
          isLoading: today.isLoading && !today.hasValue,
          isPunching: isPunching,
          onCheckIn: onCheckIn,
          onCheckOut: onCheckOut,
        ),
        SizedBox(height: AppSpacing.cardGap),
        MonthlySummaryCard(
          period: period,
          summary: summary.value,
          onPreviousMonth: onPreviousMonth,
          onNextMonth: onNextMonth,
        ),
        SizedBox(height: AppSpacing.xl),
        Align(
          alignment: Alignment.centerLeft,
          child: Text(
            'History',
            style: Theme.of(context).textTheme.titleMedium,
          ),
        ),
        SizedBox(height: AppSpacing.md),
      ],
    );
  }
}
