import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_models.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads the notification inbox and records read state.
abstract interface class NotificationRemoteDataSource {
  Future<PaginatedResponse<AppNotificationModel>> getNotifications({
    required PageRequest page,
    bool onlyUnread = false,
  });

  Future<UnreadCountModel> getUnreadCount();

  Future<void> markAsRead(String id);

  Future<void> markAllAsRead();

  Future<void> registerDevice(DeviceRegistrationModel registration);
}

/// Calls the real API.
class NotificationRemoteDataSourceImpl implements NotificationRemoteDataSource {
  const NotificationRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<PaginatedResponse<AppNotificationModel>> getNotifications({
    required PageRequest page,
    bool onlyUnread = false,
  }) {
    return _client.getPage<AppNotificationModel>(
      ApiEndpoints.notifications,
      page: page,
      fromItem: AppNotificationModel.fromJson,
      queryParameters: <String, dynamic>{
        // Omitted entirely when false, so the default query stays clean.
        ApiQueryKeys.onlyUnread: onlyUnread ? true : null,
      },
    );
  }

  @override
  Future<UnreadCountModel> getUnreadCount() {
    return _client.get<UnreadCountModel>(
      ApiEndpoints.unreadNotificationCount,
      decode: (Object? data) => UnreadCountModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<void> markAsRead(String id) =>
      _client.postVoid(ApiEndpoints.markNotificationRead(id));

  @override
  Future<void> markAllAsRead() =>
      _client.postVoid(ApiEndpoints.markAllNotificationsRead);

  @override
  Future<void> registerDevice(DeviceRegistrationModel registration) => _client
      .postVoid(ApiEndpoints.registerDevice, body: registration.toJson());
}
