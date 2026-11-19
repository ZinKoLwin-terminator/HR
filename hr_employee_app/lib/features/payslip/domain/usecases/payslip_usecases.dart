import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/domain/repositories/payslip_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// One page of payslips.
class GetPayslipsUseCase {
  const GetPayslipsUseCase(this._repository);

  final PayslipRepository _repository;

  Future<Result<PaginatedResponse<PayslipSummary>>> call({
    required PageRequest page,
    int? year,
  }) => _repository.getPayslips(page: page, year: year);
}

/// The breakdown for one pay period.
class GetPayslipDetailUseCase {
  const GetPayslipDetailUseCase(this._repository);

  final PayslipRepository _repository;

  Future<Result<PayslipDetail>> call(String id) =>
      _repository.getPayslipDetail(id);
}

/// Downloads a payslip PDF and opens it.
///
/// Combined into one use case because that is the single action the user
/// takes — "show me my payslip" — and splitting it would leave every caller
/// to re-implement the same two-step sequence and its error handling.
class ViewPayslipPdfUseCase {
  const ViewPayslipPdfUseCase(this._repository);

  final PayslipRepository _repository;

  Future<Result<PayslipDocument>> call(
    PayslipSummary payslip, {
    void Function(double progress)? onProgress,
  }) async {
    if (!payslip.isPdfAvailable) {
      return const Result<PayslipDocument>.failure(
        NotFoundFailure('No PDF has been published for this period yet.'),
      );
    }

    final Result<PayslipDocument> downloaded = await _repository
        .downloadPayslipPdf(payslip.id, onProgress: onProgress);

    // Open only on a successful download; a failure is returned as-is so the
    // UI reports the real problem rather than "could not open".
    return downloaded.fold(
      onSuccess: (PayslipDocument document) async {
        final Result<void> opened = await _repository.openPayslipPdf(document);
        return opened.fold(
          onSuccess: (_) => Result<PayslipDocument>.success(document),
          onFailure: Result<PayslipDocument>.failure,
        );
      },
      onFailure: (Failure failure) => Future<Result<PayslipDocument>>.value(
        Result<PayslipDocument>.failure(failure),
      ),
    );
  }
}
