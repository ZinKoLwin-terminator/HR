import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';

part 'auth_state.freezed.dart';

/// Whether anyone is signed in.
///
/// A sealed union rather than a class with nullable fields, so "signed in
/// with no user" is not representable and the router can switch exhaustively.
@freezed
sealed class AuthState with _$AuthState {
  /// The stored session has not been inspected yet. The splash screen shows
  /// while this is the state.
  const factory AuthState.unknown() = AuthUnknown;

  /// No valid session. The router sends the user to the login screen.
  const factory AuthState.unauthenticated({
    /// Set when the user arrived here because the session lapsed rather than
    /// because they signed out, so the login screen can explain why.
    @Default(false) bool wasSessionExpired,
  }) = AuthUnauthenticated;

  /// Signed in.
  const factory AuthState.authenticated(AuthenticatedUser user) =
      AuthAuthenticated;
}

/// Convenience accessors used by the router and the app shell.
extension AuthStateX on AuthState {
  bool get isAuthenticated => this is AuthAuthenticated;

  bool get isUnknown => this is AuthUnknown;

  AuthenticatedUser? get userOrNull =>
      this is AuthAuthenticated ? (this as AuthAuthenticated).user : null;

  /// True when the signed-in account is on a temporary password and must set
  /// a new one before using the rest of the app.
  bool get requiresPasswordChange => userOrNull?.mustChangePassword ?? false;
}
