import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:material_ui/material_ui.dart';

/// One day in the attendance history list.
///
/// The date block on the left is fixed width so the rows line up into a
/// column even though the month names vary in length.
class AttendanceHistoryTile extends StatelessWidget {
  const AttendanceHistoryTile({
    required this.record,
    required this.onTap,
    super.key,
  });

  final AttendanceRecord record;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isToday = DateFormatter.isToday(record.attendanceDate);

    return AppCard(
      onTap: onTap,
      borderColor: isToday ? Theme.of(context).colorScheme.primary : null,
      child: Row(
        children: <Widget>[
          _DateBlock(date: record.attendanceDate, isToday: isToday),
          Gap.wMd,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    AttendanceStatusChip(
                      status: record.status,
                      isCompact: true,
                    ),
                    if (record.isLateArrival) ...<Widget>[
                      Gap.wXs,
                      const StatusChip(
                        label: 'Late',
                        tone: StatusTone.warning,
                        isCompact: true,
                      ),
                    ],
                    if (record.isEarlyDeparture) ...<Widget>[
                      Gap.wXs,
                      const StatusChip(
                        label: 'Early out',
                        tone: StatusTone.warning,
                        isCompact: true,
                      ),
                    ],
                  ],
                ),
                Gap.sm,
                // Only drawn for a day that was actually worked; a weekly off
                // has no times to show.
                if (record.hasCheckedIn)
                  Row(
                    children: <Widget>[
                      _TimeLabel(
                        icon: Icons.login_rounded,
                        time: record.checkInAt,
                      ),
                      Gap.wMd,
                      _TimeLabel(
                        icon: Icons.logout_rounded,
                        time: record.checkOutAt,
                      ),
                      if (record.workedMinutes != null) ...<Widget>[
                        Gap.wMd,
                        Flexible(
                          child: Row(
                            children: <Widget>[
                              Icon(
                                Icons.timer_outlined,
                                size: AppSizes.iconSm,
                                color: palette.textTertiary,
                              ),
                              Gap.wXs,
                              Flexible(
                                child: Text(
                                  DateFormatter.duration(record.workedMinutes),
                                  style: textStyles.bodySmall,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ],
                  )
                else
                  Text(_absentLabel(record), style: textStyles.bodySmall),
              ],
            ),
          ),
          Icon(
            Icons.chevron_right_rounded,
            size: AppSizes.iconMd,
            color: palette.textTertiary,
          ),
        ],
      ),
    );
  }

  /// What to say when there are no punch times.
  String _absentLabel(AttendanceRecord record) {
    if (record.remark != null && record.remark!.isNotEmpty) {
      return record.remark!;
    }
    return 'No attendance recorded';
  }
}

/// The fixed-width day/month block on the left of the row.
class _DateBlock extends StatelessWidget {
  const _DateBlock({required this.date, required this.isToday});

  final DateTime date;
  final bool isToday;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color primary = Theme.of(context).colorScheme.primary;

    return Container(
      width: 52,
      padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
      decoration: BoxDecoration(
        color: isToday ? primary.withValues(alpha: 0.08) : palette.surfaceMuted,
        borderRadius: AppRadius.field,
      ),
      child: Column(
        children: <Widget>[
          Text(
            date.day.toString().padLeft(2, '0'),
            style: AppTextStyles.numeric.copyWith(
              color: isToday ? primary : palette.textPrimary,
            ),
          ),
          Text(
            DateFormatter.dayMonth(date).split(' ').last,
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: isToday ? primary : palette.textSecondary),
          ),
        ],
      ),
    );
  }
}

/// A small icon plus time, or an em dash when the punch is missing.
class _TimeLabel extends StatelessWidget {
  const _TimeLabel({required this.icon, required this.time});

  final IconData icon;
  final DateTime? time;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Row(
      children: <Widget>[
        Icon(icon, size: AppSizes.iconSm, color: palette.textTertiary),
        Gap.wXs,
        Text(
          time == null ? '—' : DateFormatter.time(time!),
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
            fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
          ),
        ),
      ],
    );
  }
}
