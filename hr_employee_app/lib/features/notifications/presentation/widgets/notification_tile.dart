import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_links.dart';
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
    this.onOpenLink,
  });

  final AppNotification notification;
  final VoidCallback onTap;
  final bool isCompact;

  /// Follows the notification's deep link — "View request ›". Offered only
  /// in the inbox, and only when the link leads somewhere the app has.
  final void Function(NotificationDestination destination)? onOpenLink;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final (IconData icon, Color tint, Color surface) = notificationAppearance(
      palette,
      notification.type,
    );
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

    return _InboxRow(
      notification: notification,
      icon: icon,
      tint: tint,
      surface: surface,
      onTap: onTap,
      onOpenLink: onOpenLink,
    );
  }
}

/// One notification in the inbox, as a row inside a grouped card: a teal dot
/// and a faint tint while unread, the type's icon, title and time, two lines
/// of body, and a link when the notification leads somewhere.
class _InboxRow extends StatelessWidget {
  const _InboxRow({
    required this.notification,
    required this.icon,
    required this.tint,
    required this.surface,
    required this.onTap,
    required this.onOpenLink,
  });

  final AppNotification notification;
  final IconData icon;
  final Color tint;
  final Color surface;
  final VoidCallback onTap;
  final void Function(NotificationDestination destination)? onOpenLink;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isUnread = notification.isUnread;
    final NotificationDestination? destination = onOpenLink == null
        ? null
        : notificationDestination(notification);

    return Semantics(
      button: true,
      label:
          '${isUnread ? 'Unread. ' : ''}${notification.title}. '
          '${notification.body}',
      child: Material(
        color: isUnread
            ? Color.alphaBlend(
                AppColors.summitTeal.withValues(alpha: 0.05),
                AppColors.surface,
              )
            : AppColors.surface,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: EdgeInsets.fromLTRB(
              AppSpacing.sm,
              AppSpacing.md,
              AppSpacing.md,
              AppSpacing.md,
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                // The unread marker, in its own gutter so read and unread
                // rows line up.
                SizedBox(
                  width: 12,
                  child: isUnread
                      ? Padding(
                          padding: const EdgeInsets.only(top: 14),
                          child: Container(
                            height: 6,
                            width: 6,
                            decoration: const BoxDecoration(
                              color: AppColors.summitTeal,
                              shape: BoxShape.circle,
                            ),
                          ),
                        )
                      : null,
                ),
                Container(
                  height: 36,
                  width: 36,
                  decoration: BoxDecoration(
                    color: surface,
                    borderRadius: BorderRadius.circular(AppRadius.md),
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
                                // Weight carries the unread state, so it
                                // does not depend on colour alone.
                                fontWeight: isUnread
                                    ? FontWeight.w800
                                    : FontWeight.w600,
                                color: isUnread
                                    ? palette.textPrimary
                                    : palette.textSecondary,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          Gap.wSm,
                          Text(
                            DateFormatter.relative(notification.createdAt),
                            style: textStyles.labelSmall?.copyWith(
                              color: palette.textTertiary,
                            ),
                          ),
                        ],
                      ),
                      Gap.xxs,
                      Text(
                        notification.body,
                        style: textStyles.bodySmall?.copyWith(
                          color: isUnread
                              ? palette.textSecondary
                              : palette.textTertiary,
                        ),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                      if (destination != null) ...<Widget>[
                        Gap.xs,
                        InkWell(
                          onTap: () => onOpenLink!(destination),
                          borderRadius: BorderRadius.circular(AppRadius.sm),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(vertical: 4),
                            child: Text(
                              '${destination.label} ›',
                              style: textStyles.labelLarge?.copyWith(
                                color: AppColors.link,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Icon, ink and backplate for a notification type — shared by the inbox
/// and the detail screen so a type looks the same in both.
(IconData, Color, Color) notificationAppearance(
  AppPalette palette,
  NotificationType type,
) {
  return switch (type) {
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
      AppColors.apexNavy,
      AppColors.primarySurface,
    ),
    NotificationType.announcement => (
      Icons.campaign_rounded,
      AppColors.violet,
      AppColors.violet.withValues(alpha: 0.12),
    ),
    NotificationType.general || NotificationType.unknown => (
      Icons.notifications_rounded,
      palette.textSecondary,
      palette.surfaceMuted,
    ),
  };
}
