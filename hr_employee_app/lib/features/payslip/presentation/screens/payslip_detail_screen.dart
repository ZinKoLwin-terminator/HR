import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
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

  /// How far the summary card rides up over the header's foot.
  static const double _overlap = 32;

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
    final AuthenticatedUser? user = ref.watch(currentUserProvider);
    void toggleMask() => ref.read(salaryMaskedProvider.notifier).toggle();

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: AsyncValueView<PayslipDetail>(
          value: payslip,
          loadingMessages: const <String>[
            'Fetching the payslip…',
            'Itemising earnings and deductions…',
            'Almost there…',
          ],
          onRetry: () => ref.invalidate(payslipDetailProvider(payslipId)),
          data: (PayslipDetail detail) => Column(
            children: <Widget>[
              Expanded(
                child: ListView(
                  padding: EdgeInsets.zero,
                  children: <Widget>[
                    _Header(
                      detail: detail,
                      isMasked: isMasked,
                      onToggleMask: toggleMask,
                    ),
                    Transform.translate(
                      offset: const Offset(0, -_overlap),
                      child: Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: AppSpacing.lg,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: <Widget>[
                            PayslipSplitCard(
                              detail: detail,
                              isMasked: isMasked,
                            ),
                            if (detail.earnings.isNotEmpty) ...<Widget>[
                              SizedBox(height: AppSpacing.md),
                              SalaryBreakdownSection(
                                title: 'Earnings',
                                components: detail.earnings,
                                total: detail.grossEarnings,
                                currencyCode: detail.currencyCode,
                                isMasked: isMasked,
                                isDeduction: false,
                              ),
                            ],
                            if (detail.deductions.isNotEmpty) ...<Widget>[
                              SizedBox(height: AppSpacing.md),
                              SalaryBreakdownSection(
                                title: 'Deductions',
                                components: detail.deductions,
                                total: detail.totalDeductions,
                                currencyCode: detail.currencyCode,
                                isMasked: isMasked,
                                isDeduction: true,
                              ),
                            ],
                            SizedBox(height: AppSpacing.md),
                            _PeriodCard(
                              detail: detail,
                              employeeId: user?.employeeId,
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              if (detail.isPdfAvailable)
                _DownloadBar(
                  isDownloading: download.isDownloading(detail.id),
                  progress: download.progress,
                  onDownload: () => _openPdf(context, ref, detail),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Navy band: back, the period and pay date, the eye, and net pay large in
/// the middle.
class _Header extends StatelessWidget {
  const _Header({
    required this.detail,
    required this.isMasked,
    required this.onToggleMask,
  });

  final PayslipDetail detail;
  final bool isMasked;
  final VoidCallback onToggleMask;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    final List<String> meta = <String>[
      if (detail.payDate != null)
        'Paid ${DateFormatter.dayMonthYear(detail.payDate!)}',
      if (detail.paidDays != null)
        '${DayCountFormatter.formatNumber(detail.paidDays!)} paid days',
    ];

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.xl + PayslipDetailScreen._overlap,
          ),
          child: Column(
            children: <Widget>[
              Row(
                children: <Widget>[
                  if (Navigator.of(context).canPop()) ...<Widget>[
                    Material(
                      color: onBrand.withValues(alpha: 0.12),
                      shape: CircleBorder(
                        side: BorderSide(
                          color: onBrand.withValues(alpha: 0.16),
                        ),
                      ),
                      clipBehavior: Clip.antiAlias,
                      child: IconButton(
                        onPressed: () => Navigator.of(context).maybePop(),
                        icon: const Icon(
                          Icons.arrow_back_rounded,
                          color: onBrand,
                        ),
                        iconSize: AppSizes.iconMd,
                        tooltip: MaterialLocalizations.of(context)
                            .backButtonTooltip,
                      ),
                    ),
                    Gap.wMd,
                  ],
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          detail.periodLabel ??
                              DateFormatter.monthYear(detail.period),
                          style: textStyles.titleLarge?.copyWith(
                            color: onBrand,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        if (meta.isNotEmpty)
                          Text(
                            meta.join(' · '),
                            style: textStyles.bodySmall?.copyWith(
                              color: onBrand.withValues(alpha: 0.72),
                            ),
                          ),
                      ],
                    ),
                  ),
                  MaskToggleButton(isMasked: isMasked, onPressed: onToggleMask),
                ],
              ),
              Gap.xl,
              Text(
                'Net pay',
                style: textStyles.labelMedium?.copyWith(
                  color: onBrand.withValues(alpha: 0.72),
                ),
              ),
              Gap.xxs,
              Text(
                isMasked
                    ? CurrencyFormatter.masked
                    : CurrencyFormatter.format(
                        detail.netPay,
                        detail.currencyCode,
                      ),
                textAlign: TextAlign.center,
                style: textStyles.headlineMedium?.copyWith(
                  color: onBrand,
                  fontWeight: FontWeight.w800,
                  letterSpacing: isMasked ? 3 : -0.5,
                  fontFeatures: const <FontFeature>[
                    FontFeature.tabularFigures(),
                  ],
                ),
                semanticsLabel: isMasked ? 'Net pay hidden' : null,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Period, paid days, pay date and employee ID as a two-column grid.
///
/// Basic salary is deliberately not repeated here: it is already the first
/// line of the earnings.
class _PeriodCard extends StatelessWidget {
  const _PeriodCard({required this.detail, required this.employeeId});

  final PayslipDetail detail;
  final String? employeeId;

  @override
  Widget build(BuildContext context) {
    final DateTime first = DateTime(detail.year, detail.month);
    final DateTime last = DateTime(detail.year, detail.month + 1, 0);

    final List<(String, String)> facts = <(String, String)>[
      ('Pay period', '${first.day}–${DateFormatter.dayMonthYear(last)}'),
      if (detail.paidDays != null)
        ('Paid days', DayCountFormatter.formatNumber(detail.paidDays!)),
      if (detail.payDate != null)
        ('Pay date', DateFormatter.dayMonthYear(detail.payDate!)),
      if (employeeId != null) ('Employee ID', employeeId!),
      if (detail.lopDays != null && detail.lopDays! > 0)
        ('Unpaid days', DayCountFormatter.formatNumber(detail.lopDays!)),
    ];

    return PayslipCard(
      child: LayoutBuilder(
        builder: (BuildContext context, BoxConstraints constraints) {
          final double cell = (constraints.maxWidth - AppSpacing.md) / 2;
          return Wrap(
            spacing: AppSpacing.md,
            runSpacing: AppSpacing.md,
            children: <Widget>[
              for (final (String label, String value) in facts)
                SizedBox(
                  width: cell,
                  child: _Fact(label: label, value: value),
                ),
            ],
          );
        },
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.label, required this.value});

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
        Text(
          value,
          style: textStyles.labelLarge?.copyWith(fontWeight: FontWeight.w800),
        ),
      ],
    );
  }
}

/// Pinned to the foot of the screen, so the PDF is always one tap away.
class _DownloadBar extends StatelessWidget {
  const _DownloadBar({
    required this.isDownloading,
    required this.progress,
    required this.onDownload,
  });

  final bool isDownloading;

  /// 0 to 1 when the server declared a length, else 0 (shown indeterminate).
  final double progress;

  final VoidCallback onDownload;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: context.palette.border)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: EdgeInsets.all(AppSpacing.md),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              AppButton(
                label: 'Download PDF',
                loadingLabel: 'Downloading PDF…',
                icon: Icons.download_rounded,
                isLoading: isDownloading,
                onPressed: onDownload,
              ),
              if (isDownloading) ...<Widget>[
                Gap.sm,
                _DownloadProgress(progress: progress),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// A slim teal bar under the download button, with the percentage when the
/// size is known.
class _DownloadProgress extends StatelessWidget {
  const _DownloadProgress({required this.progress});

  final double progress;

  @override
  Widget build(BuildContext context) {
    final bool isKnown = progress > 0;

    return Row(
      children: <Widget>[
        Expanded(
          child: ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.pill),
            child: LinearProgressIndicator(
              value: isKnown ? progress : null,
              minHeight: 4,
              backgroundColor: context.palette.surfaceMuted,
              valueColor: const AlwaysStoppedAnimation<Color>(
                AppColors.summitTeal,
              ),
            ),
          ),
        ),
        if (isKnown) ...<Widget>[
          Gap.wSm,
          Text(
            '${(progress * 100).round()}%',
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: context.palette.textSecondary,
              fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
            ),
          ),
        ],
      ],
    );
  }
}
