import 'dart:math' as math;

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_avatar.dart';
import 'package:hr_employee_app/core/widgets/app_loading_indicator.dart';
import 'package:hr_employee_app/core/widgets/app_page_loader.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_overview.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_controllers.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_balance_tile.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_request_tile.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_status_chip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:intl/intl.dart';
import 'package:material_ui/material_ui.dart';

/// Leave: what is left this year, what is waiting on a manager, and what
/// was asked for recently, on one scrolling page.
///
/// The navy band holds the year's headline (a donut over the everyday leave
/// types) and a horizontal strip of every type, which straddles the band's
/// foot. The strip keeps one fixed height however many types the employer
/// offers; "View all" lists them in full.
class LeaveScreen extends ConsumerWidget {
  const LeaveScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final int year = DateTime.now().year;
    final AsyncValue<List<LeaveBalance>> balances = ref.watch(
      leaveBalancesProvider(year),
    );
    final AsyncValue<List<LeaveRequest>> history = ref.watch(
      leaveRequestHistoryProvider,
    );

    // First load: nothing to show yet and nothing has failed. The page
    // loader stands in for the whole page; once either side fails, its own
    // section shows the error and a retry instead.
    bool isFirstLoad(AsyncValue<Object?> value) =>
        !value.hasValue && !value.hasError;
    if (isFirstLoad(balances) || isFirstLoad(history)) {
      return const Scaffold(
        body: AppPageLoader(
          background: _LeaveSkeleton(),
          messages: <String>[
            'Fetching your balances…',
            'Checking pending requests…',
            'Almost there…',
          ],
        ),
      );
    }

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () async {
          ref
            ..invalidate(leaveBalancesProvider(year))
            ..invalidate(leaveRequestHistoryProvider);
          await ref.read(leaveBalancesProvider(year).future);
        },
        child: ListView(
          padding: EdgeInsets.only(bottom: AppSpacing.xxl),
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            _LeaveHero(year: year),
            const _RequestsSections(),
          ],
        ),
      ),
    );
  }
}

/// The page's shape while it loads: the navy band with its type strip, and
/// cards below. Shown blurred behind the page loader.
class _LeaveSkeleton extends StatelessWidget {
  const _LeaveSkeleton();

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const ColoredBox(
          color: AppColors.apexNavy,
          child: SafeArea(bottom: false, child: SizedBox(height: 300)),
        ),
        Expanded(
          child: ListView(
            physics: const NeverScrollableScrollPhysics(),
            padding: AppSpacing.screenPadding,
            children: <Widget>[
              const AppSkeletonCard(lines: 2),
              SizedBox(height: AppSpacing.cardGap),
              const AppSkeletonCard(),
              SizedBox(height: AppSpacing.cardGap),
              const AppSkeletonCard(lines: 2),
            ],
          ),
        ),
      ],
    );
  }
}

// --- The navy band -------------------------------------------------------

class _LeaveHero extends ConsumerWidget {
  const _LeaveHero({required this.year});

  final int year;

  /// Height of the type strip. Half of it hangs below the navy band.
  static const double _stripHeight = 132;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<List<LeaveBalance>> balances = ref.watch(
      leaveBalancesProvider(year),
    );
    final List<LeaveBalance>? data = balances.value;

