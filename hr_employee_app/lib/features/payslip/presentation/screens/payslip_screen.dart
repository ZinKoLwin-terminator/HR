import 'dart:math' as math;

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_controllers.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_providers.dart';
import 'package:hr_employee_app/features/payslip/presentation/widgets/payslip_tile.dart';
import 'package:hr_employee_app/features/payslip/presentation/widgets/salary_breakdown_section.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// The payslip list: the latest payslip as a headline card on the navy
/// header, the rest of the chosen year grouped in one card below.
///
/// Amounts are masked by default; the eye reveals them on this screen and
/// the detail screen alike. See `salaryMaskedProvider` for the reasoning.
class PayslipScreen extends ConsumerStatefulWidget {
  const PayslipScreen({super.key});

  @override
  ConsumerState<PayslipScreen> createState() => _PayslipScreenState();
}

class _PayslipScreenState extends ConsumerState<PayslipScreen> {
  /// The year being shown. Null means "the year of the newest payslip".
  int? _year;

  Future<void> _openPdf(PayslipSummary payslip) async {
    final bool opened = await ref
        .read(payslipDownloadControllerProvider.notifier)
        .openPdf(payslip);

    if (!mounted || opened) {
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

  void _openDetail(PayslipSummary payslip) {
    context.pushNamed(
      RouteNames.payslipDetail,
      pathParameters: <String, String>{RouteParams.payslipId: payslip.id},
    );
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<PaginatedState<PayslipSummary>> payslips = ref.watch(
      payslipsControllerProvider,
    );
    final bool isMasked = ref.watch(salaryMaskedProvider);

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: AsyncValueView<PaginatedState<PayslipSummary>>(
          value: payslips,
          loadingMessages: const <String>[
            'Fetching your payslips…',
            'Sorting them by month…',
            'Almost there…',
          ],
          loading: const PaginatedListSkeleton(lines: 3),
          onRetry: () => ref.invalidate(payslipsControllerProvider),
          data: (PaginatedState<PayslipSummary> state) =>
              _buildBody(state, isMasked: isMasked),
        ),
      ),
    );
  }

