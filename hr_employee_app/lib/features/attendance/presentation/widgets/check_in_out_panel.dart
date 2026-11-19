import 'dart:async';

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_loading_indicator.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/punch_action_button.dart';
import 'package:material_ui/material_ui.dart';

/// Today's punch panel on the attendance screen.
///
/// Fuller than the dashboard card: it adds a live "time on site" readout and
/// shows where each punch was recorded.
///
/// The elapsed counter ticks once a minute, not once a second. A seconds
/// counter would rebuild sixty times more often for a readout nobody reads at
/// that precision.
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

  /// Runs the ticker only while the day is open; a finished or unstarted day
  /// has nothing to count.
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
      return const AppCard(
        child: SizedBox(height: 180, child: AppLoadingIndicator()),
      );
    }

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      DateFormatter.weekdayDayMonth(DateTime.now()),
                      style: textStyles.titleMedium,
                    ),
                    Gap.xxs,
                    Text(
                      record == null
                          ? 'Not checked in yet'
                          : _statusLine(record),
                      style: textStyles.bodySmall,
                    ),
                  ],
                ),
              ),
              if (record != null) AttendanceStatusChip(status: record.status),
            ],
          ),

          if (record?.isInProgress ?? false) ...<Widget>[
            Gap.lg,
            _ElapsedBanner(minutes: record!.elapsedMinutes() ?? 0),
          ],

          Gap.lg,
          Row(
            children: <Widget>[
              Expanded(
                child: _PunchDetail(
                  title: 'Check in',
                  icon: Icons.login_rounded,
                  time: record?.checkInAt,
                  locationLabel: record?.checkInLocation?.displayLabel,
                  flag: (record?.isLateArrival ?? false)
                      ? 'Late by ${record!.lateByMinutes}m'
                      : null,
                ),
              ),
              Container(
                width: 1,
                height: 56,
                color: palette.border,
                margin: EdgeInsets.symmetric(horizontal: AppSpacing.md),
              ),
              Expanded(
                child: _PunchDetail(
                  title: 'Check out',
                  icon: Icons.logout_rounded,
                  time: record?.checkOutAt,
                  locationLabel: record?.checkOutLocation?.displayLabel,
                  flag: (record?.isEarlyDeparture ?? false)
                      ? 'Early by ${record!.earlyByMinutes}m'
                      : null,
                ),
              ),
            ],
          ),

          Gap.lg,
          PunchActionButton(
            availability:
                record?.availability ?? CheckInAvailability.canCheckIn,
            isPunching: widget.isPunching,
            onCheckIn: widget.onCheckIn,
            onCheckOut: widget.onCheckOut,
          ),
        ],
      ),
    );
  }

  String _statusLine(AttendanceRecord record) {
    if (record.hasCheckedOut) {
      return 'Worked ${DateFormatter.duration(record.workedMinutes)}';
    }
    if (record.hasCheckedIn) {
      return 'Checked in at ${DateFormatter.time(record.checkInAt!)}';
    }
    return 'Nothing recorded';
  }
}

/// Live "on site for" readout while the day is open.
class _ElapsedBanner extends StatelessWidget {
  const _ElapsedBanner({required this.minutes});

  final int minutes;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Container(
      width: double.infinity,
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.md,
      ),
      decoration: BoxDecoration(
        color: palette.infoSurface,
        borderRadius: AppRadius.field,
      ),
      child: Row(
        children: <Widget>[
          Icon(
            Icons.hourglass_bottom_rounded,
            size: AppSizes.iconMd,
            color: palette.info,
          ),
          Gap.wSm,
          Text('On site for', style: Theme.of(context).textTheme.bodySmall),
          const Spacer(),
          Text(
            DateFormatter.duration(minutes),
            style: AppTextStyles.numeric.copyWith(color: palette.info),
          ),
        ],
      ),
    );
  }
}

/// One punch: time, optional flag, and where it was recorded.
class _PunchDetail extends StatelessWidget {
  const _PunchDetail({
    required this.title,
    required this.icon,
    required this.time,
    this.locationLabel,
    this.flag,
  });

  final String title;
  final IconData icon;
  final DateTime? time;
  final String? locationLabel;
  final String? flag;

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
            Text(title, style: textStyles.bodySmall),
          ],
        ),
        Gap.xs,
        Text(
          time == null ? '—' : DateFormatter.time(time!),
          style: AppTextStyles.numeric.copyWith(
            color: time == null ? palette.textTertiary : palette.textPrimary,
          ),
        ),
        if (flag != null) ...<Widget>[
          Gap.xs,
          StatusChip(label: flag!, tone: StatusTone.warning, isCompact: true),
        ],
        if (locationLabel != null) ...<Widget>[
          Gap.xs,
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
