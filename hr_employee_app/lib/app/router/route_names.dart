/// Route names and paths.
///
/// Names are used for navigation (`context.goNamed(...)`) because they survive
/// a path change; paths are declared here too so the router and any deep-link
/// handling agree on one spelling.
abstract final class RouteNames {
  static const String splash = 'splash';
  static const String login = 'login';
  static const String forgotPassword = 'forgotPassword';
  static const String resetPassword = 'resetPassword';

  static const String dashboard = 'dashboard';

  static const String attendance = 'attendance';
  static const String attendanceDetail = 'attendanceDetail';

  static const String leave = 'leave';
  static const String applyLeave = 'applyLeave';
  static const String leaveDetail = 'leaveDetail';

  static const String payslip = 'payslip';
  static const String payslipDetail = 'payslipDetail';

  static const String profile = 'profile';
  static const String editProfile = 'editProfile';
  static const String changePassword = 'changePassword';

  static const String notifications = 'notifications';
  static const String notificationDetail = 'notificationDetail';
}

/// Absolute and relative paths.
///
/// Branch roots are absolute; screens pushed on top of a branch use a
/// relative segment so they stack inside that branch and keep the bottom
/// navigation bar visible where that is intended.
abstract final class RoutePaths {
  static const String splash = '/';
  static const String login = '/login';
  static const String forgotPassword = '/forgot-password';
  static const String resetPassword = '/reset-password';

  static const String dashboard = '/dashboard';
  static const String notifications = '/notifications';

  /// Relative to [notifications].
  static const String notificationDetail = ':notificationId';

  static const String attendance = '/attendance';

  /// Relative to [attendance].
  static const String attendanceDetail = ':attendanceId';

  static const String leave = '/leave';

  /// Relative to [leave].
  static const String applyLeave = 'apply';

  /// Relative to [leave].
  static const String leaveDetail = 'request/:leaveRequestId';

  static const String payslip = '/payslip';

  /// Relative to [payslip].
  static const String payslipDetail = ':payslipId';

  static const String profile = '/profile';

  /// Relative to [profile].
  static const String editProfile = 'edit';

  /// Relative to [profile].
  static const String changePassword = 'change-password';
}

/// Path parameter names, so the router and the screens cannot disagree.
abstract final class RouteParams {
  static const String attendanceId = 'attendanceId';
  static const String leaveRequestId = 'leaveRequestId';
  static const String payslipId = 'payslipId';
  static const String notificationId = 'notificationId';

  /// Query parameter carrying the email through the password reset flow.
  static const String email = 'email';

  /// Query parameter carrying the reset token from the emailed link.
  static const String token = 'token';
}
