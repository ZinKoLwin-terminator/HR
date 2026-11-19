import 'dart:async';

import 'package:dio/dio.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/core/platform/platform_info.dart';
import 'package:hr_employee_app/core/services/session_event_bus.dart';
import 'package:hr_employee_app/core/storage/token_storage.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';

/// Adds the headers every request carries, regardless of authentication.
class AppHeadersInterceptor extends Interceptor {
  AppHeadersInterceptor({required this._appVersion, required this._deviceId});

  final String _appVersion;
  final String _deviceId;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    options.headers[ApiHeaders.accept] = 'application/json';
    options.headers[ApiHeaders.appVersion] = _appVersion;
    options.headers[ApiHeaders.deviceId] = _deviceId;
    options.headers[ApiHeaders.platform] = PlatformInfo.operatingSystem;
    handler.next(options);
  }
}

/// Attaches the bearer token and recovers from an expired one.
///
/// Two things keep this correct under concurrency:
///   * a **single-flight** refresh — if ten requests expire at once, exactly
///     one refresh call is made and the other nine await its result;
///   * a **retry marker** on the request, so a request is replayed at most
///     once and a persistently rejected token cannot loop forever.
///
/// The refresh call itself goes through a separate [Dio] that has no
/// interceptors, which is what prevents the refresh from re-entering this
/// interceptor and recursing.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this._tokenStorage,
    required this._refreshClient,
    required this._sessionEvents,
    required this._logger,
    required this._deviceId,
  });

  static const String _retriedFlag = 'hr.auth.retried';

  final TokenStorage _tokenStorage;
  final Dio _refreshClient;
  final SessionEventBus _sessionEvents;
  final AppLogger _logger;
  final String _deviceId;

  /// Non-null while a refresh is in progress; awaited by concurrent callers.
  Future<AuthTokens?>? _inFlightRefresh;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (_isPublic(options.path)) {
      handler.next(options);
      return;
    }

    AuthTokens? tokens = await _tokenStorage.read();
    if (tokens == null) {
      // No session at all. Let the request go out unauthenticated; the server
      // answers 401 and the normal expiry path signs the user out.
      handler.next(options);
      return;
    }

    // Refresh proactively so the request does not have to fail first.
    if (tokens.isAccessTokenExpired) {
      if (!tokens.canBeRestored) {
        await _endSession();
        handler.reject(_unauthorized(options), true);
        return;
      }
      tokens = await _refreshOnce(tokens);
      if (tokens == null) {
        handler.reject(_unauthorized(options), true);
        return;
      }
    }

    options.headers[ApiHeaders.authorization] = 'Bearer ${tokens.accessToken}';
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final RequestOptions options = err.requestOptions;
    final bool isUnauthorized = err.response?.statusCode == 401;
    final bool alreadyRetried = options.extra[_retriedFlag] == true;

    if (!isUnauthorized || _isPublic(options.path) || alreadyRetried) {
      handler.next(err);
      return;
    }

    final AuthTokens? current = await _tokenStorage.read();
    if (current == null || !current.canBeRestored) {
      await _endSession();
      handler.next(err);
      return;
    }

    final AuthTokens? refreshed = await _refreshOnce(current);
    if (refreshed == null) {
      handler.next(err);
      return;
    }

    try {
      final Response<dynamic> response = await _replay(options, refreshed);
      handler.resolve(response);
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }

  /// Replays [options] with a fresh token, marked so it is never retried again.
  Future<Response<dynamic>> _replay(RequestOptions options, AuthTokens tokens) {
    final Options retryOptions = Options(
      method: options.method,
      headers: <String, dynamic>{
        ...options.headers,
        ApiHeaders.authorization: 'Bearer ${tokens.accessToken}',
      },
      responseType: options.responseType,
      contentType: options.contentType,
      sendTimeout: options.sendTimeout,
      receiveTimeout: options.receiveTimeout,
      extra: <String, dynamic>{...options.extra, _retriedFlag: true},
    );

    return _refreshClient.request<dynamic>(
      options.path,
      data: options.data,
      queryParameters: options.queryParameters,
      cancelToken: options.cancelToken,
      options: retryOptions,
    );
  }

  /// Performs at most one refresh at a time, sharing the result.
  Future<AuthTokens?> _refreshOnce(AuthTokens current) {
    final Future<AuthTokens?>? existing = _inFlightRefresh;
    if (existing != null) {
      return existing;
    }

    final Future<AuthTokens?> operation = _performRefresh(current);
    _inFlightRefresh = operation;
    return operation.whenComplete(() {
      _inFlightRefresh = null;
    });
  }

  Future<AuthTokens?> _performRefresh(AuthTokens current) async {
    _logger.debug('Refreshing access token');
    try {
      final Response<dynamic> response = await _refreshClient.post<dynamic>(
        ApiEndpoints.refreshToken,
        data: <String, dynamic>{
          'refreshToken': current.refreshToken,
          'deviceId': _deviceId,
        },
      );

      final AuthTokens? tokens = _parseTokens(response.data);
      if (tokens == null) {
        await _endSession();
        return null;
      }

      await _tokenStorage.save(tokens);
      _logger.debug('Access token refreshed');
      return tokens;
    } on DioException catch (error) {
      // A refresh that fails because the network is down must not sign the
      // user out — only an explicit rejection by the server does that.
      final int? status = error.response?.statusCode;
      final bool rejected = status == 400 || status == 401 || status == 403;
      if (rejected) {
        _logger.warning('Refresh token rejected; ending session');
        await _endSession();
      } else {
        _logger.warning('Token refresh failed (transport): ${error.type}');
      }
      return null;
    }
  }

  /// Reads the token payload out of the response envelope.
  ///
  /// Returns null when the body does not match the agreed contract, which is
  /// treated the same as a rejection.
  AuthTokens? _parseTokens(Object? body) {
    if (body is! Map) {
      return null;
    }
    final Object? data = body['data'] ?? body;
    if (data is! Map) {
      return null;
    }

    final String? accessToken = data['accessToken'] as String?;
    final String? refreshToken = data['refreshToken'] as String?;
    final DateTime? accessExpiry = _parseDate(data['accessTokenExpiresAt']);
    final DateTime? refreshExpiry = _parseDate(data['refreshTokenExpiresAt']);

    if (accessToken == null ||
        refreshToken == null ||
        accessExpiry == null ||
        refreshExpiry == null) {
      return null;
    }

    return AuthTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
      accessTokenExpiresAt: accessExpiry,
      refreshTokenExpiresAt: refreshExpiry,
    );
  }

  static DateTime? _parseDate(Object? value) =>
      value is String ? DateTime.tryParse(value) : null;

  Future<void> _endSession() async {
    await _tokenStorage.clear();
    _sessionEvents.emit(SessionEvent.expired);
  }

  bool _isPublic(String path) =>
      ApiEndpoints.unauthenticated.any(path.endsWith);

  DioException _unauthorized(RequestOptions options) => DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response<dynamic>(requestOptions: options, statusCode: 401),
  );
}

/// Logs requests and responses with sensitive values removed.
///
/// Bodies are only logged when verbose logging is on, which is never the case
/// in a production build.
class LoggingInterceptor extends Interceptor {
  LoggingInterceptor({required this._logger, required this._logBodies});

  final AppLogger _logger;
  final bool _logBodies;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    _logger.debug('--> ${options.method} ${options.uri}');
    if (_logBodies && options.data != null && options.data is! FormData) {
      _logger.debug('    body: ${AppLogger.redact(options.data)}');
    }
    handler.next(options);
  }

  @override
  void onResponse(
    Response<dynamic> response,
    ResponseInterceptorHandler handler,
  ) {
    _logger.debug(
      '<-- ${response.statusCode} ${response.requestOptions.method} '
      '${response.requestOptions.uri}',
    );
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    // Logged at warning level so production builds still record failures,
    // but the body is withheld because it may echo submitted values.
    _logger.warning(
      '<-- ERROR ${err.response?.statusCode ?? err.type.name} '
      '${err.requestOptions.method} ${err.requestOptions.uri}',
    );
    handler.next(err);
  }
}
