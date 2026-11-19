import 'package:dio/dio.dart';
import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_models.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads leave data and submits applications.
abstract interface class LeaveRemoteDataSource {
  Future<List<LeaveTypeModel>> getLeaveTypes();

  Future<List<LeaveBalanceModel>> getBalances({int? year});

  Future<LeaveRequestModel> apply(
    LeaveApplication application, {
    void Function(double progress)? onProgress,
  });

  Future<PaginatedResponse<LeaveRequestModel>> getRequests({
    required PageRequest page,
    LeaveStatus? status,
  });

  Future<LeaveRequestModel> getRequestById(String id);

  Future<LeaveRequestModel> cancelRequest(String id);
}

/// Calls the real API.
class LeaveRemoteDataSourceImpl implements LeaveRemoteDataSource {
  const LeaveRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<List<LeaveTypeModel>> getLeaveTypes() {
    return _client.get<List<LeaveTypeModel>>(
      ApiEndpoints.leaveTypes,
      decode: (Object? data) =>
          Decode.objectList(data)
              .map(LeaveTypeModel.fromJson)
              .toList(growable: false),
    );
  }

  @override
  Future<List<LeaveBalanceModel>> getBalances({int? year}) {
    return _client.get<List<LeaveBalanceModel>>(
      ApiEndpoints.leaveBalances,
      queryParameters: <String, dynamic>{ApiQueryKeys.year: year},
      decode: (Object? data) =>
          Decode.objectList(data)
              .map(LeaveBalanceModel.fromJson)
              .toList(growable: false),
    );
  }

  /// Submits the application as `multipart/form-data`.
  ///
  /// The whole request is multipart — not JSON plus a second upload call —
  /// so the server can accept or reject the application and its attachments
  /// as one transaction. A JSON-then-upload sequence can leave a request
  /// stored with its evidence missing.
  @override
  Future<LeaveRequestModel> apply(
    LeaveApplication application, {
    void Function(double progress)? onProgress,
  }) async {
    final FormData formData = FormData.fromMap(<String, dynamic>{
      'leaveTypeId': application.leaveTypeId,
      'startDate': DateFormatter.toApiDate(application.startDate),
      'endDate': DateFormatter.toApiDate(application.endDate),
      'dayPortion': LeaveDayPortionMapper.toApi(application.dayPortion),
      'reason': application.reason,
      if (application.contactDuringLeave != null)
        'contactDuringLeave': application.contactDuringLeave,
      if (application.attachmentPaths.isNotEmpty)
        'attachments': <MultipartFile>[
          for (final String path in application.attachmentPaths)
            await MultipartFile.fromFile(
              path,
              filename: path.split(RegExp(r'[/\\]')).last,
            ),
        ],
    });

    return _client.upload<LeaveRequestModel>(
      ApiEndpoints.leaveRequests,
      formData: formData,
      onSendProgress: onProgress == null
          ? null
          : (int sent, int total) {
              // `total` is -1 when the length is unknown; reporting a
              // negative ratio would drive the progress bar backwards.
              if (total > 0) {
                onProgress(sent / total);
              }
            },
      decode: (Object? data) => LeaveRequestModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<PaginatedResponse<LeaveRequestModel>> getRequests({
    required PageRequest page,
    LeaveStatus? status,
  }) {
    return _client.getPage<LeaveRequestModel>(
      ApiEndpoints.leaveRequests,
      page: page,
      fromItem: LeaveRequestModel.fromJson,
      queryParameters: <String, dynamic>{
        // Null for "all", and null for an unrecognised status rather than
        // sending a value the server would reject.
        ApiQueryKeys.status: status == null
            ? null
            : LeaveStatusMapper.toApi(status),
      },
    );
  }

  @override
  Future<LeaveRequestModel> getRequestById(String id) {
    return _client.get<LeaveRequestModel>(
      ApiEndpoints.leaveRequestById(id),
      decode: (Object? data) => LeaveRequestModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<LeaveRequestModel> cancelRequest(String id) {
    return _client.post<LeaveRequestModel>(
      ApiEndpoints.cancelLeaveRequest(id),
      decode: (Object? data) => LeaveRequestModel.fromJson(Decode.object(data)),
    );
  }
}
