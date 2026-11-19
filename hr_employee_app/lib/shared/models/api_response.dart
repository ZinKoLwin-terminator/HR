import 'package:hr_employee_app/core/exceptions/app_exception.dart';

/// The success envelope every JSON endpoint returns.
///
/// See `docs/api_contracts.md` §1.1.
///
/// Written by hand rather than generated: the type is generic over its
/// payload, and `json_serializable` needs `genericArgumentFactories` plus a
/// converter per call site to handle that. An explicit `fromJson` that takes a
/// decoder for `T` is shorter and easier to follow than the generated
/// equivalent.
class ApiResponse<T> {
  const ApiResponse({
    required this.success,
    this.data,
    this.message,
    this.traceId,
  });

  /// Parses the envelope and decodes `data` with [fromData].
  ///
  /// Throws [ParsingException] when the body is not the agreed shape, so the
  /// caller sees a typed error rather than a cast failure.
  factory ApiResponse.fromJson(
    Object? json,
    T Function(Object? data) fromData,
  ) {
    if (json is! Map) {
      throw const ParsingException();
    }
    final Map<String, dynamic> map = json.cast<String, dynamic>();

    // `success` is optional so that an endpoint returning a bare object still
    // parses; absence is treated as success because a failure would have
    // arrived as a non-2xx status.
    final bool success = map['success'] as bool? ?? true;

    return ApiResponse<T>(
      success: success,
      data: fromData(map['data']),
      message: map['message'] as String?,
      traceId: map['traceId'] as String?,
    );
  }

  final bool success;
  final T? data;
  final String? message;
  final String? traceId;

  /// The payload, or a [ParsingException] when the server omitted it.
  ///
  /// Use this wherever the contract says `data` is non-null; it turns a silent
  /// null into an explicit, typed failure.
  T get requireData {
    final T? value = data;
    if (value == null) {
      throw const ParsingException('The server did not return any data.');
    }
    return value;
  }
}

/// Decodes a `Map<String, dynamic>` payload, or throws [ParsingException].
Map<String, dynamic> asJsonObject(Object? data) {
  if (data is Map) {
    return data.cast<String, dynamic>();
  }
  throw const ParsingException();
}

/// Decodes a JSON array into a typed list.
List<T> asJsonList<T>(Object? data, T Function(Map<String, dynamic>) fromJson) {
  if (data is! List) {
    throw const ParsingException();
  }
  return data
      .map((Object? item) => fromJson(asJsonObject(item)))
      .toList(growable: false);
}
