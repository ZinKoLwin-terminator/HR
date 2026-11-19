import 'dart:typed_data';

import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/payslip/data/datasources/payslip_document_store.dart';
import 'package:hr_employee_app/features/payslip/data/datasources/payslip_remote_datasource.dart';
import 'package:hr_employee_app/features/payslip/data/models/payslip_models.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/domain/repositories/payslip_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Maps payslip payloads to entities and manages the downloaded PDF.
///
/// Where the PDF is written, and whether it can be written at all, is the one
/// part of this that differs per host, so it lives behind
/// [PayslipDocumentStore] — see that file for the storage policy. Everything
/// the user reads stays here, next to the rest of the feature's wording.
///
/// Payslip JSON is never written to disk at all: it lives only as long as the
/// provider holds it.
class PayslipRepositoryImpl with RepositoryGuard implements PayslipRepository {
  PayslipRepositoryImpl({
    required this._remote,
    required this.logger,
    PayslipDocumentStore? documentStore,
  }) : _documents = documentStore ?? createPayslipDocumentStore();

  final PayslipRemoteDataSource _remote;
  final PayslipDocumentStore _documents;

  @override
  final AppLogger logger;

  @override
  Future<Result<PaginatedResponse<PayslipSummary>>> getPayslips({
    required PageRequest page,
    int? year,
  }) {
    return guard(operation: 'getPayslips', () async {
      final PaginatedResponse<PayslipSummaryModel> response = await _remote
          .getPayslips(page: page, year: year);
      return response.mapItems((PayslipSummaryModel model) => model.toEntity());
    });
  }

  @override
  Future<Result<PayslipDetail>> getPayslipDetail(String id) {
    return guard(operation: 'getPayslipDetail', () async {
      final PayslipDetailModel model = await _remote.getPayslipDetail(id);
      return model.toEntity();
    });
  }

  @override
  Future<Result<PayslipDocument>> downloadPayslipPdf(
    String id, {
    void Function(double progress)? onProgress,
  }) {
    return guard(operation: 'downloadPayslipPdf', () async {
      final Uint8List bytes = await _remote.downloadPayslipPdf(
        id,
        onProgress: onProgress,
      );

      final String fileName = 'payslip-$id.pdf';
      final String path;
      try {
        path = await _documents.save(fileName: fileName, bytes: bytes);
      } on PayslipStoreUnavailable catch (error) {
        logger.warning('Could not write payslip PDF: ${error.detail}');
        throw const CacheException(
          'Could not save the payslip on this device.',
        );
      }

      return PayslipDocument(payslipId: id, filePath: path, fileName: fileName);
    });
  }

  @override
  Future<Result<void>> openPayslipPdf(PayslipDocument document) {
    return guard(operation: 'openPayslipPdf', () async {
      final PayslipOpenOutcome outcome = await _documents.open(
        document.filePath,
      );

      switch (outcome) {
        case PayslipOpenOutcome.opened:
          return;

        // The common case on a device, and it has a specific remedy, so it
        // gets its own message rather than a generic failure.
        case PayslipOpenOutcome.noViewer:
          throw const NotFoundException(
            'No PDF viewer is installed. Install one to open your payslip.',
          );

        case PayslipOpenOutcome.unsupported:
          throw const UnknownException(
            'Payslip documents can only be opened in the mobile app.',
          );

        case PayslipOpenOutcome.failed:
          logger.warning('Could not open payslip PDF: ${document.filePath}');
          throw const UnknownException('Could not open the payslip.');
      }
    });
  }

  /// Deletes every downloaded payslip.
  ///
  /// Called on sign-out: the next person to use the device must not find the
  /// previous user's pay in app storage.
  @override
  Future<void> clearDownloadedPayslips() async {
    try {
      await _documents.clear();
    } on Object catch (error) {
      // Best effort: failing to clean up must not block sign-out.
      logger.warning('Could not clear downloaded payslips: $error');
    }
  }
}
