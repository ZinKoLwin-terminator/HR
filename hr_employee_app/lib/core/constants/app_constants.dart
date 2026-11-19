/// Values that are fixed for the whole application.
abstract final class AppConstants {
  static const String appName = 'HR Employee';

  /// Scheme used by notification deep links, e.g. `hrapp://leave/lr-5501`.
  static const String deepLinkScheme = 'hrapp';

  /// How long before actual expiry an access token is treated as expired.
  ///
  /// Refreshing slightly early avoids a request failing mid-flight because the
  /// token lapsed between the check and the server receiving it.
  static const Duration tokenExpiryLeeway = Duration(seconds: 30);

  /// Debounce applied to search fields and rapid taps.
  static const Duration inputDebounce = Duration(milliseconds: 350);

  /// Minimum time the splash screen stays visible, so a fast auto-login does
  /// not flash the screen.
  static const Duration minimumSplashDuration = Duration(milliseconds: 600);

  /// Standard page size for paginated lists.
  static const int defaultPageSize = 20;

  /// Page size for the payslip list, which is naturally monthly.
  static const int payslipPageSize = 12;

  /// Distance from the end of a list at which the next page is requested.
  static const double paginationTriggerExtent = 320;
}

/// Limits enforced on the client before a file is uploaded.
///
/// These mirror the server-side rules in `docs/api_contracts.md`. The client
/// checks them to fail fast and save the user an upload, but the server
/// remains the authority.
abstract final class UploadLimits {
  static const int maxLeaveAttachments = 5;
  static const int maxLeaveAttachmentBytes = 5 * 1024 * 1024;
  static const int maxProfilePhotoBytes = 2 * 1024 * 1024;

  static const List<String> leaveAttachmentExtensions = <String>[
    'pdf',
    'jpg',
    'jpeg',
    'png',
  ];

  static const List<String> profilePhotoExtensions = <String>[
    'jpg',
    'jpeg',
    'png',
  ];
}

/// Rules applied to form input.
abstract final class ValidationRules {
  static const int minPasswordLength = 8;
  static const int maxPasswordLength = 64;
  static const int minLeaveReasonLength = 10;
  static const int maxLeaveReasonLength = 500;
  static const int maxRemarkLength = 250;
  static const int minUserNameLength = 3;
  static const int maxUserNameLength = 100;
}
