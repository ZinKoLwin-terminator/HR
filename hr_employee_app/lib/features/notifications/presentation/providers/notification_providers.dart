import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/push_notification_service.dart';
import 'package:hr_employee_app/features/notifications/data/datasources/notification_mock_datasource.dart';
import 'package:hr_employee_app/features/notifications/data/datasources/notification_remote_datasource.dart';
import 'package:hr_employee_app/features/notifications/data/repositories/notification_repository_impl.dart';
import 'package:hr_employee_app/features/notifications/domain/repositories/notification_repository.dart';
import 'package:hr_employee_app/features/notifications/domain/usecases/notification_usecases.dart';

/// Dependency injection for the notifications feature.

/// The push provider.
///
/// Currently a no-op — see [PushNotificationService] for why, and for the
/// four steps to wire up a real provider. This is the only line that changes.
final Provider<PushNotificationService> pushNotificationServiceProvider =
    Provider<PushNotificationService>((Ref ref) {
      final PushNotificationService service = NoopPushNotificationService(
        ref.watch(appLoggerProvider),
      );
      ref.onDispose(service.dispose);
      return service;
    });

final Provider<NotificationRemoteDataSource>
notificationRemoteDataSourceProvider = Provider<NotificationRemoteDataSource>((
  Ref ref,
) {
  final AppConfig config = ref.watch(appConfigProvider);
  if (config.useMockData) {
    return const NotificationMockDataSource();
  }
  return NotificationRemoteDataSourceImpl(ref.watch(apiClientProvider));
});

final Provider<NotificationRepository> notificationRepositoryProvider =
    Provider<NotificationRepository>(
      (Ref ref) => NotificationRepositoryImpl(
        remote: ref.watch(notificationRemoteDataSourceProvider),
        deviceInfo: ref.watch(deviceInfoProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<GetNotificationsUseCase> getNotificationsUseCaseProvider =
    Provider<GetNotificationsUseCase>(
      (Ref ref) =>
          GetNotificationsUseCase(ref.watch(notificationRepositoryProvider)),
    );

final Provider<GetUnreadNotificationCountUseCase>
getUnreadNotificationCountUseCaseProvider =
    Provider<GetUnreadNotificationCountUseCase>(
      (Ref ref) => GetUnreadNotificationCountUseCase(
        ref.watch(notificationRepositoryProvider),
      ),
    );

final Provider<MarkNotificationAsReadUseCase>
markNotificationAsReadUseCaseProvider = Provider<MarkNotificationAsReadUseCase>(
  (Ref ref) =>
      MarkNotificationAsReadUseCase(ref.watch(notificationRepositoryProvider)),
);

final Provider<MarkAllNotificationsAsReadUseCase>
markAllNotificationsAsReadUseCaseProvider =
    Provider<MarkAllNotificationsAsReadUseCase>(
      (Ref ref) => MarkAllNotificationsAsReadUseCase(
        ref.watch(notificationRepositoryProvider),
      ),
    );

final Provider<RegisterPushDeviceUseCase> registerPushDeviceUseCaseProvider =
    Provider<RegisterPushDeviceUseCase>(
      (Ref ref) =>
          RegisterPushDeviceUseCase(ref.watch(notificationRepositoryProvider)),
    );
