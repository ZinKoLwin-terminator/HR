import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/payslip/domain/entities/payslip.dart';

part 'payslip_models.freezed.dart';
part 'payslip_models.g.dart';

/// Wire shape of a payslip list row. See `docs/api_contracts.md` §6.
///
/// Money arrives as `num` so a backend sending `1842500` (int) or
/// `1842500.00` (double) both parse; it is converted to `double` in the
/// entity so downstream arithmetic has one type.
@freezed
abstract class PayslipSummaryModel with _$PayslipSummaryModel {
  const factory PayslipSummaryModel({
    required String id,
    required int year,
    required int month,
    @Default(0) num netPay,
    @Default('MMK') String currencyCode,
    String? periodLabel,

    /// `yyyy-MM-dd`.
    String? payDate,
    @Default(false) bool isPdfAvailable,
  }) = _PayslipSummaryModel;

  const PayslipSummaryModel._();

  factory PayslipSummaryModel.fromJson(Map<String, dynamic> json) =>
      _$PayslipSummaryModelFromJson(json);

  PayslipSummary toEntity() => PayslipSummary(
    id: id,
    year: year,
    month: month,
    netPay: netPay.toDouble(),
    currencyCode: currencyCode,
    periodLabel: periodLabel,
    payDate: DateFormatter.parseApiDate(payDate),
    isPdfAvailable: isPdfAvailable,
  );

  /// Omits the amount: a payslip must not reach a log line through an
  /// interpolated object.
  @override
  String toString() => 'PayslipSummaryModel(id: $id, period: $year-$month)';
}

/// Wire shape of one earning or deduction line.
@freezed
abstract class PayslipComponentModel with _$PayslipComponentModel {
  const factory PayslipComponentModel({
    required String label,
    @Default(0) num amount,
    String? code,
  }) = _PayslipComponentModel;

  const PayslipComponentModel._();

  factory PayslipComponentModel.fromJson(Map<String, dynamic> json) =>
      _$PayslipComponentModelFromJson(json);

  PayslipComponent toEntity() =>
      PayslipComponent(label: label, amount: amount.toDouble(), code: code);

  @override
  String toString() => 'PayslipComponentModel(code: $code)';
}

/// Wire shape of the full payslip breakdown.
@freezed
abstract class PayslipDetailModel with _$PayslipDetailModel {
  const factory PayslipDetailModel({
    required String id,
    required int year,
    required int month,
    @Default('MMK') String currencyCode,
    @Default(0) num basicSalary,
    @Default(0) num grossEarnings,
    @Default(0) num totalDeductions,
    @Default(0) num netPay,
    String? periodLabel,
    String? payDate,
    num? paidDays,
    num? lopDays,
    @Default(<PayslipComponentModel>[]) List<PayslipComponentModel> earnings,
    @Default(<PayslipComponentModel>[]) List<PayslipComponentModel> deductions,
    @Default(false) bool isPdfAvailable,
  }) = _PayslipDetailModel;

  const PayslipDetailModel._();

  factory PayslipDetailModel.fromJson(Map<String, dynamic> json) =>
      _$PayslipDetailModelFromJson(json);

  PayslipDetail toEntity() => PayslipDetail(
    id: id,
    year: year,
    month: month,
    currencyCode: currencyCode,
    basicSalary: basicSalary.toDouble(),
    grossEarnings: grossEarnings.toDouble(),
    totalDeductions: totalDeductions.toDouble(),
    netPay: netPay.toDouble(),
    periodLabel: periodLabel,
    payDate: DateFormatter.parseApiDate(payDate),
    paidDays: paidDays?.toDouble(),
    lopDays: lopDays?.toDouble(),
    earnings: earnings
        .map((PayslipComponentModel model) => model.toEntity())
        .toList(growable: false),
    deductions: deductions
        .map((PayslipComponentModel model) => model.toEntity())
        .toList(growable: false),
    isPdfAvailable: isPdfAvailable,
  );

  @override
  String toString() => 'PayslipDetailModel(id: $id, period: $year-$month)';
}
