import 'package:flutter/foundation.dart';
import 'package:logger/logger.dart';

/// Structured logging with redaction of sensitive values.
///
/// Every log call in the app goes through here rather than through `print` or
/// a raw [Logger], so that:
///   * tokens, passwords and salary figures are never written out, and
///   * verbose output is compiled away in release builds.
class AppLogger {
  AppLogger({required bool verbose, Logger? logger})
    : _verbose = verbose,
      _logger =
          logger ??
          Logger(
            filter: _LevelFilter(verbose ? Level.debug : Level.warning),
            printer: PrettyPrinter(
              methodCount: 0,
              errorMethodCount: 8,
              lineLength: 100,
              colors: false,
              printEmojis: false,
              dateTimeFormat: DateTimeFormat.onlyTimeAndSinceStart,
            ),
          );

  final Logger _logger;
  final bool _verbose;

  /// Keys whose values are replaced with [_redacted] wherever they appear.
  ///
  /// Matching is case-insensitive and substring-based, so `accessToken`,
  /// `access_token` and `Authorization` are all covered.
  static const List<String> _sensitiveKeyFragments = <String>[
    'password',
    'token',
    'authorization',
    'secret',
    'apikey',
    'api_key',
    'refresh',
    'otp',
    'pin',
    'salary',
    'netpay',
    'net_pay',
    'basicsalary',
    'basic_salary',
    'grossearnings',
    'gross_earnings',
    'amount',
    'nationalid',
    'national_id',
    'ssn',
    'bankaccount',
    'bank_account',
    'latitude',
    'longitude',
  ];

  static const String _redacted = '***REDACTED***';

  void debug(String message, [Object? data]) {
    if (!_verbose) {
      return;
    }
    _logger.d(_compose(message, data));
  }

  void info(String message, [Object? data]) {
    _logger.i(_compose(message, data));
  }

  void warning(String message, [Object? data]) {
    _logger.w(_compose(message, data));
  }

  void error(String message, [Object? error, StackTrace? stackTrace]) {
    _logger.e(message, error: error, stackTrace: stackTrace);
  }

  String _compose(String message, Object? data) {
    if (data == null) {
      return message;
    }
    return '$message ${redact(data)}';
  }

  /// Returns a copy of [value] with sensitive entries replaced.
  ///
  /// Exposed for testing and for the Dio interceptor, which redacts headers
  /// and bodies before they are logged.
  static Object? redact(Object? value) {
    if (value is Map) {
      return value.map<String, Object?>((Object? key, Object? child) {
        final String name = key.toString();
        return MapEntry<String, Object?>(
          name,
          _isSensitive(name) ? _redacted : redact(child),
        );
      });
    }
    if (value is Iterable) {
      return value.map(redact).toList();
    }
    return value;
  }

  static bool _isSensitive(String key) {
    final String lower = key.toLowerCase().replaceAll('-', '');
    return _sensitiveKeyFragments.any(lower.contains);
  }
}

/// Emits records at or above [minimum], and nothing at all in release unless
/// the record is a warning or worse.
class _LevelFilter extends LogFilter {
  _LevelFilter(this.minimum);

  final Level minimum;

  @override
  bool shouldLog(LogEvent event) {
    if (kReleaseMode && event.level.index < Level.warning.index) {
      return false;
    }
    return event.level.index >= minimum.index;
  }
}
