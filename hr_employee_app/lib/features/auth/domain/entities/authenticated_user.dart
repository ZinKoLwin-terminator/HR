import 'package:freezed_annotation/freezed_annotation.dart';

part 'authenticated_user.freezed.dart';

/// The identity of the signed-in employee.
///
/// Deliberately small: it holds only what the app shell needs to render
/// before the full profile loads (greeting, avatar, role line). The complete
/// record lives in the profile feature.
///
/// Note what is absent — no tokens. Credentials never enter the domain layer;
/// the auth repository writes them straight to secure storage.
@freezed
abstract class AuthenticatedUser with _$AuthenticatedUser {
  const factory AuthenticatedUser({
    required String userId,
    required String employeeId,
    required String fullName,
    required String email,
    String? department,
    String? designation,
    String? profilePhotoUrl,

    /// Set by the server when the account is on a temporary password.
    /// The router forces the change-password screen while this is true.
    @Default(false) bool mustChangePassword,
  }) = _AuthenticatedUser;

  const AuthenticatedUser._();

  /// `Finance - Senior Accountant`, with either part omitted when unknown.
  String get roleLine {
    final List<String> parts = <String>[
      if (designation != null && designation!.isNotEmpty) designation!,
      if (department != null && department!.isNotEmpty) department!,
    ];
    return parts.join(' - ');
  }
}
