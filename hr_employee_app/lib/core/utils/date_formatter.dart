import 'package:intl/intl.dart';

/// Date and duration formatting used across the app.
///
/// Two rules the whole codebase follows:
///   * the API speaks UTC, the UI speaks local time — conversion happens here
///     and nowhere else;
///   * date-only values (`yyyy-MM-dd`) are never shifted by a timezone, since
///     an attendance date is a calendar day, not an instant.
abstract final class DateFormatter {
  static final DateFormat _apiDate = DateFormat('yyyy-MM-dd');
  static final DateFormat _dayMonthYear = DateFormat('d MMM yyyy');
  static final DateFormat _dayMonth = DateFormat('d MMM');
  static final DateFormat _weekdayDayMonth = DateFormat('EEE, d MMM');
  static final DateFormat _fullDate = DateFormat('EEEE, d MMMM yyyy');
  static final DateFormat _monthYear = DateFormat('MMMM yyyy');
  static final DateFormat _shortMonthYear = DateFormat('MMM yyyy');
  static final DateFormat _time = DateFormat('h:mm a');
  static final DateFormat _dateTime = DateFormat('d MMM yyyy, h:mm a');

  /// `2026-03-14` — the wire format for date-only fields.
  static String toApiDate(DateTime date) => _apiDate.format(date);

  /// Parses a `yyyy-MM-dd` value as a local calendar day.
  static DateTime? parseApiDate(String? value) {
    if (value == null || value.isEmpty) {
      return null;
    }
    try {
      return _apiDate.parseStrict(value);
    } on FormatException {
      // Tolerate a full timestamp where the contract says date-only.
      final DateTime? parsed = DateTime.tryParse(value);
      return parsed == null
          ? null
          : DateTime(parsed.year, parsed.month, parsed.day);
    }
  }

  /// `14 Mar 2026`
  static String dayMonthYear(DateTime date) => _dayMonthYear.format(date);

  /// `14 Mar`
  static String dayMonth(DateTime date) => _dayMonth.format(date);

  /// `Sat, 14 Mar`
  static String weekdayDayMonth(DateTime date) => _weekdayDayMonth.format(date);

  /// `Saturday, 14 March 2026`
  static String fullDate(DateTime date) => _fullDate.format(date);

  /// `March 2026`
  static String monthYear(DateTime date) => _monthYear.format(date);

  /// `Mar 2026`
  static String shortMonthYear(DateTime date) => _shortMonthYear.format(date);

  /// `9:02 AM`, converted from UTC to the device timezone.
  static String time(DateTime instant) => _time.format(instant.toLocal());

  /// `14 Mar 2026, 9:02 AM`, converted to the device timezone.
  static String dateTime(DateTime instant) =>
      _dateTime.format(instant.toLocal());

  /// A compact date range: `2 - 3 Apr 2026`, or the single date when both
  /// ends are the same day.
  static String dateRange(DateTime start, DateTime end) {
    if (isSameDay(start, end)) {
      return dayMonthYear(start);
    }
    if (start.year == end.year && start.month == end.month) {
      return '${start.day} - ${dayMonthYear(end)}';
    }
    if (start.year == end.year) {
      return '${dayMonth(start)} - ${dayMonthYear(end)}';
    }
    return '${dayMonthYear(start)} - ${dayMonthYear(end)}';
  }

  /// `8h 32m`, used for worked hours. Returns `--` for a null or zero value
  /// so an incomplete day reads as unknown rather than as zero work.
  static String duration(int? minutes) {
    if (minutes == null || minutes <= 0) {
      return '--';
    }
    final int hours = minutes ~/ 60;
    final int remaining = minutes % 60;
    if (hours == 0) {
      return '${remaining}m';
    }
    if (remaining == 0) {
      return '${hours}h';
    }
    return '${hours}h ${remaining}m';
  }

  /// A short relative label for notification timestamps.
  static String relative(DateTime instant, {DateTime? now}) {
    final DateTime reference = (now ?? DateTime.now()).toLocal();
    final DateTime local = instant.toLocal();
    final Duration elapsed = reference.difference(local);

    if (elapsed.isNegative) {
      return time(instant);
    }
    if (elapsed.inMinutes < 1) {
      return 'Just now';
    }
    if (elapsed.inMinutes < 60) {
      return '${elapsed.inMinutes}m ago';
    }
    if (elapsed.inHours < 24 && isSameDay(local, reference)) {
      return '${elapsed.inHours}h ago';
    }
    if (isSameDay(local, reference.subtract(const Duration(days: 1)))) {
      return 'Yesterday';
    }
    if (elapsed.inDays < 7) {
      return '${elapsed.inDays}d ago';
    }
    if (local.year == reference.year) {
      return dayMonth(local);
    }
    return dayMonthYear(local);
  }

  static bool isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  static bool isToday(DateTime date) => isSameDay(date, DateTime.now());

  /// Strips the time component, giving the calendar day in local time.
  static DateTime dateOnly(DateTime value) =>
      DateTime(value.year, value.month, value.day);

  /// Inclusive count of calendar days between two dates.
  static int inclusiveDayCount(DateTime start, DateTime end) =>
      dateOnly(end).difference(dateOnly(start)).inDays + 1;
}
