import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:material_ui/material_ui.dart';

/// The check-in / check-out control.
///
/// One widget for all four states of [CheckInAvailability], used by both the
/// dashboard card and the attendance screen. Having a single implementation
/// is what guarantees the two surfaces cannot disagree about whether a punch
/// is allowed.
class PunchActionButton extends StatelessWidget {
  const PunchActionButton({
    required this.availability,
    required this.isPunching,
    required this.onCheckIn,
    required this.onCheckOut,
    super.key,
    this.trailing,
  });

  final CheckInAvailability availability;

  /// While true the button shows a spinner and its callback is detached, so a
  /// second tap cannot record a second punch.
  final bool isPunching;

  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;

  /// Optional control shown inside the completed/not-required notice, such as
  /// a link to the history screen.
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    switch (availability) {
      case CheckInAvailability.canCheckIn:
        return AppButton(
          label: 'Check in',
          loadingLabel: 'Checking in…',
          icon: Icons.login_rounded,
          isLoading: isPunching,
          onPressed: onCheckIn,
        );

      case CheckInAvailability.canCheckOut:
        return AppButton.secondary(
          label: 'Check out',
          loadingLabel: 'Checking out…',
          icon: Icons.logout_rounded,
          isLoading: isPunching,
          onPressed: onCheckOut,
        );

      case CheckInAvailability.completed:
        return _Notice(
          icon: Icons.check_circle_outline_rounded,
          message: 'Your day is recorded.',
          tone: _NoticeTone.success,
          trailing: trailing,
        );

      case CheckInAvailability.notRequired:
        return _Notice(
          icon: Icons.beach_access_outlined,
          message: 'No attendance needed today.',
          tone: _NoticeTone.neutral,
          trailing: trailing,
        );
    }
  }
}

enum _NoticeTone { success, neutral }

/// The non-actionable states, rendered as an inline banner rather than a
/// disabled button — a disabled button invites tapping.
class _Notice extends StatelessWidget {
  const _Notice({
    required this.icon,
    required this.message,
    required this.tone,
    this.trailing,
  });

  final IconData icon;
  final String message;
  final _NoticeTone tone;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final (Color foreground, Color background) = switch (tone) {
      _NoticeTone.success => (palette.success, palette.successSurface),
      _NoticeTone.neutral => (palette.textSecondary, palette.surfaceMuted),
    };

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: background,
        borderRadius: AppRadius.field,
      ),
      child: Row(
        children: <Widget>[
          Icon(icon, size: AppSizes.iconMd, color: foreground),
          Gap.wSm,
          Expanded(
            child: Text(
              message,
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: foreground, fontWeight: FontWeight.w600),
            ),
          ),
          if (trailing != null) trailing!,
        ],
      ),
    );
  }
}
