import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/paginated_list_notifier.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_providers.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// Paginated payslips, newest first.
class PayslipsController extends PaginatedListNotifier<PayslipSummary> {
  /// Twelve rather than twenty: payslips are monthly, so a page is a year.
  @override
  int get pageSize => AppConstants.payslipPageSize;

  @override
  Future<Result<PaginatedResponse<PayslipSummary>>> fetchPage(
    PageRequest page,
  ) {
    return ref.read(getPayslipsUseCaseProvider)(page: page);
  }

  @override
  Object identityOf(PayslipSummary item) => item.id;
}

final AsyncNotifierProvider<PayslipsController, PaginatedState<PayslipSummary>>
payslipsControllerProvider =
    AsyncNotifierProvider<PayslipsController, PaginatedState<PayslipSummary>>(
      PayslipsController.new,
    );

/// The full breakdown for one period.
final payslipDetailProvider = FutureProvider.family<PayslipDetail, String>((
  Ref ref,
  String id,
) async {
  final Result<PayslipDetail> result = await ref.watch(
    getPayslipDetailUseCaseProvider,
  )(id);
  return result.valueOrThrow();
});

/// Downloads a payslip PDF and opens it.
///
/// Tracks which payslip is being fetched, so a list of twelve rows can show
/// a spinner on the one the user tapped rather than on all of them.
class PayslipDownloadController extends Notifier<PayslipDownloadState> {
  @override
  PayslipDownloadState build() => const PayslipDownloadState();

  Future<bool> openPdf(PayslipSummary payslip) async {
    if (state.isBusy) {
      return false;
    }

    state = PayslipDownloadState(
      payslipId: payslip.id,
      submission: const SubmissionState.submitting(),
    );

    final Result<PayslipDocument> result =
        await ref.read(viewPayslipPdfUseCaseProvider)(
          payslip,
          onProgress: (double progress) {
            if (state.isBusy) {
              state = state.copyWith(progress: progress);
            }
          },
        );

    return result.fold(
      onSuccess: (_) {
        state = const PayslipDownloadState(
          submission: SubmissionState.success(),
        );
        return true;
      },
      onFailure: (failure) {
        state = PayslipDownloadState(
          payslipId: payslip.id,
          submission: SubmissionState.failure(failure),
        );
        return false;
      },
    );
  }

  void reset() => state = const PayslipDownloadState();
}

/// Which payslip is downloading, and how far along it is.
class PayslipDownloadState {
  const PayslipDownloadState({
    this.payslipId,
    this.submission = const SubmissionState.idle(),
    this.progress = 0,
  });

  /// The payslip this state refers to; null when idle.
  final String? payslipId;

  final SubmissionState submission;

  /// 0 to 1, when the response declared a length.
  final double progress;

  bool get isBusy => submission.isSubmitting;

  /// Whether [id] is the payslip currently downloading.
  bool isDownloading(String id) => isBusy && payslipId == id;

  PayslipDownloadState copyWith({
    String? payslipId,
    SubmissionState? submission,
    double? progress,
  }) {
    return PayslipDownloadState(
      payslipId: payslipId ?? this.payslipId,
      submission: submission ?? this.submission,
      progress: progress ?? this.progress,
    );
  }
}

final NotifierProvider<PayslipDownloadController, PayslipDownloadState>
payslipDownloadControllerProvider =
    NotifierProvider<PayslipDownloadController, PayslipDownloadState>(
      PayslipDownloadController.new,
    );
