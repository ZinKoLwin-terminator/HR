import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// One payslip as a row inside the grouped "Earlier" card: a month block,
/// the period and pay date, the net pay, and a download action.
///
/// The net pay is masked unless the user has revealed amounts; see
/// `salaryMaskedProvider` for why that is the default.
class PayslipTile extends StatelessWidget {
  const PayslipTile({
    required this.payslip,
    required this.isMasked,
    required this.onTap,
    required this.onDownload,
    super.key,
    this.isDownloading = false,
  });

  final PayslipSummary payslip;
  final bool isMasked;
  final VoidCallback onTap;
  final VoidCallback onDownload;

  /// True only for the row whose PDF is being fetched.
  final bool isDownloading;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          AppSpacing.md,
          AppSpacing.sm + 2,
          AppSpacing.xs,
          AppSpacing.sm + 2,
        ),
        child: Row(
          children: <Widget>[
            PayslipMonthBlock(period: payslip.period),
            Gap.wMd,
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    payslip.periodLabel ??
                        DateFormatter.monthYear(payslip.period),
                    style: textStyles.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    payslip.payDate == null
                        ? 'Pay date not published'
                        : 'Paid ${DateFormatter.dayMonthYear(payslip.payDate!)}',
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
                ],
              ),
            ),
            Gap.wSm,
            Text(
              CurrencyFormatter.formatMaskable(
                payslip.netPay,
                payslip.currencyCode,
                isMasked: isMasked,
              ),
              style: textStyles.labelLarge?.copyWith(
                color: isMasked ? palette.textTertiary : palette.textPrimary,
                fontWeight: FontWeight.w700,
                fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
              ),
              // Keeps the row readable for a screen reader without
              // announcing a masked value as dots.
              semanticsLabel: isMasked
                  ? 'Net pay hidden'
                  : 'Net pay ${CurrencyFormatter.format(payslip.netPay, payslip.currencyCode)}',
            ),
            if (payslip.isPdfAvailable)
              IconButton(
                onPressed: isDownloading ? null : onDownload,
                tooltip: 'Download PDF',
                icon: isDownloading
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Icon(
                        Icons.download_rounded,
                        size: AppSizes.iconMd,
                        color: palette.textPrimary,
                      ),
              )
            else
              SizedBox(width: AppSpacing.md),
          ],
        ),
      ),
    );
  }
}

/// `SEP / 2026` in a muted rounded block.
class PayslipMonthBlock extends StatelessWidget {
  const PayslipMonthBlock({required this.period, super.key});

  final DateTime period;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Container(
      width: 44,
      padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
      decoration: BoxDecoration(
        color: AppColors.primarySurface,
        borderRadius: AppRadius.field,
      ),
      child: Column(
        children: <Widget>[
          Text(
            DateFormatter.shortMonthYear(period).split(' ').first.toUpperCase(),
            style: textStyles.labelSmall?.copyWith(
              fontSize: 9,
              letterSpacing: 0.6,
              fontWeight: FontWeight.w800,
              color: AppColors.apexNavy,
              height: 1.2,
            ),
          ),
          Text(
            '${period.year}',
            style: textStyles.labelSmall?.copyWith(
              fontSize: 10,
              color: palette.textSecondary,
              height: 1.2,
            ),
          ),
        ],
      ),
    );
  }
}
