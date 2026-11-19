import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads the notification inbox and records what has been read.
abstract interface class NotificationRepository {
  /// One page of notifications, newest first.
  Future<Result<PaginatedResponse<AppNotification>>> getNotifications({
    required PageRequest page,
    bool onlyUnread = false,
  });

  /// The unread badge count.
  Future<Result<int>> getUnreadCount();

  /// Marks one notification read.
  Future<Result<void>> markAsRead(String id);

  /// Marks every notification read.
  Future<Result<void>> markAllAsRead();

  /// Registers this device for push delivery.
  ///
  /// Called after sign-in once a push provider is configured. See
  /// `PushNotificationService`, which is a no-op until one is.
  Future<Result<void>> registerDevice(String pushToken);
}
