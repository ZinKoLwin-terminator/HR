import 'dart:math' as math;

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_status_mapper.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_timing.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:intl/intl.dart';
import 'package:material_ui/material_ui.dart';

/// One day of attendance: hours worked, the shift against what actually
/// happened, both punches with where they were made, and a way to ask for a
/// correction.
class AttendanceDetailScreen extends ConsumerWidget {
  const AttendanceDetailScreen({required this.attendanceId, super.key});

  final String attendanceId;

  /// How far the first card rides up over the navy band.
  static const double _overlap = 36;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<AttendanceRecord> record = ref.watch(
      attendanceDetailProvider(attendanceId),
    );

    return Scaffold(
      body: AsyncValueView<AttendanceRecord>(
        value: record,
        loadingMessages: const <String>[
          'Fetching the day…',
          'Checking the shift…',
          'Adding up your hours…',
        ],
        onRetry: () => ref.invalidate(attendanceDetailProvider(attendanceId)),
        data: (AttendanceRecord data) => ListView(
          padding: EdgeInsets.only(bottom: AppSpacing.xxxl),
          children: <Widget>[
            _Header(record: data),
            Transform.translate(
              offset: const Offset(0, -_overlap),
              child: Padding(
                padding: AppSpacing.screenHorizontal,
                child: data.hasCheckedIn
                    ? _ShiftVersusActual(record: data)
                    : _NoPunchesCard(record: data),
              ),
            ),
            Transform.translate(
              offset: const Offset(0, -_overlap + 12),
              child: Padding(
                padding: AppSpacing.screenHorizontal,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    if (data.hasCheckedIn) ...<Widget>[
                      _PunchTiles(record: data),
                      Gap.md,
                      _ActivityCard(record: data),
                      Gap.md,
                    ],
                    if (data.remark.isNotNullOrBlank) ...<Widget>[
                      _WhiteCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            const _CardTitle('Note'),
                            Gap.xs,
                            Text(
                              data.remark!,
                              style: Theme.of(context).textTheme.bodyMedium,
                            ),
                          ],
                        ),
                      ),
                      Gap.md,
                    ],
                    Gap.xs,
                    const _CorrectionButton(),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// --- Header ---------------------------------------------------------------

class _Header extends StatelessWidget {
  const _Header({required this.record});

  final AttendanceRecord record;

  static final DateFormat _date = DateFormat('EEE, d MMMM');

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String? lengthNote = AttendanceTiming.versusShiftLength(record);
    final int? shift = AttendanceTiming.shiftMinutes(record);
    final bool isOver = shift != null && (record.workedMinutes ?? 0) >= shift;

    final String worked;
    if (!record.hasCheckedIn) {
      worked = '—';
    } else if (record.isInProgress) {
      worked = DateFormatter.duration(record.elapsedMinutes());
    } else {
      worked = DateFormatter.duration(record.workedMinutes);
    }

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.xl + AttendanceDetailScreen._overlap,
          ),
          child: Column(
            children: <Widget>[
              Row(
                children: <Widget>[
                  _GlassCircleButton(
                    icon: Icons.arrow_back_rounded,
                    tooltip: 'Back',
                    onTap: () => context.canPop()
                        ? context.pop()
                        : context.goNamed(RouteNames.attendance),
                  ),
                  Gap.wMd,
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          'Attendance',
                          style: textStyles.labelSmall?.copyWith(
                            color: onBrand.withValues(alpha: 0.7),
                          ),
                        ),
                        Text(
                          _date.format(record.attendanceDate),
                          style: textStyles.titleLarge?.copyWith(
                            color: onBrand,
                            fontWeight: FontWeight.w800,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  Gap.wSm,
                  _StatusPill(status: record.status),
                ],
              ),
              Gap.xl,
              Text(
                record.isInProgress ? 'Worked so far' : 'Worked',
                style: textStyles.bodySmall?.copyWith(
                  color: onBrand.withValues(alpha: 0.75),
                ),
              ),
              Text(
                worked,
                style: textStyles.displaySmall?.copyWith(
                  color: onBrand,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -1,
                  height: 1.15,
                ),
              ),
              if (lengthNote != null) ...<Widget>[
                Gap.sm,
                Container(
                  padding: EdgeInsets.symmetric(
                    horizontal: AppSpacing.md,
                    vertical: AppSpacing.xs,
                  ),
                  decoration: BoxDecoration(
                    color:
                        (isOver
                                ? AppColors.summitTealBright
                                : AppColors.warning)
                            .withValues(alpha: 0.18),
                    borderRadius: AppRadius.chip,
                  ),
                  child: Text(
                    lengthNote,
                    style: textStyles.labelMedium?.copyWith(
                      color: isOver
                          ? AppColors.accentLight
                          : const Color(0xFFFDB022),
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// The day's status on the navy band: a dot in the status colour on glass.
class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.status});

  final AttendanceStatus status;

  @override
  Widget build(BuildContext context) {
    // The dark palette's tones, which stay bright enough on navy.
    const AppPalette onNavy = AppPalette.dark;
    final Color dot = switch (AttendanceStatusChip.toneFor(status)) {
      StatusTone.positive => onNavy.success,
      StatusTone.warning => onNavy.warning,
      StatusTone.negative => onNavy.danger,
      StatusTone.info => onNavy.info,
      StatusTone.neutral => onNavy.textSecondary,
    };

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.xs + 1,
      ),
      decoration: BoxDecoration(
        color: dot.withValues(alpha: 0.16),
        borderRadius: AppRadius.chip,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 7,
            height: 7,
            decoration: BoxDecoration(color: dot, shape: BoxShape.circle),
          ),
          Gap.wXs,
          Text(
            status.label,
            style: Theme.of(context).textTheme.labelMedium
                ?.copyWith(color: dot, fontWeight: FontWeight.w700),
          ),
        ],
      ),
    );
  }
}

class _GlassCircleButton extends StatelessWidget {
  const _GlassCircleButton({
    required this.icon,
    required this.tooltip,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Tooltip(
      message: tooltip,
      child: Material(
        color: onBrand.withValues(alpha: 0.12),
        shape: const CircleBorder(),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: SizedBox.square(
            dimension: 40,
            child: Icon(icon, color: onBrand, size: AppSizes.iconMd),
          ),
        ),
      ),
    );
  }
}

// --- Cards -----------------------------------------------------------------

class _WhiteCard extends StatelessWidget {
  const _WhiteCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: AppSpacing.cardPadding,
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: AppRadius.card,
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadowLift,
            blurRadius: 18,
            offset: Offset(0, 6),
          ),
        ],
      ),
      child: child,
    );
  }
}

