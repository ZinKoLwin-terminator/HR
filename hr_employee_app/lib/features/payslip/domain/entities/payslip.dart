import 'package:freezed_annotation/freezed_annotation.dart';

part 'payslip.freezed.dart';

/// One row in the payslip list.
///
/// Only the figures needed for the list: fetching the full breakdown for
/// twelve months at once would move a lot of sensitive data the user has not
/// asked to see.
@freezed
abstract class PayslipSummary with _$PayslipSummary {
  const factory PayslipSummary({
    required String id,
    required int year,
    required int month,
    required double netPay,
    required String currencyCode,

    /// Server-rendered label, e.g. `March 2026`. Used in preference to
    /// formatting `month` locally so the app and the PDF agree.
    String? periodLabel,
    DateTime? payDate,
    @Default(false) bool isPdfAvailable,
  }) = _PayslipSummary;

  const PayslipSummary._();

  /// First day of the pay period, for sorting and grouping.
  DateTime get period => DateTime(year, month);
}

/// A single earning or deduction line.
@freezed
abstract class PayslipComponent with _$PayslipComponent {
  const factory PayslipComponent({
    required String label,
    required double amount,

    /// Payroll code, e.g. `BASIC` or `SSB`. Shown only where it helps.
    String? code,
  }) = _PayslipComponent;
}

/// The full breakdown for one pay period.
///
/// Never written to disk. The repository holds it in memory only, and the
/// downloaded PDF goes to the app-private directory — see
/// `docs/api_contracts.md` §6.
@freezed
abstract class PayslipDetail with _$PayslipDetail {
  const factory PayslipDetail({
    required String id,
    required int year,
    required int month,
    required String currencyCode,
    required double basicSalary,
    required double grossEarnings,
    required double totalDeductions,
    required double netPay,
    String? periodLabel,
    DateTime? payDate,

    /// Days paid in this period, and days lost to unpaid absence.
    double? paidDays,
    double? lopDays,
    @Default(<PayslipComponent>[]) List<PayslipComponent> earnings,
    @Default(<PayslipComponent>[]) List<PayslipComponent> deductions,
    @Default(false) bool isPdfAvailable,
  }) = _PayslipDetail;

  const PayslipDetail._();

  DateTime get period => DateTime(year, month);

  /// Whether the server's totals agree with the line items.
  ///
  /// Used to decide whether to show the breakdown as authoritative. If the
  /// parts do not add up, the screen shows the server's totals and omits the
  /// implied arithmetic rather than displaying numbers that contradict each
  /// other.
  bool get isInternallyConsistent {
    const double tolerance = 0.01;
    final double earningsSum = earnings.fold<double>(
      0,
      (double sum, PayslipComponent component) => sum + component.amount,
    );
    final double deductionsSum = deductions.fold<double>(
      0,
      (double sum, PayslipComponent component) => sum + component.amount,
    );
    return (earningsSum - grossEarnings).abs() < tolerance &&
        (deductionsSum - totalDeductions).abs() < tolerance &&
        (grossEarnings - totalDeductions - netPay).abs() < tolerance;
  }
}

/// A payslip PDF that has been downloaded to app-private storage.
@freezed
abstract class PayslipDocument with _$PayslipDocument {
  const factory PayslipDocument({
    required String payslipId,

    /// Absolute path inside the app's private directory.
    required String filePath,
    required String fileName,
  }) = _PayslipDocument;
}
