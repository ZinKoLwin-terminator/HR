import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// The summary grid under the header: leave left on the left, spanning the
/// height of the two tiles stacked on the right — leave awaiting approval
/// and payslips.
class DashboardBento extends StatelessWidget {
  const DashboardBento({
    required this.balances,
    required this.totalRemainingDays,
    required this.maxVisibleBalances,
    required this.onLeaveTap,
    required this.onPayslipsTap,
    super.key,
  });

  final List<LeaveBalance> balances;

  /// Days left across every entitlement, including any this card truncates.
  final double totalRemainingDays;

  final int maxVisibleBalances;
  final VoidCallback onLeaveTap;
  final VoidCallback onPayslipsTap;

  @override
  Widget build(BuildContext context) {
    final double pendingDays = balances.fold<double>(
      0,
      (double sum, LeaveBalance balance) => sum + balance.pendingDays,
    );

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            flex: 11,
            child: _LeaveLeftCard(
              balances: balances.take(maxVisibleBalances).toList(),
              totalRemainingDays: totalRemainingDays,
              onTap: onLeaveTap,
            ),
          ),
          SizedBox(width: AppSpacing.md),
          Expanded(
            flex: 10,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                Expanded(
                  child: _TintTile(
                    icon: Icons.schedule_rounded,
                    tint: AppColors.warning,
                    surface: context.palette.warningSurface,
                    headline: pendingDays > 0
                        ? DayCountFormatter.format(pendingDays)
                        : 'All clear',
                    caption: pendingDays > 0
                        ? 'Awaiting approval'
                        : 'Nothing pending',
                    onTap: onLeaveTap,
                  ),
                ),
                SizedBox(height: AppSpacing.md),
                Expanded(
                  child: _TintTile(
                    icon: Icons.receipt_long_rounded,
                    tint: AppColors.summitTeal,
                    // Opaque: a translucent wash goes grey over the page.
                    surface: Color.alphaBlend(
                      AppColors.summitTeal.withValues(alpha: 0.10),
                      AppColors.surface,
                    ),
                    headline: 'Payslips',
                    caption: 'View & download',
                    onTap: onPayslipsTap,
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

/// White card: total days left, then one thin bar per leave type.
class _LeaveLeftCard extends StatelessWidget {
  const _LeaveLeftCard({
    required this.balances,
    required this.totalRemainingDays,
    required this.onTap,
  });

  final List<LeaveBalance> balances;
  final double totalRemainingDays;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;

    return _CardSurface(
      color: AppColors.surface,
      onTap: onTap,
      semanticsLabel:
          '${DayCountFormatter.format(totalRemainingDays)} of leave left',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Leave left',
                  style: textStyles.labelMedium?.copyWith(
                    color: palette.textSecondary,
                  ),
                ),
              ),
              Icon(
                Icons.north_east_rounded,
                size: AppSizes.iconSm,
                color: palette.textTertiary,
              ),
            ],
          ),
          Gap.sm,
          Text.rich(
            TextSpan(
              children: <InlineSpan>[
                TextSpan(
                  text: DayCountFormatter.formatNumber(totalRemainingDays),
                  style: textStyles.headlineLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                    letterSpacing: -0.5,
                    color: palette.textPrimary,
                  ),
                ),
                TextSpan(
                  text: '  days',
                  style: textStyles.bodySmall?.copyWith(
                    color: palette.textTertiary,
                  ),
                ),
              ],
            ),
          ),
          const Spacer(),
          Gap.md,
          if (balances.isEmpty)
            Text(
              'No leave types set up yet.',
              style: textStyles.bodySmall,
            )
          else
            for (int i = 0; i < balances.length; i++) ...<Widget>[
              _BalanceBar(balance: balances[i]),
              if (i < balances.length - 1) Gap.sm,
            ],
        ],
      ),
    );
  }
}

/// Name and remaining / entitled on one line, a thin bar of what is left
/// below it.
class _BalanceBar extends StatelessWidget {
  const _BalanceBar({required this.balance});

  final LeaveBalance balance;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;
    final Color accent = ColorUtils.fromHex(
      balance.colorHex,
      fallback: AppColors.summitTeal,
    );
    final bool hasQuota = balance.entitledDays > 0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                // "Annual Leave" → "Annual": the card is too narrow for the
                // suffix, and every row would repeat it.
                balance.leaveTypeName.replaceAll(
                  RegExp(r'\s+leave$', caseSensitive: false),
                  '',
                ),
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textPrimary,
                  fontWeight: FontWeight.w600,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            Text(
              hasQuota
                  ? '${DayCountFormatter.formatNumber(balance.remainingDays)}'
                        ' / ${DayCountFormatter.formatNumber(balance.entitledDays)}'
                  : DayCountFormatter.formatNumber(balance.remainingDays),
              style: textStyles.labelSmall?.copyWith(
                color: palette.textTertiary,
              ),
            ),
          ],
        ),
        // No bar without a quota: an empty bar would imply one.
        if (hasQuota) ...<Widget>[
          const SizedBox(height: 4),
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.pill),
            child: LinearProgressIndicator(
              value: (balance.remainingDays / balance.entitledDays).clamp(
                0.0,
                1.0,
              ),
              minHeight: 4,
              backgroundColor: palette.surfaceMuted,
              valueColor: AlwaysStoppedAnimation<Color>(accent),
            ),
          ),
        ],
      ],
    );
  }
}

/// A tinted tile: icon chip, a bold headline and a caption.
class _TintTile extends StatelessWidget {
  const _TintTile({
    required this.icon,
    required this.tint,
    required this.surface,
    required this.headline,
    required this.caption,
    required this.onTap,
  });

  final IconData icon;
  final Color tint;
  final Color surface;
  final String headline;
  final String caption;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return _CardSurface(
      color: surface,
      onTap: onTap,
      semanticsLabel: '$headline, $caption',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            height: 32,
            width: 32,
            decoration: BoxDecoration(
              color: AppColors.surface.withValues(alpha: 0.75),
              borderRadius: BorderRadius.circular(AppRadius.sm + 2),
            ),
            child: Icon(icon, size: AppSizes.iconSm + 2, color: tint),
          ),
          const Spacer(),
          Gap.sm,
          Text(
            headline,
            style: textStyles.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
              color: context.palette.textPrimary,
            ),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
          Text(
            caption,
            style: textStyles.labelSmall?.copyWith(
              color: Color.lerp(tint, Colors.black, 0.25),
              fontWeight: FontWeight.w600,
            ),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}

/// Rounded, softly raised, tappable surface shared by the bento cards.
class _CardSurface extends StatelessWidget {
  const _CardSurface({
    required this.color,
    required this.onTap,
    required this.semanticsLabel,
    required this.child,
  });

  final Color color;
  final VoidCallback onTap;
  final String semanticsLabel;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final BorderRadius radius = BorderRadius.circular(AppRadius.xl);

    return Semantics(
      button: true,
      label: semanticsLabel,
      child: ExcludeSemantics(
        child: DecoratedBox(
          decoration: BoxDecoration(
            borderRadius: radius,
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: AppColors.shadow,
                blurRadius: 16,
                offset: Offset(0, 4),
              ),
            ],
          ),
          child: Material(
            color: color,
            borderRadius: radius,
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.lg),
                child: child,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
