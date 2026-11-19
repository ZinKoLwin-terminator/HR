import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
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

  _HistoryFilter _filter = _HistoryFilter.all;

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

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: AsyncValueView<PaginatedState<AttendanceRecord>>(
          value: history,
          loadingMessages: const <String>[
            'Fetching your records…',
            'Checking your shifts…',
            'Adding up your hours…',
          ],
          loading: const PaginatedListSkeleton(lines: 3),
          onRetry: () => ref.invalidate(attendanceHistoryControllerProvider),
          data: (PaginatedState<AttendanceRecord> state) {
            final List<AttendanceRecord> visible = state.items
                .where(_filter.matches)
                .toList(growable: false);
            final bool isFiltered =
                _filter != _HistoryFilter.all && state.items.isNotEmpty;

            return PaginatedListView<AttendanceRecord>(
              items: visible,
              hasMore: state.hasMore,
              isLoadingMore: state.isLoadingMore,
              loadMoreFailure: state.loadMoreFailure,
              onRefresh: _refreshAll,
              onLoadMore: () => ref
                  .read(attendanceHistoryControllerProvider.notifier)
                  .loadMore(),
              padding: EdgeInsets.only(bottom: AppSpacing.xxxl),
              separator: SizedBox(height: AppSpacing.sm + 2),
              emptyTitle: isFiltered ? 'No matching days' : 'No attendance yet',
              emptyMessage: isFiltered
                  ? 'Nothing loaded so far matches this filter.'
                  : 'Your check-ins will appear here once you start '
                        'recording them.',
              emptyIcon: Icons.schedule_outlined,
              emptyActionLabel: isFiltered ? 'Show all days' : null,
              onEmptyAction: isFiltered
                  ? () => setState(() => _filter = _HistoryFilter.all)
                  : null,
              header: _Header(
                period: _period,
                filter: _filter,
                visibleCount: visible.length,
                onFilterChanged: (_HistoryFilter filter) =>
                    setState(() => _filter = filter),
                onPreviousMonth: () =>
                    setState(() => _period = _period.previous()),
                onNextMonth: () => setState(() => _period = _period.next()),
                onCheckIn: () => _punch(PunchDirection.checkIn),
                onCheckOut: () => _punch(PunchDirection.checkOut),
              ),
              itemBuilder:
                  (BuildContext context, AttendanceRecord record, int index) =>
                      Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: AppSpacing.lg,
                        ),
                        child: AttendanceHistoryTile(
                          record: record,
                          onTap: () => context.pushNamed(
                            RouteNames.attendanceDetail,
                            pathParameters: <String, String>{
                              RouteParams.attendanceId: record.id,
                            },
                          ),
                        ),
                      ),
            );
          },
        ),
      ),
    );
  }
}

/// Which loaded days the history list shows. Applied to the pages already
/// fetched, not sent to the server.
enum _HistoryFilter {
  all('All'),
  present('Present'),
  late('Late'),
  weeklyOff('Weekly off');

  const _HistoryFilter(this.label);

  final String label;

  bool matches(AttendanceRecord record) => switch (this) {
    _HistoryFilter.all => true,
    _HistoryFilter.present => record.status.isAttended,
    _HistoryFilter.late => record.isLateArrival,
    _HistoryFilter.weeklyOff => record.status == AttendanceStatus.weeklyOff,
  };
}

/// Month header, stat tiles, today's card and the history heading,
/// scrolling above the history list.
///
/// A `ConsumerWidget` of its own so a punch rebuilds only this header, not
/// the entire history list below it.
class _Header extends ConsumerWidget {
  const _Header({
    required this.period,
    required this.filter,
    required this.visibleCount,
    required this.onFilterChanged,
    required this.onPreviousMonth,
    required this.onNextMonth,
    required this.onCheckIn,
    required this.onCheckOut,
  });

  final AttendancePeriod period;
  final _HistoryFilter filter;
  final int visibleCount;
  final ValueChanged<_HistoryFilter> onFilterChanged;
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
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        AttendanceMonthHeader(
          period: period,
          summary: summary.value,
          onPreviousMonth: onPreviousMonth,
          onNextMonth: onNextMonth,
        ),
        // The stat tiles straddle the header's foot: a navy strip behind
        // their top edge continues the band, so they sit half on it. Done
        // with a strip rather than a negative offset, which would leave a
        // matching gap at the bottom of the header.
        Stack(
          children: <Widget>[
            const Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: AttendanceMonthHeader.overlap,
              child: ColoredBox(color: AppColors.apexNavy),
            ),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
              child: AttendanceStatTiles(summary: summary.value),
            ),
          ],
        ),
        Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.xs,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              CheckInOutPanel(
                record: today.value,
                isLoading: today.isLoading && !today.hasValue,
                isPunching: isPunching,
                onCheckIn: onCheckIn,
                onCheckOut: onCheckOut,
              ),
              SizedBox(height: AppSpacing.xl),
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      'History',
                      style: textStyles.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  Text(
                    visibleCount == 1 ? '1 day' : '$visibleCount days',
                    style: textStyles.labelSmall?.copyWith(
                      color: context.palette.textTertiary,
                    ),
                  ),
                ],
              ),
              Gap.sm,
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                clipBehavior: Clip.none,
                child: Row(
                  children: <Widget>[
                    for (final _HistoryFilter option
                        in _HistoryFilter.values) ...<Widget>[
                      _FilterPill(
                        label: option.label,
                        isSelected: option == filter,
                        onTap: () => onFilterChanged(option),
                      ),
                      Gap.wSm,
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// A filter option: navy when selected, white with a hairline otherwise.
class _FilterPill extends StatelessWidget {
  const _FilterPill({
    required this.label,
    required this.isSelected,
    required this.onTap,
  });

  final String label;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Semantics(
      button: true,
      selected: isSelected,
      child: Material(
        color: isSelected ? AppColors.apexNavy : AppColors.surface,
        shape: StadiumBorder(
          side: BorderSide(
            color: isSelected ? AppColors.apexNavy : palette.border,
          ),
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.sm,
            ),
            child: Text(
              label,
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: isSelected
                    ? AppColors.textOnPrimary
                    : palette.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
