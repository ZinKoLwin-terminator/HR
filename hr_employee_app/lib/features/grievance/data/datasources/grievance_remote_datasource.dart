import 'package:dio/dio.dart';
import 'package:hr_employee_app/core/errors/error_mapper.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/shared/models/api_response.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads and writes the employee's own grievances.
abstract interface class GrievanceRemoteDataSource {
  Future<GrievanceOptionsModel> getOptions();

  Future<PaginatedResponse<MyGrievanceModel>> getMyGrievances({
    required PageRequest page,
    GrievanceStatus? status,
    String? search,
  });

  Future<MyGrievanceDetailModel> getById(int id);

  Future<MyGrievanceDetailModel> createDraft(GrievanceDraftInput input);

  Future<MyGrievanceDetailModel> updateDraft(
    int id, {
    required String rowVersion,
    required GrievanceDraftInput input,
  });

  Future<void> deleteDraft(int id, {required String rowVersion});

  Future<MyGrievanceDetailModel> submit(
    int id, {
    required String rowVersion,
    required bool declarationAccepted,
  });

  Future<MyGrievanceDetailModel> sendMessage(
    int id, {
    required String rowVersion,
    required String body,
    int? inReplyToId,
  });

  Future<void> markSeen(int id);
}

/// Calls the real API (`GrievanceEndpoints.MapSelf`).
///
/// Unlike the placeholder contract the other features were written against,
/// this module mirrors an API that exists, and that API differs from
/// `docs/api_contracts.md` §1 in three ways this class absorbs:
///
///  * bodies are **bare** objects, not the `{ success, data }` envelope
///    (an envelope is still unwrapped if a gateway adds one);
///  * pages are `{ items, total, pageIndex, pageSize }` with a **0-based**
///    `pageIndex` query parameter;
///  * errors are RFC 7807 problem details whose human message is `detail`
///    (`title` is a generic "Validation failed").
///
/// It therefore goes through [ApiClient.rawClient] — the same `Dio`, so the
/// auth interceptor and timeouts still apply — rather than the enveloped
/// helpers.
class GrievanceRemoteDataSourceImpl implements GrievanceRemoteDataSource {
  const GrievanceRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  Dio get _dio => _client.rawClient;

  @override
  Future<GrievanceOptionsModel> getOptions() {
    return _send(
      () => _dio.get<dynamic>(ApiEndpoints.grievanceOptions),
      (Object? data) => GrievanceOptionsModel.fromJson(asJsonObject(data)),
    );
  }

  @override
  Future<PaginatedResponse<MyGrievanceModel>> getMyGrievances({
    required PageRequest page,
    GrievanceStatus? status,
    String? search,
  }) {
    final String? wireStatus = status == null
        ? null
        : GrievanceStatusMapper.toApi(status);
    return _send(
      () => _dio.get<dynamic>(
        ApiEndpoints.myGrievances,
        queryParameters: <String, dynamic>{
          'pageIndex': page.pageNumber - 1,
          'pageSize': page.pageSize,
          'status': ?wireStatus,
          'search': ?search,
        },
      ),
      (Object? data) => pageFromJson(data, MyGrievanceModel.fromJson),
    );
  }

  @override
  Future<MyGrievanceDetailModel> getById(int id) {
    return _send(
      () => _dio.get<dynamic>(ApiEndpoints.myGrievanceById(id)),
      _detail,
    );
  }

  @override
  Future<MyGrievanceDetailModel> createDraft(GrievanceDraftInput input) {
    return _send(
      () => _dio.post<dynamic>(
        ApiEndpoints.myGrievances,
        data: GrievanceRequestBodies.draft(input),
      ),
      _detail,
    );
  }

  @override
  Future<MyGrievanceDetailModel> updateDraft(
    int id, {
    required String rowVersion,
    required GrievanceDraftInput input,
  }) {
    return _send(
      () => _dio.put<dynamic>(
        ApiEndpoints.myGrievanceById(id),
        data: GrievanceRequestBodies.draft(input, rowVersion: rowVersion),
      ),
      _detail,
    );
  }

  @override
  Future<void> deleteDraft(int id, {required String rowVersion}) {
    return _send(
      () => _dio.delete<dynamic>(
        ApiEndpoints.myGrievanceById(id),
        data: GrievanceRequestBodies.versioned(rowVersion),
      ),
      Decode.unit,
    );
  }

