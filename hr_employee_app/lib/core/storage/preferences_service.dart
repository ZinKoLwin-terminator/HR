import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Plain, unencrypted storage for user preferences.
///
/// Nothing sensitive may be written here — see [SecureStorageKeys] for values
/// that need encryption.
abstract interface class PreferencesService {
  String? getString(String key);

  Future<void> setString(String key, String value);

  bool getBool(String key, {bool defaultValue = false});

  Future<void> setBool(String key, {required bool value});

  int? getInt(String key);

  Future<void> setInt(String key, int value);

  Future<void> remove(String key);

  /// Clears preferences on sign-out. Install-scoped values such as the device
  /// id are preserved by the caller.
  Future<void> clear();
}

/// [SharedPreferences]-backed implementation.
///
/// The instance is resolved once during start-up and injected, so no call site
/// has to await [SharedPreferences.getInstance].
class SharedPreferencesService implements PreferencesService {
  const SharedPreferencesService(this._prefs);

  final SharedPreferences _prefs;

  /// Resolves the platform instance. Call once from `main()`.
  static Future<SharedPreferencesService> create() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    return SharedPreferencesService(prefs);
  }

  @override
  String? getString(String key) => _prefs.getString(key);

  @override
  Future<void> setString(String key, String value) =>
      _prefs.setString(key, value);

  @override
  bool getBool(String key, {bool defaultValue = false}) =>
      _prefs.getBool(key) ?? defaultValue;

  @override
  Future<void> setBool(String key, {required bool value}) =>
      _prefs.setBool(key, value);

  @override
  int? getInt(String key) => _prefs.getInt(key);

  @override
  Future<void> setInt(String key, int value) => _prefs.setInt(key, value);

  @override
  Future<void> remove(String key) => _prefs.remove(key);

  @override
  Future<void> clear() async {
    // Preserve install-scoped values that are not tied to the signed-in user.
    final String? deviceId = _prefs.getString(PreferenceKeys.deviceId);
    final bool seenOnboarding =
        _prefs.getBool(PreferenceKeys.hasSeenOnboarding) ?? false;

    await _prefs.clear();

    if (deviceId != null) {
      await _prefs.setString(PreferenceKeys.deviceId, deviceId);
    }
    await _prefs.setBool(PreferenceKeys.hasSeenOnboarding, seenOnboarding);
  }
}

/// In-memory implementation used by tests.
class InMemoryPreferencesService implements PreferencesService {
  final Map<String, Object> _values = <String, Object>{};

  @override
  String? getString(String key) => _values[key] as String?;

  @override
  Future<void> setString(String key, String value) async =>
      _values[key] = value;

  @override
  bool getBool(String key, {bool defaultValue = false}) =>
      _values[key] as bool? ?? defaultValue;

  @override
  Future<void> setBool(String key, {required bool value}) async =>
      _values[key] = value;

  @override
  int? getInt(String key) => _values[key] as int?;

  @override
  Future<void> setInt(String key, int value) async => _values[key] = value;

  @override
  Future<void> remove(String key) async => _values.remove(key);

  @override
  Future<void> clear() async => _values.clear();
}