class _CardTitle extends StatelessWidget {
  const _CardTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Text(
    text,
    style: Theme.of(context).textTheme.titleSmall
        ?.copyWith(fontWeight: FontWeight.w800),
  );
}

/// The rostered shift and the time actually worked, as two bars on one
/// clock, so arriving late or leaving early shows at a glance.
class _ShiftVersusActual extends StatelessWidget {
  const _ShiftVersusActual({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final DateTime? shiftStart = record.shiftStartAt?.toLocal();
    final DateTime? shiftEnd = record.shiftEndAt?.toLocal();
    final DateTime actualStart = record.checkInAt!.toLocal();
    final DateTime actualEnd = (record.checkOutAt ?? DateTime.now()).toLocal();

    // The clock runs from an hour before the earliest time to at least half
    // an hour after the latest, on whole hours.
    final DateTime day = DateTime(
      actualStart.year,
      actualStart.month,
      actualStart.day,
    );
    double hourOf(DateTime t) => t.difference(day).inMinutes / 60.0;
    final List<double> hours = <double>[
      hourOf(actualStart),
      hourOf(actualEnd),
      if (shiftStart != null) hourOf(shiftStart),
      if (shiftEnd != null) hourOf(shiftEnd),
    ];
    final double from = math.max(0, hours.reduce(math.min).floor() - 1.0);
    final double to = math.max(
      from + 2,
      (hours.reduce(math.max) + 0.5).ceilToDouble(),
    );

    double fraction(DateTime t) =>
        ((hourOf(t) - from) / (to - from)).clamp(0.0, 1.0);

    final List<int> ticks = <int>[
      for (int h = from.toInt(); h < to - 1.5; h += 3) h,
      to.toInt(),
    ];

    return _WhiteCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              const Expanded(child: _CardTitle('Shift vs actual')),
              if (shiftStart != null && shiftEnd != null)
                Text(
                  'Shift ${DateFormatter.time(shiftStart)} – '
                  '${DateFormatter.time(shiftEnd)}',
                  style: textStyles.labelSmall?.copyWith(
                    color: palette.textSecondary,
                  ),
                ),
            ],
          ),
          Gap.md,
          if (shiftStart != null && shiftEnd != null) ...<Widget>[
            _BarRow(
              label: 'Shift',
              start: fraction(shiftStart),
              end: fraction(shiftEnd),
              hatched: true,
            ),
            Gap.sm,
          ],
          _BarRow(
            label: 'Actual',
            start: fraction(actualStart),
            end: fraction(actualEnd),
          ),
          Gap.xs,
          Row(
            children: <Widget>[
              const SizedBox(width: _BarRow.labelWidth),
              Expanded(
                child: SizedBox(
                  height: 16,
                  child: Stack(
                    children: <Widget>[
                      for (final int h in ticks)
                        Align(
                          alignment: Alignment(
                            -1 + 2 * ((h - from) / (to - from)),
                            0,
                          ),
                          child: Text(
                            DateFormat('h a')
                                .format(day.add(Duration(hours: h))),
                            style: textStyles.labelSmall?.copyWith(
                              color: palette.textTertiary,
                              fontSize: 10,
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _BarRow extends StatelessWidget {
  const _BarRow({
    required this.label,
    required this.start,
    required this.end,
    this.hatched = false,
  });

  static const double labelWidth = 52;

  final String label;

  /// Positions on the clock, 0 to 1.
  final double start;
  final double end;

  /// The planned shift is drawn hatched, the actual time solid.
  final bool hatched;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Row(
      children: <Widget>[
        SizedBox(
          width: labelWidth,
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: palette.textSecondary),
          ),
        ),
        Expanded(
          child: SizedBox(
            height: 10,
            child: CustomPaint(
              painter: _BarPainter(
                start: start,
                end: end,
                track: palette.surfaceMuted,
                fill: hatched ? palette.textTertiary : AppColors.summitTeal,
                hatched: hatched,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _BarPainter extends CustomPainter {
  const _BarPainter({
    required this.start,
    required this.end,
    required this.track,
    required this.fill,
    required this.hatched,
  });

  final double start;
  final double end;
  final Color track;
  final Color fill;
  final bool hatched;

  @override
  void paint(Canvas canvas, Size size) {
    final Radius radius = Radius.circular(size.height / 2);
    canvas.drawRRect(
      RRect.fromRectAndRadius(Offset.zero & size, radius),
      Paint()..color = track,
    );
    final Rect bar = Rect.fromLTRB(
      size.width * start,
      0,
      size.width * math.max(end, start + 0.01),
      size.height,
    );
    final RRect rounded = RRect.fromRectAndRadius(bar, radius);
    if (!hatched) {
      canvas.drawRRect(rounded, Paint()..color = fill);
      return;
    }
    canvas
      ..save()
      ..clipRRect(rounded)
      ..drawRect(bar, Paint()..color = fill.withValues(alpha: 0.18));
    final Paint stripe = Paint()
      ..color = fill.withValues(alpha: 0.55)
      ..strokeWidth = 2;
    for (double x = bar.left - size.height; x < bar.right; x += 6) {
      canvas.drawLine(
        Offset(x, size.height),
        Offset(x + size.height, 0),
        stripe,
      );
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(_BarPainter old) =>
      old.start != start ||
      old.end != end ||
      old.fill != fill ||
      old.track != track ||
      old.hatched != hatched;
}

/// Check-in and check-out side by side, each with how far it was from the
/// shift. A gap the server flags as late or early is shown in amber.
class _PunchTiles extends StatelessWidget {
  const _PunchTiles({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: _PunchTile(
              icon: Icons.login_rounded,
              iconColour: AppColors.summitTeal,
              label: 'Check in',
              time: record.checkInAt,
              note: AttendanceTiming.versusShiftStart(record),
              noteColour: record.isLateArrival
                  ? palette.warning
                  : palette.textSecondary,
            ),
          ),
          Gap.wMd,
          Expanded(
            child: _PunchTile(
              icon: Icons.logout_rounded,
              iconColour: AppColors.steelBlue,
              label: 'Check out',
              time: record.checkOutAt,
              note: record.hasCheckedOut
                  ? AttendanceTiming.versusShiftEnd(record)
                  : 'Not checked out yet',
              noteColour: record.isEarlyDeparture
                  ? palette.warning
                  : palette.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

class _PunchTile extends StatelessWidget {
  const _PunchTile({
    required this.icon,
    required this.iconColour,
    required this.label,
    required this.time,
    required this.note,
    required this.noteColour,
  });

  final IconData icon;
  final Color iconColour;
  final String label;
  final DateTime? time;
  final String? note;
  final Color noteColour;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;

    return _WhiteCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Icon(icon, size: AppSizes.iconSm, color: iconColour),
              Gap.wXs,
              Text(
                label,
                style: textStyles.labelMedium?.copyWith(
                  color: palette.textSecondary,
                ),
              ),
            ],
          ),
          Gap.xs,
          Text(
            time == null ? '—' : DateFormatter.time(time!),
            style: textStyles.headlineSmall?.copyWith(
              fontWeight: FontWeight.w800,
              fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
            ),
          ),
          if (note != null) ...<Widget>[
            Gap.xxs,
            Text(
              note!,
              style: textStyles.labelSmall?.copyWith(color: noteColour),
            ),
          ],
        ],
      ),
    );
  }
}

/// The punches as a short timeline, each with where it was made.
///
/// Shows what the device reported and nothing more: no "verified" mark,
/// because only the server can judge a location.
class _ActivityCard extends StatelessWidget {
  const _ActivityCard({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final List<_ActivityEvent> events = <_ActivityEvent>[
      _ActivityEvent(
        title: 'Checked in',
        time: record.checkInAt!,
        location: record.checkInLocation,
        colour: AppColors.summitTeal,
      ),
      if (record.checkOutAt != null)
        _ActivityEvent(
          title: 'Checked out',
          time: record.checkOutAt!,
          location: record.checkOutLocation,
          colour: AppColors.apexNavy,
        ),
    ];

    return _WhiteCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const _CardTitle('Activity'),
          Gap.md,
          for (int i = 0; i < events.length; i++)
            _ActivityRow(event: events[i], isLast: i == events.length - 1),
        ],
      ),
    );
  }
}

class _ActivityEvent {
  const _ActivityEvent({
    required this.title,
    required this.time,
    required this.location,
    required this.colour,
  });

  final String title;
  final DateTime time;
  final GeoLocation? location;
  final Color colour;
}

class _ActivityRow extends StatelessWidget {
  const _ActivityRow({required this.event, required this.isLast});

  final _ActivityEvent event;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final GeoLocation? location = event.location;
    final String? place = location == null
        ? null
        : (location.address.isNotNullOrBlank
              ? '${location.address} · ${location.coordinatesLabel}'
              : location.coordinatesLabel);

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SizedBox(
            width: 14,
            child: Column(
              children: <Widget>[
                Gap.xxs,
                Container(
                  width: 12,
                  height: 12,
                  decoration: BoxDecoration(
                    color: event.colour,
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: event.colour.withValues(alpha: 0.25),
                      width: 3,
                      strokeAlign: BorderSide.strokeAlignOutside,
                    ),
                  ),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 2,
                      margin: EdgeInsets.symmetric(vertical: AppSpacing.xs),
                      color: palette.border,
                    ),
                  ),
              ],
            ),
          ),
          Gap.wMd,
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          event.title,
                          style: textStyles.titleSmall?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      Text(
                        DateFormatter.time(event.time),
                        style: textStyles.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                  if (place != null) ...<Widget>[
                    Gap.xxs,
                    Row(
                      children: <Widget>[
                        Icon(
                          Icons.place_outlined,
                          size: 14,
                          color: palette.textTertiary,
                        ),
                        Gap.wXs,
                        Expanded(
                          child: Text(
                            place,
                            style: textStyles.labelSmall?.copyWith(
                              color: palette.textSecondary,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ],
                  // Surfaced honestly; what a mocked fix means is the
                  // server's call.
                  if (location?.isMocked ?? false) ...<Widget>[
                    Gap.xs,
                    const StatusChip(
                      label: 'Mock location',
                      tone: StatusTone.negative,
                      isCompact: true,
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Shown instead of the bars and tiles on a day with no check-in.
class _NoPunchesCard extends StatelessWidget {
  const _NoPunchesCard({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final String message = switch (record.status) {
      AttendanceStatus.onLeave => 'You were on leave this day.',
      AttendanceStatus.holiday => 'Public holiday. No attendance needed.',
      AttendanceStatus.weeklyOff => 'Weekly day off. No attendance needed.',
      _ => 'No check-in was recorded for this day.',
    };

    return _WhiteCard(
      child: Row(
        children: <Widget>[
          Icon(
            Icons.event_busy_outlined,
            color: context.palette.textTertiary,
            size: AppSizes.iconLg,
          ),
          Gap.wMd,
          Expanded(
            child: Text(message, style: Theme.of(context).textTheme.bodyMedium),
          ),
        ],
      ),
    );
  }
}

/// Asks the manager to fix a wrong time.
///
/// There is no correction endpoint yet (`docs/api_contracts.md` has none),
/// so this says so rather than pretending to submit.
class _CorrectionButton extends StatelessWidget {
  const _CorrectionButton();

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        OutlinedButton.icon(
          onPressed: () => AppSnackBar.info(
            context,
            'Corrections are not available in the app yet. Please ask your '
            'manager or HR to fix this day.',
          ),
          icon: const Icon(Icons.edit_outlined, size: 18),
          label: const Text('Request a correction'),
          style: OutlinedButton.styleFrom(
            minimumSize: Size.fromHeight(AppSizes.buttonHeight),
            backgroundColor: Theme.of(context).colorScheme.surface,
            foregroundColor: palette.textPrimary,
            side: BorderSide(color: palette.border),
            shape: RoundedRectangleBorder(borderRadius: AppRadius.card),
          ),
        ),
        Gap.sm,
        Text(
          'Something wrong? Your manager will review the change.',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.labelSmall
              ?.copyWith(color: palette.textTertiary),
        ),
      ],
    );
  }
}