  @override
  Future<MyGrievanceDetailModel> submit(
    int id, {
    required String rowVersion,
    required bool declarationAccepted,
  }) {
    return _send(
      () => _dio.post<dynamic>(
        ApiEndpoints.submitMyGrievance(id),
        data: GrievanceRequestBodies.submit(
          rowVersion: rowVersion,
          declarationAccepted: declarationAccepted,
        ),
      ),
      _detail,
    );
  }

  @override
  Future<MyGrievanceDetailModel> sendMessage(
    int id, {
    required String rowVersion,
    required String body,
    int? inReplyToId,
  }) {
    return _send(
      () => _dio.post<dynamic>(
        ApiEndpoints.myGrievanceMessages(id),
        data: GrievanceRequestBodies.message(
          rowVersion: rowVersion,
          body: body,
          inReplyToId: inReplyToId,
        ),
      ),
      _detail,
    );
  }

  @override
  Future<void> markSeen(int id) {
    return _send(
      () => _dio.post<dynamic>(ApiEndpoints.markMyGrievanceSeen(id)),
      Decode.unit,
    );
  }

  static MyGrievanceDetailModel _detail(Object? data) =>
      MyGrievanceDetailModel.fromJson(asJsonObject(data));

  /// Parses the API's `PagedResult` (`items`, `total`, 0-based `pageIndex`,
  /// `pageSize`) into the app's 1-based [PaginatedResponse]. A body already
  /// in the app's own shape is accepted too.
  static PaginatedResponse<T> pageFromJson<T>(
    Object? data,
    T Function(Map<String, dynamic>) fromItem,
  ) {
    final Map<String, dynamic> map = asJsonObject(data);
    if (map.containsKey('pageNumber')) {
      return PaginatedResponse<T>.fromJson(map, fromItem);
    }
    final Object? rawItems = map['items'];
    if (rawItems is! List) {
      throw const ParsingException();
    }
    final List<T> items = rawItems
        .map((Object? item) => fromItem(asJsonObject(item)))
        .toList(growable: false);
    final int pageSize = (map['pageSize'] as num?)?.toInt() ?? items.length;
    final int total = (map['total'] as num?)?.toInt() ?? items.length;
    final int pageNumber = ((map['pageIndex'] as num?)?.toInt() ?? 0) + 1;
    final int totalPages = (pageSize <= 0 || total == 0)
        ? 1
        : (total / pageSize).ceil();
    return PaginatedResponse<T>(
      items: items,
      pageNumber: pageNumber,
      pageSize: pageSize,
      totalCount: total,
      totalPages: totalPages,
      hasNextPage: pageNumber < totalPages,
      hasPreviousPage: pageNumber > 1,
    );
  }

  /// Unwraps a body that a gateway may have put in the app's envelope.
  static Object? unwrap(Object? body) {
    if (body is Map &&
        body.containsKey('data') &&
        (body.containsKey('success') || body.containsKey('traceId'))) {
      return body['data'];
    }
    return body;
  }

  /// Runs [request] and decodes the bare body, mapping failures to
  /// [AppException]s the same way `ApiClient` does.
  Future<T> _send<T>(
    Future<Response<dynamic>> Function() request,
    T Function(Object? data) decode,
  ) async {
    try {
      final Response<dynamic> response = await request();
      return decode(unwrap(response.data));
    } on DioException catch (error) {
      throw ErrorMapper.fromDio(_withProblemDetail(error));
    } on AppException {
      rethrow;
    } on Object catch (_, stackTrace) {
      Error.throwWithStackTrace(const ParsingException(), stackTrace);
    }
  }

  /// Lifts a problem-details `detail` into `message`, so the shared
  /// [ErrorMapper] shows "Enter a subject." rather than the generic title.
  static DioException _withProblemDetail(DioException error) {
    final Response<dynamic>? response = error.response;
    final Object? data = response?.data;
    if (response != null &&
        data is Map &&
        data['message'] == null &&
        data['detail'] is String) {
      response.data = <String, dynamic>{
        ...data.cast<String, dynamic>(),
        'message': data['detail'],
      };
    }
    return error;
  }
}
