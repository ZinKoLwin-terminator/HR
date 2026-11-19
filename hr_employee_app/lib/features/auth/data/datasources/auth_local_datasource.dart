import 'dart:convert';

import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:hr_employee_app/core/storage/secure_storage_service.dart';
import 'package:hr_employee_app/features/auth/data/models/auth_user_model.dart';

/// Caches the signed-in identity between launches.
///
/// Stored in **secure** storage rather than preferences: it is not a
/// credential, but it is personal data (name, work email, employee ID) and
/// does not belong in a world-readable file.
abstract interface class AuthLocalDataSource {
  Future<AuthUserModel?> readUser();

  Future<void> saveUser(AuthUserModel user);

  Future<void> clear();
}

class AuthLocalDataSourceImpl implements AuthLocalDataSource {
  const AuthLocalDataSourceImpl(this._storage);

  final SecureStorageService _storage;

  @override
  Future<AuthUserModel?> readUser() async {
    final String? raw = await _storage.read(SecureStorageKeys.cachedUser);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    try {
      final Object? decoded = jsonDecode(raw);
      if (decoded is! Map) {
        return null;
      }
      return AuthUserModel.fromJson(decoded.cast<String, dynamic>());
    } on Object catch (_) {
      // A cache written by an older app version may no longer parse. Treat it
      // as absent; the next profile fetch replaces it.
      return null;
    }
  }

  @override
  Future<void> saveUser(AuthUserModel user) =>
      _storage.write(SecureStorageKeys.cachedUser, jsonEncode(user.toJson()));

  @override
  Future<void> clear() => _storage.delete(SecureStorageKeys.cachedUser);
}
