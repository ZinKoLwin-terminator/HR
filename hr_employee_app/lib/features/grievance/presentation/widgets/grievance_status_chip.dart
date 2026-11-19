import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:material_ui/material_ui.dart';

/// A [StatusChip] for a [GrievanceStatus].
///
/// Owns the status-to-tone mapping so the list and the detail screen cannot
/// colour the same status differently.
class GrievanceStatusChip extends StatelessWidget {
  const GrievanceStatusChip({
    required this.status,
    super.key,
    this.isCompact = false,
  });

  final GrievanceStatus status;
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    return StatusChip(
      label: status.label,
      tone: toneFor(status),
      icon: iconFor(status),
      isCompact: isCompact,
    );
  }

  static StatusTone toneFor(GrievanceStatus status) => switch (status) {
    GrievanceStatus.draft => StatusTone.neutral,
    // The one status that needs the employee to act.
    GrievanceStatus.awaitingEmployeeResponse => StatusTone.warning,
    GrievanceStatus.resolved || GrievanceStatus.closed => StatusTone.positive,
    GrievanceStatus.rejected => StatusTone.negative,
    GrievanceStatus.unknown => StatusTone.neutral,
    _ => StatusTone.info,
  };

  static IconData? iconFor(GrievanceStatus status) => switch (status) {
    GrievanceStatus.draft => Icons.edit_note_rounded,
    GrievanceStatus.awaitingEmployeeResponse => Icons.reply_rounded,
    GrievanceStatus.resolved || GrievanceStatus.closed => Icons.check_rounded,
    GrievanceStatus.rejected => Icons.close_rounded,
    GrievanceStatus.unknown => null,
    _ => Icons.schedule_rounded,
  };
}

/// The words the list and the summary card use for a status: shorter and
/// in the employee's terms ("Needs your reply") rather than the workflow's.
String grievanceShortLabel(GrievanceStatus status) => switch (status) {
  GrievanceStatus.awaitingEmployeeResponse => 'Needs your reply',
  GrievanceStatus.rejected => 'Not taken further',
  GrievanceStatus.underInvestigation => 'Investigating',
  GrievanceStatus.resolutionProposed => 'Resolution proposed',
  _ => status.label,
};

/// The colour a status is drawn in: amber when the employee must act, green
/// once settled, red when refused, grey for a draft, blue otherwise.
Color grievanceTone(BuildContext context, GrievanceStatus status) {
  final AppPalette palette = context.palette;
  return switch (status) {
    GrievanceStatus.draft || GrievanceStatus.unknown => palette.textTertiary,
    GrievanceStatus.awaitingEmployeeResponse => palette.warning,
    GrievanceStatus.resolved || GrievanceStatus.closed => palette.success,
    GrievanceStatus.rejected => palette.danger,
    _ => palette.info,
  };
}

/// A small tinted label in a status's colour.
class GrievancePill extends StatelessWidget {
  const GrievancePill({
    required this.label,
    required this.color,
    super.key,
    this.showDot = false,
  });

  final String label;
  final Color color;
  final bool showDot;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadius.pill),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (showDot) ...<Widget>[
            Container(
              height: 6,
              width: 6,
              decoration: BoxDecoration(color: color, shape: BoxShape.circle),
            ),
            const SizedBox(width: 5),
          ],
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Color.lerp(color, Colors.black, 0.2),
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
