/// Every path the app calls, relative to `AppConfig.apiBaseUrl`.
///
/// No URL is written anywhere else in the codebase: changing a route means
/// changing exactly one line here. Paths that take an identifier are exposed
/// as functions so the id cannot be concatenated incorrectly at the call site.
///
/// The contract these paths implement is documented in
/// `docs/api_contracts.md` and is a proposal until the backend team confirms
/// it.
abstract final class ApiEndpoints {
  // --- Authentication -------------------------------------------------
  static const String login = '/auth/login';
  static const String refreshToken = '/auth/refresh';
  static const String logout = '/auth/logout';
  static const String forgotPassword = '/auth/forgot-password';
  static const String resetPassword = '/auth/reset-password';
  static const String changePassword = '/auth/change-password';

  /// Paths that must never carry an `Authorization` header, and that the
  /// interceptor must not attempt to retry after a token refresh.
  static const Set<String> unauthenticated = <String>{
    login,
    refreshToken,
    forgotPassword,
    resetPassword,
  };

  // --- Dashboard ------------------------------------------------------
  static const String dashboardSummary = '/dashboard/summary';

  // --- Attendance -----------------------------------------------------
  static const String attendanceToday = '/attendance/today';
  static const String attendanceCheckIn = '/attendance/check-in';
  static const String attendanceCheckOut = '/attendance/check-out';
  static const String attendanceHistory = '/attendance/history';
  static const String attendanceSummary = '/attendance/summary';

  static String attendanceById(String id) => '/attendance/$id';

  // --- Leave ----------------------------------------------------------
  static const String leaveTypes = '/leave/types';
  static const String leaveBalances = '/leave/balances';
  static const String leaveRequests = '/leave/requests';

  static String leaveRequestById(String id) => '/leave/requests/$id';

  static String cancelLeaveRequest(String id) => '/leave/requests/$id/cancel';

  // --- Payslip --------------------------------------------------------
  static const String payslips = '/payslips';

  static String payslipById(String id) => '/payslips/$id';

  static String payslipPdf(String id) => '/payslips/$id/pdf';

  // --- Profile --------------------------------------------------------
  static const String myProfile = '/employees/me';
  static const String myContact = '/employees/me/contact';
  static const String myEmergencyContact = '/employees/me/emergency-contact';
  static const String myPhoto = '/employees/me/photo';

  // --- Grievances (the employee's own; see docs/api_contracts.md §10) --
  static const String grievanceOptions = '/grievances/options';
  static const String myGrievances = '/grievances/my';

  static String myGrievanceById(int id) => '/grievances/my/$id';

  static String submitMyGrievance(int id) => '/grievances/my/$id/submit';

  static String myGrievanceMessages(int id) => '/grievances/my/$id/messages';

  static String markMyGrievanceSeen(int id) => '/grievances/my/$id/seen';

  // --- Notifications --------------------------------------------------
  static const String notifications = '/notifications';
  static const String unreadNotificationCount = '/notifications/unread-count';
  static const String markAllNotificationsRead = '/notifications/read-all';
  static const String registerDevice = '/devices/register';

  static String markNotificationRead(String id) => '/notifications/$id/read';
}

/// Query-string parameter names, kept in one place for the same reason as the
/// paths above.
abstract final class ApiQueryKeys {
  static const String pageNumber = 'pageNumber';
  static const String pageSize = 'pageSize';
  static const String fromDate = 'fromDate';
  static const String toDate = 'toDate';
  static const String year = 'year';
  static const String month = 'month';
  static const String status = 'status';
  static const String onlyUnread = 'onlyUnread';
}

/// Header names used by the client.
abstract final class ApiHeaders {
  static const String authorization = 'Authorization';
  static const String accept = 'Accept';
  static const String contentType = 'Content-Type';
  static const String idempotencyKey = 'Idempotency-Key';
  static const String appVersion = 'X-App-Version';
  static const String platform = 'X-Platform';
  static const String deviceId = 'X-Device-Id';
}
