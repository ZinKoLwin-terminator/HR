import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/punch_action_button.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:material_ui/material_ui.dart';

/// Today's attendance, with the check-in or check-out action.
///
/// The hero of the dashboard: it overlaps the coloured header above it, which
/// is why it is raised and why the screen positions it with a negative offset
/// rather than letting it sit in the flow.
class TodayAttendanceCard extends StatelessWidget {
  const TodayAttendanceCard({
    required this.attendance,
    required this.isPunching,
    required this.onCheckIn,
    required this.onCheckOut,
    required this.onViewAttendance,
    super.key,
  });

  /// Null before the first punch of the day.
  final TodayAttendance? attendance;

  /// True while a punch is in flight; the button shows a spinner and is
  /// detached so a second tap cannot double-submit.
  final bool isPunching;

  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;
  final VoidCallback onViewAttendance;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TodayAttendance? today = attendance;

    return AppSectionCard(
      title: 'Today',
      subtitle: DateFormatter.fullDate(DateTime.now()),
      icon: Icons.calendar_month_rounded,
      isRaised: true,
      action: today == null
          // Before the first punch there is no status to report yet, and
          // "Absent" would be a lie at nine in the morning.
          ? const StatusChip(label: 'Not started', tone: StatusTone.neutral)
          : AttendanceStatusChip(status: today.status),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            padding: EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: palette.surfaceMuted,
              borderRadius: AppRadius.field,
            ),
            child: Column(
              children: <Widget>[
                IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      Expanded(
                        child: _PunchTime(
                          label: 'Check in',
                          icon: Icons.login_rounded,
                          time: today?.checkInAt,
                          isFlagged: today?.isLateArrival ?? false,
                          flagLabel: 'Late',
                        ),
                      ),
                      VerticalDivider(
                        width: AppSpacing.lg,
                        thickness: 1,
                        color: palette.border,
                      ),
                      Expanded(
                        child: _PunchTime(
                          label: 'Check out',
                          icon: Icons.logout_rounded,
                          time: today?.checkOutAt,
                          isFlagged: today?.isEarlyDeparture ?? false,
                          flagLabel: 'Early',
                        ),
                      ),
                    ],
                  ),
                ),
                if (today?.workedMinutes != null) ...<Widget>[
                  Gap.md,
                  Divider(height: 1, color: palette.border),
                  Gap.md,
                  _WorkedRow(minutes: today!.workedMinutes!),
                ],
              ],
            ),
          ),
          Gap.lg,

          Row(
            children: <Widget>[
              Expanded(
                child: PunchActionButton(
                  availability:
                      today?.availability ?? CheckInAvailability.canCheckIn,
                  isPunching: isPunching,
                  onCheckIn: onCheckIn,
                  onCheckOut: onCheckOut,
                ),
              ),
              Gap.wSm,
              // The history link is a peer of the punch action rather than
              // something tucked inside the "day recorded" notice, so it is
              // reachable in every state of the card.
              _HistoryButton(onTap: onViewAttendance),
            ],
          ),
        ],
      ),
    );
  }
}

/// One of the two time readouts.
class _PunchTime extends StatelessWidget {
  const _PunchTime({
    required this.label,
    required this.icon,
    required this.time,
    required this.isFlagged,
    required this.flagLabel,
  });

  final String label;
  final IconData icon;
  final DateTime? time;
  final bool isFlagged;
  final String flagLabel;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;
    final bool isRecorded = time != null;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Row(
          children: <Widget>[
            Icon(
              icon,
              size: AppSizes.iconSm,
              color: isRecorded
                  ? Theme.of(context).colorScheme.primary
                  : palette.textTertiary,
            ),
            Gap.wXs,
            Flexible(
              child: Text(
                label,
                style: textStyles.bodySmall,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            // The flag sits beside the label, not beside the time: on a
            // narrow phone a chip after "3:01 PM" is what pushes the column
            // over its width.
            if (isFlagged) ...<Widget>[
              Gap.wXs,
              StatusChip(
                label: flagLabel,
                tone: StatusTone.warning,
                isCompact: true,
              ),
            ],
          ],
        ),
        Gap.xs,
        Text(
          // An em dash, not "00:00": nothing recorded is not a time.
          isRecorded ? DateFormatter.time(time!) : '—',
          style: AppTextStyles.numeric.copyWith(
            color: isRecorded ? palette.textPrimary : palette.textTertiary,
          ),
        ),
      ],
    );
  }
}

/// Hours on the clock so far, shown once there is a figure to show.
class _WorkedRow extends StatelessWidget {
  const _WorkedRow({required this.minutes});

  final int minutes;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Row(
      children: <Widget>[
        Icon(
          Icons.timer_outlined,
          size: AppSizes.iconSm,
          color: palette.textTertiary,
        ),
        Gap.wXs,
        Expanded(child: Text('Worked today', style: textStyles.bodySmall)),
        Text(
          DateFormatter.duration(minutes),
          style: AppTextStyles.numeric.copyWith(color: palette.textPrimary),
        ),
      ],
    );
  }
}

/// Square tonal button that opens the attendance history.
class _HistoryButton extends StatelessWidget {
  const _HistoryButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final ColorScheme colors = Theme.of(context).colorScheme;

    return Tooltip(
      message: 'Attendance history',
      child: Semantics(
        button: true,
        label: 'Attendance history',
        child: ExcludeSemantics(
          child: Material(
            color: context.palette.surfaceMuted,
            borderRadius: AppRadius.button,
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: SizedBox(
                height: AppSizes.buttonHeight,
                width: AppSizes.buttonHeight,
                child: Icon(
                  Icons.history_rounded,
                  size: AppSizes.iconLg,
                  color: colors.primary,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