    return Stack(
      children: <Widget>[
        const Positioned(
          left: 0,
          right: 0,
          top: 0,
          bottom: _stripHeight / 2,
          child: ColoredBox(
            color: AppColors.apexNavy,
            child: CustomPaint(painter: _HexagonBackdrop()),
          ),
        ),
        SafeArea(
          bottom: false,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Padding(
                padding: EdgeInsets.fromLTRB(
                  AppSpacing.lg,
                  AppSpacing.md,
                  AppSpacing.lg,
                  0,
                ),
                child: _TitleRow(year: year),
              ),
              Gap.lg,
              Padding(
                padding: AppSpacing.screenHorizontal,
                child: switch (balances) {
                  AsyncData<List<LeaveBalance>>(:final List<LeaveBalance> value)
                      when value.isNotEmpty =>
                    _SummaryPanel(summary: LeaveSummary.fromBalances(value)),
                  AsyncError<List<LeaveBalance>>() when data == null =>
                    _HeroMessage(
                      message: "Couldn't load your balances.",
                      onRetry: () =>
                          ref.invalidate(leaveBalancesProvider(year)),
                    ),
                  AsyncData<List<LeaveBalance>>() => const _HeroMessage(
                    message:
                        'Your leave types have not been set up yet. Contact '
                        'HR if you think this is wrong.',
                  ),
                  _ when data != null && data.isNotEmpty => _SummaryPanel(
                    summary: LeaveSummary.fromBalances(data),
                  ),
                  _ => const _HeroLoading(),
                },
              ),
              Gap.lg,
              if (data != null && data.isNotEmpty) ...<Widget>[
                Padding(
                  padding: AppSpacing.screenHorizontal,
                  child: Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          'Your leave types',
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(
                                color: AppColors.textOnPrimary,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                      ),
                      _GlassPill(
                        label: 'View all ${data.length}',
                        trailing: Icons.chevron_right_rounded,
                        onTap: () => showLeaveTypesSheet(
                          context,
                          balances: data,
                          onSelect: (LeaveBalance b) => _apply(context, b),
                        ),
                      ),
                    ],
                  ),
                ),
                Gap.md,
                SizedBox(
                  height: _stripHeight,
                  child: _TypeStrip(
                    balances: data,
                    onTap: (LeaveBalance b) => _apply(context, b),
                  ),
                ),
              ] else
                const SizedBox(height: _stripHeight / 2),
            ],
          ),
        ),
      ],
    );
  }

  static void _apply(BuildContext context, LeaveBalance balance) {
    context.pushNamed(
      RouteNames.applyLeave,
      queryParameters: <String, String>{
        RouteParams.leaveTypeId: balance.leaveTypeId,
      },
    );
  }
}

class _TitleRow extends StatelessWidget {
  const _TitleRow({required this.year});

