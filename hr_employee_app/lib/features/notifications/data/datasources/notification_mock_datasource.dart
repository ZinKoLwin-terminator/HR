import 'package:hr_employee_app/features/notifications/data/datasources/notification_remote_datasource.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_models.dart';
import 'package:hr_employee_app/shared/helpers/mock_fixtures.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// A fixture-backed stand-in for [NotificationRemoteDataSource].
///
/// NOT A BACKEND — see [MockSupport].
///
/// Read state is held in [MockFixtures], so marking something read lowers
/// the dashboard badge the same way the real API would.
class NotificationMockDataSource implements NotificationRemoteDataSource {
  const NotificationMockDataSource();

  @override
  Future<PaginatedResponse<AppNotificationModel>> getNotifications({
    required PageRequest page,
    bool onlyUnread = false,
  }) async {
    await MockSupport.delay(const Duration(milliseconds: 400));

    List<AppNotificationModel> all = MockFixtures.instance.notifications;
    if (onlyUnread) {
      all = all.where((AppNotificationModel n) => !n.isRead).toList();
    }

    final List<AppNotificationModel> items = MockSupport.page(
      all,
      page.pageNumber,
      page.pageSize,
    );
    final int totalPages = all.isEmpty
        ? 1
        : (all.length / page.pageSize).ceil();

    return PaginatedResponse<AppNotificationModel>(
      items: items,
      pageNumber: page.pageNumber,
      pageSize: page.pageSize,
      totalCount: all.length,
      totalPages: totalPages,
      hasNextPage: page.pageNumber < totalPages,
      hasPreviousPage: page.pageNumber > 1,
    );
  }

  @override
  Future<UnreadCountModel> getUnreadCount() async {
    await MockSupport.delay(const Duration(milliseconds: 200));
    return UnreadCountModel(
      count: MockFixtures.instance.unreadNotificationCount,
    );
  }

  @override
  Future<void> markAsRead(String id) async {
    await MockSupport.delay(const Duration(milliseconds: 200));
    MockFixtures.instance.markNotificationRead(id);
  }

  @override
  Future<void> markAllAsRead() async {
    await MockSupport.delay(const Duration(milliseconds: 350));
    MockFixtures.instance.markAllNotificationsRead();
  }

  @override
  Future<void> registerDevice(DeviceRegistrationModel registration) =>
      MockSupport.delay(const Duration(milliseconds: 200));
}
