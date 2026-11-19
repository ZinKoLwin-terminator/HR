/// Build-time application configuration.
///
/// Values are supplied with `--dart-define` so that no environment-specific
/// endpoint is baked into the source tree. See `README.md` for the launch
/// commands used by each environment.
library;

/// The deployment target this build points at.
enum AppEnvironment {
  development,
  staging,
  production;

  static AppEnvironment fromName(String value) {
    return AppEnvironment.values.firstWhere(
      (AppEnvironment e) => e.name == value.toLowerCase(),
      orElse: () => AppEnvironment.development,
    );
  }

  bool get isProduction => this == AppEnvironment.production;

  bool get isDevelopment => this == AppEnvironment.development;
}

/// Immutable, globally available configuration for the running app.
///
/// Resolve once in `main()` and inject through Riverpod (`appConfigProvider`)
/// rather than reading a static accessor from feature code.
class AppConfig {
  const AppConfig({
    required this.environment,
    required this.apiBaseUrl,
    required this.useMockData,
    required this.enableVerboseLogging,
    required this.connectTimeout,
    required this.receiveTimeout,
    required this.sendTimeout,
    required this.defaultPageSize,
  });

  /// Builds the configuration from `--dart-define` values, falling back to
  /// development defaults.
  ///
  /// `API_BASE_URL` is deliberately not defaulted for production: a production
  /// build that forgets to pass it fails fast in [validate] instead of
  /// silently pointing at a development server.
  factory AppConfig.fromDartDefine() {
    const String envName = String.fromEnvironment(
      'APP_ENV',
      defaultValue: 'development',
    );
    final AppEnvironment environment = AppEnvironment.fromName(envName);

    const String definedBaseUrl = String.fromEnvironment('API_BASE_URL');
    final String baseUrl = definedBaseUrl.isNotEmpty
        ? definedBaseUrl
        : _defaultBaseUrlFor(environment);

    // Mock data is opt-out in development and force-disabled in production.
    const bool definedUseMock = bool.fromEnvironment(
      'USE_MOCK_DATA',
      defaultValue: true,
    );

    return AppConfig(
      environment: environment,
      apiBaseUrl: baseUrl,
      useMockData: environment.isProduction ? false : definedUseMock,
      enableVerboseLogging: !environment.isProduction,
      connectTimeout: const Duration(seconds: 20),
      receiveTimeout: const Duration(seconds: 30),
      sendTimeout: const Duration(seconds: 60),
      defaultPageSize: 20,
    );
  }

  final AppEnvironment environment;

  /// Fully qualified API root, for example `https://hr-api.example.com/api/v1`.
  final String apiBaseUrl;

  /// When true the app serves local fixtures instead of calling the API, and
  /// shows a persistent banner so fixtures are never mistaken for real data.
  final bool useMockData;

  final bool enableVerboseLogging;
  final Duration connectTimeout;
  final Duration receiveTimeout;
  final Duration sendTimeout;
  final int defaultPageSize;

  /// Placeholder endpoints. Replace with the real hosts once the backend team
  /// confirms them; see `docs/api_contracts.md`.
  static String _defaultBaseUrlFor(AppEnvironment environment) {
    switch (environment) {
      case AppEnvironment.development:
        // 10.0.2.2 is the Android emulator alias for the host machine.
        return 'http://10.0.2.2:5000/api/v1';
      case AppEnvironment.staging:
        return 'https://hr-api-staging.example.com/api/v1';
      case AppEnvironment.production:
        return '';
    }
  }

  /// Throws [StateError] when the configuration cannot safely be used.
  ///
  /// Called from `main()` so a misconfigured release build fails at startup
  /// rather than at the first network call.
  void validate() {
    if (apiBaseUrl.isEmpty) {
      throw StateError(
        'API_BASE_URL is not configured for ${environment.name}. '
        'Pass --dart-define=API_BASE_URL=https://your-host/api/v1',
      );
    }
    if (environment.isProduction && !apiBaseUrl.startsWith('https://')) {
      throw StateError('Production builds must use HTTPS. Got: $apiBaseUrl');
    }
  }
}