  final int year;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Row(
      children: <Widget>[
        Text(
          'Leave',
          style: textStyles.headlineSmall?.copyWith(
            color: onBrand,
            fontWeight: FontWeight.w800,
          ),
        ),
        Gap.wSm,
        Container(
          padding: EdgeInsets.symmetric(
            horizontal: AppSpacing.sm,
            vertical: AppSpacing.xxs,
          ),
          decoration: BoxDecoration(
            color: onBrand.withValues(alpha: 0.14),
            borderRadius: AppRadius.chip,
          ),
          child: Text(
            '$year',
            style: textStyles.labelMedium?.copyWith(
              color: onBrand,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
        const Spacer(),
        Material(
          color: onBrand,
          shape: const StadiumBorder(),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: () => context.pushNamed(RouteNames.applyLeave),
            child: Padding(
              padding: EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.sm + 2,
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Icon(
                    Icons.add_rounded,
                    size: AppSizes.iconMd,
                    color: AppColors.apexNavy,
                  ),
                  Gap.wXs,
                  Text(
                    'Apply',
                    style: textStyles.labelLarge?.copyWith(
                      color: AppColors.apexNavy,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// The donut and its legend.
class _SummaryPanel extends StatelessWidget {
  const _SummaryPanel({required this.summary});

  final LeaveSummary summary;

  static const Color _leftColour = AppColors.summitTealBright;
  static const Color _pendingColour = Color(0xFFFBBF24);
  static const Color _usedColour = Color(0xFF6B8AAB);

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Row(
      children: <Widget>[
        Semantics(
          label:
              '${DayCountFormatter.format(summary.left)} left, '
              '${DayCountFormatter.format(summary.pending)} pending, '
              '${DayCountFormatter.format(summary.used)} used',
          child: ExcludeSemantics(
            child: SizedBox.square(
              dimension: 136,
              child: CustomPaint(
                painter: _DonutPainter(
                  segments: <(double, Color)>[
                    (summary.left, _leftColour),
                    (summary.pending, _pendingColour),
                    (summary.used, _usedColour),
                  ],
                ),
                child: Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Text(
                        DayCountFormatter.formatNumber(summary.left),
                        style: textStyles.headlineMedium?.copyWith(
                          color: onBrand,
                          fontWeight: FontWeight.w800,
                          height: 1,
                          letterSpacing: -0.5,
                        ),
                      ),
                      Gap.xxs,
                      Text(
                        'days left',
                        style: textStyles.labelSmall?.copyWith(
                          color: onBrand.withValues(alpha: 0.75),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
        Gap.wXl,
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _LegendRow(
                colour: _leftColour,
                label: 'Left',
                value: summary.left,
              ),
              Gap.sm,
              _LegendRow(
                colour: _pendingColour,
                label: 'Pending',
                value: summary.pending,
              ),
              Gap.sm,
              _LegendRow(
                colour: _usedColour,
                label: 'Used',
                value: summary.used,
              ),
              Padding(
                padding: EdgeInsets.symmetric(vertical: AppSpacing.sm + 2),
                child: Divider(
                  height: 1,
                  color: onBrand.withValues(alpha: 0.16),
                ),
              ),
              _LegendRow(label: 'Entitled', value: summary.entitled),
              Gap.xs,
              Text(
                summary.includedTypeNames.join(' · '),
                style: textStyles.labelSmall?.copyWith(
                  color: onBrand.withValues(alpha: 0.6),
                ),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _LegendRow extends StatelessWidget {
  const _LegendRow({required this.label, required this.value, this.colour});

  final String label;
  final double value;

  /// Null for a total, which has no slice of its own.
  final Color? colour;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Row(
      children: <Widget>[
        if (colour != null) ...<Widget>[
          Container(
            width: 9,
            height: 9,
            decoration: BoxDecoration(
              color: colour,
              borderRadius: BorderRadius.circular(2.5),
            ),
          ),
          Gap.wSm,
        ],
        Expanded(
          child: Text(
            label,
            style: textStyles.bodySmall?.copyWith(
              color: onBrand.withValues(alpha: 0.85),
            ),
          ),
        ),
        Text(
          DayCountFormatter.formatNumber(value),
          style: textStyles.titleSmall?.copyWith(
            color: onBrand,
            fontWeight: FontWeight.w800,
            fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
          ),
        ),
      ],
    );
  }
}

class _DonutPainter extends CustomPainter {
  const _DonutPainter({required this.segments});

  final List<(double, Color)> segments;

  static const double _stroke = 16;

  /// Gap between slices, in radians.
  static const double _gap = 0.05;

  @override
  void paint(Canvas canvas, Size size) {
    final Rect rect = (Offset.zero & size).deflate(_stroke / 2);
    final double total = segments.fold(
      0,
      (double sum, (double, Color) s) => sum + math.max(0, s.$1),
    );

    if (total <= 0) {
      canvas.drawArc(
        rect,
        0,
        math.pi * 2,
        false,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = _stroke
          ..color = AppColors.textOnPrimary.withValues(alpha: 0.14),
      );
      return;
    }

    final List<(double, Color)> visible = segments
        .where(((double, Color) s) => s.$1 > 0)
        .toList();
    final double gap = visible.length > 1 ? _gap : 0;
    double start = -math.pi / 2;
    for (final (double value, Color colour) in visible) {
      final double sweep = math.pi * 2 * value / total;
      canvas.drawArc(
        rect,
        start + gap / 2,
        math.max(0.001, sweep - gap),
        false,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = _stroke
          ..color = colour,
      );
      start += sweep;
    }
  }

  @override
  bool shouldRepaint(_DonutPainter oldDelegate) =>
      oldDelegate.segments.length != segments.length ||
      !Iterable<int>.generate(segments.length)
          .every((int i) => oldDelegate.segments[i] == segments[i]);
}

/// Faint hexagon outlines behind the band: the brand mark's frame.
class _HexagonBackdrop extends CustomPainter {
  const _HexagonBackdrop();

  @override
  void paint(Canvas canvas, Size size) {
    final Offset centre = Offset(size.width * 0.3, size.height * 0.42);
    for (final (double radius, double alpha) in <(double, double)>[
      (120, 0.06),
      (190, 0.04),
    ]) {
      final Path path = Path();
      for (int i = 0; i < 6; i++) {
        final double angle = math.pi / 3 * i - math.pi / 2;
        final Offset point =
            centre + Offset(math.cos(angle), math.sin(angle)) * radius;
        i == 0
            ? path.moveTo(point.dx, point.dy)
            : path.lineTo(point.dx, point.dy);
      }
      canvas.drawPath(
        path..close(),
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.2
          ..color = AppColors.textOnPrimary.withValues(alpha: alpha),
      );
    }
  }

  @override
  bool shouldRepaint(_HexagonBackdrop oldDelegate) => false;
}

/// Every leave type side by side. Three fit across with the fourth peeking
/// in, so it is plain the strip scrolls.
class _TypeStrip extends StatelessWidget {
  const _TypeStrip({required this.balances, required this.onTap});

  final List<LeaveBalance> balances;
  final ValueChanged<LeaveBalance> onTap;

  /// How much of the fourth tile shows.
  static const double _peek = 28;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        final double gap = AppSpacing.sm + 2;
        final double inner = constraints.maxWidth - AppSpacing.lg * 2;
        final double width = balances.length > 3
            ? (inner - gap * 3 - _peek) / 3
            : (inner - gap * 2) / 3;

        return ListView.separated(
          scrollDirection: Axis.horizontal,
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            0,
            AppSpacing.lg,
            AppSpacing.xs,
          ),
          clipBehavior: Clip.none,
          itemCount: balances.length,
          separatorBuilder: (BuildContext context, int index) =>
              SizedBox(width: gap),
          itemBuilder: (BuildContext context, int index) => LeaveTypeTile(
            balance: balances[index],
            width: width,
            onTap: () => onTap(balances[index]),
          ),
        );
      },
    );
  }
}

class _GlassPill extends StatelessWidget {
  const _GlassPill({required this.label, required this.onTap, this.trailing});

  final String label;
  final IconData? trailing;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Material(
      color: onBrand.withValues(alpha: 0.12),
      shape: StadiumBorder(
        side: BorderSide(color: onBrand.withValues(alpha: 0.14)),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.md,
            AppSpacing.xs + 2,
            trailing == null ? AppSpacing.md : AppSpacing.sm,
            AppSpacing.xs + 2,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                label,
                style: Theme.of(context).textTheme.labelMedium
                    ?.copyWith(color: onBrand, fontWeight: FontWeight.w700),
              ),
              if (trailing != null)
                Icon(trailing, size: AppSizes.iconMd, color: onBrand),
            ],
          ),
        ),
      ),
    );
  }
}

class _HeroLoading extends StatelessWidget {
  const _HeroLoading();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      height: 136,
      child: Center(
        child: CircularProgressIndicator(color: AppColors.textOnPrimary),
      ),
    );
  }
}

class _HeroMessage extends StatelessWidget {
  const _HeroMessage({required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return SizedBox(
      height: 136,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium
                  ?.copyWith(color: onBrand.withValues(alpha: 0.85)),
            ),
            if (onRetry != null) ...<Widget>[
              Gap.md,
              _GlassPill(label: 'Try again', onTap: onRetry!),
            ],
          ],
        ),
      ),
    );
  }
}

// --- Below the band ------------------------------------------------------

/// Pending approval and recent requests, both from the request history.
class _RequestsSections extends ConsumerWidget {
  const _RequestsSections();

  static const int _recentCount = 3;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<List<LeaveRequest>> history = ref.watch(
      leaveRequestHistoryProvider,
    );
    final TextTheme textStyles = Theme.of(context).textTheme;

    final List<LeaveRequest>? all = history.value;
    if (all == null) {
      return Padding(
        padding: EdgeInsets.all(AppSpacing.xxl),
        child: Center(
          child: history.hasError
              ? TextButton.icon(
                  onPressed: () => ref.invalidate(leaveRequestHistoryProvider),
                  icon: const Icon(Icons.refresh_rounded),
                  label: const Text("Couldn't load your requests. Try again"),
                )
              : const CircularProgressIndicator(),
        ),
      );
    }

    final List<LeaveRequest> pending =
        all.where((LeaveRequest r) => r.status == LeaveStatus.pending).toList()
          ..sort(
            (LeaveRequest a, LeaveRequest b) =>
                a.startDate.compareTo(b.startDate),
          );
    final List<LeaveRequest> recent =
        all.where((LeaveRequest r) => r.status != LeaveStatus.pending).toList()
          ..sort(
            (LeaveRequest a, LeaveRequest b) =>
                b.startDate.compareTo(a.startDate),
          );

    return Padding(
      padding: EdgeInsets.fromLTRB(
        AppSpacing.lg,
        AppSpacing.lg,
        AppSpacing.lg,
        0,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          if (pending.isNotEmpty) ...<Widget>[
            Text('Pending approval', style: textStyles.titleMedium),
            Gap.md,
            for (final LeaveRequest request in pending) ...<Widget>[
              _PendingCard(request: request),
              Gap.md,
            ],
            Gap.sm,
          ],
          Row(
            children: <Widget>[
              Expanded(
                child: Text('Recent requests', style: textStyles.titleMedium),
              ),
              TextButton(
                onPressed: () => context.pushNamed(RouteNames.leaveRequests),
                child: const Text('See all'),
              ),
            ],
          ),
          Gap.xs,
          if (recent.isEmpty)
            Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.lg),
              child: Text(
                pending.isEmpty
                    ? 'Requests you submit will appear here with their status.'
                    : 'Nothing decided yet.',
                style: textStyles.bodySmall,
                textAlign: TextAlign.center,
              ),
            )
          else
            LeaveRequestGroupCard(
              children: <Widget>[
                for (final LeaveRequest request in recent.take(_recentCount))
                  LeaveRequestRow(
                    request: request,
                    showDateBox: false,
                    onTap: () => _openDetail(context, request),
                  ),
              ],
            ),
        ],
      ),
    );
  }
}

void _openDetail(BuildContext context, LeaveRequest request) {
  context.pushNamed(
    RouteNames.leaveDetail,
    pathParameters: <String, String>{RouteParams.leaveRequestId: request.id},
  );
}

/// A request still waiting on a manager, with a way to withdraw it.
class _PendingCard extends ConsumerWidget {
  const _PendingCard({required this.request});

