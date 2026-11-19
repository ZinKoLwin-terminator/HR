import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:material_ui/material_ui.dart';

/// One notification row.
///
/// Owned by the notifications feature and reused by the dashboard, so an
/// unread notification looks the same in both places. [isCompact] trims it
/// for the dashboard's summary card.
class NotificationTile extends StatelessWidget {
  const NotificationTile({
    required this.notification,
    required this.onTap,
    super.key,
    this.isCompact = false,
  });

  final AppNotification notification;
  final VoidCallback onTap;
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final (IconData icon, Color tint, Color surface) = _appearance(palette);
    final bool isUnread = notification.isUnread;

    final Widget content = Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: EdgeInsets.all(AppSpacing.sm),
          decoration: BoxDecoration(
            color: surface,
            borderRadius: AppRadius.field,
          ),
          child: Icon(icon, size: AppSizes.iconMd, color: tint),
        ),
        Gap.wMd,
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Expanded(
                    child: Text(
                      notification.title,
                      style: textStyles.titleSmall?.copyWith(
                        // Weight carries the unread state, so it does not
                        // depend on colour alone.
                        fontWeight: isUnread
                            ? FontWeight.w700
                            : FontWeight.w500,
                      ),
                      maxLines: isCompact ? 1 : 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  if (isUnread) ...<Widget>[
                    Gap.wSm,
                    Container(
                      margin: const EdgeInsets.only(top: 5),
                      height: 8,
                      width: 8,
                      decoration: BoxDecoration(
                        color: Theme.of(context).colorScheme.primary,
                        shape: BoxShape.circle,
                      ),
                    ),
                  ],
                ],
              ),
              Gap.xxs,
              Text(
                notification.body,
                style: textStyles.bodySmall,
                maxLines: isCompact ? 1 : 3,
                overflow: TextOverflow.ellipsis,
              ),
              Gap.xs,
              Text(
                DateFormatter.relative(notification.createdAt),
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textTertiary,
                ),
              ),
            ],
          ),
        ),
        if (notification.isActionable && !isCompact) ...<Widget>[
          Gap.wXs,
          Icon(
            Icons.chevron_right_rounded,
            size: AppSizes.iconMd,
            color: palette.textTertiary,
          ),
        ],
      ],
    );

    // The dashboard supplies its own card, so the compact variant is a plain
    // tappable row rather than a nested card.
    if (isCompact) {
      return InkWell(
        onTap: onTap,
        borderRadius: AppRadius.field,
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
          child: content,
        ),
      );
    }

    return AppCard(
      onTap: onTap,
      backgroundColor: isUnread
          ? Theme.of(context).colorScheme.primary.withValues(alpha: 0.04)
          : null,
      child: content,
    );
  }

  /// Icon and colours for the notification type.
  (IconData, Color, Color) _appearance(AppPalette palette) {
    return switch (notification.type) {
      NotificationType.leaveApproval => (
        Icons.event_available_rounded,
        palette.success,
        palette.successSurface,
      ),
      NotificationType.leaveRejection => (
        Icons.event_busy_rounded,
        palette.danger,
        palette.dangerSurface,
      ),
      NotificationType.attendance => (
        Icons.schedule_rounded,
        palette.warning,
        palette.warningSurface,
      ),
      NotificationType.payslip => (
        Icons.receipt_long_rounded,
        palette.info,
        palette.infoSurface,
      ),
      NotificationType.announcement => (
        Icons.campaign_rounded,
        palette.info,
        palette.infoSurface,
      ),
      NotificationType.general || NotificationType.unknown => (
        Icons.notifications_rounded,
        palette.textSecondary,
        palette.surfaceMuted,
      ),
    };
  }
}
