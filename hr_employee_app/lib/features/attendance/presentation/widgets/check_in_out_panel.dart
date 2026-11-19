import 'dart:async';

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_loading_indicator.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/punch_action_button.dart';
import 'package:material_ui/material_ui.dart';

/// Today's card on the attendance screen: both punches with where they were
/// recorded, and the check-in / check-out action while one is due.
///
/// While the day is open the check-out column shows the time on site,
/// ticking once a minute — a seconds counter would rebuild sixty times more
/// often for a readout nobody reads at that precision.
class CheckInOutPanel extends StatefulWidget {
  const CheckInOutPanel({
    required this.record,
    required this.isLoading,
    required this.isPunching,
    required this.onCheckIn,
    required this.onCheckOut,
    super.key,
  });

  /// Null before the first punch of the day.
  final AttendanceRecord? record;

  final bool isLoading;
  final bool isPunching;
  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;

  @override
  State<CheckInOutPanel> createState() => _CheckInOutPanelState();
}

class _CheckInOutPanelState extends State<CheckInOutPanel> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _syncTicker();
  }

  @override
  void didUpdateWidget(covariant CheckInOutPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    _syncTicker();
  }

  /// Runs the ticker only while the day is open.
  void _syncTicker() {
    final bool shouldTick = widget.record?.isInProgress ?? false;
    if (shouldTick && _ticker == null) {
      _ticker = Timer.periodic(
        const Duration(minutes: 1),
        (_) => setState(() {}),
      );
    } else if (!shouldTick) {
      _ticker?.cancel();
      _ticker = null;
    }
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AttendanceRecord? record = widget.record;

    if (widget.isLoading) {
      return const _CardShell(
        child: SizedBox(height: 180, child: AppLoadingIndicator()),
      );
    }

    final CheckInAvailability availability =
        record?.availability ?? CheckInAvailability.canCheckIn;

    final String workedLine = switch (record) {
      final AttendanceRecord r when r.workedMinutes != null =>
        'Worked ${DateFormatter.duration(r.workedMinutes)}',
      final AttendanceRecord r when r.isInProgress =>
        (r.elapsedMinutes() ?? 0) < 1
            ? 'Just checked in'
            : 'On site ${DateFormatter.duration(r.elapsedMinutes())}',
      _ => 'Worked —',
    };

    return _CardShell(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'TODAY',
                      style: textStyles.labelSmall?.copyWith(
                        color: palette.textTertiary,
                        letterSpacing: 1,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    Text(
                      DateFormatter.weekdayDayMonthLong(DateTime.now()),
                      style: textStyles.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ],
                ),
              ),
              if (record != null) AttendanceStatusChip(status: record.status),
            ],
          ),
          Gap.md,
          Container(
            padding: EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.cloud,
              borderRadius: AppRadius.field,
              border: Border.all(color: palette.border),
            ),
            child: IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Expanded(
                    child: _PunchDetail(
                      title: 'Check in',
                      icon: Icons.login_rounded,
                      time: record?.checkInAt,
                      locationLabel: record?.checkInLocation?.displayLabel,
                      flag: (record?.isLateArrival ?? false)
                          ? 'Late by '
                                '${DateFormatter.duration(record!.lateByMinutes)}'
                          : null,
                    ),
                  ),
                  VerticalDivider(
                    width: AppSpacing.xl,
                    thickness: 1,
                    color: palette.border,
                  ),
                  Expanded(
                    child: _PunchDetail(
                      title: 'Check out',
                      icon: Icons.logout_rounded,
                      time: record?.checkOutAt,
                      locationLabel: record?.checkOutLocation?.displayLabel,
                      note: workedLine,
                      flag: (record?.isEarlyDeparture ?? false)
                          ? 'Early by '
                                '${DateFormatter.duration(record!.earlyByMinutes)}'
                          : null,
                    ),
                  ),
                ],
              ),
            ),
          ),
          Gap.md,
          switch (availability) {
            CheckInAvailability.completed => _Outcome(
              icon: Icons.check_circle_outline_rounded,
              message: 'Your day is recorded',
              color: palette.success,
            ),
            CheckInAvailability.notRequired => _Outcome(
              icon: Icons.beach_access_outlined,
              message: 'No attendance needed today',
              color: palette.textSecondary,
            ),
            _ => PunchActionButton(
              availability: availability,
              isPunching: widget.isPunching,
              onCheckIn: widget.onCheckIn,
              onCheckOut: widget.onCheckOut,
            ),
          },
        ],
      ),
    );
  }
}

/// White, softly raised card.
class _CardShell extends StatelessWidget {
  const _CardShell({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
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

/// The non-actionable states: one line, no disabled button to tap at.
class _Outcome extends StatelessWidget {
  const _Outcome({
    required this.icon,
    required this.message,
    required this.color,
  });

  final IconData icon;
  final String message;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Icon(icon, size: AppSizes.iconMd, color: color),
        Gap.wSm,
        Expanded(
          child: Text(
            message,
            style: Theme.of(context).textTheme.labelLarge
                ?.copyWith(color: color, fontWeight: FontWeight.w700),
          ),
        ),
      ],
    );
  }
}

/// One punch: label, the time large, an optional flag, an optional note,
/// and where it was recorded.
class _PunchDetail extends StatelessWidget {
  const _PunchDetail({
    required this.title,
    required this.icon,
    required this.time,
    this.locationLabel,
    this.flag,
    this.note,
  });

  final String title;
  final IconData icon;
  final DateTime? time;
  final String? locationLabel;
  final String? flag;
  final String? note;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Icon(icon, size: AppSizes.iconSm, color: palette.textTertiary),
            Gap.wXs,
            Text(
              title,
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
            color: time == null ? palette.textTertiary : palette.textPrimary,
            fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
          ),
        ),
        if (flag != null) ...<Widget>[
          Gap.xs,
          StatusChip(label: flag!, tone: StatusTone.warning, isCompact: true),
        ],
        if (note != null) ...<Widget>[
          Gap.xs,
          Text(
            note!,
            style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
          ),
        ],
        const Spacer(),
        if (locationLabel != null) ...<Widget>[
          Gap.sm,
          Row(
            children: <Widget>[
              Icon(
                Icons.place_outlined,
                size: AppSizes.iconSm,
                color: palette.textTertiary,
              ),
              Gap.wXs,
              Expanded(
                child: Text(
                  locationLabel!,
                  style: textStyles.labelSmall?.copyWith(
                    color: palette.textTertiary,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ],
      ],
    );
  }
}
