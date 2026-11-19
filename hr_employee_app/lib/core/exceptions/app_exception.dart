/// Low-level exceptions thrown by the data layer.
///
/// These never reach the presentation layer: repositories translate them into
/// `Failure` values so that UI code handles data rather than control flow.
library;

/// Base type for every exception raised inside the data layer.
sealed class AppException implements Exception {
  const AppException(this.message, {this.statusCode, this.traceId});

  final String message;
  final int? statusCode;

  /// Server-side correlation id, surfaced in logs to help backend debugging.
  final String? traceId;

  @override
  String toString() =>
      '$runtimeType(statusCode: $statusCode, message: $message)';
}

/// The device could not reach the server at all (no connectivity, DNS, TLS).
class NetworkException extends AppException {
  const NetworkException([super.message = 'No internet connection.']);
}

/// The request did not complete within the configured timeout.
class RequestTimeoutException extends AppException {
  const RequestTimeoutException([super.message = 'The request timed out.']);
}

/// The server replied with a 5xx status.
class ServerException extends AppException {
  const ServerException(super.message, {super.statusCode, super.traceId});
}

/// The access token is missing, expired or rejected and could not be refreshed.
class UnauthorizedException extends AppException {
  const UnauthorizedException([
    super.message = 'Your session has expired. Please sign in again.',
  ]) : super(statusCode: 401);
}

/// Authenticated, but the account lacks permission for this operation.
class ForbiddenException extends AppException {
  const ForbiddenException([
    super.message = 'You do not have permission to perform this action.',
  ]) : super(statusCode: 403);
}

/// The requested resource does not exist.
class NotFoundException extends AppException {
  const NotFoundException([super.message = 'The requested item was not found.'])
    : super(statusCode: 404);
}

/// 400 or 422 — the server rejected the payload.
///
/// [fieldErrors] maps a field name to its messages so forms can show errors
/// inline. The key `_` holds errors that belong to no particular field.
class ValidationException extends AppException {
  const ValidationException(
    super.message, {
    this.fieldErrors = const <String, List<String>>{},
    super.statusCode,
    super.traceId,
  });

  final Map<String, List<String>> fieldErrors;
}

/// A business rule blocked the request, for example already checked in today.
class ConflictException extends AppException {
  const ConflictException(super.message, {super.statusCode, super.traceId});
}

/// Too many requests.
class RateLimitException extends AppException {
  const RateLimitException(super.message, {this.retryAfter})
    : super(statusCode: 429);

  final Duration? retryAfter;
}

/// Reading from or writing to local storage failed.
class CacheException extends AppException {
  const CacheException([super.message = 'Local storage is unavailable.']);
}

/// The response body did not match the agreed contract.
class ParsingException extends AppException {
  const ParsingException([
    super.message = 'The server returned an unexpected response.',
  ]);
}

/// The user denied, or the OS withheld, a required runtime permission.
class PermissionException extends AppException {
  const PermissionException(super.message, {required this.isPermanentlyDenied});

  /// When true the user must change the setting in system settings; the OS
  /// will not show an in-app prompt again.
  final bool isPermanentlyDenied;
}

/// Device location services are off, or a position fix could not be obtained.
class LocationException extends AppException {
  const LocationException(super.message);
}

/// The operation was cancelled before it completed, usually by the user.
class CancelledException extends AppException {
  const CancelledException([super.message = 'The request was cancelled.']);
}

/// Anything that does not fit the cases above.
class UnknownException extends AppException {
  const UnknownException([super.message = 'Something went wrong.']);
}
