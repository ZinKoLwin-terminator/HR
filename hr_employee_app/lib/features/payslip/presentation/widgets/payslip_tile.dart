import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/context_extensions.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// One payslip row.
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

    return AppCard(
      onTap: onTap,
      child: Row(
        children: <Widget>[
          Container(
            padding: EdgeInsets.all(AppSpacing.md),
            // Brand-tinted like the section icons elsewhere, rather than a
            // grey plate that left the list looking colourless.
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.primary.withValues(
                alpha: context.isDarkMode ? 0.22 : 0.1,
              ),
              borderRadius: AppRadius.field,
            ),
            child: Icon(
              Icons.receipt_long_outlined,
              size: AppSizes.iconLg,
              color: Theme.of(context).colorScheme.primary,
            ),
          ),
          Gap.wMd,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  payslip.periodLabel ??
                      DateFormatter.monthYear(payslip.period),
                  style: textStyles.titleSmall,
                ),
                Gap.xxs,
                Text(
                  payslip.payDate == null
                      ? 'Pay date not published'
                      : 'Paid ${DateFormatter.dayMonthYear(payslip.payDate!)}',
                  style: textStyles.bodySmall,
                ),
                Gap.xs,
                Text(
                  CurrencyFormatter.formatMaskable(
                    payslip.netPay,
                    payslip.currencyCode,
                    isMasked: isMasked,
                  ),
                  style: AppTextStyles.numeric.copyWith(
                    color: isMasked
                        ? palette.textTertiary
                        : palette.textPrimary,
                  ),
                  // Keeps the row readable for a screen reader without
                  // announcing a masked value as dots.
                  semanticsLabel: isMasked
                      ? 'Net pay hidden'
                      : 'Net pay ${CurrencyFormatter.format(payslip.netPay, payslip.currencyCode)}',
                ),
              ],
            ),
          ),
          if (payslip.isPdfAvailable)
            IconButton(
              onPressed: isDownloading ? null : onDownload,
              tooltip: 'Open PDF',
              icon: isDownloading
                  ? const SizedBox.square(
                      dimension: 20,
                      child: CircularProgressIndicator(strokeWidth: 2.2),
                    )
                  : Icon(
                      Icons.picture_as_pdf_outlined,
                      color: Theme.of(context).colorScheme.primary,
                    ),
            ),
        ],
      ),
    );
  }
}
