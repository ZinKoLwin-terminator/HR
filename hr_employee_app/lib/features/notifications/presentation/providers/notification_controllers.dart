import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/paginated_list_notifier.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_controller.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/providers/notification_providers.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';

/// Whether the inbox is filtered to unread only.
class UnreadOnlyFilter extends Notifier<bool> {
  @override
  bool build() => false;

  void toggle() => state = !state;
}

final NotifierProvider<UnreadOnlyFilter, bool> unreadOnlyFilterProvider =
    NotifierProvider<UnreadOnlyFilter, bool>(UnreadOnlyFilter.new);

/// The paginated notification inbox.
class NotificationsController extends PaginatedListNotifier<AppNotification> {
  @override
  Future<Result<PaginatedResponse<AppNotification>>> fetchPage(
    PageRequest page,
  ) {
    // Watched so flipping the filter reloads from page 1.
    final bool onlyUnread = ref.watch(unreadOnlyFilterProvider);
    return ref.read(getNotificationsUseCaseProvider)(
      page: page,
      onlyUnread: onlyUnread,
    );
  }

  @override
  Object identityOf(AppNotification item) => item.id;

  /// Marks one notification read.
  ///
  /// The row is patched locally **before** the request, so opening a
  /// notification feels instant. On failure the local change is rolled back,
  /// because leaving a row marked read when the server disagrees would hide
  /// it from the unread filter on the next load.
  Future<void> markAsRead(AppNotification notification) async {
    if (notification.isRead) {
      return;
    }

    replaceItem(
      notification.copyWith(isRead: true, readAt: DateTime.now().toUtc()),
    );
    ref.read(dashboardControllerProvider.notifier).decrementUnreadCount();

    final Result<void> result = await ref.read(
      markNotificationAsReadUseCaseProvider,
    )(notification.id, current: notification);

    if (result.isFailure) {
      replaceItem(notification);
      ref.invalidate(unreadNotificationCountProvider);
      ref.invalidate(dashboardControllerProvider);
      return;
    }

    ref.invalidate(unreadNotificationCountProvider);
  }

  /// Marks the whole inbox read.
  ///
  /// Returns false when the server rejected it, so the screen can report the
  /// failure rather than silently leaving rows unread.
  Future<bool> markAllAsRead() async {
    final Result<void> result = await ref.read(
      markAllNotificationsAsReadUseCaseProvider,
    )();

    if (result.isFailure) {
      return false;
    }

    final DateTime now = DateTime.now().toUtc();
    updateAllItems(
      (AppNotification item) =>
          item.isRead ? item : item.copyWith(isRead: true, readAt: now),
    );
    ref.read(dashboardControllerProvider.notifier).clearUnreadCount();
    ref.invalidate(unreadNotificationCountProvider);
    return true;
  }
}

final AsyncNotifierProvider<
  NotificationsController,
  PaginatedState<AppNotification>
>
notificationsControllerProvider =
    AsyncNotifierProvider<
      NotificationsController,
      PaginatedState<AppNotification>
    >(NotificationsController.new);

/// The unread badge count.
final unreadNotificationCountProvider = FutureProvider<int>((Ref ref) async {
  final Result<int> result = await ref.watch(
    getUnreadNotificationCountUseCaseProvider,
  )();
  // A badge that cannot be fetched shows nothing rather than an error; it is
  // decoration, not content.
  return result.getOrElse(0);
});

/// One notification, read from the loaded list.
///
/// There is no single-notification endpoint in the contract, and adding one
/// would be a round trip for data the inbox already holds. The detail screen
/// is only ever reached from the list, so the row is always loaded.
final notificationByIdProvider = Provider.family<AppNotification?, String>((
  Ref ref,
  String id,
) {
  final PaginatedState<AppNotification>? state = ref
      .watch(notificationsControllerProvider)
      .value;
  return state?.items
      .where((AppNotification item) => item.id == id)
      .firstOrNull;
});
