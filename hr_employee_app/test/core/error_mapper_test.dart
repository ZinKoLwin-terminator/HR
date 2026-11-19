import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/error_mapper.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';

/// The error contract in `docs/api_contracts.md` §1.4 is only as good as the
/// mapping that implements it, so each status gets a test.
void main() {
  RequestOptions options() => RequestOptions(path: '/attendance/check-in');

  DioException responseError(int statusCode, {Object? body}) {
    return DioException(
      requestOptions: options(),
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: options(),
        statusCode: statusCode,
        data: body,
      ),
    );
  }

  group('ErrorMapper.fromDio transport failures', () {
    test('maps connection errors to NetworkException', () {
      final AppException result = ErrorMapper.fromDio(
        DioException(
          requestOptions: options(),
          type: DioExceptionType.connectionError,
        ),
      );
      expect(result, isA<NetworkException>());
    });

    test('maps a SocketException to NetworkException', () {
      final AppException result = ErrorMapper.fromDio(
        DioException(
          requestOptions: options(),
          error: const SocketException('no route'),
        ),
      );
      expect(result, isA<NetworkException>());
    });

    test('maps every timeout kind to RequestTimeoutException', () {
      for (final DioExceptionType type in <DioExceptionType>[
        DioExceptionType.connectionTimeout,
        DioExceptionType.sendTimeout,
        DioExceptionType.receiveTimeout,
      ]) {
        expect(
          ErrorMapper.fromDio(
            DioException(requestOptions: options(), type: type),
          ),
          isA<RequestTimeoutException>(),
          reason: '$type should be a timeout',
        );
      }
    });

    test('maps cancellation to CancelledException', () {
      expect(
        ErrorMapper.fromDio(
          DioException(
            requestOptions: options(),
            type: DioExceptionType.cancel,
          ),
        ),
        isA<CancelledException>(),
      );
    });
  });

  group('ErrorMapper.fromDio status codes', () {
    test('400 becomes a ValidationException carrying field errors', () {
      final AppException result = ErrorMapper.fromDio(
        responseError(
          400,
          body: <String, dynamic>{
            'success': false,
            'message': 'Validation failed.',
            'errors': <String, dynamic>{
              'password': <String>['Password must be at least 8 characters.'],
            },
            'traceId': 'trace-1',
          },
        ),
      );

      expect(result, isA<ValidationException>());
      final ValidationException validation = result as ValidationException;
      expect(validation.message, 'Validation failed.');
      expect(validation.traceId, 'trace-1');
      expect(validation.fieldErrors['password'], <String>[
        'Password must be at least 8 characters.',
      ]);
    });

    test('422 is also a ValidationException', () {
      expect(responseErrorType(422), isA<ValidationException>());
    });

    test('401 becomes UnauthorizedException', () {
      expect(responseErrorType(401), isA<UnauthorizedException>());
    });

    test('403 becomes ForbiddenException', () {
      expect(responseErrorType(403), isA<ForbiddenException>());
    });

    test('404 becomes NotFoundException', () {
      expect(responseErrorType(404), isA<NotFoundException>());
    });

    test('409 becomes ConflictException', () {
      expect(responseErrorType(409), isA<ConflictException>());
    });

    test('429 becomes RateLimitException', () {
      expect(responseErrorType(429), isA<RateLimitException>());
    });

    test('5xx becomes ServerException', () {
      expect(responseErrorType(500), isA<ServerException>());
      expect(responseErrorType(503), isA<ServerException>());
    });
  });

  group('ErrorMapper error-body parsing', () {
    test('lifts the first field error when no summary is sent', () {
      final AppException result = ErrorMapper.fromDio(
        responseError(
          400,
          body: <String, dynamic>{
            'errors': <String, dynamic>{
              'startDate': <String>['The start date is in the past.'],
            },
          },
        ),
      );

      expect(result.message, 'The start date is in the past.');
    });

    test('falls back to ProblemDetails title', () {
      final AppException result = ErrorMapper.fromDio(
        responseError(
          409,
          body: <String, dynamic>{'title': 'Already checked in today.'},
        ),
      );
      expect(result.message, 'Already checked in today.');
    });

    test('survives a non-JSON body', () {
      // Proxies often return HTML for a 502; describing one error must not
      // raise another.
      final AppException result = ErrorMapper.fromDio(
        responseError(502, body: '<html>Bad Gateway</html>'),
      );
      expect(result, isA<ServerException>());
      expect(result.message, isNotEmpty);
    });

    test('survives a null body', () {
      expect(responseErrorType(500), isA<ServerException>());
    });

    test('accepts a string instead of a list in errors', () {
      final AppException result = ErrorMapper.fromDio(
        responseError(
          400,
          body: <String, dynamic>{
            'errors': <String, dynamic>{'email': 'Not a valid address.'},
          },
        ),
      );
      expect(result, isA<ValidationException>());
      expect((result as ValidationException).fieldErrors['email'], <String>[
        'Not a valid address.',
      ]);
    });
  });

  group('ErrorMapper.toFailure', () {
    test('maps each exception to its user-facing failure', () {
      expect(
        ErrorMapper.toFailure(const NetworkException()),
        isA<NetworkFailure>(),
      );
      expect(
        ErrorMapper.toFailure(const UnauthorizedException()),
        isA<SessionExpiredFailure>(),
      );
      expect(
        ErrorMapper.toFailure(const ForbiddenException()),
        isA<PermissionDeniedFailure>(),
      );
      expect(
        ErrorMapper.toFailure(const NotFoundException()),
        isA<NotFoundFailure>(),
      );
      expect(
        ErrorMapper.toFailure(const ServerException('boom')),
        isA<ServerFailure>(),
      );
    });

    test('preserves field errors through the boundary', () {
      final Failure failure = ErrorMapper.toFailure(
        const ValidationException(
          'Check your details.',
          fieldErrors: <String, List<String>>{
            'reason': <String>['Too short.'],
          },
        ),
      );

      expect(failure, isA<ValidationFailure>());
      expect((failure as ValidationFailure).errorFor('reason'), 'Too short.');
    });

    test('maps an unknown object to UnexpectedFailure', () {
      // Anything that escapes the data layer without being an AppException
      // is a bug, and must still be reportable rather than crashing.
      expect(
        ErrorMapper.toFailure(ArgumentError('not an AppException')),
        isA<UnexpectedFailure>(),
      );
    });

    test('carries the permanently-denied flag for permissions', () {
      final Failure failure = ErrorMapper.toFailure(
        const PermissionException('No location', isPermanentlyDenied: true),
      );
      expect(failure, isA<DevicePermissionFailure>());
      expect((failure as DevicePermissionFailure).isPermanentlyDenied, isTrue);
    });
  });
}

/// Maps a bare status code, for the one-line status assertions above.
AppException responseErrorType(int statusCode) {
  final RequestOptions options = RequestOptions(path: '/test');
  return ErrorMapper.fromDio(
    DioException(
      requestOptions: options,
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: options,
        statusCode: statusCode,
      ),
    ),
  );
}
