import 'package:dio/dio.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/network/api_interceptor.dart';
import 'package:hr_employee_app/core/services/session_event_bus.dart';
import 'package:hr_employee_app/core/storage/token_storage.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';

/// Builds the two [Dio] instances the app uses.
///
/// Why two: the authenticated client runs [AuthInterceptor], which needs to
/// call the refresh endpoint. If it used the same client, that call would pass
/// back through the interceptor and recurse. The refresh client is therefore
/// deliberately plain — it shares the base options but carries no auth
/// behaviour, and is also used to replay a request after a successful refresh.
abstract final class DioFactory {
  static BaseOptions _baseOptions(AppConfig config) => BaseOptions(
    baseUrl: config.apiBaseUrl,
    connectTimeout: config.connectTimeout,
    receiveTimeout: config.receiveTimeout,
    sendTimeout: config.sendTimeout,
    contentType: 'application/json',
    responseType: ResponseType.json,
    // Non-2xx responses are surfaced as DioException so every failure takes
    // the same path through ErrorMapper.
    validateStatus: (int? status) =>
        status != null && status >= 200 && status < 300,
  );

  /// The client every data source uses.
  static Dio createAuthenticatedClient({
    required AppConfig config,
    required TokenStorage tokenStorage,
    required SessionEventBus sessionEvents,
    required AppLogger logger,
    required String appVersion,
    required String deviceId,
  }) {
    final Dio refreshClient = createRefreshClient(config);
    final Dio dio = Dio(_baseOptions(config));

    dio.interceptors.addAll(<Interceptor>[
      AppHeadersInterceptor(appVersion: appVersion, deviceId: deviceId),
      AuthInterceptor(
        tokenStorage: tokenStorage,
        refreshClient: refreshClient,
        sessionEvents: sessionEvents,
        logger: logger,
        deviceId: deviceId,
      ),
      LoggingInterceptor(
        logger: logger,
        logBodies: config.enableVerboseLogging,
      ),
    ]);

    return dio;
  }

  /// A bare client used for the refresh call and for replaying requests.
  static Dio createRefreshClient(AppConfig config) => Dio(_baseOptions(config));
}
