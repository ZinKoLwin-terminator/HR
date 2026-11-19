import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/providers/notification_controllers.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_links.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_tile.dart';
import 'package:material_ui/material_ui.dart';

/// One notification in full: what it says and when it came, a word on what
/// to do about it where there is one, and a button to where it leads.
class NotificationDetailScreen extends ConsumerWidget {
  const NotificationDetailScreen({required this.notificationId, super.key});

  final String notificationId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AppNotification? notification = ref.watch(
      notificationByIdProvider(notificationId),
    );
    final AppConfig config = ref.watch(appConfigProvider);
    final NotificationDestination? destination = notification == null
        ? null
        : notificationDestination(notification);

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: Column(
          children: <Widget>[
            if (config.useMockData)
              const SafeArea(
                bottom: false,
                child: MockDataBanner(
                  compact: true,
                  message:
                      'Development build · sample data, not from the HR '
                      'system',
                ),
              ),
            Expanded(
              child: MediaQuery.removePadding(
                context: context,
                removeTop: config.useMockData,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    _Header(
                      subtitle: notification == null
                          ? null
                          : _categoryOf(notification.type),
                    ),
                    Expanded(
                      child: notification == null
                          // Reachable if the inbox was reloaded while this
                          // screen was open, or from a deep link opened
                          // before the list loaded.
                          ? AppEmptyView(
                              title: 'Notification unavailable',
                              message:
                                  'It may have been removed, or the list has '
                                  'been refreshed. Open it again from your '
                                  'notifications.',
                              icon: Icons.notifications_off_outlined,
                              actionLabel: 'Back to notifications',
                              onAction: () => Navigator.of(context).pop(),
                            )
                          : _Body(notification: notification),
                    ),
                    if (destination != null)
                      _ActionBar(
                        label: destination.label,
                        onPressed: () => context.pushNamed(
                          destination.name,
                          pathParameters: destination.params,
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The inbox group a type belongs to, as the header's subtitle.
String _categoryOf(NotificationType type) => switch (type) {
  NotificationType.leaveApproval || NotificationType.leaveRejection => 'Leave',
  NotificationType.attendance => 'Attendance',
  NotificationType.payslip => 'Payslips',
  NotificationType.announcement => 'Announcements',
  _ => 'General',
};

class _Body extends StatelessWidget {
  const _Body({required this.notification});

  final AppNotification notification;

  /// What to do next, said once and only where it is true for the type.
  static String? _tipFor(NotificationType type) => switch (type) {
    NotificationType.attendance =>
      'If something here is wrong — a check-out you forgot, say — open the '
          'day and ask HR to correct it, so your hours are counted.',
    NotificationType.leaveRejection =>
      'You can apply again with different dates from Leave.',
    NotificationType.payslip =>
      'Amounts stay hidden until you tap the eye on the payslip.',
    _ => null,
  };

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final (IconData icon, Color tint, Color surface) = notificationAppearance(
      palette,
      notification.type,
    );
    final String? tip = _tipFor(notification.type);

    return ListView(
      padding: AppSpacing.screenPadding,
      children: <Widget>[
        Container(
          padding: EdgeInsets.all(AppSpacing.lg),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.xl),
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: AppColors.shadow,
                blurRadius: 16,
                offset: Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Container(
                    height: 44,
                    width: 44,
                    decoration: BoxDecoration(
                      color: surface,
                      borderRadius: BorderRadius.circular(AppRadius.md),
                    ),
                    child: Icon(icon, size: AppSizes.iconLg, color: tint),
                  ),
                  Gap.wMd,
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          notification.title,
                          style: textStyles.titleMedium?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        Text(
                          'Received '
                          '${DateFormatter.dateTime(notification.createdAt)} '
                          '· ${DateFormatter.relative(notification.createdAt)}',
                          style: textStyles.labelSmall?.copyWith(
                            color: palette.textTertiary,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              Gap.md,
              const Divider(height: 1, color: AppColors.divider),
              Gap.md,
              Text(
                notification.body,
                style: textStyles.bodyLarge?.copyWith(
                  color: palette.textPrimary,
                  height: 1.45,
                ),
              ),
            ],
          ),
        ),
        if (tip != null) ...<Widget>[
          Gap.md,
          Container(
            padding: EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.primarySurface,
              borderRadius: BorderRadius.circular(AppRadius.lg),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Icon(
                  Icons.info_outline_rounded,
                  size: AppSizes.iconMd,
                  color: AppColors.apexNavy,
                ),
                Gap.wSm,
                Expanded(
                  child: Text(
                    tip,
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }
}

/// Navy band: back, title and the notification's type.
class _Header extends StatelessWidget {
  const _Header({required this.subtitle});

  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.sm,
            AppSpacing.lg,
            AppSpacing.md,
          ),
          child: Row(
            children: <Widget>[
              if (Navigator.of(context).canPop()) ...<Widget>[
                Material(
                  color: onBrand.withValues(alpha: 0.12),
                  shape: CircleBorder(
                    side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: IconButton(
                    onPressed: () => Navigator.of(context).maybePop(),
                    icon: const Icon(Icons.arrow_back_rounded, color: onBrand),
                    iconSize: AppSizes.iconMd,
                    tooltip: MaterialLocalizations.of(context)
                        .backButtonTooltip,
                  ),
                ),
                Gap.wMd,
              ],
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      'Notification',
                      style: textStyles.titleLarge?.copyWith(
                        color: onBrand,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    if (subtitle != null)
                      Text(
                        subtitle!,
                        style: textStyles.bodySmall?.copyWith(
                          color: onBrand.withValues(alpha: 0.72),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The way to what the notification is about, pinned to the foot.
class _ActionBar extends StatelessWidget {
  const _ActionBar({required this.label, required this.onPressed});

  final String label;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: context.palette.border)),
      ),
      child: SafeArea(
        top: false,
        minimum: EdgeInsets.all(AppSpacing.md),
        child: AppButton(
          label: label,
          trailingIcon: Icons.arrow_forward_rounded,
          onPressed: onPressed,
        ),
      ),
    );
  }
}
