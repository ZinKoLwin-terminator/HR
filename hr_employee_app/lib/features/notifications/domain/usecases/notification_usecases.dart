import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/domain/repositories/notification_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// One page of the notification inbox.
class GetNotificationsUseCase {
  const GetNotificationsUseCase(this._repository);

  final NotificationRepository _repository;

  Future<Result<PaginatedResponse<AppNotification>>> call({
    required PageRequest page,
    bool onlyUnread = false,
  }) => _repository.getNotifications(page: page, onlyUnread: onlyUnread);
}

/// The unread badge count.
class GetUnreadNotificationCountUseCase {
  const GetUnreadNotificationCountUseCase(this._repository);

  final NotificationRepository _repository;

  Future<Result<int>> call() => _repository.getUnreadCount();
}

/// Marks one notification read.
class MarkNotificationAsReadUseCase {
  const MarkNotificationAsReadUseCase(this._repository);

  final NotificationRepository _repository;

  /// [current] is the notification as displayed. When it is already read the
  /// call is skipped, so simply opening an old notification does not generate
  /// a pointless request every time.
  Future<Result<void>> call(String id, {AppNotification? current}) {
    if (current != null && current.isRead) {
      return Future<Result<void>>.value(const Result<void>.success(null));
    }
    return _repository.markAsRead(id);
  }
}

/// Marks the whole inbox read.
class MarkAllNotificationsAsReadUseCase {
  const MarkAllNotificationsAsReadUseCase(this._repository);

  final NotificationRepository _repository;

  Future<Result<void>> call() => _repository.markAllAsRead();
}

/// Registers the device for push delivery.
class RegisterPushDeviceUseCase {
  const RegisterPushDeviceUseCase(this._repository);

  final NotificationRepository _repository;

  Future<Result<void>> call(String pushToken) =>
      _repository.registerDevice(pushToken);
}
