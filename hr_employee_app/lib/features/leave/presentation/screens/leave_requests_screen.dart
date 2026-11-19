import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_overview.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_controllers.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_request_tile.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// Every leave request for a year, grouped by month, with a status filter.
///
/// Each month shows the days it accounts for; rejected and cancelled
/// requests stay in the list but are left out of that total, since they
/// never used any leave.
class LeaveRequestsScreen extends ConsumerStatefulWidget {
  const LeaveRequestsScreen({super.key});

  @override
  ConsumerState<LeaveRequestsScreen> createState() =>
      _LeaveRequestsScreenState();
}

class _LeaveRequestsScreenState extends ConsumerState<LeaveRequestsScreen> {
  int _year = DateTime.now().year;
  LeaveStatus? _status;

  static const List<LeaveStatus?> _filters = <LeaveStatus?>[
    null,
    LeaveStatus.pending,
    LeaveStatus.approved,
    LeaveStatus.rejected,
    LeaveStatus.cancelled,
  ];

  @override
  Widget build(BuildContext context) {
    final AsyncValue<LeaveRequestYear> value = ref.watch(
      leaveRequestYearProvider(_year),
    );
    final List<LeaveRequest> history =
        ref.watch(leaveRequestHistoryProvider).value ?? const <LeaveRequest>[];

    return Scaffold(
      body: Column(
        children: <Widget>[
          _Header(
            year: _year,
            years: _yearOptions(history),
            data: value.value,
            filters: _filters,
            selected: _status,
            onYear: (int year) => setState(() => _year = year),
            onFilter: (LeaveStatus? status) => setState(() => _status = status),
          ),
          Expanded(
            child: AsyncValueView<LeaveRequestYear>(
              value: value,
              loadingMessages: const <String>[
                'Fetching your requests…',
                'Sorting them by month…',
                'Almost there…',
              ],
              loading: const PaginatedListSkeleton(lines: 3),
              onRetry: () => ref.invalidate(leaveRequestHistoryProvider),
              data: (LeaveRequestYear data) => RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(leaveRequestHistoryProvider);
                  await ref.read(leaveRequestYearProvider(_year).future);
                },
                child: _MonthList(
                  groups: data.monthGroups(_status),
                  emptyTitle: _status == null || data.requests.isEmpty
                      ? 'No leave requests yet'
                      : 'No ${_status!.label.toLowerCase()} requests',
                  emptyMessage: _emptyMessage(data),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _emptyMessage(LeaveRequestYear data) {
    final LeaveStatus? status = _status;
    if (status == null || data.requests.isEmpty) {
      return 'When you apply for leave, your requests and their status '
          'will show up here.';
    }
    return 'No ${status.label.toLowerCase()} requests in $_year.';
  }

  /// This year and the two before it, plus any older year with requests.
  List<int> _yearOptions(List<LeaveRequest> history) {
    final int current = DateTime.now().year;
    final Set<int> years = <int>{
      current,
      current - 1,
      current - 2,
      _year,
      for (final LeaveRequest r in history) r.startDate.year,
    };
    return years.toList()..sort((int a, int b) => b.compareTo(a));
  }
}

class _Header extends StatelessWidget {
  const _Header({
    required this.year,
    required this.years,
    required this.data,
    required this.filters,
    required this.selected,
    required this.onYear,
    required this.onFilter,
  });

  final int year;
  final List<int> years;

  /// Null while loading; the counts are left off until it arrives.
  final LeaveRequestYear? data;

  final List<LeaveStatus?> filters;
  final LeaveStatus? selected;
  final ValueChanged<int> onYear;
  final ValueChanged<LeaveStatus?> onFilter;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final int? total = data?.requests.length;
    final bool isThisYear = year == DateTime.now().year;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.only(top: AppSpacing.md, bottom: AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Padding(
                padding: AppSpacing.screenHorizontal,
                child: Row(
                  children: <Widget>[
                    _GlassCircleButton(
                      icon: Icons.arrow_back_rounded,
                      tooltip: 'Back',
                      onTap: () => context.canPop()
                          ? context.pop()
                          : context.goNamed(RouteNames.leave),
                    ),
                    Gap.wMd,
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            'My requests',
                            style: textStyles.titleLarge?.copyWith(
                              color: onBrand,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          if (total != null)
                            Text(
                              '$total ${total == 1 ? 'request' : 'requests'}'
                              '${isThisYear ? ' this year' : ' in $year'}',
                              style: textStyles.bodySmall?.copyWith(
                                color: onBrand.withValues(alpha: 0.72),
                              ),
                            ),
                        ],
                      ),
                    ),
                    _YearPicker(year: year, years: years, onSelected: onYear),
                  ],
                ),
              ),
              Gap.md,
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                padding: AppSpacing.screenHorizontal,
                child: Row(
                  children: <Widget>[
                    for (final LeaveStatus? filter in filters) ...<Widget>[
                      _FilterChip(
                        label: filter == null ? 'All' : filter.label,
                        count: data?.countFor(filter),
                        isSelected: filter == selected,
                        onTap: () => onFilter(filter),
                      ),
                      Gap.wSm,
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _GlassCircleButton extends StatelessWidget {
  const _GlassCircleButton({
    required this.icon,
    required this.tooltip,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Tooltip(
      message: tooltip,
      child: Material(
        color: onBrand.withValues(alpha: 0.12),
        shape: const CircleBorder(),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: SizedBox.square(
            dimension: 40,
            child: Icon(icon, color: onBrand, size: AppSizes.iconMd),
          ),
        ),
      ),
    );
  }
}

class _YearPicker extends StatelessWidget {
  const _YearPicker({
    required this.year,
    required this.years,
    required this.onSelected,
  });

  final int year;
  final List<int> years;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return PopupMenuButton<int>(
      tooltip: 'Choose year',
      initialValue: year,
      onSelected: onSelected,
      position: PopupMenuPosition.under,
      itemBuilder: (BuildContext context) => <PopupMenuEntry<int>>[
        for (final int option in years)
          PopupMenuItem<int>(value: option, child: Text('$option')),
      ],
      child: Container(
        padding: EdgeInsets.fromLTRB(
          AppSpacing.md,
          AppSpacing.xs + 2,
          AppSpacing.sm,
          AppSpacing.xs + 2,
        ),
        decoration: BoxDecoration(
          color: onBrand.withValues(alpha: 0.12),
          borderRadius: AppRadius.chip,
          border: Border.all(color: onBrand.withValues(alpha: 0.14)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              '$year',
              style: Theme.of(context).textTheme.labelLarge
                  ?.copyWith(color: onBrand, fontWeight: FontWeight.w700),
            ),
            Icon(
              Icons.keyboard_arrow_down_rounded,
              color: onBrand,
              size: AppSizes.iconMd,
            ),
          ],
        ),
      ),
    );
  }
}

class _FilterChip extends StatelessWidget {
  const _FilterChip({
    required this.label,
    required this.count,
    required this.isSelected,
    required this.onTap,
  });

  final String label;
  final int? count;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final Color ink = isSelected ? AppColors.apexNavy : onBrand;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Semantics(
      button: true,
      selected: isSelected,
      label: count == null ? label : '$label, $count',
      child: ExcludeSemantics(
        child: Material(
          color: isSelected ? onBrand : onBrand.withValues(alpha: 0.12),
          shape: const StadiumBorder(),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: onTap,
            child: Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.md,
                AppSpacing.xs + 2,
                count == null ? AppSpacing.md : AppSpacing.xs + 2,
                AppSpacing.xs + 2,
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    label,
                    style: textStyles.labelLarge?.copyWith(
                      color: ink,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (count != null) ...<Widget>[
                    Gap.wXs,
                    Container(
                      constraints: const BoxConstraints(minWidth: 20),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 6,
                        vertical: 1,
                      ),
                      decoration: BoxDecoration(
                        color: isSelected
                            ? AppColors.apexNavy.withValues(alpha: 0.10)
                            : onBrand.withValues(alpha: 0.18),
                        borderRadius: AppRadius.chip,
                      ),
                      child: Text(
                        '$count',
                        textAlign: TextAlign.center,
                        style: textStyles.labelSmall?.copyWith(
                          color: ink,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _MonthList extends StatelessWidget {
  const _MonthList({
    required this.groups,
    required this.emptyTitle,
    required this.emptyMessage,
  });

  final List<LeaveMonthGroup> groups;
  final String emptyTitle;
  final String emptyMessage;

  @override
  Widget build(BuildContext context) {
    if (groups.isEmpty) {
      return LayoutBuilder(
        builder: (BuildContext context, BoxConstraints constraints) =>
            SingleChildScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              child: SizedBox(
                height: constraints.maxHeight,
                child: AppEmptyView(
                  title: emptyTitle,
                  message: emptyMessage,
                  icon: Icons.event_note_outlined,
                  actionLabel: 'Apply leave',
                  actionIcon: Icons.add_rounded,
                  onAction: () => context.pushNamed(RouteNames.applyLeave),
                ),
              ),
            ),
      );
    }

    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;

    return ListView.builder(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: EdgeInsets.fromLTRB(
        AppSpacing.lg,
        AppSpacing.sm,
        AppSpacing.lg,
        AppSpacing.xxl,
      ),
      itemCount: groups.length,
      itemBuilder: (BuildContext context, int index) {
        final LeaveMonthGroup group = groups[index];
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.xxs,
                AppSpacing.lg,
                AppSpacing.xxs,
                AppSpacing.sm,
              ),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      DateFormatter.monthYear(group.month),
                      style: textStyles.titleSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  Text(
                    DayCountFormatter.format(group.countedDays),
                    style: textStyles.labelMedium?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
                ],
              ),
            ),
            LeaveRequestGroupCard(
              children: <Widget>[
                for (final LeaveRequest request in group.requests)
                  LeaveRequestRow(
                    request: request,
                    onTap: () => context.pushNamed(
                      RouteNames.leaveDetail,
                      pathParameters: <String, String>{
                        RouteParams.leaveRequestId: request.id,
                      },
                    ),
                  ),
              ],
            ),
          ],
        );
      },
    );
  }
}
