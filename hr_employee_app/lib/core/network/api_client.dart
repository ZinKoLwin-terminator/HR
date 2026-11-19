import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:hr_employee_app/core/errors/error_mapper.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/shared/models/api_response.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// The single entry point to the HTTP layer.
///
/// Responsibilities, and deliberately nothing else:
///   * issue the request through the configured [Dio];
///   * unwrap the response envelope described in `docs/api_contracts.md`;
///   * convert any transport error into an [AppException].
///
/// It knows nothing about tokens (the interceptor owns that), nothing about
/// domain types (data sources own that) and nothing about widgets.
class ApiClient {
  const ApiClient(this._dio);

  final Dio _dio;

  /// Exposed so a data source can build a `FormData` with the same options.
  Dio get rawClient => _dio;

  Future<T> get<T>(
    String path, {
    required T Function(Object? data) decode,
    Map<String, dynamic>? queryParameters,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      decode: decode,
      request: () => _dio.get<dynamic>(
        path,
        queryParameters: _clean(queryParameters),
        cancelToken: cancelToken,
      ),
    );
  }

  /// Fetches one page of a list endpoint.
  Future<PaginatedResponse<T>> getPage<T>(
    String path, {
    required T Function(Map<String, dynamic> json) fromItem,
    required PageRequest page,
    Map<String, dynamic>? queryParameters,
    CancelToken? cancelToken,
  }) {
    return get<PaginatedResponse<T>>(
      path,
      decode: (Object? data) => PaginatedResponse<T>.fromJson(data, fromItem),
      queryParameters: <String, dynamic>{
        'pageNumber': page.pageNumber,
        'pageSize': page.pageSize,
        ...?queryParameters,
      },
      cancelToken: cancelToken,
    );
  }

  Future<T> post<T>(
    String path, {
    required T Function(Object? data) decode,
    Object? body,
    Map<String, dynamic>? queryParameters,
    Map<String, dynamic>? headers,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      decode: decode,
      request: () => _dio.post<dynamic>(
        path,
        data: body,
        queryParameters: _clean(queryParameters),
        options: headers == null ? null : Options(headers: headers),
        cancelToken: cancelToken,
      ),
    );
  }

  /// For endpoints that answer `204 No Content`.
  Future<void> postVoid(
    String path, {
    Object? body,
    Map<String, dynamic>? headers,
    CancelToken? cancelToken,
  }) {
    return _sendVoid(
      request: () => _dio.post<dynamic>(
        path,
        data: body,
        options: headers == null ? null : Options(headers: headers),
        cancelToken: cancelToken,
      ),
    );
  }

  Future<T> put<T>(
    String path, {
    required T Function(Object? data) decode,
    Object? body,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      decode: decode,
      request: () =>
          _dio.put<dynamic>(path, data: body, cancelToken: cancelToken),
    );
  }

  Future<void> putVoid(String path, {Object? body, CancelToken? cancelToken}) {
    return _sendVoid(
      request: () =>
          _dio.put<dynamic>(path, data: body, cancelToken: cancelToken),
    );
  }

  Future<void> deleteVoid(String path, {CancelToken? cancelToken}) {
    return _sendVoid(
      request: () => _dio.delete<dynamic>(path, cancelToken: cancelToken),
    );
  }

  /// Submits `multipart/form-data`, used for leave attachments and the
  /// profile photo.
  Future<T> upload<T>(
    String path, {
    required FormData formData,
    required T Function(Object? data) decode,
    String method = 'POST',
    ProgressCallback? onSendProgress,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      decode: decode,
      request: () => _dio.request<dynamic>(
        path,
        data: formData,
        options: Options(method: method, contentType: 'multipart/form-data'),
        onSendProgress: onSendProgress,
        cancelToken: cancelToken,
      ),
    );
  }

  /// Downloads binary content such as a payslip PDF.
  ///
  /// Returns the bytes rather than writing to disk so the caller decides where
  /// the file lives — payslips go to app-private storage only.
  Future<Uint8List> downloadBytes(
    String path, {
    Map<String, dynamic>? queryParameters,
    ProgressCallback? onReceiveProgress,
    CancelToken? cancelToken,
  }) async {
    try {
      final Response<List<int>> response = await _dio.get<List<int>>(
        path,
        queryParameters: _clean(queryParameters),
        options: Options(responseType: ResponseType.bytes),
        onReceiveProgress: onReceiveProgress,
        cancelToken: cancelToken,
      );
      final List<int>? bytes = response.data;
      if (bytes == null || bytes.isEmpty) {
        throw const ParsingException('The file could not be downloaded.');
      }
      return Uint8List.fromList(bytes);
    } on DioException catch (error) {
      throw ErrorMapper.fromDio(error);
    }
  }

  /// Runs [request], unwraps the envelope and decodes the payload.
  Future<T> _send<T>({
    required Future<Response<dynamic>> Function() request,
    required T Function(Object? data) decode,
  }) async {
    try {
      final Response<dynamic> response = await request();
      return ApiResponse<T>.fromJson(response.data, decode).requireData;
    } on DioException catch (error) {
      throw ErrorMapper.fromDio(error);
    } on AppException {
      rethrow;
    } on Object catch (error, stackTrace) {
      // A cast or missing-field error while decoding is a contract mismatch,
      // not a crash; surface it as one.
      Error.throwWithStackTrace(const ParsingException(), stackTrace);
    }
  }

  Future<void> _sendVoid({
    required Future<Response<dynamic>> Function() request,
  }) async {
    try {
      await request();
    } on DioException catch (error) {
      throw ErrorMapper.fromDio(error);
    }
  }

  /// Drops null entries so an absent optional filter is not sent as `null`.
  Map<String, dynamic>? _clean(Map<String, dynamic>? parameters) {
    if (parameters == null) {
      return null;
    }
    final Map<String, dynamic> cleaned = <String, dynamic>{
      for (final MapEntry<String, dynamic> entry in parameters.entries)
        if (entry.value != null) entry.key: entry.value,
    };
    return cleaned.isEmpty ? null : cleaned;
  }
}

/// Decoders for the common payload shapes, so data sources do not repeat them.
abstract final class Decode {
  /// A single JSON object.
  static Map<String, dynamic> object(Object? data) => asJsonObject(data);

  /// A JSON object that the contract allows to be null.
  static Map<String, dynamic>? nullableObject(Object? data) =>
      data == null ? null : asJsonObject(data);

  /// A JSON array of objects.
  static List<Map<String, dynamic>> objectList(Object? data) {
    if (data is! List) {
      throw const ParsingException();
    }
    return data.map(asJsonObject).toList(growable: false);
  }

  /// Ignores the payload; for endpoints whose body carries no information.
  static void unit(Object? data) {}
}
