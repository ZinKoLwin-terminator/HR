import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:material_ui/material_ui.dart';

/// A [StatusChip] for a [LeaveStatus].
///
/// Owns the status-to-tone mapping so the list, the detail screen and the
/// dashboard cannot colour the same status differently.
class LeaveStatusChip extends StatelessWidget {
  const LeaveStatusChip({
    required this.status,
    super.key,
    this.isCompact = false,
  });

  final LeaveStatus status;
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

  static StatusTone toneFor(LeaveStatus status) => switch (status) {
    LeaveStatus.approved => StatusTone.positive,
    LeaveStatus.pending => StatusTone.warning,
    LeaveStatus.rejected => StatusTone.negative,
    LeaveStatus.cancelled => StatusTone.neutral,
    LeaveStatus.unknown => StatusTone.neutral,
  };

  static IconData? iconFor(LeaveStatus status) => switch (status) {
    LeaveStatus.approved => Icons.check_rounded,
    LeaveStatus.pending => Icons.schedule_rounded,
    LeaveStatus.rejected => Icons.close_rounded,
    LeaveStatus.cancelled => Icons.block_rounded,
    LeaveStatus.unknown => null,
  };
}
