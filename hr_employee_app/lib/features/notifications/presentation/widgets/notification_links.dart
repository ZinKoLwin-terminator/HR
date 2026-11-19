import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';

/// Where a notification's deep link leads in the app, and what to call it.
typedef NotificationDestination = ({
  String name,
  Map<String, String> params,
  String label,
});

/// Resolves `hrapp://leave/lr-1` to an in-app route.
///
/// Only the destinations the app actually has are handled; anything else
/// returns null and the action is simply not offered, rather than taking the
/// user somewhere that does not exist.
NotificationDestination? notificationDestination(AppNotification notification) {
  final String? link = notification.deepLink;
  if (link == null || link.isEmpty) {
    return null;
  }

  final Uri? uri = Uri.tryParse(link);
  if (uri == null || uri.scheme != AppConstants.deepLinkScheme) {
    return null;
  }

  final List<String> segments = <String>[
    uri.host,
    ...uri.pathSegments,
  ].where((String segment) => segment.isNotEmpty).toList();

  if (segments.length < 2) {
    return null;
  }

  final String area = segments.first;
  final String id = segments[1];

  return switch (area) {
    'leave' => (
      name: RouteNames.leaveDetail,
      params: <String, String>{RouteParams.leaveRequestId: id},
      label: 'View request',
    ),
    'attendance' => (
      name: RouteNames.attendanceDetail,
      params: <String, String>{RouteParams.attendanceId: id},
      label: 'Check attendance',
    ),
    'payslip' => (
      name: RouteNames.payslipDetail,
      params: <String, String>{RouteParams.payslipId: id},
      label: 'Open payslip',
    ),
    _ => null,
  };
}
