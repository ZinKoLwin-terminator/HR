import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/constants/storage_keys.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/storage/preferences_service.dart';
import 'package:hr_employee_app/features/payslip/data/datasources/payslip_mock_datasource.dart';
import 'package:hr_employee_app/features/payslip/data/datasources/payslip_remote_datasource.dart';
import 'package:hr_employee_app/features/payslip/data/repositories/payslip_repository_impl.dart';
import 'package:hr_employee_app/features/payslip/domain/repositories/payslip_repository.dart';
import 'package:hr_employee_app/features/payslip/domain/usecases/payslip_usecases.dart';

/// Dependency injection for the payslip feature.

final Provider<PayslipRemoteDataSource> payslipRemoteDataSourceProvider =
    Provider<PayslipRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return const PayslipMockDataSource();
      }
      return PayslipRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<PayslipRepository> payslipRepositoryProvider =
    Provider<PayslipRepository>(
      (Ref ref) => PayslipRepositoryImpl(
        remote: ref.watch(payslipRemoteDataSourceProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<GetPayslipsUseCase> getPayslipsUseCaseProvider =
    Provider<GetPayslipsUseCase>(
      (Ref ref) => GetPayslipsUseCase(ref.watch(payslipRepositoryProvider)),
    );

final Provider<GetPayslipDetailUseCase> getPayslipDetailUseCaseProvider =
    Provider<GetPayslipDetailUseCase>(
      (Ref ref) =>
          GetPayslipDetailUseCase(ref.watch(payslipRepositoryProvider)),
    );

final Provider<ViewPayslipPdfUseCase> viewPayslipPdfUseCaseProvider =
    Provider<ViewPayslipPdfUseCase>(
      (Ref ref) => ViewPayslipPdfUseCase(ref.watch(payslipRepositoryProvider)),
    );

/// Whether salary figures start hidden.
///
/// Payslips are read in public — on a bus, at a desk with someone behind you
/// — so the amounts are masked by default and revealed deliberately. The
/// choice is remembered per install, not per session, because a user who
/// always wants them visible should not re-tap every time.
class SalaryVisibility extends Notifier<bool> {
  @override
  bool build() {
    final PreferencesService preferences = ref.watch(
      preferencesServiceProvider,
    );
    return preferences.getBool(
      PreferenceKeys.maskSalaryAmounts,
      defaultValue: true,
    );
  }

  /// True when amounts are hidden.
  bool get isMasked => state;

  void toggle() {
    final bool next = !state;
    state = next;
    ref
        .read(preferencesServiceProvider)
        .setBool(PreferenceKeys.maskSalaryAmounts, value: next)
        .ignore();
  }
}

final NotifierProvider<SalaryVisibility, bool> salaryMaskedProvider =
    NotifierProvider<SalaryVisibility, bool>(SalaryVisibility.new);
