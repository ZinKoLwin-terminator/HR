import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_controllers.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_providers.dart';
import 'package:hr_employee_app/features/payslip/presentation/widgets/salary_breakdown_section.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// The full salary breakdown for one pay period.
///
/// Nothing here is written to disk. The only file this screen can produce is
/// the PDF, which goes to app-private storage — see `PayslipRepositoryImpl`.
class PayslipDetailScreen extends ConsumerWidget {
  const PayslipDetailScreen({required this.payslipId, super.key});

  final String payslipId;

  Future<void> _openPdf(
    BuildContext context,
    WidgetRef ref,
    PayslipDetail detail,
  ) async {
    // The download use case takes a summary; build one from the detail
    // rather than refetching the list to find the matching row.
    final PayslipSummary summary = PayslipSummary(
      id: detail.id,
      year: detail.year,
      month: detail.month,
      netPay: detail.netPay,
      currencyCode: detail.currencyCode,
      periodLabel: detail.periodLabel,
      payDate: detail.payDate,
      isPdfAvailable: detail.isPdfAvailable,
    );

    final bool opened = await ref
        .read(payslipDownloadControllerProvider.notifier)
        .openPdf(summary);

    if (!context.mounted || opened) {
      return;
    }
    final Failure? failure = ref
        .read(payslipDownloadControllerProvider)
        .submission
        .failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<PayslipDetail> payslip = ref.watch(
      payslipDetailProvider(payslipId),
    );
    final bool isMasked = ref.watch(salaryMaskedProvider);
    final PayslipDownloadState download = ref.watch(
      payslipDownloadControllerProvider,
    );

    return Scaffold(
      appBar: const BrandAppBar(title: Text('Payslip')),
      body: SafeArea(
        child: AsyncValueView<PayslipDetail>(
          value: payslip,
          onRetry: () => ref.invalidate(payslipDetailProvider(payslipId)),
          data: (PayslipDetail detail) => ListView(
            padding: AppSpacing.screenPadding,
            children: <Widget>[
              _PeriodHeader(detail: detail),
              SizedBox(height: AppSpacing.cardGap),

              NetPayCard(
                detail: detail,
                isMasked: isMasked,
                onToggleMask: () =>
                    ref.read(salaryMaskedProvider.notifier).toggle(),
              ),
              SizedBox(height: AppSpacing.cardGap),

              if (detail.earnings.isNotEmpty) ...<Widget>[
                SalaryBreakdownSection(
                  title: 'Earnings',
                  components: detail.earnings,
                  total: detail.grossEarnings,
                  currencyCode: detail.currencyCode,
                  isMasked: isMasked,
                  isDeduction: false,
                ),
                SizedBox(height: AppSpacing.cardGap),
              ],

              if (detail.deductions.isNotEmpty) ...<Widget>[
                SalaryBreakdownSection(
                  title: 'Deductions',
                  components: detail.deductions,
                  total: detail.totalDeductions,
                  currencyCode: detail.currencyCode,
                  isMasked: isMasked,
                  isDeduction: true,
                ),
                SizedBox(height: AppSpacing.cardGap),
              ],

              _AttendanceCard(detail: detail, isMasked: isMasked),

              if (detail.isPdfAvailable) ...<Widget>[
                SizedBox(height: AppSpacing.xxl),
                AppButton.secondary(
                  label: 'Open PDF',
                  icon: Icons.picture_as_pdf_outlined,
                  isLoading: download.isDownloading(detail.id),
                  onPressed: () => _openPdf(context, ref, detail),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _PeriodHeader extends StatelessWidget {
  const _PeriodHeader({required this.detail});

  final PayslipDetail detail;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            detail.periodLabel ?? DateFormatter.monthYear(detail.period),
            style: textStyles.titleLarge,
          ),
          if (detail.payDate != null) ...<Widget>[
            Gap.xxs,
            Text(
              'Paid on ${DateFormatter.dayMonthYear(detail.payDate!)}',
              style: textStyles.bodySmall,
            ),
          ],
        ],
      ),
    );
  }
}

/// Paid days and loss-of-pay days, which explain a smaller-than-usual net.
class _AttendanceCard extends StatelessWidget {
  const _AttendanceCard({required this.detail, required this.isMasked});

  final PayslipDetail detail;

  /// Day counts are not sensitive, but the basic salary is, so it follows
  /// the same mask as every other amount on the screen.
  final bool isMasked;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Pay period',
      icon: Icons.event_note_outlined,
      child: Column(
        children: <Widget>[
          AppInfoRow(
            label: 'Basic salary',
            value: CurrencyFormatter.formatMaskable(
              detail.basicSalary,
              detail.currencyCode,
              isMasked: isMasked,
            ),
          ),
          if (detail.paidDays != null)
            AppInfoRow(
              label: 'Paid days',
              value: DayCountFormatter.formatNumber(detail.paidDays!),
            ),
          if (detail.lopDays != null && detail.lopDays! > 0)
            AppInfoRow(
              label: 'Unpaid days',
              value: DayCountFormatter.formatNumber(detail.lopDays!),
            ),
        ],
      ),
    );
  }
}
