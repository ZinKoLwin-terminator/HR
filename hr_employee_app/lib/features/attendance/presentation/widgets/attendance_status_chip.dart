import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_status_mapper.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:material_ui/material_ui.dart';

/// A [StatusChip] for an [AttendanceStatus].
///
/// Owns the status-to-tone mapping so the dashboard, the history list and the
/// detail screen cannot colour the same status differently.
class AttendanceStatusChip extends StatelessWidget {
  const AttendanceStatusChip({
    required this.status,
    super.key,
    this.isCompact = false,
  });

  final AttendanceStatus status;
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    return StatusChip(
      label: status.label,
      tone: toneFor(status),
      isCompact: isCompact,
    );
  }

  /// The tone for [status]. Exposed so a dot or a border can use it too.
  static StatusTone toneFor(AttendanceStatus status) => switch (status) {
    AttendanceStatus.present => StatusTone.positive,
    AttendanceStatus.halfDay => StatusTone.warning,
    AttendanceStatus.absent => StatusTone.negative,
    AttendanceStatus.onLeave => StatusTone.info,
    AttendanceStatus.holiday ||
    AttendanceStatus.weeklyOff ||
    AttendanceStatus.unknown => StatusTone.neutral,
  };
}
