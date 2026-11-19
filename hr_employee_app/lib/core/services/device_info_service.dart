import 'dart:math';

import 'package:device_info_plus/device_info_plus.dart';
import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:hr_employee_app/core/platform/platform_info.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:package_info_plus/package_info_plus.dart';

/// Identity of the installation, resolved once at start-up.
class DeviceInfo {
  const DeviceInfo({
    required this.deviceId,
    required this.deviceName,
    required this.appVersion,
    required this.platform,
  });

  /// Stable for the life of the install. Sent with login and push
  /// registration so HR can list and revoke a person's devices.
  ///
  /// Generated locally rather than read from a hardware identifier: vendor
  /// IDs are restricted on both platforms, and a random value per install is
  /// all the server actually needs.
  final String deviceId;

  /// Human-readable, e.g. `Pixel 8 / Android 15`. Shown in the server's
  /// session list, never used for logic.
  final String deviceName;

  /// `1.0.0+1`
  final String appVersion;

  /// `android`, `ios` or `web`.
  final String platform;
}

/// Resolves [DeviceInfo], persisting the generated id.
class DeviceInfoService {
  const DeviceInfoService(this._preferences);

  final PreferencesService _preferences;

  Future<DeviceInfo> resolve() async {
    return DeviceInfo(
      deviceId: await _resolveDeviceId(),
      deviceName: await _resolveDeviceName(),
      appVersion: await _resolveAppVersion(),
      platform: PlatformInfo.operatingSystem,
    );
  }

  Future<String> _resolveDeviceId() async {
    final String? existing = _preferences.getString(PreferenceKeys.deviceId);
    if (existing != null && existing.isNotEmpty) {
      return existing;
    }
    final String generated = _generateId();
    await _preferences.setString(PreferenceKeys.deviceId, generated);
    return generated;
  }

  /// A 32-character hex string from a cryptographically secure source.
  String _generateId() {
    final Random random = Random.secure();
    final StringBuffer buffer = StringBuffer();
    for (int i = 0; i < 16; i++) {
      buffer.write(random.nextInt(256).toRadixString(16).padLeft(2, '0'));
    }
    return buffer.toString();
  }

  Future<String> _resolveDeviceName() async {
    final DeviceInfoPlugin plugin = DeviceInfoPlugin();
    try {
      if (PlatformInfo.isAndroid) {
        final AndroidDeviceInfo info = await plugin.androidInfo;
        return '${info.manufacturer} ${info.model} / Android '
            '${info.version.release}';
      }
      if (PlatformInfo.isIOS) {
        final IosDeviceInfo info = await plugin.iosInfo;
        return '${info.utsname.machine} / iOS ${info.systemVersion}';
      }
    } on Object catch (_) {
      // The plugin is unavailable in unit tests and on unsupported hosts.
      // A generic label is better than failing start-up over a display string.
    }
    return PlatformInfo.operatingSystem;
  }

  Future<String> _resolveAppVersion() async {
    try {
      final PackageInfo info = await PackageInfo.fromPlatform();
      return '${info.version}+${info.buildNumber}';
    } on Object catch (_) {
      return 'unknown';
    }
  }
}
