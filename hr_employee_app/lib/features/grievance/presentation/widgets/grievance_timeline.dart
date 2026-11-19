import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:material_ui/material_ui.dart';

/// The status history, oldest first, with who acted, when, and why.
///
/// The reason is shown in full, in a bubble: it is where HR explains a step,
/// and where a grievance recorded on the employee's behalf says so
/// ("Recorded by HR on behalf of the employee (received by phone).").
/// The latest step is filled in teal; the ones before it are hollow.
class GrievanceTimeline extends StatelessWidget {
  const GrievanceTimeline({required this.entries, super.key});

  final List<GrievanceTimelineEntry> entries;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    if (entries.isEmpty) {
      return Text(
        'Nothing has happened yet. The history starts when you submit.',
        style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
      );
    }

    final List<GrievanceTimelineEntry> ordered =
        List<GrievanceTimelineEntry>.of(entries)..sort(
          (GrievanceTimelineEntry a, GrievanceTimelineEntry b) =>
              a.occurredAt.compareTo(b.occurredAt),
        );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        for (int i = 0; i < ordered.length; i++)
          _TimelineRow(entry: ordered[i], isLast: i == ordered.length - 1),
      ],
    );
  }
}

class _TimelineRow extends StatelessWidget {
  const _TimelineRow({required this.entry, required this.isLast});

  final GrievanceTimelineEntry entry;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String? reason = entry.reason;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SizedBox(
            width: 18,
            child: Column(
              children: <Widget>[
                Container(
                  margin: const EdgeInsets.only(top: 2),
                  height: 16,
                  width: 16,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: isLast ? AppColors.summitTeal : AppColors.surface,
                    border: Border.all(
                      color: isLast
                          ? AppColors.summitTeal.withValues(alpha: 0.35)
                          : palette.border,
                      width: isLast ? 3 : 2,
                    ),
                  ),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 2,
                      margin: const EdgeInsets.symmetric(vertical: 2),
                      color: palette.border,
                    ),
                  ),
              ],
            ),
          ),
          Gap.wMd,
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : AppSpacing.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    grievanceActionLabel(entry.action),
                    style: textStyles.labelLarge?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  Text(
                    '${entry.actor} · '
                    '${DateFormatter.dayMonthTime(entry.occurredAt)}',
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
                  if (reason != null && reason.trim().isNotEmpty) ...<Widget>[
                    Gap.xs,
                    Container(
                      padding: EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.sm,
                      ),
                      decoration: BoxDecoration(
                        color: palette.surfaceMuted,
                        borderRadius: BorderRadius.circular(AppRadius.md),
                      ),
                      child: Text(
                        reason,
                        style: textStyles.bodySmall?.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
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
