/// Domain-facing error values.
///
/// A [Failure] is what the domain and presentation layers see. It is plain
/// data, safe to put in widget state, and carries a message already written
/// for an end user. Exceptions never cross the repository boundary.
library;

/// Base type for every recoverable error surfaced to the domain layer.
sealed class Failure {
  const Failure(this.message, {this.traceId});

  /// Human-readable text, safe to show in the UI.
  final String message;

  /// Server correlation id, for support tickets and log lookups.
  final String? traceId;

  @override
  String toString() => '$runtimeType($message)';

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is Failure &&
          runtimeType == other.runtimeType &&
          message == other.message &&
          traceId == other.traceId;

  @override
  int get hashCode => Object.hash(runtimeType, message, traceId);
}

/// No usable connection to the server.
class NetworkFailure extends Failure {
  const NetworkFailure([
    super.message = 'No internet connection. Check your network and try again.',
  ]);
}

/// The request took too long.
class TimeoutFailure extends Failure {
  const TimeoutFailure([
    super.message = 'The request timed out. Please try again.',
  ]);
}

/// The server failed to handle a well-formed request.
class ServerFailure extends Failure {
  /// [traceId] is named because the base constructor declares it that way;
  /// it carries the server's correlation id for support lookups.
  const ServerFailure([
    super.message = 'The server is temporarily unavailable.',
    String? traceId,
  ]) : super(traceId: traceId);
}

/// The session is no longer valid.
///
/// The auth layer listens for this and signs the user out; no other feature
/// should handle it specially.
class SessionExpiredFailure extends Failure {
  const SessionExpiredFailure([
    super.message = 'Your session has expired. Please sign in again.',
  ]);
}

/// Wrong credentials, or an account that cannot sign in.
class AuthenticationFailure extends Failure {
  const AuthenticationFailure([super.message = 'Incorrect ID or password.']);
}

/// Authenticated but not permitted.
class PermissionDeniedFailure extends Failure {
  const PermissionDeniedFailure([
    super.message = 'You do not have permission to do this.',
  ]);
}

/// Server-side validation rejected the submitted values.
class ValidationFailure extends Failure {
  const ValidationFailure(
    super.message, {
    this.fieldErrors = const <String, List<String>>{},
    super.traceId,
  });

  /// Field name to messages, so a form can show errors against inputs.
  final Map<String, List<String>> fieldErrors;

  /// First message recorded for [field], or null when the field is clean.
  String? errorFor(String field) {
    final List<String>? errors = fieldErrors[field];
    return (errors == null || errors.isEmpty) ? null : errors.first;
  }
}

/// The resource does not exist.
class NotFoundFailure extends Failure {
  const NotFoundFailure([super.message = 'Not found.']);
}

/// A business rule rejected the operation, e.g. already checked in.
class ConflictFailure extends Failure {
  const ConflictFailure(super.message, {super.traceId});
}

/// Too many requests in a short window.
class RateLimitFailure extends Failure {
  const RateLimitFailure([
    super.message = 'Too many attempts. Please wait a moment and try again.',
  ]);
}

/// Local storage could not be read or written.
class CacheFailure extends Failure {
  const CacheFailure([super.message = 'Could not read local data.']);
}

/// A runtime permission was refused.
class DevicePermissionFailure extends Failure {
  const DevicePermissionFailure(
    super.message, {
    required this.isPermanentlyDenied,
  });

  /// When true, direct the user to system settings instead of re-prompting.
  final bool isPermanentlyDenied;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is DevicePermissionFailure &&
          message == other.message &&
          isPermanentlyDenied == other.isPermanentlyDenied;

  @override
  int get hashCode => Object.hash(runtimeType, message, isPermanentlyDenied);
}

/// Location services are disabled, or no fix could be obtained.
class LocationFailure extends Failure {
  const LocationFailure(super.message);
}

/// Anything not covered above.
class UnexpectedFailure extends Failure {
  const UnexpectedFailure([
    super.message = 'Something went wrong. Please try again.',
    String? traceId,
  ]) : super(traceId: traceId);
}
