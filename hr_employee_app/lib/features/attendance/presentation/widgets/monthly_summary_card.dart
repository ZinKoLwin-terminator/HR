import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:material_ui/material_ui.dart';

/// The month picker plus the monthly aggregate.
///
/// The figures come from the server, not from the loaded history page: the
/// list is paginated, so counting it client-side would report whatever
/// happened to be fetched.
class MonthlySummaryCard extends StatelessWidget {
  const MonthlySummaryCard({
    required this.period,
    required this.summary,
    required this.onPreviousMonth,
    required this.onNextMonth,
    super.key,
  });

  final AttendancePeriod period;

  /// Null while loading, so the card can keep its shape.
  final AttendanceSummary? summary;

  final VoidCallback onPreviousMonth;
  final VoidCallback onNextMonth;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AttendanceSummary? data = summary;

    return AppCard(
      child: Column(
        children: <Widget>[
          _MonthSelector(
            period: period,
            onPrevious: onPreviousMonth,
            onNext: onNextMonth,
          ),
          Gap.lg,
          Row(
            children: <Widget>[
              Expanded(
                child: _Metric(
                  label: 'Present',
                  value: data?.presentDays.toString(),
                  tone: palette.success,
                ),
              ),
              Expanded(
                child: _Metric(
                  label: 'Absent',
                  value: data?.absentDays.toString(),
                  tone: palette.danger,
                ),
              ),
              Expanded(
                child: _Metric(
                  label: 'Leave',
                  value: data?.leaveDays.toString(),
                  tone: palette.info,
                ),
              ),
              Expanded(
                child: _Metric(
                  label: 'Late',
                  value: data?.lateArrivals.toString(),
                  tone: palette.warning,
                ),
              ),
            ],
          ),
          if (data != null) ...<Widget>[
            Gap.lg,
            Divider(height: 1, color: palette.border),
            Gap.md,
            Row(
              children: <Widget>[
                Expanded(
                  child: _FooterStat(
                    icon: Icons.timer_outlined,
                    label: 'Worked',
                    value: DateFormatter.duration(data.totalWorkedMinutes),
                  ),
                ),
                Expanded(
                  child: _FooterStat(
                    icon: Icons.more_time_rounded,
                    label: 'Overtime',
                    value: DateFormatter.duration(data.totalOvertimeMinutes),
                  ),
                ),
                if (data.attendanceRate != null)
                  Expanded(
                    child: _FooterStat(
                      icon: Icons.insights_outlined,
                      label: 'Rate',
                      value: '${(data.attendanceRate! * 100).round()}%',
                    ),
                  ),
              ],
            ),
          ],
          if (data == null) ...<Widget>[
            Gap.lg,
            Text('Loading summary...', style: textStyles.bodySmall),
          ],
        ],
      ),
    );
  }
}

/// Previous/next month control.
class _MonthSelector extends StatelessWidget {
  const _MonthSelector({
    required this.period,
    required this.onPrevious,
    required this.onNext,
  });

  final AttendancePeriod period;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    // Nothing exists in the future, so the forward arrow is disabled at the
    // current month rather than loading an empty one.
    final bool canGoForward = !period.isCurrentOrFuture;

    return Row(
      children: <Widget>[
        IconButton(
          onPressed: onPrevious,
          icon: const Icon(Icons.chevron_left_rounded),
          tooltip: 'Previous month',
        ),
        Expanded(
          child: Text(
            DateFormatter.monthYear(period.asDate),
            style: Theme.of(context).textTheme.titleMedium,
            textAlign: TextAlign.center,
          ),
        ),
        IconButton(
          onPressed: canGoForward ? onNext : null,
          icon: const Icon(Icons.chevron_right_rounded),
          tooltip: 'Next month',
        ),
      ],
    );
  }
}

/// One of the four headline counts.
class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value, required this.tone});

  final String label;

  /// Null while loading; an em dash is shown rather than a misleading zero.
  final String? value;

  final Color tone;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Text(
          value ?? '—',
          style: AppTextStyles.numeric.copyWith(color: tone, fontSize: 20),
        ),
        Gap.xxs,
        Text(
          label,
          style: Theme.of(context).textTheme.labelSmall,
          textAlign: TextAlign.center,
        ),
      ],
    );
  }
}

class _FooterStat extends StatelessWidget {
  const _FooterStat({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Icon(icon, size: AppSizes.iconSm, color: palette.textTertiary),
            Gap.wXs,
            Text(label, style: Theme.of(context).textTheme.labelSmall),
          ],
        ),
        Gap.xxs,
        Text(value, style: Theme.of(context).textTheme.titleSmall),
      ],
    );
  }
}