  Widget _buildBody(
    PaginatedState<PayslipSummary> state, {
    required bool isMasked,
  }) {
    final List<int> years =
        state.items.map((PayslipSummary p) => p.year).toSet().toList()
          ..sort((int a, int b) => b.compareTo(a));
    final int? year = years.contains(_year)
        ? _year
        : (years.isEmpty ? null : years.first);

    final List<PayslipSummary> ofYear =
        state.items.where((PayslipSummary p) => p.year == year).toList()..sort(
          (PayslipSummary a, PayslipSummary b) => b.period.compareTo(a.period),
        );
    final PayslipSummary? latest = ofYear.firstOrNull;
    final List<PayslipSummary> earlier = ofYear.skip(1).toList();
    final PayslipDownloadState download = ref.watch(
      payslipDownloadControllerProvider,
    );

    return RefreshIndicator(
      onRefresh: () => ref.read(payslipsControllerProvider.notifier).refresh(),
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.only(bottom: AppSpacing.xxxl),
        children: <Widget>[
          _Header(
            years: years,
            year: year,
            isMasked: isMasked,
            onYearChanged: (int value) => setState(() => _year = value),
            onToggleMask: () =>
                ref.read(salaryMaskedProvider.notifier).toggle(),
            latest: latest == null
                ? null
                : _LatestCard(
                    payslip: latest,
                    isMasked: isMasked,
                    isDownloading: download.isDownloading(latest.id),
                    onView: () => _openDetail(latest),
                    onDownload: () => _openPdf(latest),
                  ),
          ),
          if (latest == null)
            const Padding(
              padding: EdgeInsets.only(top: 48),
              child: AppEmptyView(
                title: 'No payslips yet',
                message:
                    'Your payslips will appear here once payroll publishes '
                    'them.',
                icon: Icons.receipt_long_outlined,
              ),
            )
          else
            Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.lg,
                AppSpacing.lg,
                0,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  if (earlier.isNotEmpty) ...<Widget>[
                    _SectionHeading(count: earlier.length),
                    Gap.sm,
                    _GroupedCard(
                      children: <Widget>[
                        for (final PayslipSummary payslip in earlier)
                          PayslipTile(
                            payslip: payslip,
                            isMasked: isMasked,
                            isDownloading: download.isDownloading(payslip.id),
                            onTap: () => _openDetail(payslip),
                            onDownload: () => _openPdf(payslip),
                          ),
                      ],
                    ),
                  ],
                  if (state.hasMore) ...<Widget>[
                    Gap.sm,
                    Center(
                      child: TextButton(
                        onPressed: state.isLoadingMore
                            ? null
                            : () => ref
                                  .read(payslipsControllerProvider.notifier)
                                  .loadMore(),
                        child: Text(
                          state.isLoadingMore
                              ? 'Loading…'
                              : 'Load older payslips',
                        ),
                      ),
                    ),
                  ],
                  Gap.md,
                  const _PrivacyNote(),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Navy band: title, year picker and the eye; the latest payslip below.
class _Header extends StatelessWidget {
  const _Header({
    required this.years,
    required this.year,
    required this.isMasked,
    required this.onYearChanged,
    required this.onToggleMask,
    required this.latest,
  });

  final List<int> years;
  final int? year;
  final bool isMasked;
  final ValueChanged<int> onYearChanged;
  final VoidCallback onToggleMask;
  final Widget? latest;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: ClipRect(
        child: CustomPaint(
          painter: const _HeaderHexagons(),
          child: SafeArea(
            bottom: false,
            child: Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.md,
                AppSpacing.lg,
                AppSpacing.xl,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          'Payslips',
                          style: Theme.of(context).textTheme.titleLarge
                              ?.copyWith(
                                color: onBrand,
                                fontWeight: FontWeight.w800,
                              ),
                        ),
                      ),
                      if (years.isNotEmpty && year != null)
                        _YearPicker(
                          years: years,
                          year: year!,
                          onChanged: onYearChanged,
                        ),
                      Gap.wSm,
                      MaskToggleButton(
                        isMasked: isMasked,
                        onPressed: onToggleMask,
                      ),
                    ],
                  ),
                  if (latest != null) ...<Widget>[Gap.lg, latest!],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// `2026 ▾` in a glass pill; opens a menu of the years loaded so far.
class _YearPicker extends StatelessWidget {
  const _YearPicker({
    required this.years,
    required this.year,
    required this.onChanged,
  });

  final List<int> years;
  final int year;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return PopupMenuButton<int>(
      initialValue: year,
      onSelected: onChanged,
      tooltip: 'Choose year',
      position: PopupMenuPosition.under,
      itemBuilder: (BuildContext context) => <PopupMenuEntry<int>>[
        for (final int option in years)
          PopupMenuItem<int>(value: option, child: Text('$option')),
      ],
      child: Container(
        height: 40,
        padding: EdgeInsets.symmetric(horizontal: AppSpacing.md),
        decoration: BoxDecoration(
          color: onBrand.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(AppRadius.pill),
          border: Border.all(color: onBrand.withValues(alpha: 0.16)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              '$year',
              style: Theme.of(context).textTheme.labelLarge
                  ?.copyWith(color: onBrand, fontWeight: FontWeight.w700),
            ),
            const Icon(
              Icons.keyboard_arrow_down_rounded,
              size: 18,
              color: onBrand,
            ),
          ],
        ),
      ),
    );
  }
}

/// The newest payslip of the year, as a glass card on the navy header.
class _LatestCard extends StatelessWidget {
  const _LatestCard({
    required this.payslip,
    required this.isMasked,
    required this.isDownloading,
    required this.onView,
    required this.onDownload,
  });

  final PayslipSummary payslip;
  final bool isMasked;
  final bool isDownloading;
  final VoidCallback onView;
  final VoidCallback onDownload;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String period =
        payslip.periodLabel ?? DateFormatter.monthYear(payslip.period);

    return Container(
      padding: EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: onBrand.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppRadius.xl),
        border: Border.all(color: onBrand.withValues(alpha: 0.12)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'LATEST · ${period.toUpperCase()}',
                      style: textStyles.labelSmall?.copyWith(
                        color: onBrand.withValues(alpha: 0.72),
                        letterSpacing: 0.8,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    if (payslip.payDate != null)
                      Text(
                        'Paid ${DateFormatter.dayMonthYear(payslip.payDate!)}',
                        style: textStyles.bodySmall?.copyWith(
                          color: onBrand.withValues(alpha: 0.72),
                        ),
                      ),
                  ],
                ),
              ),
              if (payslip.payDate != null)
                Container(
                  padding: EdgeInsets.symmetric(
                    horizontal: AppSpacing.sm,
                    vertical: 2,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.summitTeal.withValues(alpha: 0.24),
                    borderRadius: BorderRadius.circular(AppRadius.pill),
                  ),
                  child: Text(
                    'Paid',
                    style: textStyles.labelSmall?.copyWith(
                      color: AppColors.accentLight,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
            ],
          ),
          Gap.md,
          Text(
            'Net pay',
            style: textStyles.labelMedium?.copyWith(
              color: onBrand.withValues(alpha: 0.72),
            ),
          ),
          Text(
            CurrencyFormatter.formatMaskable(
              payslip.netPay,
              payslip.currencyCode,
              isMasked: isMasked,
            ),
            style: textStyles.headlineSmall?.copyWith(
              color: onBrand,
              fontWeight: FontWeight.w800,
              letterSpacing: isMasked ? 2 : 0,
            ),
            semanticsLabel: isMasked ? 'Net pay hidden' : null,
          ),
          Gap.lg,
          Row(
            children: <Widget>[
              Expanded(
                child: FilledButton(
                  onPressed: onView,
                  style: FilledButton.styleFrom(
                    backgroundColor: onBrand,
                    foregroundColor: AppColors.apexNavy,
                    minimumSize: const Size(0, AppSizes.minTouchTarget),
                    shape: RoundedRectangleBorder(
                      borderRadius: AppRadius.button,
                    ),
                    textStyle: textStyles.labelLarge?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  child: const Text('View payslip'),
                ),
              ),
              if (payslip.isPdfAvailable) ...<Widget>[
                Gap.wSm,
                Material(
                  color: onBrand.withValues(alpha: 0.12),
                  shape: RoundedRectangleBorder(
                    borderRadius: AppRadius.button,
                    side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: IconButton(
                    onPressed: isDownloading ? null : onDownload,
                    tooltip: 'Download PDF',
                    constraints: const BoxConstraints.tightFor(
                      width: AppSizes.minTouchTarget,
                      height: AppSizes.minTouchTarget,
                    ),
                    icon: isDownloading
                        ? const SizedBox.square(
                            dimension: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: onBrand,
                            ),
                          )
                        : const Icon(Icons.download_rounded, color: onBrand),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            'Earlier',
            style: textStyles.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        Text(
          count == 1 ? '1 payslip' : '$count payslips',
          style: textStyles.labelSmall?.copyWith(
            color: context.palette.textTertiary,
          ),
        ),
      ],
    );
  }
}

/// One white card holding several rows, divided by hairlines.
class _GroupedCard extends StatelessWidget {
  const _GroupedCard({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final BorderRadius radius = BorderRadius.circular(AppRadius.xl);

    return DecoratedBox(
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
        color: AppColors.surface,
        borderRadius: radius,
        clipBehavior: Clip.antiAlias,
        child: Column(
          children: <Widget>[
            for (int i = 0; i < children.length; i++) ...<Widget>[
              children[i],
              if (i < children.length - 1)
                Divider(
                  height: 1,
                  indent: AppSpacing.md,
                  endIndent: AppSpacing.md,
                  color: AppColors.divider,
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _PrivacyNote extends StatelessWidget {
  const _PrivacyNote();

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Row(
      children: <Widget>[
        Icon(Icons.lock_outline_rounded, size: 14, color: palette.textTertiary),
        Gap.wXs,
        Expanded(
          child: Text(
            'Amounts stay hidden until you tap the eye.',
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: palette.textTertiary),
          ),
        ),
      ],
    );
  }
}

/// Two hairline hexagons off the top-right corner.
class _HeaderHexagons extends CustomPainter {
  const _HeaderHexagons();

  @override
  void paint(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2
      ..color = AppColors.textOnPrimary.withValues(alpha: 0.06);
    final Offset centre = Offset(size.width * 0.92, size.height * 0.3);
    for (final double radius in <double>[100, 150]) {
      final Path path = Path();
      for (int i = 0; i < 6; i++) {
        final double angle = math.pi / 3 * i - math.pi / 2;
        final Offset p =
            centre + Offset(math.cos(angle), math.sin(angle)) * radius;
        if (i == 0) {
          path.moveTo(p.dx, p.dy);
        } else {
          path.lineTo(p.dx, p.dy);
        }
      }
      canvas.drawPath(path..close(), paint);
    }
  }

  @override
  bool shouldRepaint(_HeaderHexagons oldDelegate) => false;
}
