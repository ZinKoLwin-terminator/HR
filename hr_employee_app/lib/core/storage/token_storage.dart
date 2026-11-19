import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:hr_employee_app/core/storage/secure_storage_service.dart';

/// The credential pair plus its lifetimes.
///
/// A plain immutable class rather than a Freezed model: this is infrastructure
/// that lives below the domain layer, it has no JSON contract of its own, and
/// keeping it codegen-free means the networking stack compiles before
/// `build_runner` has ever run.
class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.refreshToken,
    required this.accessTokenExpiresAt,
    required this.refreshTokenExpiresAt,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime accessTokenExpiresAt;
  final DateTime refreshTokenExpiresAt;

  /// True once the access token is within [AppConstants.tokenExpiryLeeway] of
  /// expiring, so it is refreshed before a request can fail on it.
  bool get isAccessTokenExpired => DateTime.now().toUtc().isAfter(
    accessTokenExpiresAt.toUtc().subtract(AppConstants.tokenExpiryLeeway),
  );

  /// True when the refresh token itself has lapsed; the user must sign in.
  bool get isRefreshTokenExpired =>
      DateTime.now().toUtc().isAfter(refreshTokenExpiresAt.toUtc());

  /// A session is usable while the refresh token is still valid, even if the
  /// access token has expired, because it can be exchanged silently.
  bool get canBeRestored => !isRefreshTokenExpired && refreshToken.isNotEmpty;

  AuthTokens copyWith({
    String? accessToken,
    String? refreshToken,
    DateTime? accessTokenExpiresAt,
    DateTime? refreshTokenExpiresAt,
  }) {
    return AuthTokens(
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      accessTokenExpiresAt: accessTokenExpiresAt ?? this.accessTokenExpiresAt,
      refreshTokenExpiresAt:
          refreshTokenExpiresAt ?? this.refreshTokenExpiresAt,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AuthTokens &&
          accessToken == other.accessToken &&
          refreshToken == other.refreshToken &&
          accessTokenExpiresAt == other.accessTokenExpiresAt &&
          refreshTokenExpiresAt == other.refreshTokenExpiresAt;

  @override
  int get hashCode => Object.hash(
    accessToken,
    refreshToken,
    accessTokenExpiresAt,
    refreshTokenExpiresAt,
  );

  /// Deliberately omits the token values so they cannot leak into a log line.
  @override
  String toString() =>
      'AuthTokens(accessTokenExpiresAt: $accessTokenExpiresAt, '
      'refreshTokenExpiresAt: $refreshTokenExpiresAt)';
}

/// Persists [AuthTokens] in encrypted storage.
///
/// Lives in `core` rather than in the auth feature because the Dio
/// interceptor needs it, and `core` must not depend on `features`.
class TokenStorage {
  const TokenStorage(this._storage);

  final SecureStorageService _storage;

  /// Returns the stored tokens, or null when any required part is missing or
  /// unreadable.
  Future<AuthTokens?> read() async {
    final String? accessToken = await _storage.read(
      SecureStorageKeys.accessToken,
    );
    final String? refreshToken = await _storage.read(
      SecureStorageKeys.refreshToken,
    );
    final String? accessExpiry = await _storage.read(
      SecureStorageKeys.accessTokenExpiresAt,
    );
    final String? refreshExpiry = await _storage.read(
      SecureStorageKeys.refreshTokenExpiresAt,
    );

    if (accessToken == null ||
        refreshToken == null ||
        accessExpiry == null ||
        refreshExpiry == null) {
      return null;
    }

    final DateTime? accessExpiresAt = DateTime.tryParse(accessExpiry);
    final DateTime? refreshExpiresAt = DateTime.tryParse(refreshExpiry);
    if (accessExpiresAt == null || refreshExpiresAt == null) {
      return null;
    }

    return AuthTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
      accessTokenExpiresAt: accessExpiresAt,
      refreshTokenExpiresAt: refreshExpiresAt,
    );
  }

  Future<void> save(AuthTokens tokens) async {
    await _storage.write(SecureStorageKeys.accessToken, tokens.accessToken);
    await _storage.write(SecureStorageKeys.refreshToken, tokens.refreshToken);
    await _storage.write(
      SecureStorageKeys.accessTokenExpiresAt,
      tokens.accessTokenExpiresAt.toUtc().toIso8601String(),
    );
    await _storage.write(
      SecureStorageKeys.refreshTokenExpiresAt,
      tokens.refreshTokenExpiresAt.toUtc().toIso8601String(),
    );
  }

  Future<void> clear() async {
    await _storage.delete(SecureStorageKeys.accessToken);
    await _storage.delete(SecureStorageKeys.refreshToken);
    await _storage.delete(SecureStorageKeys.accessTokenExpiresAt);
    await _storage.delete(SecureStorageKeys.refreshTokenExpiresAt);
    await _storage.delete(SecureStorageKeys.cachedUser);
  }

  /// Convenience for the request interceptor.
  Future<String?> readAccessToken() async {
    return _storage.read(SecureStorageKeys.accessToken);
  }
}
