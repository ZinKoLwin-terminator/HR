import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:material_ui/material_ui.dart';

/// One day in the attendance history list.
///
/// The date block is fixed width so the rows line up into a column, and
/// tinted by the day's status so a run of absences or days off stands out
/// before any chip is read. Days that were not working days sit on a muted
/// card, flatter than the days that count.
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
    final bool isQuiet = !record.status.isWorkingDay;
    final BorderRadius radius = BorderRadius.circular(AppRadius.lg);

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: radius,
        boxShadow: isQuiet
            ? null
            : const <BoxShadow>[
                BoxShadow(
                  color: AppColors.shadow,
                  blurRadius: 12,
                  offset: Offset(0, 3),
                ),
              ],
      ),
      child: Material(
        color: isQuiet ? palette.surfaceMuted : AppColors.surface,
        shape: RoundedRectangleBorder(
          borderRadius: radius,
          side: isToday
              ? BorderSide(color: Theme.of(context).colorScheme.primary)
              : BorderSide.none,
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: EdgeInsets.all(AppSpacing.md),
            child: Row(
              children: <Widget>[
                _DateBlock(record: record),
                Gap.wMd,
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Wrap(
                        spacing: AppSpacing.xs,
                        runSpacing: AppSpacing.xs,
                        children: <Widget>[
                          AttendanceStatusChip(
                            status: record.status,
                            isCompact: true,
                          ),
                          if (record.isLateArrival)
                            const StatusChip(
                              label: 'Late',
                              tone: StatusTone.warning,
                              isCompact: true,
                            ),
                          if (record.isEarlyDeparture)
                            const StatusChip(
                              label: 'Early out',
                              tone: StatusTone.warning,
                              isCompact: true,
                            ),
                        ],
                      ),
                      Gap.xs,
                      if (record.hasCheckedIn)
                        Text.rich(
                          TextSpan(
                            children: <InlineSpan>[
                              TextSpan(
                                text:
                                    '${DateFormatter.time(record.checkInAt!)}'
                                    ' – '
                                    '${record.checkOutAt == null ? '…' : DateFormatter.time(record.checkOutAt!)}',
                              ),
                              if (record.workedMinutes != null) ...<InlineSpan>[
                                TextSpan(
                                  text: '  •  ',
                                  style: TextStyle(color: palette.border),
                                ),
                                TextSpan(
                                  text: DateFormatter.duration(
                                    record.workedMinutes,
                                  ),
                                  style: TextStyle(
                                    color: palette.textPrimary,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ],
                            ],
                          ),
                          style: textStyles.bodySmall?.copyWith(
                            color: palette.textSecondary,
                            fontFeatures: const <FontFeature>[
                              FontFeature.tabularFigures(),
                            ],
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        )
                      else
                        Text(
                          _absentLabel(record),
                          style: textStyles.bodySmall?.copyWith(
                            color: palette.textTertiary,
                          ),
                        ),
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
          ),
        ),
      ),
    );
  }

  /// What to say when there are no punch times.
  static String _absentLabel(AttendanceRecord record) {
    if (record.remark != null && record.remark!.isNotEmpty) {
      return record.remark!;
    }
    return 'No attendance recorded';
  }
}

/// Weekday over day number, tinted by status.
class _DateBlock extends StatelessWidget {
  const _DateBlock({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    final (Color surface, Color ink) = switch (record.status) {
      AttendanceStatus.present ||
      AttendanceStatus.halfDay => (palette.successSurface, palette.textPrimary),
      AttendanceStatus.absent => (palette.dangerSurface, palette.danger),
      AttendanceStatus.onLeave => (palette.infoSurface, palette.info),
      _ => (AppColors.surface, palette.textSecondary),
    };

    return Container(
      width: 46,
      padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
      decoration: BoxDecoration(color: surface, borderRadius: AppRadius.field),
      child: Column(
        children: <Widget>[
          Text(
            DateFormatter.weekdayShort(record.attendanceDate).toUpperCase(),
            style: textStyles.labelSmall?.copyWith(
              fontSize: 9,
              letterSpacing: 0.6,
              fontWeight: FontWeight.w700,
              color: palette.textTertiary,
              height: 1.2,
            ),
          ),
          Text(
            '${record.attendanceDate.day}',
            style: textStyles.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
              color: ink,
              height: 1.15,
            ),
          ),
        ],
      ),
    );
  }
}
