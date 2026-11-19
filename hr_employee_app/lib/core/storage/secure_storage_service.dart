import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';

/// Encrypted key/value storage for credentials.
///
/// Declared as an interface so tests and the mock data source can substitute
/// an in-memory implementation without touching the platform keystore.
abstract interface class SecureStorageService {
  Future<String?> read(String key);

  Future<void> write(String key, String value);

  Future<void> delete(String key);

  /// Removes every value written by this app.
  Future<void> clear();
}

/// Backed by the Android Keystore and the iOS Keychain.
class FlutterSecureStorageService implements SecureStorageService {
  FlutterSecureStorageService({FlutterSecureStorage? storage})
    : _storage =
          storage ??
          const FlutterSecureStorage(
            aOptions: AndroidOptions(encryptedSharedPreferences: true),
            iOptions: IOSOptions(
              // Keeps tokens off iCloud/iTunes backups and requires the device
              // to have been unlocked at least once since boot.
              accessibility: KeychainAccessibility.first_unlock_this_device,
            ),
          );

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read(String key) async {
    try {
      return await _storage.read(key: key);
    } on Object catch (_) {
      // A corrupted keystore entry must not crash start-up; treat it as absent
      // so the user is simply asked to sign in again.
      return null;
    }
  }

  @override
  Future<void> write(String key, String value) async {
    try {
      await _storage.write(key: key, value: value);
    } on Object catch (_) {
      throw const CacheException('Could not save secure data on this device.');
    }
  }

  @override
  Future<void> delete(String key) async {
    try {
      await _storage.delete(key: key);
    } on Object catch (_) {
      // Deleting is best-effort: a failure here must not block sign-out.
    }
  }

  @override
  Future<void> clear() async {
    try {
      await _storage.deleteAll();
    } on Object catch (_) {
      // Same reasoning as [delete].
    }
  }
}

/// In-memory implementation used by tests and by mock mode.
class InMemorySecureStorageService implements SecureStorageService {
  final Map<String, String> _values = <String, String>{};

  @override
  Future<String?> read(String key) async => _values[key];

  @override
  Future<void> write(String key, String value) async => _values[key] = value;

  @override
  Future<void> delete(String key) async => _values.remove(key);

  @override
  Future<void> clear() async => _values.clear();
}
