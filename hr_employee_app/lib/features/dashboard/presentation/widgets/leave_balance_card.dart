import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// Leave balances, most depleted first.
///
/// The ordering is decided by `GetDashboardSummaryUseCase`, not here — this
/// widget renders whatever order it is given.
class LeaveBalanceCard extends StatelessWidget {
  const LeaveBalanceCard({
    required this.balances,
    required this.totalRemainingDays,
    required this.onSeeAll,
    super.key,
    this.maxVisible = 4,
  });

  final List<LeaveBalance> balances;

  /// Days left across every entitlement, including the ones this card
  /// truncates. Passed in rather than summed here: the figure is the
  /// domain's to compute, and the card only ever sees part of the list.
  final double totalRemainingDays;

  final VoidCallback onSeeAll;

  /// How many rows to show before the list is truncated.
  final int maxVisible;

  @override
  Widget build(BuildContext context) {
    if (balances.isEmpty) {
      return const AppCard(
        child: AppEmptyView(
          title: 'No leave entitlements',
          message: 'Your leave types have not been set up yet.',
          icon: Icons.event_busy_outlined,
          isCompact: true,
        ),
      );
    }

    final List<LeaveBalance> visible = balances.take(maxVisible).toList();
    final int hidden = balances.length - visible.length;

    return AppSectionCard(
      title: 'Leave balance',
      subtitle: '${DayCountFormatter.format(totalRemainingDays)} left in total',
      icon: Icons.event_available_rounded,
      action: TextButton(onPressed: onSeeAll, child: const Text('See all')),
      child: Column(
        children: <Widget>[
          for (int i = 0; i < visible.length; i++) ...<Widget>[
            _BalanceRow(balance: visible[i]),
            if (i < visible.length - 1) Gap.md,
          ],
          if (hidden > 0) ...<Widget>[
            Gap.md,
            Align(
              alignment: Alignment.centerLeft,
              child: Text(
                '+ $hidden more',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// One leave type: name, remaining days and a usage bar.
class _BalanceRow extends StatelessWidget {
  const _BalanceRow({required this.balance});

  final LeaveBalance balance;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = ColorUtils.fromHex(
      balance.colorHex,
      fallback: Theme.of(context).colorScheme.primary,
    );

    // Null when there is no entitlement; the bar is omitted rather than drawn
    // empty, which would imply a quota that does not exist.
    final double? usedFraction = balance.usedFraction;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Container(
              height: 10,
              width: 10,
              decoration: BoxDecoration(color: accent, shape: BoxShape.circle),
            ),
            Gap.wSm,
            Expanded(
              child: Text(
                balance.leaveTypeName,
                style: textStyles.titleSmall,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            Gap.wSm,
            Text.rich(
              TextSpan(
                children: <InlineSpan>[
                  TextSpan(
                    text: DayCountFormatter.formatNumber(balance.remainingDays),
                    style: AppTextStyles.numeric.copyWith(color: accent),
                  ),
                  TextSpan(
                    text: balance.entitledDays > 0
                        ? ' / ${DayCountFormatter.formatNumber(balance.entitledDays)}'
                        : '',
                    style: textStyles.bodySmall,
                  ),
                ],
              ),
              semanticsLabel:
                  '${DayCountFormatter.format(balance.remainingDays)} '
                  'remaining of ${balance.leaveTypeName}',
            ),
          ],
        ),
        if (usedFraction != null) ...<Widget>[
          Gap.sm,
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.pill),
            child: LinearProgressIndicator(
              value: usedFraction,
              minHeight: 6,
              backgroundColor: palette.surfaceMuted,
              valueColor: AlwaysStoppedAnimation<Color>(accent),
            ),
          ),
        ],
        if (balance.pendingDays > 0) ...<Widget>[
          Gap.xs,
          Text(
            '${DayCountFormatter.format(balance.pendingDays)} awaiting '
            'approval',
            style: textStyles.bodySmall?.copyWith(color: palette.warning),
          ),
        ],
      ],
    );
  }
}
