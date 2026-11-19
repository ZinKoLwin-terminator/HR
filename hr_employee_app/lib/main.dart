import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/app.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/device_info_service.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:material_ui/material_ui.dart';

/// Application entry point.
///
/// Everything asynchronous is resolved here and injected as a provider
/// override, so no feature has to await its dependencies or guard against a
/// half-initialised service. The three overrides below are the ones
/// `core_providers.dart` declares as "must be overridden in main()".
Future<void> main() async {
  // Must be first: the plugins used below need the binding.
  WidgetsFlutterBinding.ensureInitialized();

  final AppConfig config = AppConfig.fromDartDefine();

  // Fails fast on a misconfigured build rather than at the first request.
  config.validate();

  final AppLogger logger = AppLogger(verbose: config.enableVerboseLogging);

  // Routes framework errors through the app logger so they are redacted and
  // formatted like everything else.
  FlutterError.onError = (FlutterErrorDetails details) {
    logger.error(
      'Flutter error: ${details.exceptionAsString()}',
      details.exception,
      details.stack,
    );
    FlutterError.presentError(details);
  };

  // Catches errors raised outside the Flutter callback stack, which would
  // otherwise be silent.
  PlatformDispatcher.instance.onError = (Object error, StackTrace stack) {
    logger.error('Uncaught platform error', error, stack);
    return true;
  };

  await SystemChrome.setPreferredOrientations(<DeviceOrientation>[
    DeviceOrientation.portraitUp,
  ]);

  final SharedPreferencesService preferences =
      await SharedPreferencesService.create();
  final DeviceInfo deviceInfo = await DeviceInfoService(preferences).resolve();

  logger.info(
    'Starting ${config.environment.name} build '
    '(mockData: ${config.useMockData})',
  );

  runApp(
    ProviderScope(
      // The element type is left to inference: Riverpod 3 does not export
      // `Override`, so it cannot be named here.
      overrides: [
        appConfigProvider.overrideWithValue(config),
        preferencesServiceProvider.overrideWithValue(preferences),
        deviceInfoProvider.overrideWithValue(deviceInfo),
      ],
      child: const HrEmployeeApp(),
    ),
  );
}
