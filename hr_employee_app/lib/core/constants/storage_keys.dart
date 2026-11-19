/// Keys used for persisted values.
///
/// Centralised so a key is never spelled two different ways, and so it is
/// obvious at a glance which values are secret and which are not.
abstract final class SecureStorageKeys {
  /// JWT used to authorise API calls.
  static const String accessToken = 'auth.access_token';

  /// Long-lived token used to obtain a new access token.
  static const String refreshToken = 'auth.refresh_token';

  /// ISO-8601 expiry of the access token.
  static const String accessTokenExpiresAt = 'auth.access_token_expires_at';

  /// ISO-8601 expiry of the refresh token.
  static const String refreshTokenExpiresAt = 'auth.refresh_token_expires_at';

  /// Cached identity of the signed-in employee, used to render the shell
  /// before the profile request completes.
  static const String cachedUser = 'auth.cached_user';
}

/// Non-sensitive preferences.
///
/// Nothing here may identify a person or carry a credential; those belong in
/// [SecureStorageKeys].
abstract final class PreferenceKeys {
  /// Stable per-install id sent with auth and push registration.
  static const String deviceId = 'device.id';

  /// Whether the onboarding carousel has been dismissed.
  static const String hasSeenOnboarding = 'app.has_seen_onboarding';

  /// Last successfully used login identifier, to pre-fill the field.
  ///
  /// An employee ID is low-sensitivity and never a credential, but it is still
  /// only written when the user ticks "remember me".
  static const String rememberedUserName = 'auth.remembered_user_name';

  /// Whether the login screen should pre-fill the identifier.
  static const String rememberMe = 'auth.remember_me';

  /// Whether salary figures start hidden on the dashboard and payslip list.
  static const String maskSalaryAmounts = 'payslip.mask_amounts';

  /// Selected theme mode name, one of `system`, `light`, `dark`.
  static const String themeMode = 'app.theme_mode';

  /// IETF language tag of the chosen locale, e.g. `en`.
  static const String localeTag = 'app.locale_tag';
}
