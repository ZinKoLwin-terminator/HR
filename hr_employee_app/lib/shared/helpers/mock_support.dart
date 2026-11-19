import 'dart:math';

/// Shared scaffolding for the mock data sources.
///
/// ---------------------------------------------------------------------------
/// THESE VALUES ARE INVENTED. They are not payroll, attendance or HR records.
/// ---------------------------------------------------------------------------
///
/// The app ships with `AppConfig.useMockData` enabled because no backend
/// contract has been agreed yet (see `docs/api_contracts.md`). Mock data
/// sources let the UI be built and reviewed before the API exists. Three
/// things keep that honest:
///
///   * every screen that can show mock data renders `MockDataBanner`;
///   * `AppConfig` forces `useMockData` to false for production builds;
///   * the fixtures use an obviously fictional employee.
///
/// Delete this file and the `*MockDataSource` classes once the real API is
/// available.
abstract final class MockSupport {
  /// Fixed seed so a run is reproducible and screenshots do not churn.
  static final Random random = Random(20260314);

  /// Simulates network latency so loading states are exercised during
  /// development instead of resolving instantly.
  static Future<void> delay([
    Duration duration = const Duration(milliseconds: 450),
  ]) => Future<void>.delayed(duration);

  /// The fictional employee every fixture belongs to.
  static const String userId = 'mock-user-0001';
  static const String employeeId = 'EMP-00421';
  static const String fullName = 'Aye Chan Moe';
  static const String email = 'aye.chan@example.com';
  static const String department = 'Finance';
  static const String designation = 'Senior Accountant';

  /// Deliberately null: a fixture must not reach out to a real image host.
  static const String? profilePhotoUrl = null;

  static const String currencyCode = 'MMK';

  /// The password the mock login accepts.
  ///
  /// Not a credential — it guards nothing and exists only so the login screen
  /// can demonstrate both the success and the failure path.
  static const String acceptedPassword = 'Password1';

  /// A stable "today" is not used; fixtures are generated relative to the
  /// real current date so date formatting and "is today" logic behave
  /// normally during development.
  static DateTime get today {
    final DateTime now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  /// `today` shifted by [days], useful for building history fixtures.
  static DateTime daysAgo(int days) => today.subtract(Duration(days: days));

  /// A UTC instant on [date] at the given local wall-clock time.
  static DateTime at(DateTime date, int hour, int minute) =>
      DateTime(date.year, date.month, date.day, hour, minute).toUtc();

  /// Paginates a fixture list the way the API would.
  static List<T> page<T>(List<T> all, int pageNumber, int pageSize) {
    final int start = (pageNumber - 1) * pageSize;
    if (start >= all.length) {
      return <T>[];
    }
    final int end = min(start + pageSize, all.length);
    return all.sublist(start, end);
  }
}
