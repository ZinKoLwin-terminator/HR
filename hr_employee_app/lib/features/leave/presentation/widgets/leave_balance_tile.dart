import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// A full balance card for the leave screen's Balances tab.
///
/// Shows the whole breakdown — entitled, used, pending, remaining — rather
/// than just the remaining figure, because "why is my balance lower than I
/// expected" is the question this screen exists to answer.
class LeaveBalanceTile extends StatelessWidget {
  const LeaveBalanceTile({required this.balance, super.key, this.onApply});

  final LeaveBalance balance;

  /// Offers an apply shortcut for this type. Hidden when there is nothing
  /// left, since the server would reject the application.
  final VoidCallback? onApply;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = ColorUtils.fromHex(
      balance.colorHex,
      fallback: Theme.of(context).colorScheme.primary,
    );
    final double? usedFraction = balance.usedFraction;
    final bool hasRemaining = balance.remainingDays > 0;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                padding: EdgeInsets.all(AppSpacing.sm),
                decoration: BoxDecoration(
                  color: ColorUtils.surfaceTint(
                    accent,
                    isDark: Theme.of(context).brightness == Brightness.dark,
                  ),
                  borderRadius: AppRadius.field,
                ),
                child: Icon(
                  Icons.event_available_outlined,
                  size: AppSizes.iconMd,
                  color: accent,
                ),
              ),
              Gap.wMd,
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      balance.leaveTypeName,
                      style: textStyles.titleSmall,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    Gap.xxs,
                    Text('${balance.year}', style: textStyles.bodySmall),
                  ],
                ),
              ),
              // Apply sits in the header row rather than on a row of its own,
              // which used to add a mostly empty band to every card.
              if (onApply != null && hasRemaining)
                FilledButton.tonalIcon(
                  onPressed: onApply,
                  icon: const Icon(Icons.post_add_rounded, size: 18),
                  label: const Text('Apply'),
                  style: FilledButton.styleFrom(
                    backgroundColor: ColorUtils.surfaceTint(
                      accent,
                      isDark: Theme.of(context).brightness == Brightness.dark,
                    ),
                    foregroundColor: accent,
                    visualDensity: VisualDensity.compact,
                    // The app theme makes filled buttons full-width; inside
                    // this row that would be an infinite width, so size to
                    // the label instead.
                    minimumSize: const Size(0, 36),
                    padding: EdgeInsets.symmetric(horizontal: AppSpacing.md),
                  ),
                ),
            ],
          ),
          Gap.lg,

          // The headline figure, read before anything else on the card.
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                DayCountFormatter.formatNumber(balance.remainingDays),
                style: AppTextStyles.numericLarge.copyWith(
                  color: hasRemaining ? accent : palette.textTertiary,
                  fontSize: 28,
                  height: 1,
                ),
              ),
              Gap.wSm,
              Padding(
                padding: EdgeInsets.only(bottom: AppSpacing.xxs),
                child: Text(
                  balance.entitledDays > 0
                      ? 'of ${DayCountFormatter.format(balance.entitledDays)} left'
                      : '${balance.remainingDays == 1 ? 'day' : 'days'} left',
                  style: textStyles.bodyMedium?.copyWith(
                    color: palette.textSecondary,
                  ),
                ),
              ),
            ],
          ),

          if (usedFraction != null) ...<Widget>[
            Gap.md,
            ClipRRect(
              borderRadius: BorderRadius.circular(AppRadius.pill),
              child: LinearProgressIndicator(
                value: usedFraction,
                minHeight: 8,
                backgroundColor: palette.surfaceMuted,
                valueColor: AlwaysStoppedAnimation<Color>(accent),
              ),
            ),
          ],

          Gap.md,
          Container(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm + AppSpacing.xxs,
            ),
            decoration: BoxDecoration(
              color: palette.surfaceMuted,
              borderRadius: AppRadius.field,
            ),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: _Figure(
                    label: 'Entitled',
                    value: balance.entitledDays,
                    color: palette.textPrimary,
                  ),
                ),
                Expanded(
                  child: _Figure(
                    label: 'Used',
                    value: balance.usedDays,
                    color: palette.textSecondary,
                  ),
                ),
                Expanded(
                  child: _Figure(
                    label: 'Pending',
                    value: balance.pendingDays,
                    color: balance.pendingDays > 0
                        ? palette.warning
                        : palette.textSecondary,
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

class _Figure extends StatelessWidget {
  const _Figure({
    required this.label,
    required this.value,
    required this.color,
  });

  final String label;
  final double value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(label, style: Theme.of(context).textTheme.labelSmall),
        Gap.xxs,
        Text(
          DayCountFormatter.formatNumber(value),
          style: AppTextStyles.numeric.copyWith(color: color, fontSize: 15),
        ),
      ],
    );
  }
}
