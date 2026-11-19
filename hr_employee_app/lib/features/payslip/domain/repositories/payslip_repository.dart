import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Reads payslips.
///
/// Everything here is sensitive. The implementation must not cache payslip
/// data on disk, and the PDF is written only to the app-private directory —
/// see `docs/api_contracts.md` §6.
abstract interface class PayslipRepository {
  /// One page of payslips, newest first.
  Future<Result<PaginatedResponse<PayslipSummary>>> getPayslips({
    required PageRequest page,
    int? year,
  });

  /// The full breakdown for one period.
  Future<Result<PayslipDetail>> getPayslipDetail(String id);

  /// Downloads the PDF to app-private storage and returns its location.
  ///
  /// [onProgress] reports 0 to 1 where the response declares a length.
  Future<Result<PayslipDocument>> downloadPayslipPdf(
    String id, {
    void Function(double progress)? onProgress,
  });

  /// Opens a previously downloaded PDF in the platform viewer.
  Future<Result<void>> openPayslipPdf(PayslipDocument document);

  /// Deletes every payslip PDF this device has downloaded.
  ///
  /// Part of the contract rather than an implementation detail because
  /// sign-out must be able to call it: on a shared device, leaving the
  /// previous user's pay in app storage is a data leak. Best effort — it
  /// never throws, so it cannot block a sign-out.
  Future<void> clearDownloadedPayslips();
}
