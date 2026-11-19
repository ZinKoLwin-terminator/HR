import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_controllers.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_balance_tile.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_request_tile.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// Leave: entitlements on one tab, applications on the other.
///
/// Two tabs rather than one long page: "how much do I have left" and "what
/// did I ask for" are different questions, and mixing them makes both harder
/// to scan.
class LeaveScreen extends ConsumerStatefulWidget {
  const LeaveScreen({super.key});

  @override
  ConsumerState<LeaveScreen> createState() => _LeaveScreenState();
}

class _LeaveScreenState extends ConsumerState<LeaveScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController = TabController(
    length: 2,
    vsync: this,
  );

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.pushNamed(RouteNames.applyLeave),
        icon: const Icon(Icons.post_add_rounded),
        label: const Text('Apply'),
      ),
      body: Column(
        children: <Widget>[
          _LeaveHeader(tabController: _tabController),
          Expanded(
            child: SafeArea(
              top: false,
              child: TabBarView(
                controller: _tabController,
                children: const <Widget>[_BalancesTab(), _RequestsTab()],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Brand header: title, a one-line summary of what is left this year, and
/// the tabs, drawn as a pill switch so they read as part of the header.
class _LeaveHeader extends ConsumerWidget {
  const _LeaveHeader({required this.tabController});

  final TabController tabController;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color onBrand = Theme.of(context).colorScheme.onPrimary;
    final Color primary = Theme.of(context).colorScheme.primary;
    final List<LeaveBalance>? balances = ref
        .watch(leaveBalancesProvider(DateTime.now().year))
        .value;

    String? summary;
    if (balances != null && balances.isNotEmpty) {
      final double left = balances.fold(
        0,
        (double sum, LeaveBalance b) => sum + b.remainingDays,
      );
      final double pending = balances.fold(
        0,
        (double sum, LeaveBalance b) => sum + b.pendingDays,
      );
      summary =
          '${DayCountFormatter.format(left)} left this year'
          '${pending > 0 ? ' · ${DayCountFormatter.format(pending)} pending' : ''}';
    }

    return Container(
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
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.lg,
            AppSpacing.lg,
            AppSpacing.lg,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Leave',
                style: textStyles.titleLarge?.copyWith(color: onBrand),
              ),
              if (summary != null) ...<Widget>[
                Gap.xxs,
                Text(
                  summary,
                  style: textStyles.bodyMedium?.copyWith(
                    color: onBrand.withValues(alpha: 0.8),
                  ),
                ),
              ],
              Gap.lg,
              Container(
                padding: EdgeInsets.all(AppSpacing.xs),
                decoration: BoxDecoration(
                  color: onBrand.withValues(alpha: 0.14),
                  borderRadius: AppRadius.field,
                ),
                child: TabBar(
                  controller: tabController,
                  indicatorSize: TabBarIndicatorSize.tab,
                  dividerColor: Colors.transparent,
                  indicator: BoxDecoration(
                    color: onBrand,
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  labelColor: primary,
                  unselectedLabelColor: onBrand.withValues(alpha: 0.85),
                  labelStyle: textStyles.labelLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                  unselectedLabelStyle: textStyles.labelLarge,
                  splashBorderRadius: BorderRadius.circular(AppRadius.sm),
                  overlayColor: WidgetStateProperty.all(
                    onBrand.withValues(alpha: 0.08),
                  ),
                  tabs: const <Widget>[
                    Tab(text: 'Balances', height: 40),
                    Tab(text: 'Requests', height: 40),
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

/// Entitlements for the current year.
class _BalancesTab extends ConsumerWidget {
  const _BalancesTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final int year = DateTime.now().year;
    final AsyncValue<List<LeaveBalance>> balances = ref.watch(
      leaveBalancesProvider(year),
    );

    return AsyncValueView<List<LeaveBalance>>(
      value: balances,
      loading: const PaginatedListSkeleton(itemCount: 4, lines: 4),
      onRetry: () => ref.invalidate(leaveBalancesProvider(year)),
      data: (List<LeaveBalance> data) {
        if (data.isEmpty) {
          return const AppEmptyView(
            title: 'No entitlements',
            message:
                'Your leave types have not been set up yet. Contact HR '
                'if you think this is wrong.',
            icon: Icons.event_busy_outlined,
          );
        }

        return RefreshIndicator(
          onRefresh: () async => ref.invalidate(leaveBalancesProvider(year)),
          child: ListView.separated(
            // Extra bottom room so the floating Apply button never covers
            // the last card's figures.
            padding: EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.lg,
              AppSpacing.lg,
              AppSpacing.huge * 2,
            ),
            physics: const AlwaysScrollableScrollPhysics(),
            itemCount: data.length,
            separatorBuilder: (BuildContext context, int index) =>
                SizedBox(height: AppSpacing.cardGap),
            itemBuilder: (BuildContext context, int index) => LeaveBalanceTile(
              balance: data[index],
              onApply: () => context.pushNamed(RouteNames.applyLeave),
            ),
          ),
        );
      },
    );
  }
}

/// The employee's own applications, with a status filter.
class _RequestsTab extends ConsumerWidget {
  const _RequestsTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<PaginatedState<LeaveRequest>> requests = ref.watch(
      leaveRequestsControllerProvider,
    );

    return Column(
      children: <Widget>[
        const _StatusFilterBar(),
        Expanded(
          child: AsyncValueView<PaginatedState<LeaveRequest>>(
            value: requests,
            loading: const PaginatedListSkeleton(lines: 4),
            onRetry: () => ref.invalidate(leaveRequestsControllerProvider),
            data: (PaginatedState<LeaveRequest> state) =>
                PaginatedListView<LeaveRequest>(
                  items: state.items,
                  hasMore: state.hasMore,
                  isLoadingMore: state.isLoadingMore,
                  loadMoreFailure: state.loadMoreFailure,
                  onRefresh: () => ref
                      .read(leaveRequestsControllerProvider.notifier)
                      .refresh(),
                  onLoadMore: () => ref
                      .read(leaveRequestsControllerProvider.notifier)
                      .loadMore(),
                  padding: EdgeInsets.fromLTRB(
                    AppSpacing.lg,
                    AppSpacing.lg,
                    AppSpacing.lg,
                    AppSpacing.huge * 2,
                  ),
                  emptyTitle: 'No leave requests',
                  emptyMessage:
                      'Applications you submit will appear here with their '
                      'status.',
                  emptyIcon: Icons.event_note_outlined,
                  itemBuilder:
                      (BuildContext context, LeaveRequest request, int index) =>
                          LeaveRequestTile(
                            request: request,
                            onTap: () => context.pushNamed(
                              RouteNames.leaveDetail,
                              pathParameters: <String, String>{
                                RouteParams.leaveRequestId: request.id,
                              },
                            ),
                          ),
                ),
          ),
        ),
      ],
    );
  }
}

/// Horizontal status filter.
///
/// Changing it rebuilds `leaveRequestsControllerProvider`, which reloads from
/// page 1 — the correct behaviour for a filter, and handled by the controller
/// watching the filter provider rather than by this widget.
class _StatusFilterBar extends ConsumerWidget {
  const _StatusFilterBar();

  static const List<LeaveStatus?> _options = <LeaveStatus?>[
    null,
    LeaveStatus.pending,
    LeaveStatus.approved,
    LeaveStatus.rejected,
    LeaveStatus.cancelled,
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final LeaveStatus? selected = ref.watch(leaveStatusFilterProvider);

    return Container(
      width: double.infinity,
      padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: context.palette.border)),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        padding: AppSpacing.screenHorizontal,
        child: Row(
          children: <Widget>[
            for (final LeaveStatus? option in _options) ...<Widget>[
              _FilterChip(
                label: option == null ? 'All' : option.label,
                isSelected: option == selected,
                onTap: () =>
                    ref.read(leaveStatusFilterProvider.notifier).set(option),
              ),
              Gap.wSm,
            ],
          ],
        ),
      ),
    );
  }
}

class _FilterChip extends StatelessWidget {
  const _FilterChip({
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
    final Color primary = Theme.of(context).colorScheme.primary;

    return InkWell(
      onTap: onTap,
      borderRadius: AppRadius.chip,
      child: Container(
        constraints: const BoxConstraints(
          minHeight: AppSizes.minTouchTarget - 12,
        ),
        padding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.sm,
        ),
        decoration: BoxDecoration(
          color: isSelected
              ? primary.withValues(alpha: 0.10)
              : palette.surfaceMuted,
          borderRadius: AppRadius.chip,
          border: Border.all(color: isSelected ? primary : Colors.transparent),
        ),
        child: Center(
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: isSelected ? primary : palette.textSecondary,
              fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
            ),
          ),
        ),
      ),
    );
  }
}
