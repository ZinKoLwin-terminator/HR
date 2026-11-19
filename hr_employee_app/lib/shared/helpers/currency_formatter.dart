import 'package:intl/intl.dart';

/// Formats money for display.
///
/// Amounts arrive from the API as a number plus an ISO-4217 code. The code,
/// not a hardcoded symbol, decides how the value is rendered, so adding a
/// second payroll currency needs no code change.
abstract final class CurrencyFormatter {
  /// Placeholder used wherever an amount is deliberately hidden.
  ///
  /// Salary figures start masked on shared surfaces; see
  /// `PreferenceKeys.maskSalaryAmounts`.
  static const String masked = '••••••';

  static final Map<String, NumberFormat> _cache = <String, NumberFormat>{};

  /// `1,842,500` with the currency symbol, e.g. `K 1,842,500`.
  ///
  /// [decimalDigits] defaults to the convention for the currency: zero for
  /// currencies without a minor unit in practice (MMK, JPY, KRW), two
  /// otherwise.
  static String format(num amount, String currencyCode, {int? decimalDigits}) {
    final int digits = decimalDigits ?? _defaultDigits(currencyCode);
    final String key = '$currencyCode|$digits';
    final NumberFormat formatter = _cache.putIfAbsent(
      key,
      () => NumberFormat.currency(
        name: currencyCode,
        symbol: _symbolFor(currencyCode),
        decimalDigits: digits,
      ),
    );
    return formatter.format(amount);
  }

  /// The number without a symbol, for use beside a separate currency label.
  static String formatPlain(
    num amount,
    String currencyCode, {
    int? decimalDigits,
  }) {
    final int digits = decimalDigits ?? _defaultDigits(currencyCode);
    final String key = 'plain|$digits';
    final NumberFormat formatter = _cache.putIfAbsent(
      key,
      () => NumberFormat.decimalPatternDigits(decimalDigits: digits),
    );
    return formatter.format(amount);
  }

  /// Formats, or returns [masked] when [isMasked] is set.
  static String formatMaskable(
    num amount,
    String currencyCode, {
    required bool isMasked,
  }) {
    return isMasked ? masked : format(amount, currencyCode);
  }

  /// A signed amount, used for deduction rows: `- K 6,000`.
  static String formatSigned(
    num amount,
    String currencyCode, {
    required bool isNegative,
  }) {
    final String value = format(amount.abs(), currencyCode);
    return isNegative ? '- $value' : value;
  }

  static int _defaultDigits(String currencyCode) {
    const Set<String> zeroDecimalCurrencies = <String>{
      'MMK',
      'JPY',
      'KRW',
      'VND',
      'IDR',
    };
    return zeroDecimalCurrencies.contains(currencyCode.toUpperCase()) ? 0 : 2;
  }

  static String _symbolFor(String currencyCode) {
    const Map<String, String> symbols = <String, String>{
      'MMK': 'K ',
      'USD': r'$',
      'EUR': '€',
      'GBP': '£',
      'SGD': r'S$',
      'THB': '฿',
      'INR': '₹',
      'JPY': '¥',
    };
    return symbols[currencyCode.toUpperCase()] ?? '$currencyCode ';
  }
}

/// Formats a count of days, which the API sends as a decimal to allow halves.
abstract final class DayCountFormatter {
  /// `1 day`, `2 days`, `0.5 day`.
  static String format(num days) {
    final String value = days == days.roundToDouble()
        ? days.toInt().toString()
        : days.toStringAsFixed(1);
    return days == 1 ? '$value day' : '$value days';
  }

  /// Just the number: `1`, `2`, `0.5`.
  static String formatNumber(num days) => days == days.roundToDouble()
      ? days.toInt().toString()
      : days.toStringAsFixed(1);
}
