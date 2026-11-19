import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';

/// Translates the API's `notificationType` strings to [NotificationType].
abstract final class NotificationTypeMapper {
  static const Map<String, NotificationType> _byWireName =
      <String, NotificationType>{
        'announcement': NotificationType.announcement,
        'leaveapproval': NotificationType.leaveApproval,
        'leaverejection': NotificationType.leaveRejection,
        'attendance': NotificationType.attendance,
        'payslip': NotificationType.payslip,
        'general': NotificationType.general,
      };

  static const Map<NotificationType, String> _toWireName =
      <NotificationType, String>{
        NotificationType.announcement: 'Announcement',
        NotificationType.leaveApproval: 'LeaveApproval',
        NotificationType.leaveRejection: 'LeaveRejection',
        NotificationType.attendance: 'Attendance',
        NotificationType.payslip: 'Payslip',
        NotificationType.general: 'General',
      };

  static NotificationType fromApi(Object? value) {
    if (value is! String) {
      return NotificationType.unknown;
    }
    return _byWireName[_normalize(value)] ?? NotificationType.unknown;
  }

  static String toApi(NotificationType type) => _toWireName[type] ?? 'General';

  static String _normalize(String value) =>
      value.toLowerCase().replaceAll(RegExp(r'[_\-\s]'), '');
}

/// Display labels.
extension NotificationTypeLabel on NotificationType {
  String get label => switch (this) {
    NotificationType.announcement => 'Announcement',
    NotificationType.leaveApproval => 'Leave approved',
    NotificationType.leaveRejection => 'Leave rejected',
    NotificationType.attendance => 'Attendance',
    NotificationType.payslip => 'Payslip',
    NotificationType.general => 'Notice',
    NotificationType.unknown => 'Notice',
  };
}
