import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/notifications/data/datasources/notification_remote_datasource.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_models.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/domain/repositories/notification_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Maps notification payloads to entities.
class NotificationRepositoryImpl
    with RepositoryGuard
    implements NotificationRepository {
  NotificationRepositoryImpl({
    required this._remote,
    required this._deviceInfo,
    required this.logger,
  });

  final NotificationRemoteDataSource _remote;
  final DeviceInfo _deviceInfo;

  @override
  final AppLogger logger;

  @override
  Future<Result<PaginatedResponse<AppNotification>>> getNotifications({
    required PageRequest page,
    bool onlyUnread = false,
  }) {
    return guard(operation: 'getNotifications', () async {
      final PaginatedResponse<AppNotificationModel> response = await _remote
          .getNotifications(page: page, onlyUnread: onlyUnread);
      return response.mapItems(
        (AppNotificationModel model) => model.toEntity(),
      );
    });
  }

  @override
  Future<Result<int>> getUnreadCount() {
    return guard(operation: 'getUnreadCount', () async {
      final UnreadCountModel model = await _remote.getUnreadCount();
      return model.count;
    });
  }

  @override
  Future<Result<void>> markAsRead(String id) {
    return guard(
      operation: 'markNotificationRead',
      () => _remote.markAsRead(id),
    );
  }

  @override
  Future<Result<void>> markAllAsRead() {
    return guard(operation: 'markAllNotificationsRead', _remote.markAllAsRead);
  }

  @override
  Future<Result<void>> registerDevice(String pushToken) {
    return guard(
      operation: 'registerPushDevice',
      () => _remote.registerDevice(
        DeviceRegistrationModel(
          deviceId: _deviceInfo.deviceId,
          platform: _deviceInfo.platform,
          pushToken: pushToken,
          appVersion: _deviceInfo.appVersion,
        ),
      ),
    );
  }
}
