import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:material_ui/material_ui.dart';

/// The standard error state.
///
/// Takes a [Failure] rather than a string so the illustration, wording and
/// whether a retry is offered are all derived from the kind of failure. A
/// network drop invites a retry; a permission failure does not, because
/// retrying would fail the same way.
class AppErrorView extends StatelessWidget {
  const AppErrorView({
    required this.failure,
    super.key,
    this.onRetry,
    this.onOpenSettings,
    this.isCompact = false,
  });

  final Failure failure;
  final VoidCallback? onRetry;

  /// Supplied when the failure is a permanently denied permission, so the
  /// user can be sent to system settings.
  final VoidCallback? onOpenSettings;

  /// Renders inline inside a card instead of filling the viewport.
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final _ErrorPresentation presentation = _ErrorPresentation.of(failure);

    final bool showSettings =
        onOpenSettings != null &&
        failure is DevicePermissionFailure &&
        (failure as DevicePermissionFailure).isPermanentlyDenied;

    final Widget content = Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Container(
          padding: EdgeInsets.all(isCompact ? AppSpacing.md : AppSpacing.lg),
          decoration: BoxDecoration(
            color: presentation.surfaceColor(palette),
            shape: BoxShape.circle,
          ),
          child: Icon(
            presentation.icon,
            size: isCompact ? 24 : 32,
            color: presentation.iconColor(palette),
          ),
        ),
        Gap.lg,
        Text(
          presentation.title,
          style: isCompact ? textStyles.titleSmall : textStyles.titleMedium,
          textAlign: TextAlign.center,
        ),
        Gap.xs,
        Text(
          failure.message,
          style: textStyles.bodyMedium,
          textAlign: TextAlign.center,
        ),
        if (failure.traceId != null) ...<Widget>[
          Gap.sm,
          Text(
            'Reference: ${failure.traceId}',
            style: textStyles.bodySmall?.copyWith(color: palette.textTertiary),
            textAlign: TextAlign.center,
          ),
        ],
        if (showSettings) ...<Widget>[
          Gap.lg,
          AppButton.secondary(
            label: 'Open settings',
            icon: Icons.settings_outlined,
            onPressed: onOpenSettings,
            expand: false,
          ),
        ] else if (onRetry != null && presentation.allowsRetry) ...<Widget>[
          Gap.lg,
          AppButton.secondary(
            label: 'Try again',
            icon: Icons.refresh_rounded,
            onPressed: onRetry,
            expand: false,
          ),
        ],
      ],
    );

    if (isCompact) {
      return Padding(padding: EdgeInsets.all(AppSpacing.lg), child: content);
    }

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(AppSpacing.xxl),
        child: content,
      ),
    );
  }
}

/// Chooses the icon, heading and retry affordance for a [Failure].
class _ErrorPresentation {
  const _ErrorPresentation({
    required this.icon,
    required this.title,
    required this.tone,
    required this.allowsRetry,
  });

  factory _ErrorPresentation.of(Failure failure) {
    return switch (failure) {
      NetworkFailure() => const _ErrorPresentation(
        icon: Icons.wifi_off_rounded,
        title: 'No connection',
        tone: _Tone.warning,
        allowsRetry: true,
      ),
      TimeoutFailure() => const _ErrorPresentation(
        icon: Icons.schedule_rounded,
        title: 'Taking too long',
        tone: _Tone.warning,
        allowsRetry: true,
      ),
      ServerFailure() => const _ErrorPresentation(
        icon: Icons.cloud_off_rounded,
        title: 'Server unavailable',
        tone: _Tone.danger,
        allowsRetry: true,
      ),
      SessionExpiredFailure() => const _ErrorPresentation(
        icon: Icons.lock_clock_rounded,
        title: 'Session expired',
        tone: _Tone.warning,
        allowsRetry: false,
      ),
      AuthenticationFailure() => const _ErrorPresentation(
        icon: Icons.person_off_outlined,
        title: 'Sign-in failed',
        tone: _Tone.danger,
        allowsRetry: false,
      ),
      PermissionDeniedFailure() => const _ErrorPresentation(
        icon: Icons.block_rounded,
        title: 'Not permitted',
        tone: _Tone.danger,
        allowsRetry: false,
      ),
      ValidationFailure() => const _ErrorPresentation(
        icon: Icons.error_outline_rounded,
        title: 'Check your details',
        tone: _Tone.warning,
        allowsRetry: false,
      ),
      NotFoundFailure() => const _ErrorPresentation(
        icon: Icons.search_off_rounded,
        title: 'Not found',
        tone: _Tone.neutral,
        allowsRetry: false,
      ),
      ConflictFailure() => const _ErrorPresentation(
        icon: Icons.info_outline_rounded,
        title: 'Cannot continue',
        tone: _Tone.warning,
        allowsRetry: false,
      ),
      RateLimitFailure() => const _ErrorPresentation(
        icon: Icons.hourglass_top_rounded,
        title: 'Too many attempts',
        tone: _Tone.warning,
        allowsRetry: true,
      ),
      CacheFailure() => const _ErrorPresentation(
        icon: Icons.sd_card_alert_outlined,
        title: 'Storage problem',
        tone: _Tone.warning,
        allowsRetry: true,
      ),
      DevicePermissionFailure() => const _ErrorPresentation(
        icon: Icons.privacy_tip_outlined,
        title: 'Permission needed',
        tone: _Tone.warning,
        allowsRetry: true,
      ),
      LocationFailure() => const _ErrorPresentation(
        icon: Icons.location_off_outlined,
        title: 'Location unavailable',
        tone: _Tone.warning,
        allowsRetry: true,
      ),
      UnexpectedFailure() => const _ErrorPresentation(
        icon: Icons.error_outline_rounded,
        title: 'Something went wrong',
        tone: _Tone.danger,
        allowsRetry: true,
      ),
    };
  }

  final IconData icon;
  final String title;
  final _Tone tone;
  final bool allowsRetry;

  Color iconColor(AppPalette palette) => switch (tone) {
    _Tone.danger => palette.danger,
    _Tone.warning => palette.warning,
    _Tone.neutral => palette.textSecondary,
  };

  Color surfaceColor(AppPalette palette) => switch (tone) {
    _Tone.danger => palette.dangerSurface,
    _Tone.warning => palette.warningSurface,
    _Tone.neutral => palette.surfaceMuted,
  };
}

enum _Tone { danger, warning, neutral }
