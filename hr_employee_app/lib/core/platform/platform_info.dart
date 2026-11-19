import 'package:flutter/foundation.dart';

/// The host the app is running on.
///
/// Resolved from [defaultTargetPlatform] rather than `dart:io`'s `Platform`,
/// for two reasons: `dart:io` does not exist in a browser build, and a widget
/// test can override [debugDefaultTargetPlatformOverride] to exercise the
/// other platform's behaviour — something `Platform` never allows.
abstract final class PlatformInfo {
  /// Lower-case host name, e.g. `android`, `ios`, `web`.
  ///
  /// Sent to the API as the client platform and stored on the server's device
  /// list, so the spelling is part of the contract: see
  /// `docs/api_contracts.md`.
  static String get operatingSystem {
    if (kIsWeb) {
      return 'web';
    }
    return switch (defaultTargetPlatform) {
      TargetPlatform.android => 'android',
      TargetPlatform.iOS => 'ios',
      TargetPlatform.windows => 'windows',
      TargetPlatform.macOS => 'macos',
      TargetPlatform.linux => 'linux',
      TargetPlatform.fuchsia => 'fuchsia',
    };
  }

  /// True only on a native Android build — a browser on an Android phone is
  /// [isWeb], not [isAndroid], because the plugins available to it differ.
  static bool get isAndroid =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  static bool get isIOS =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.iOS;

  static bool get isWeb => kIsWeb;
}
