import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/providers/notification_controllers.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_tile.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// The notification inbox.
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  Future<void> _markAllAsRead(BuildContext context, WidgetRef ref) async {
    final bool succeeded = await ref
        .read(notificationsControllerProvider.notifier)
        .markAllAsRead();

    if (!context.mounted) {
      return;
    }
    if (succeeded) {
      AppSnackBar.success(context, 'All notifications marked as read.');
    } else {
      AppSnackBar.error(
        context,
        'Could not mark them as read. Please try again.',
      );
    }
  }

  void _open(
    BuildContext context,
    WidgetRef ref,
    AppNotification notification,
  ) {
    // Marking read is fire-and-forget with an optimistic local update, so
    // navigation is not held up by the request.
    ref.read(notificationsControllerProvider.notifier).markAsRead(notification);

    context.pushNamed(
      RouteNames.notificationDetail,
      pathParameters: <String, String>{
        RouteParams.notificationId: notification.id,
      },
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<PaginatedState<AppNotification>> notifications = ref.watch(
      notificationsControllerProvider,
    );
    final bool onlyUnread = ref.watch(unreadOnlyFilterProvider);

    final bool hasUnread =
        notifications.value?.items.any(
          (AppNotification item) => item.isUnread,
        ) ??
        false;

    return Scaffold(
      appBar: BrandAppBar(
        title: const Text('Notifications'),
        actions: <Widget>[
          IconButton(
            onPressed: () =>
                ref.read(unreadOnlyFilterProvider.notifier).toggle(),
            tooltip: onlyUnread ? 'Show all' : 'Show unread only',
            icon: Icon(
              onlyUnread ? Icons.filter_alt_rounded : Icons.filter_alt_outlined,
            ),
          ),
          if (hasUnread)
            IconButton(
              onPressed: () => _markAllAsRead(context, ref),
              tooltip: 'Mark all as read',
              icon: const Icon(Icons.done_all_rounded),
            ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: AsyncValueView<PaginatedState<AppNotification>>(
          value: notifications,
          loading: const PaginatedListSkeleton(lines: 3),
          onRetry: () => ref.invalidate(notificationsControllerProvider),
          data: (PaginatedState<AppNotification> state) =>
              PaginatedListView<AppNotification>(
                items: state.items,
                hasMore: state.hasMore,
                isLoadingMore: state.isLoadingMore,
                loadMoreFailure: state.loadMoreFailure,
                onRefresh: () => ref
                    .read(notificationsControllerProvider.notifier)
                    .refresh(),
                onLoadMore: () => ref
                    .read(notificationsControllerProvider.notifier)
                    .loadMore(),
                emptyTitle: onlyUnread ? 'Nothing unread' : 'No notifications',
                emptyMessage: onlyUnread
                    ? 'You are all caught up.'
                    : 'Announcements, approvals and attendance notices will '
                          'appear here.',
                emptyIcon: Icons.notifications_none_rounded,
                itemBuilder:
                    (
                      BuildContext context,
                      AppNotification notification,
                      int index,
                    ) => NotificationTile(
                      notification: notification,
                      onTap: () => _open(context, ref, notification),
                    ),
              ),
        ),
      ),
    );
  }
}
