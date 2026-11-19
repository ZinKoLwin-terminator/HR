import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_controllers.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_header.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_tile.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// The employee's own grievances, newest activity first, with status
/// filters on the navy header, a callout when HR is waiting for a reply,
/// pull-to-refresh and a button to start a new one.
///
/// Only the employee's own cases: HR's case screens are web-only.
class MyGrievancesScreen extends ConsumerStatefulWidget {
  const MyGrievancesScreen({super.key});

  /// The filters offered, with their chip labels. Null is "All".
  static const List<(GrievanceStatus?, String)> filters =
      <(GrievanceStatus?, String)>[
        (null, 'All'),
        (GrievanceStatus.awaitingEmployeeResponse, 'Needs reply'),
        (GrievanceStatus.draft, 'Drafts'),
        (GrievanceStatus.submitted, 'Submitted'),
        (GrievanceStatus.underReview, 'Under review'),
        (GrievanceStatus.resolved, 'Resolved'),
        (GrievanceStatus.rejected, 'Not taken further'),
        (GrievanceStatus.closed, 'Closed'),
      ];

  @override
  ConsumerState<MyGrievancesScreen> createState() => _MyGrievancesScreenState();
}

class _MyGrievancesScreenState extends ConsumerState<MyGrievancesScreen> {
  /// Counts per filter, taken from the unfiltered list whenever it is on
  /// screen. The filters themselves are applied by the server, so a count is
  /// shown only once it is known — and it covers the pages loaded so far.
  Map<GrievanceStatus?, int>? _counts;

  void _open(GrievanceSummary grievance) {
    context.pushNamed(
      RouteNames.grievanceDetail,
      pathParameters: <String, String>{
        RouteParams.grievanceId: '${grievance.id}',
      },
    );
  }

  void _rememberCounts(List<GrievanceSummary> all) {
    final Map<GrievanceStatus?, int> counts = <GrievanceStatus?, int>{
      null: all.length,
    };
    for (final GrievanceSummary g in all) {
      counts[g.status] = (counts[g.status] ?? 0) + 1;
    }
    _counts = counts;
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<PaginatedState<GrievanceSummary>> grievances = ref.watch(
      myGrievancesControllerProvider,
    );
    final GrievanceStatus? filter = ref.watch(grievanceStatusFilterProvider);
    final List<GrievanceSummary>? loaded = grievances.value?.items;
    if (filter == null && loaded != null) {
      _rememberCounts(loaded);
    }

    return GrievanceScaffold(
      title: 'My grievances',
      subtitle: 'Raise a concern with HR and follow it',
      bottom: _FilterRow(
        selected: filter,
        counts: _counts,
        onSelect: (GrievanceStatus? status) =>
            ref.read(grievanceStatusFilterProvider.notifier).select(status),
      ),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: 'new-grievance',
        onPressed: () => context.pushNamed(RouteNames.newGrievance),
        backgroundColor: AppColors.summitTeal,
        foregroundColor: AppColors.textOnPrimary,
        icon: const Icon(Icons.add_rounded),
        label: const Text('New grievance'),
      ),
      body: AsyncValueView<PaginatedState<GrievanceSummary>>(
        value: grievances,
        loadingMessages: const <String>[
          'Fetching your grievances…',
          'Checking for replies from HR…',
          'Almost there…',
        ],
        loading: const PaginatedListSkeleton(lines: 3),
        onRetry: () => ref.invalidate(myGrievancesControllerProvider),
        data: (PaginatedState<GrievanceSummary> state) {
          final GrievanceSummary? waiting = state.items
              .where(
                (GrievanceSummary g) =>
                    g.status == GrievanceStatus.awaitingEmployeeResponse,
              )
              .firstOrNull;

          return PaginatedListView<GrievanceSummary>(
            items: state.items,
            hasMore: state.hasMore,
            isLoadingMore: state.isLoadingMore,
            loadMoreFailure: state.loadMoreFailure,
            padding: EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.md,
              AppSpacing.lg,
              // Clear of the floating button.
              AppSpacing.huge + AppSpacing.xxxl,
            ),
            separator: const SizedBox.shrink(),
            header: waiting == null
                ? null
                : Padding(
                    padding: EdgeInsets.only(bottom: AppSpacing.md),
                    child: _WaitingCallout(
                      grievance: waiting,
                      onTap: () => _open(waiting),
                    ),
                  ),
            onRefresh: () =>
                ref.read(myGrievancesControllerProvider.notifier).refresh(),
            onLoadMore: () =>
                ref.read(myGrievancesControllerProvider.notifier).loadMore(),
            emptyTitle: filter == null
                ? 'No grievances yet'
                : 'Nothing with this status',
            emptyMessage: filter == null
                ? 'If something at work is not right, you can tell HR here, '
                      'with your name or anonymously.'
                : 'None of your grievances is in this group.',
            emptyIcon: Icons.forum_outlined,
            emptyActionLabel: filter == null ? 'New grievance' : 'Show all',
            emptyActionIcon: filter == null ? Icons.add_rounded : null,
            onEmptyAction: filter == null
                ? () => context.pushNamed(RouteNames.newGrievance)
                : () => ref
                      .read(grievanceStatusFilterProvider.notifier)
                      .select(null),
            itemBuilder:
                (BuildContext context, GrievanceSummary grievance, int index) =>
                    GrievanceTile(
                      grievance: grievance,
                      onTap: () => _open(grievance),
                    ),
          );
        },
      ),
    );
  }
}

