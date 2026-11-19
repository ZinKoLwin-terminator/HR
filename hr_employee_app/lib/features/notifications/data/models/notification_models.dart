import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_type_mapper.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';

part 'notification_models.freezed.dart';
part 'notification_models.g.dart';

/// Wire shape of a notification. See `docs/api_contracts.md` §8.
@freezed
abstract class AppNotificationModel with _$AppNotificationModel {
  const factory AppNotificationModel({
    required String id,
    required String title,
    required DateTime createdAt,
    @Default('') String body,

    /// One of `Announcement`, `LeaveApproval`, `LeaveRejection`,
    /// `Attendance`, `Payslip`, `General`.
    @Default('General') String type,
    @Default(false) bool isRead,
    DateTime? readAt,
    String? deepLink,
    String? imageUrl,
  }) = _AppNotificationModel;

  const AppNotificationModel._();

  factory AppNotificationModel.fromJson(Map<String, dynamic> json) =>
      _$AppNotificationModelFromJson(json);

  AppNotification toEntity() => AppNotification(
    id: id,
    title: title,
    body: body,
    type: NotificationTypeMapper.fromApi(type),
    createdAt: createdAt,
    isRead: isRead,
    readAt: readAt,
    deepLink: deepLink,
    imageUrl: imageUrl,
  );
}

/// Response shape of `/notifications/unread-count`.
@freezed
abstract class UnreadCountModel with _$UnreadCountModel {
  const factory UnreadCountModel({@Default(0) int count}) = _UnreadCountModel;

  factory UnreadCountModel.fromJson(Map<String, dynamic> json) =>
      _$UnreadCountModelFromJson(json);
}

/// Request body for `/devices/register`.
///
/// Sent after sign-in once a push provider is wired up. See
/// `PushNotificationService` for the current no-op implementation.
@freezed
abstract class DeviceRegistrationModel with _$DeviceRegistrationModel {
  const factory DeviceRegistrationModel({
    required String deviceId,
    required String platform,
    required String pushToken,
    required String appVersion,
  }) = _DeviceRegistrationModel;

  const DeviceRegistrationModel._();

  factory DeviceRegistrationModel.fromJson(Map<String, dynamic> json) =>
      _$DeviceRegistrationModelFromJson(json);

  /// Omits the push token, which is a credential for sending to this device.
  @override
  String toString() =>
      'DeviceRegistrationModel(deviceId: $deviceId, platform: $platform)';
}
