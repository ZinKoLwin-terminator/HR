import 'dart:typed_data';

import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/features/payslip/data/models/payslip_models.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads payslips.
abstract interface class PayslipRemoteDataSource {
  Future<PaginatedResponse<PayslipSummaryModel>> getPayslips({
    required PageRequest page,
    int? year,
  });

  Future<PayslipDetailModel> getPayslipDetail(String id);

  /// Returns the PDF bytes. Where they are written is the repository's
  /// decision, because that is a storage-policy question.
  Future<Uint8List> downloadPayslipPdf(
    String id, {
    void Function(double progress)? onProgress,
  });
}

/// Calls the real API.
class PayslipRemoteDataSourceImpl implements PayslipRemoteDataSource {
  const PayslipRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<PaginatedResponse<PayslipSummaryModel>> getPayslips({
    required PageRequest page,
    int? year,
  }) {
    return _client.getPage<PayslipSummaryModel>(
      ApiEndpoints.payslips,
      page: page,
      fromItem: PayslipSummaryModel.fromJson,
      queryParameters: <String, dynamic>{ApiQueryKeys.year: year},
    );
  }

  @override
  Future<PayslipDetailModel> getPayslipDetail(String id) {
    return _client.get<PayslipDetailModel>(
      ApiEndpoints.payslipById(id),
      decode: (Object? data) =>
          PayslipDetailModel.fromJson(Decode.object(data)),
    );
  }

  @override
  Future<Uint8List> downloadPayslipPdf(
    String id, {
    void Function(double progress)? onProgress,
  }) {
    return _client.downloadBytes(
      ApiEndpoints.payslipPdf(id),
      onReceiveProgress: onProgress == null
          ? null
          : (int received, int total) {
              // `total` is -1 when the response has no Content-Length; an
              // indeterminate bar is better than a wrong one.
              if (total > 0) {
                onProgress(received / total);
              }
            },
    );
  }
}
