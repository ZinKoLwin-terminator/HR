import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_avatar.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:material_ui/material_ui.dart';

/// The identity header at the top of the dashboard.
///
/// Painted on the brand gradient and running under the status bar, so the top
/// of the screen reads as one band rather than a card floating on grey. It
/// takes the safe-area inset itself for that reason: a `SafeArea` wrapped
/// *around* it would leave an unpainted strip above the gradient.
///
/// The bottom corners are rounded and the body overlaps them, which is what
/// makes the first card read as sitting on the header rather than after it.
class ProfileSummaryCard extends StatelessWidget {
  const ProfileSummaryCard({
    required this.profile,
    required this.unreadCount,
    required this.onNotificationsTap,
    required this.onProfileTap,
    super.key,
  });

  final EmployeeHeadline profile;
  final int unreadCount;
  final VoidCallback onNotificationsTap;
  final VoidCallback onProfileTap;

  /// How far the header content clears its own bottom edge.
  ///
  /// Generous because the first card is pulled up into this space; without
  /// it the card would cover the role line.
  static double get bottomPadding => AppSpacing.huge;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Container(
      width: double.infinity,
      // Clipped so the decorative disc cannot paint outside the rounded
      // corners.
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        // Top to bottom, so the lighter stop sits under the status bar and
        // the darker one meets the page: the reverse leaves the overlapping
        // card edged against the brightest part of the band.
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: context.palette.brandGradient,
        ),
        borderRadius: BorderRadius.vertical(
          bottom: Radius.circular(AppRadius.xl),
        ),
      ),
      child: Stack(
        children: <Widget>[
          const Positioned(top: -40, right: -30, child: _HeaderGlow()),
          SafeArea(
            bottom: false,
            child: Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.lg,
                AppSpacing.lg,
                bottomPadding,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      AppAvatar(
                        name: profile.fullName,
                        imageUrl: profile.profilePhotoUrl,
                        size: AppSizes.avatarMd,
                        borderColor: Colors.white24,
                        onTap: onProfileTap,
                      ),
                      Gap.wMd,
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            Text(
                              _greeting(),
                              style: textStyles.bodySmall?.copyWith(
                                color: Colors.white70,
                              ),
                            ),
                            Gap.xxs,
                            Text(
                              profile.fullName,
                              style: textStyles.titleLarge?.copyWith(
                                color: Colors.white,
                                fontWeight: FontWeight.w700,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ],
                        ),
                      ),
                      Gap.wSm,
                      _NotificationBell(
                        unreadCount: unreadCount,
                        onTap: onNotificationsTap,
                      ),
                    ],
                  ),
                  Gap.md,

                  // A wrap, not a row: on the narrowest phones a long
                  // designation and the employee id do not fit on one line,
                  // and a wrapped second line beats a truncated job title.
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.xs,
                    children: <Widget>[
                      if (profile.roleLine.isNotEmpty)
                        _HeaderPill(
                          label: profile.roleLine,
                          icon: Icons.work_outline_rounded,
                        ),
                      _HeaderPill(
                        label: profile.employeeId,
                        icon: Icons.badge_outlined,
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  /// Time-of-day greeting, using the device clock.
  String _greeting() {
    final int hour = DateTime.now().hour;
    if (hour < 12) {
      return 'Good morning';
    }
    if (hour < 17) {
      return 'Good afternoon';
    }
    return 'Good evening';
  }
}

/// The faint disc behind the top-right corner.
///
/// Decorative only: it stops the gradient reading as a flat rectangle without
/// introducing a second colour.
class _HeaderGlow extends StatelessWidget {
  const _HeaderGlow();

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: Container(
        height: 180,
        width: 180,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: Colors.white.withValues(alpha: 0.07),
        ),
      ),
    );
  }
}

/// A translucent chip carrying one fact about the employee.
class _HeaderPill extends StatelessWidget {
  const _HeaderPill({required this.label, required this.icon});

  final String label;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xxs,
      ),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.16),
        borderRadius: AppRadius.chip,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: AppSizes.iconSm, color: Colors.white70),
          Gap.wXs,
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: Colors.white, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

/// Bell icon with an unread badge.
class _NotificationBell extends StatelessWidget {
  const _NotificationBell({required this.unreadCount, required this.onTap});

  final int unreadCount;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final bool hasUnread = unreadCount > 0;

    return Semantics(
      button: true,
      label: hasUnread ? 'Notifications, $unreadCount unread' : 'Notifications',
      child: ExcludeSemantics(
        child: Material(
          color: Colors.white.withValues(alpha: 0.16),
          shape: const CircleBorder(),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: onTap,
            child: Padding(
              padding: EdgeInsets.all(AppSpacing.sm),
              child: Stack(
                clipBehavior: Clip.none,
                children: <Widget>[
                  Icon(
                    Icons.notifications_none_rounded,
                    color: Colors.white,
                    size: AppSizes.iconLg,
                  ),
                  if (hasUnread)
                    Positioned(
                      top: -2,
                      right: -4,
                      child: Container(
                        padding: EdgeInsets.symmetric(
                          horizontal: unreadCount > 9 ? 4 : 0,
                        ),
                        constraints: const BoxConstraints(
                          minWidth: 16,
                          minHeight: 16,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.error,
                          shape: unreadCount > 9
                              ? BoxShape.rectangle
                              : BoxShape.circle,
                          borderRadius: unreadCount > 9
                              ? BorderRadius.circular(8)
                              : null,
                          // White, not the header colour: the band behind the
                          // badge is a gradient, so no single opaque ring
                          // would match it everywhere.
                          border: Border.all(color: Colors.white, width: 1.5),
                        ),
                        child: Center(
                          child: Text(
                            // Caps the badge so a large count cannot widen
                            // the header.
                            unreadCount > 99 ? '99+' : '$unreadCount',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 9,
                              fontWeight: FontWeight.w700,
                              height: 1,
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
