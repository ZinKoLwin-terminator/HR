import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_status_chip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// One leave request in the list.
class LeaveRequestTile extends StatelessWidget {
  const LeaveRequestTile({
    required this.request,
    required this.onTap,
    super.key,
  });

  final LeaveRequest request;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = ColorUtils.fromHex(
      request.colorHex,
      fallback: Theme.of(context).colorScheme.primary,
    );

    return AppCard(
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              // A colour bar rather than a coloured background, so the type
              // is identifiable without tinting the whole card.
              Container(
                width: 4,
                height: 32,
                decoration: BoxDecoration(
                  color: accent,
                  borderRadius: BorderRadius.circular(AppRadius.pill),
                ),
              ),
              Gap.wMd,
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      request.leaveTypeName,
                      style: textStyles.titleSmall,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    Gap.xxs,
                    Text(
                      DateFormatter.dateRange(
                        request.startDate,
                        request.endDate,
                      ),
                      style: textStyles.bodySmall,
                    ),
                  ],
                ),
              ),
              Gap.wSm,
              LeaveStatusChip(status: request.status, isCompact: true),
            ],
          ),

          Gap.md,
          Divider(height: 1, color: palette.border),
          Gap.md,

          Row(
            children: <Widget>[
              _Meta(
                icon: Icons.event_note_outlined,
                label: DayCountFormatter.format(request.totalDays),
              ),
              Gap.wLg,
              if (request.hasAttachments)
                _Meta(
                  icon: Icons.attach_file_rounded,
                  label: '${request.attachments.length}',
                ),
              const Spacer(),
              Text(
                'Applied ${DateFormatter.relative(request.appliedAt)}',
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textTertiary,
                ),
              ),
            ],
          ),

          // Shown inline because a rejection reason is the first thing the
          // employee wants to read, not something to tap through for.
          if (request.status.isDecided &&
              (request.decisionRemark?.isNotEmpty ?? false)) ...<Widget>[
            Gap.md,
            _DecisionRemark(request: request),
          ],
        ],
      ),
    );
  }
}

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Row(
      children: <Widget>[
        Icon(icon, size: AppSizes.iconSm, color: palette.textTertiary),
        Gap.wXs,
        Text(label, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}

class _DecisionRemark extends StatelessWidget {
  const _DecisionRemark({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final bool isRejection = request.status == LeaveStatus.rejected;
    final Color tint = isRejection ? palette.danger : palette.textSecondary;
    final Color surface = isRejection
        ? palette.dangerSurface
        : palette.surfaceMuted;

    return Container(
      width: double.infinity,
      padding: EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(color: surface, borderRadius: AppRadius.field),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(Icons.comment_outlined, size: AppSizes.iconSm, color: tint),
          Gap.wSm,
          Expanded(
            child: Text(
              request.decisionRemark!,
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: tint),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}
