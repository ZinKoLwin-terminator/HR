import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_tile.dart';
import 'package:material_ui/material_ui.dart';

/// The most recent notifications, as a dashboard summary card.
///
/// Reuses [NotificationTile] in its compact form rather than defining a
/// second row layout, so an unread item reads identically here and in the
/// inbox.
class RecentNotificationsList extends StatelessWidget {
  const RecentNotificationsList({
    required this.notifications,
    required this.onSeeAll,
    required this.onNotificationTap,
    super.key,
  });

  final List<AppNotification> notifications;
  final VoidCallback onSeeAll;
  final void Function(AppNotification notification) onNotificationTap;

  @override
  Widget build(BuildContext context) {
    if (notifications.isEmpty) {
      return const AppCard(
        child: AppEmptyView(
          title: 'No notifications',
          message: 'Announcements and approvals will appear here.',
          icon: Icons.notifications_none_rounded,
          isCompact: true,
        ),
      );
    }

    return AppSectionCard(
      title: 'Recent notifications',
      icon: Icons.notifications_none_rounded,
      action: TextButton(onPressed: onSeeAll, child: const Text('See all')),
      child: Column(
        children: <Widget>[
          for (int i = 0; i < notifications.length; i++) ...<Widget>[
            NotificationTile(
              notification: notifications[i],
              isCompact: true,
              onTap: () => onNotificationTap(notifications[i]),
            ),
            if (i < notifications.length - 1)
              Divider(height: 1, color: context.palette.border),
          ],
        ],
      ),
    );
  }
}