/// The status filters as pills on the navy header, each with its count when
/// known.
class _FilterRow extends StatelessWidget {
  const _FilterRow({
    required this.selected,
    required this.counts,
    required this.onSelect,
  });

  final GrievanceStatus? selected;
  final Map<GrievanceStatus?, int>? counts;
  final ValueChanged<GrievanceStatus?> onSelect;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return SizedBox(
      height: 40,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
        itemCount: MyGrievancesScreen.filters.length,
        separatorBuilder: (BuildContext context, int index) => Gap.wSm,
        itemBuilder: (BuildContext context, int index) {
          final (GrievanceStatus? status, String label) =
              MyGrievancesScreen.filters[index];
          final bool isSelected = status == selected;
          final int? count = counts?[status];
          final Color ink = isSelected ? AppColors.apexNavy : onBrand;

          // The app's chip theme is for light surfaces; on the navy band
          // the unselected chips are glass, so the theme is cleared here.
          return Theme(
            data: Theme.of(context).copyWith(chipTheme: const ChipThemeData()),
            child: ChoiceChip(
              showCheckmark: false,
              selected: isSelected,
              onSelected: (_) => onSelect(status),
              selectedColor: AppColors.surface,
              // Opaque: a chip paints its colour over a white surface, so a
            // translucent glass would come out solid white.
            backgroundColor: Color.alphaBlend(
              onBrand.withValues(alpha: 0.12),
              AppColors.apexNavy,
            ),
              side: BorderSide(
                color: isSelected
                    ? AppColors.surface
                    : onBrand.withValues(alpha: 0.16),
              ),
              shape: const StadiumBorder(),
              padding: EdgeInsets.symmetric(horizontal: AppSpacing.xs),
              label: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    label,
                    style: textStyles.labelMedium?.copyWith(
                      color: ink,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (count != null && count > 0) ...<Widget>[
                    const SizedBox(width: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 6,
                        vertical: 1,
                      ),
                      decoration: BoxDecoration(
                        color: isSelected
                            ? AppColors.apexNavy
                            : onBrand.withValues(alpha: 0.18),
                        borderRadius: BorderRadius.circular(AppRadius.pill),
                      ),
                      child: Text(
                        '$count',
                        style: textStyles.labelSmall?.copyWith(
                          fontSize: 10,
                          color: onBrand,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

/// "HR is waiting for your reply", above the list, for the newest grievance
/// that needs one.
class _WaitingCallout extends StatelessWidget {
  const _WaitingCallout({required this.grievance, required this.onTap});

  final GrievanceSummary grievance;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color tone = palette.warning;

    return Semantics(
      button: true,
      label: 'HR is waiting for your reply on ${grievance.reference}',
      child: ExcludeSemantics(
        child: Material(
          color: palette.warningSurface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.lg),
            side: BorderSide(color: tone.withValues(alpha: 0.35)),
          ),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: onTap,
            child: Padding(
              padding: EdgeInsets.all(AppSpacing.md),
              child: Row(
                children: <Widget>[
                  Container(
                    height: 34,
                    width: 34,
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: BorderRadius.circular(AppRadius.sm + 2),
                    ),
                    child: Icon(
                      Icons.forum_outlined,
                      size: AppSizes.iconMd,
                      color: tone,
                    ),
                  ),
                  Gap.wMd,
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          'HR is waiting for your reply',
                          style: textStyles.labelLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                            color: Color.lerp(tone, Colors.black, 0.35),
                          ),
                        ),
                        Text(
                          '${grievance.reference} · asked '
                          '${DateFormatter.relative(grievance.lastActivity)}',
                          style: textStyles.bodySmall?.copyWith(
                            color: palette.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Icon(
                    Icons.chevron_right_rounded,
                    color: palette.textTertiary,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
