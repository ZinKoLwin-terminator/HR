import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// Earnings or deductions, itemised, with the total on a tinted row —
/// teal for earnings, amber for deductions.
class SalaryBreakdownSection extends StatelessWidget {
  const SalaryBreakdownSection({
    required this.title,
    required this.components,
    required this.total,
    required this.currencyCode,
    required this.isMasked,
    required this.isDeduction,
    super.key,
  });

  final String title;
  final List<PayslipComponent> components;
  final double total;
  final String currencyCode;
  final bool isMasked;

  /// Amber and a minus sign rather than teal and a plus.
  final bool isDeduction;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color tint = isDeduction ? palette.warning : AppColors.summitTeal;
    final Color totalInk = Color.lerp(tint, Colors.black, 0.3)!;
    final Color totalSurface = isDeduction
        ? palette.warningSurface
        : Color.alphaBlend(
            AppColors.summitTeal.withValues(alpha: 0.08),
            AppColors.surface,
          );

    return PayslipCard(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.lg,
              AppSpacing.lg,
              AppSpacing.xs,
            ),
            child: Row(
              children: <Widget>[
                Container(
                  height: 30,
                  width: 30,
                  decoration: BoxDecoration(
                    color: tint.withValues(alpha: 0.14),
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  child: Icon(
                    isDeduction ? Icons.remove_rounded : Icons.add_rounded,
                    size: 18,
                    color: totalInk,
                  ),
                ),
                Gap.wMd,
                Text(
                  title,
                  style: textStyles.titleSmall?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
          ),
          for (final PayslipComponent component in components)
            _ComponentRow(
              component: component,
              currencyCode: currencyCode,
              isMasked: isMasked,
              isDeduction: isDeduction,
            ),
          Gap.xs,
          Container(
            color: totalSurface,
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.md,
            ),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    'Total ${title.toLowerCase()}',
                    style: textStyles.labelLarge?.copyWith(
                      color: totalInk,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                Text(
                  isMasked
                      ? CurrencyFormatter.masked
                      : CurrencyFormatter.formatSigned(
                          total,
                          currencyCode,
                          isNegative: isDeduction,
                        ),
                  style: textStyles.labelLarge?.copyWith(
                    color: totalInk,
                    fontWeight: FontWeight.w800,
                    fontFeatures: const <FontFeature>[
                      FontFeature.tabularFigures(),
                    ],
                  ),
                  semanticsLabel: isMasked ? 'Amount hidden' : null,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// One line item: label, its payroll code as a small tag, and the amount.
class _ComponentRow extends StatelessWidget {
  const _ComponentRow({
    required this.component,
    required this.currencyCode,
    required this.isMasked,
    required this.isDeduction,
  });

  final PayslipComponent component;
  final String currencyCode;
  final bool isMasked;
  final bool isDeduction;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Padding(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.sm,
      ),
      child: Row(
        children: <Widget>[
          // Label and code share the left; the amount keeps its natural
          // width on the right, so amounts line up down the card.
          Expanded(
            child: Row(
              children: <Widget>[
                Flexible(
                  child: Text(
                    component.label,
                    style: textStyles.bodyMedium?.copyWith(
                      color: palette.textPrimary,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                // The payroll code, as it appears on HR's own reports.
                if (component.code != null) ...<Widget>[
                  Gap.wSm,
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 1,
                    ),
                    decoration: BoxDecoration(
                      color: palette.surfaceMuted,
                      borderRadius: BorderRadius.circular(5),
                    ),
                    child: Text(
                      component.code!,
                      style: textStyles.labelSmall?.copyWith(
                        fontSize: 9,
                        letterSpacing: 0.4,
                        fontWeight: FontWeight.w700,
                        color: palette.textSecondary,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
          Gap.wMd,
          Text(
            isMasked
                ? CurrencyFormatter.masked
                : CurrencyFormatter.formatSigned(
                    component.amount,
                    currencyCode,
                    isNegative: isDeduction,
                  ),
            style: textStyles.bodyMedium?.copyWith(
              color: isMasked ? palette.textTertiary : palette.textPrimary,
              fontWeight: FontWeight.w600,
              fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
            ),
            semanticsLabel: isMasked ? 'Amount hidden' : null,
          ),
        ],
      ),
    );
  }
}

/// Take-home against deductions as one split bar, with gross and
/// deductions below.
///
/// The split is shown even while amounts are masked: a percentage reveals
/// proportion, not pay.
class PayslipSplitCard extends StatelessWidget {
  const PayslipSplitCard({
    required this.detail,
    required this.isMasked,
    super.key,
  });

  final PayslipDetail detail;
  final bool isMasked;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final double gross = detail.grossEarnings;
    final double deductionShare = gross <= 0
        ? 0
        : (detail.totalDeductions / gross).clamp(0.0, 1.0);
    final double takeHomeShare = 1 - deductionShare;

    String percent(double share) =>
        '${(share * 100).toStringAsFixed(share * 100 >= 10 ? 0 : 1)}%';

    return PayslipCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.pill),
            child: SizedBox(
              height: 8,
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Expanded(
                    flex: (takeHomeShare * 1000).round(),
                    child: const ColoredBox(color: AppColors.summitTeal),
                  ),
                  if (deductionShare > 0)
                    Expanded(
                      flex: (deductionShare * 1000).round().clamp(8, 1000),
                      child: ColoredBox(color: palette.warning),
                    ),
                ],
              ),
            ),
          ),
          Gap.sm,
          Row(
            children: <Widget>[
              _Legend(
                color: AppColors.summitTeal,
                label: 'Take-home ${percent(takeHomeShare)}',
              ),
              Gap.wLg,
              _Legend(
                color: palette.warning,
                label: 'Deductions ${percent(deductionShare)}',
              ),
            ],
          ),
          Gap.md,
          const Divider(height: 1, color: AppColors.divider),
          Gap.md,
          Row(
            children: <Widget>[
              Expanded(
                child: _Figure(
                  label: 'Gross earnings',
                  value: CurrencyFormatter.formatMaskable(
                    gross,
                    detail.currencyCode,
                    isMasked: isMasked,
                  ),
                ),
              ),
              Expanded(
                child: _Figure(
                  label: 'Deductions',
                  value: CurrencyFormatter.formatMaskable(
                    detail.totalDeductions,
                    detail.currencyCode,
                    isMasked: isMasked,
                  ),
                ),
              ),
            ],
          ),

          // Shown only when the server's totals do not reconcile with its
          // own line items. Better to say so than to render a breakdown that
          // silently contradicts the headline figure.
          if (!detail.isInternallyConsistent) ...<Widget>[
            Gap.md,
            Container(
              padding: EdgeInsets.all(AppSpacing.sm),
              decoration: BoxDecoration(
                color: palette.warningSurface,
                borderRadius: AppRadius.field,
              ),
              child: Row(
                children: <Widget>[
                  Icon(
                    Icons.warning_amber_rounded,
                    size: AppSizes.iconSm,
                    color: palette.warning,
                  ),
                  Gap.wSm,
                  Expanded(
                    child: Text(
                      'The line items below do not add up to these totals. '
                      'Contact HR if the figures look wrong.',
                      style: textStyles.labelSmall?.copyWith(
                        color: palette.warning,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _Legend extends StatelessWidget {
  const _Legend({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          height: 8,
          width: 8,
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        Gap.wXs,
        Text(
          label,
          style: Theme.of(context).textTheme.labelSmall
              ?.copyWith(color: context.palette.textSecondary),
        ),
      ],
    );
  }
}

class _Figure extends StatelessWidget {
  const _Figure({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: textStyles.labelSmall?.copyWith(
            color: context.palette.textSecondary,
          ),
        ),
        Gap.xxs,
        Text(
          value,
          style: textStyles.titleSmall?.copyWith(
            fontWeight: FontWeight.w800,
            fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
          ),
        ),
      ],
    );
  }
}

/// White, softly raised card used across the payslip screens.
class PayslipCard extends StatelessWidget {
  const PayslipCard({required this.child, super.key, this.padding});

  final Widget child;

  /// Defaults to `AppSpacing.lg` all round.
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    final BorderRadius radius = BorderRadius.circular(AppRadius.xl);

    return Container(
      padding: padding ?? EdgeInsets.all(AppSpacing.lg),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: radius,
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadow,
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: child,
    );
  }
}

/// The eye in a glass circle, for the navy headers of both payslip screens.
class MaskToggleButton extends StatelessWidget {
  const MaskToggleButton({
    required this.isMasked,
    required this.onPressed,
    super.key,
  });

  final bool isMasked;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Material(
      color: onBrand.withValues(alpha: 0.12),
      shape: CircleBorder(
        side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
      ),
      clipBehavior: Clip.antiAlias,
      child: IconButton(
        onPressed: onPressed,
        tooltip: isMasked ? 'Show amounts' : 'Hide amounts',
        iconSize: AppSizes.iconMd,
        icon: Icon(
          isMasked ? Icons.visibility_outlined : Icons.visibility_off_outlined,
          color: onBrand,
        ),
      ),
    );
  }
}
