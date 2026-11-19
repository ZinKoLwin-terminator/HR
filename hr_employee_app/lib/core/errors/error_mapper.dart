import 'package:dio/dio.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/core/platform/network_error.dart';

/// Translates transport and platform errors into the app's own error types.
///
/// There are exactly two hops:
///   1. data source: `DioException` -> [AppException]  ([ErrorMapper.fromDio])
///   2. repository:  [AppException] -> [Failure]       ([ErrorMapper.toFailure])
///
/// Keeping the mapping in one place means the error contract in
/// `docs/api_contracts.md` is honoured consistently by every feature.
abstract final class ErrorMapper {
  /// Converts a [DioException] into the matching [AppException].
  static AppException fromDio(DioException exception) {
    switch (exception.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      // Decoding the response exceeded its budget. Rare, and nothing the user
      // can act on differently, so it joins the other timeouts.
      case DioExceptionType.transformTimeout:
        return const RequestTimeoutException();

      case DioExceptionType.cancel:
        return const CancelledException();

      case DioExceptionType.badCertificate:
        return const ServerException(
          'The server certificate could not be verified.',
        );

      case DioExceptionType.connectionError:
        return const NetworkException();

      case DioExceptionType.unknown:
        if (isHostUnreachableError(exception.error)) {
          return const NetworkException();
        }
        return UnknownException(exception.message ?? 'Something went wrong.');

      case DioExceptionType.badResponse:
        return _fromResponse(exception.response);
    }
  }

  /// Converts an HTTP error response into the matching [AppException].
  static AppException _fromResponse(Response<dynamic>? response) {
    final int status = response?.statusCode ?? 0;
    final _ErrorBody body = _ErrorBody.parse(response?.data);

    switch (status) {
      case 400:
      case 422:
        return ValidationException(
          body.message ?? 'Please check the information you entered.',
          fieldErrors: body.fieldErrors,
          statusCode: status,
          traceId: body.traceId,
        );
      case 401:
        return UnauthorizedException(
          body.message ?? 'Your session has expired. Please sign in again.',
        );
      case 403:
        return ForbiddenException(
          body.message ?? 'You do not have permission to perform this action.',
        );
      case 404:
        return NotFoundException(
          body.message ?? 'The requested item was not found.',
        );
      case 409:
        return ConflictException(
          body.message ?? 'This action conflicts with the current state.',
          statusCode: status,
          traceId: body.traceId,
        );
      case 429:
        return RateLimitException(
          body.message ?? 'Too many attempts. Please wait and try again.',
        );
      default:
        if (status >= 500) {
          return ServerException(
            body.message ?? 'The server is temporarily unavailable.',
            statusCode: status,
            traceId: body.traceId,
          );
        }
        return UnknownException(body.message ?? 'Something went wrong.');
    }
  }

  /// Converts an [AppException] into the [Failure] the domain layer exposes.
  static Failure toFailure(Object error) {
    if (error is! AppException) {
      return const UnexpectedFailure();
    }

    return switch (error) {
      NetworkException() => NetworkFailure(error.message),
      RequestTimeoutException() => TimeoutFailure(error.message),
      ServerException() => ServerFailure(error.message, error.traceId),
      UnauthorizedException() => SessionExpiredFailure(error.message),
      ForbiddenException() => PermissionDeniedFailure(error.message),
      NotFoundException() => NotFoundFailure(error.message),
      ValidationException() => ValidationFailure(
        error.message,
        fieldErrors: error.fieldErrors,
        traceId: error.traceId,
      ),
      ConflictException() => ConflictFailure(
        error.message,
        traceId: error.traceId,
      ),
      RateLimitException() => RateLimitFailure(error.message),
      CacheException() => CacheFailure(error.message),
      ParsingException() => UnexpectedFailure(error.message),
      PermissionException() => DevicePermissionFailure(
        error.message,
        isPermanentlyDenied: error.isPermanentlyDenied,
      ),
      LocationException() => LocationFailure(error.message),
      CancelledException() => UnexpectedFailure(error.message),
      UnknownException() => UnexpectedFailure(error.message),
    };
  }
}

/// The error half of the response envelope described in `docs/api_contracts.md`.
///
/// Parsing is defensive on purpose: an error path must never throw while
/// trying to describe another error.
class _ErrorBody {
  const _ErrorBody({this.message, this.traceId, this.fieldErrors = const {}});

  factory _ErrorBody.parse(Object? data) {
    if (data is! Map) {
      // Some proxies return plain text or HTML for 5xx responses.
      final String? text = data is String && data.trim().isNotEmpty
          ? data.trim()
          : null;
      return _ErrorBody(message: text);
    }

    final Map<String, dynamic> map = data.cast<String, dynamic>();

    String? message = map['message'] as String?;
    // ASP.NET Core ProblemDetails fallback.
    message ??= map['title'] as String?;
    message ??= map['detail'] as String?;

    final Map<String, List<String>> fieldErrors = <String, List<String>>{};
    final Object? rawErrors = map['errors'];
    if (rawErrors is Map) {
      for (final MapEntry<Object?, Object?> entry in rawErrors.entries) {
        final String key = entry.key.toString();
        final Object? value = entry.value;
        if (value is List) {
          fieldErrors[key] = value.map((Object? e) => e.toString()).toList();
        } else if (value is String) {
          fieldErrors[key] = <String>[value];
        }
      }
    }

    // When the server sent field errors but no summary, lift the first one up
    // so the user always sees something specific.
    if ((message == null || message.isEmpty) && fieldErrors.isNotEmpty) {
      final List<String> first = fieldErrors.values.first;
      if (first.isNotEmpty) {
        message = first.first;
      }
    }

    return _ErrorBody(
      message: (message == null || message.isEmpty) ? null : message,
      traceId: map['traceId'] as String?,
      fieldErrors: fieldErrors,
    );
  }

  final String? message;
  final String? traceId;
  final Map<String, List<String>> fieldErrors;
}
