import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/dio_factory.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/services/session_event_bus.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/core/storage/secure_storage_service.dart';
import 'package:hr_employee_app/core/storage/token_storage.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';

/// Application-wide dependency injection.
///
/// Riverpod is the only container in the app — there is no service locator
/// and no global singleton. That means every dependency is overridable in a
/// test with `ProviderScope(overrides: [...])`, and the graph is explicit.
///
/// The three providers at the top throw by default and are overridden in
/// `main()` once their asynchronous initialisation has completed. Throwing is
/// deliberate: it turns "forgot to initialise" into an immediate, obvious
/// error instead of a subtly wrong default.

/// Resolved in `main()` from `--dart-define` values.
final Provider<AppConfig> appConfigProvider = Provider<AppConfig>(
  (Ref ref) => throw UnimplementedError(
    'appConfigProvider must be overridden in main()',
  ),
);

/// Resolved in `main()` after `SharedPreferences.getInstance()`.
final Provider<PreferencesService> preferencesServiceProvider =
    Provider<PreferencesService>(
      (Ref ref) => throw UnimplementedError(
        'preferencesServiceProvider must be overridden in main()',
      ),
    );

/// Resolved in `main()` after the platform plugins have answered.
final Provider<DeviceInfo> deviceInfoProvider = Provider<DeviceInfo>(
  (Ref ref) => throw UnimplementedError(
    'deviceInfoProvider must be overridden in main()',
  ),
);

final Provider<AppLogger> appLoggerProvider = Provider<AppLogger>((Ref ref) {
  final AppConfig config = ref.watch(appConfigProvider);
  return AppLogger(verbose: config.enableVerboseLogging);
});

final Provider<SecureStorageService> secureStorageServiceProvider =
    Provider<SecureStorageService>((Ref ref) => FlutterSecureStorageService());

final Provider<TokenStorage> tokenStorageProvider = Provider<TokenStorage>(
  (Ref ref) => TokenStorage(ref.watch(secureStorageServiceProvider)),
);

/// Broadcasts session-wide events from the networking layer upwards.
///
/// Closed when the container is disposed so the stream does not outlive it.
final Provider<SessionEventBus> sessionEventBusProvider =
    Provider<SessionEventBus>((Ref ref) {
      final SessionEventBus bus = SessionEventBus();
      ref.onDispose(bus.dispose);
      return bus;
    });

/// The configured Dio instance, with headers, auth and logging attached.
final Provider<Dio> dioProvider = Provider<Dio>((Ref ref) {
  final DeviceInfo device = ref.watch(deviceInfoProvider);

  final Dio dio = DioFactory.createAuthenticatedClient(
    config: ref.watch(appConfigProvider),
    tokenStorage: ref.watch(tokenStorageProvider),
    sessionEvents: ref.watch(sessionEventBusProvider),
    logger: ref.watch(appLoggerProvider),
    appVersion: device.appVersion,
    deviceId: device.deviceId,
  );

  // Cancels in-flight requests if the container is torn down.
  ref.onDispose(dio.close);
  return dio;
});

final Provider<ApiClient> apiClientProvider = Provider<ApiClient>(
  (Ref ref) => ApiClient(ref.watch(dioProvider)),
);
