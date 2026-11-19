import 'package:freezed_annotation/freezed_annotation.dart';

part 'app_notification.freezed.dart';

/// What a notification is about.
///
/// Drives the icon and tone in the list, and is also what a push payload
/// carries so the app can route a tap to the right screen.
enum NotificationType {
  /// Company-wide HR announcement.
  announcement,
  leaveApproval,
  leaveRejection,
  attendance,
  payslip,
  general,

  /// A type this build does not recognise; rendered with the generic style.
  unknown,
}

/// One item in the notification inbox.
@freezed
abstract class AppNotification with _$AppNotification {
  const factory AppNotification({
    required String id,
    required String title,
    required String body,
    required NotificationType type,
    required DateTime createdAt,
    @Default(false) bool isRead,
    DateTime? readAt,

    /// In-app destination, e.g. `hrapp://leave/lr-5501`.
    ///
    /// Held as a string because the domain has no opinion about routing; the
    /// presentation layer resolves it to a route.
    String? deepLink,
    String? imageUrl,
  }) = _AppNotification;

  const AppNotification._();

  bool get isUnread => !isRead;

  /// Whether tapping this notification should navigate somewhere.
  bool get isActionable => deepLink != null && deepLink!.isNotEmpty;
}
