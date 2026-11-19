import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_controllers.dart';
import 'package:hr_employee_app/features/payslip/presentation/providers/payslip_providers.dart';
import 'package:hr_employee_app/features/payslip/presentation/widgets/payslip_tile.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// The payslip list.
///
/// Amounts are masked by default; the eye in the app bar reveals them. See
/// `salaryMaskedProvider` for the reasoning.
class PayslipScreen extends ConsumerWidget {
  const PayslipScreen({super.key});

  Future<void> _openPdf(
    BuildContext context,
    WidgetRef ref,
    PayslipSummary payslip,
  ) async {
    final bool opened = await ref
        .read(payslipDownloadControllerProvider.notifier)
        .openPdf(payslip);

    if (!context.mounted || opened) {
      return;
    }

    final Failure? failure = ref
        .read(payslipDownloadControllerProvider)
        .submission
        .failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<PaginatedState<PayslipSummary>> payslips = ref.watch(
      payslipsControllerProvider,
    );
    final bool isMasked = ref.watch(salaryMaskedProvider);
    final PayslipDownloadState download = ref.watch(
      payslipDownloadControllerProvider,
    );

    return Scaffold(
      appBar: BrandAppBar(
        title: const Text('Payslips'),
        actions: <Widget>[
          IconButton(
            onPressed: () => ref.read(salaryMaskedProvider.notifier).toggle(),
            tooltip: isMasked ? 'Show amounts' : 'Hide amounts',
            icon: Icon(
              isMasked
                  ? Icons.visibility_outlined
                  : Icons.visibility_off_outlined,
            ),
          ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: AsyncValueView<PaginatedState<PayslipSummary>>(
          value: payslips,
          loading: const PaginatedListSkeleton(lines: 3),
          onRetry: () => ref.invalidate(payslipsControllerProvider),
          data: (PaginatedState<PayslipSummary> state) =>
              PaginatedListView<PayslipSummary>(
                items: state.items,
                hasMore: state.hasMore,
                isLoadingMore: state.isLoadingMore,
                loadMoreFailure: state.loadMoreFailure,
                onRefresh: () =>
                    ref.read(payslipsControllerProvider.notifier).refresh(),
                onLoadMore: () =>
                    ref.read(payslipsControllerProvider.notifier).loadMore(),
                emptyTitle: 'No payslips yet',
                emptyMessage:
                    'Your payslips will appear here once payroll publishes '
                    'them.',
                emptyIcon: Icons.receipt_long_outlined,
                itemBuilder:
                    (BuildContext context, PayslipSummary payslip, int index) =>
                        PayslipTile(
                          payslip: payslip,
                          isMasked: isMasked,
                          isDownloading: download.isDownloading(payslip.id),
                          onTap: () => context.pushNamed(
                            RouteNames.payslipDetail,
                            pathParameters: <String, String>{
                              RouteParams.payslipId: payslip.id,
                            },
                          ),
                          onDownload: () => _openPdf(context, ref, payslip),
                        ),
              ),
        ),
      ),
    );
  }
}
