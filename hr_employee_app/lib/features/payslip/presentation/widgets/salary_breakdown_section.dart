import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// Earnings or deductions, itemised with a total.
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

  /// Renders amounts as negative and in the danger colour.
  final bool isDeduction;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color tint = isDeduction ? palette.danger : palette.success;

    return AppSectionCard(
      title: title,
      icon: isDeduction
          ? Icons.remove_circle_outline_rounded
          : Icons.add_circle_outline_rounded,
      child: Column(
        children: <Widget>[
          for (final PayslipComponent component in components)
            _ComponentRow(
              component: component,
              currencyCode: currencyCode,
              isMasked: isMasked,
              isDeduction: isDeduction,
            ),
          Divider(height: AppSpacing.lg, color: palette.border),
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Total $title',
                  style: Theme.of(context).textTheme.titleSmall,
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
                style: AppTextStyles.numeric.copyWith(
                  color: isMasked ? palette.textTertiary : tint,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

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
      padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(component.label, style: textStyles.bodyMedium),
                // The payroll code is shown small beside the label, because
                // it is what appears on HR's own reports.
                if (component.code != null)
                  Text(
                    component.code!,
                    style: textStyles.labelSmall?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
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
            style: AppTextStyles.numeric.copyWith(
              fontSize: 15,
              color: isMasked ? palette.textTertiary : palette.textPrimary,
            ),
          ),
        ],
      ),
    );
  }
}

/// The headline net-pay card at the top of the detail screen.
class NetPayCard extends StatelessWidget {
  const NetPayCard({
    required this.detail,
    required this.isMasked,
    required this.onToggleMask,
    super.key,
  });

  final PayslipDetail detail;
  final bool isMasked;
  final VoidCallback onToggleMask;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(child: Text('Net pay', style: textStyles.bodyMedium)),
              IconButton(
                onPressed: onToggleMask,
                tooltip: isMasked ? 'Show amounts' : 'Hide amounts',
                icon: Icon(
                  isMasked
                      ? Icons.visibility_outlined
                      : Icons.visibility_off_outlined,
                ),
              ),
            ],
          ),
          Text(
            isMasked
                ? CurrencyFormatter.masked
                : CurrencyFormatter.format(detail.netPay, detail.currencyCode),
            style: AppTextStyles.numericLarge.copyWith(
              color: isMasked
                  ? palette.textTertiary
                  : Theme.of(context).colorScheme.primary,
            ),
          ),
          Gap.sm,
          Row(
            children: <Widget>[
              Expanded(
                child: _MiniStat(
                  label: 'Gross',
                  value: isMasked
                      ? CurrencyFormatter.masked
                      : CurrencyFormatter.format(
                          detail.grossEarnings,
                          detail.currencyCode,
                        ),
                ),
              ),
              Expanded(
                child: _MiniStat(
                  label: 'Deductions',
                  value: isMasked
                      ? CurrencyFormatter.masked
                      : CurrencyFormatter.format(
                          detail.totalDeductions,
                          detail.currencyCode,
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

class _MiniStat extends StatelessWidget {
  const _MiniStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(label, style: Theme.of(context).textTheme.labelSmall),
        Gap.xxs,
        Text(value, style: AppTextStyles.numeric.copyWith(fontSize: 15)),
      ],
    );
  }
}