  final LeaveRequest request;

  static final DateFormat _weekdayDay = DateFormat('EEE d');
  static final DateFormat _weekdayDayMonth = DateFormat('EEE d MMM');

  String get _dates {
    final DateTime start = request.startDate;
    final DateTime end = request.endDate;
    if (request.isSingleDay) {
      return _weekdayDayMonth.format(start);
    }
    final String from = start.month == end.month && start.year == end.year
        ? _weekdayDay.format(start)
        : _weekdayDayMonth.format(start);
    return '$from – ${_weekdayDayMonth.format(end)}';
  }

  Future<void> _cancel(BuildContext context, WidgetRef ref) async {
    final bool confirmed = await ConfirmDialog.show(
      context,
      title: 'Cancel this request?',
      message:
          'Your ${sentenceLeaveTypeName(request.leaveTypeName)} request for '
          '$_dates will be withdrawn. You can apply again if you need to.',
      confirmLabel: 'Cancel request',
      cancelLabel: 'Keep it',
      isDestructive: true,
      icon: Icons.event_busy_outlined,
    );
    if (!confirmed || !context.mounted) {
      return;
    }

    final bool succeeded = await ref
        .read(cancelLeaveControllerProvider.notifier)
        .cancel(request.id, current: request);
    if (!context.mounted) {
      return;
    }
    if (succeeded) {
      AppSnackBar.success(context, 'Leave request cancelled.');
      return;
    }
    final SubmissionState state = ref.read(cancelLeaveControllerProvider);
    if (state.failureOrNull != null) {
      AppSnackBar.failure(context, state.failureOrNull!);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isCancelling = ref
        .watch(cancelLeaveControllerProvider)
        .isSubmitting;

    return Container(
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: AppRadius.card,
        border: Border.all(color: palette.warning.withValues(alpha: 0.45)),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadow,
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          onTap: () => _openDetail(context, request),
          child: IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                Container(width: 5, color: palette.warning),
                Expanded(
                  child: Padding(
                    padding: EdgeInsets.fromLTRB(
                      AppSpacing.lg - 1,
                      AppSpacing.md + 2,
                      AppSpacing.lg,
                      AppSpacing.md,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  Text(
                                    '${sentenceLeaveTypeName(request.leaveTypeName)}'
                                    ' · ${DayCountFormatter.format(request.totalDays)}',
                                    style: textStyles.titleSmall?.copyWith(
                                      fontWeight: FontWeight.w800,
                                    ),
                                  ),
                                  Gap.xxs,
                                  Text(
                                    _dates,
                                    style: textStyles.bodySmall?.copyWith(
                                      color: palette.textSecondary,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            Gap.wSm,
                            StatusChip(
                              label: 'Pending',
                              tone: LeaveStatusChip.toneFor(
                                LeaveStatus.pending,
                              ),
                              isCompact: true,
                            ),
                          ],
                        ),
                        Padding(
                          padding: EdgeInsets.symmetric(
                            vertical: AppSpacing.sm + 2,
                          ),
                          child: Divider(height: 1, color: palette.border),
                        ),
                        Row(
                          children: <Widget>[
                            AppAvatar(
                              name: request.approverName ?? 'Manager',
                              size: 26,
                            ),
                            Gap.wSm,
                            Expanded(
                              child: Text(
                                'Waiting for your manager',
                                style: textStyles.bodySmall?.copyWith(
                                  color: palette.textSecondary,
                                ),
                              ),
                            ),
                            if (request.isCancellable)
                              OutlinedButton(
                                onPressed: isCancelling
                                    ? null
                                    : () => _cancel(context, ref),
                                style: OutlinedButton.styleFrom(
                                  foregroundColor: palette.danger,
                                  side: BorderSide(color: palette.border),
                                  minimumSize: const Size(0, 36),
                                  visualDensity: VisualDensity.compact,
                                  padding: EdgeInsets.symmetric(
                                    horizontal: AppSpacing.md,
                                  ),
                                ),
                                child: const Text('Cancel'),
                              ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
