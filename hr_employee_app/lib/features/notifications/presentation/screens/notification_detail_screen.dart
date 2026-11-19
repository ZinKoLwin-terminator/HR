import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/notifications/data/models/notification_type_mapper.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/providers/notification_controllers.dart';
import 'package:material_ui/material_ui.dart';

/// One notification in full, with a link to whatever it refers to.
class NotificationDetailScreen extends ConsumerWidget {
  const NotificationDetailScreen({required this.notificationId, super.key});

  final String notificationId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AppNotification? notification = ref.watch(
      notificationByIdProvider(notificationId),
    );

    return Scaffold(
      appBar: const BrandAppBar(title: Text('Notification')),
      body: SafeArea(
        child: notification == null
            // Reachable if the inbox was reloaded while this screen was open,
            // or from a deep link opened before the list loaded.
            ? AppEmptyView(
                title: 'Notification unavailable',
                message:
                    'It may have been removed, or the list has been '
                    'refreshed. Open it again from your notifications.',
                icon: Icons.notifications_off_outlined,
                actionLabel: 'Back to notifications',
                onAction: () => Navigator.of(context).pop(),
              )
            : _Body(notification: notification),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({required this.notification});

  final AppNotification notification;

  /// Resolves `hrapp://leave/lr-1` to an in-app route.
  ///
  /// Only the destinations the app actually has are handled; anything else
  /// returns null and the action is simply not offered, rather than taking
  /// the user somewhere that does not exist.
  ({String name, Map<String, String> params})? _destination() {
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
      ),
      'attendance' => (
        name: RouteNames.attendanceDetail,
        params: <String, String>{RouteParams.attendanceId: id},
      ),
      'payslip' => (
        name: RouteNames.payslipDetail,
        params: <String, String>{RouteParams.payslipId: id},
      ),
      _ => null,
    };
  }

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final ({String name, Map<String, String> params})? destination =
        _destination();

    return ListView(
      padding: AppSpacing.screenPadding,
      children: <Widget>[
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  StatusChip(
                    label: notification.type.label,
                    tone: _toneFor(notification.type),
                  ),
                  const Spacer(),
                  Text(
                    DateFormatter.relative(notification.createdAt),
                    style: textStyles.labelSmall?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
                ],
              ),
              Gap.lg,
              Text(notification.title, style: textStyles.headlineMedium),
              Gap.md,
              Text(notification.body, style: textStyles.bodyLarge),
              Gap.lg,
              Divider(height: 1, color: palette.border),
              Gap.md,
              Text(
                'Received ${DateFormatter.dateTime(notification.createdAt)}',
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textTertiary,
                ),
              ),
            ],
          ),
        ),

        if (destination != null) ...<Widget>[
          SizedBox(height: AppSpacing.xxl),
          AppButton(
            label: 'View details',
            icon: Icons.open_in_new_rounded,
            onPressed: () => context.pushNamed(
              destination.name,
              pathParameters: destination.params,
            ),
          ),
        ],
      ],
    );
  }

  StatusTone _toneFor(NotificationType type) => switch (type) {
    NotificationType.leaveApproval => StatusTone.positive,
    NotificationType.leaveRejection => StatusTone.negative,
    NotificationType.attendance => StatusTone.warning,
    NotificationType.payslip ||
    NotificationType.announcement => StatusTone.info,
    NotificationType.general || NotificationType.unknown => StatusTone.neutral,
  };
}
