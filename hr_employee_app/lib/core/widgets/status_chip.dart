import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// The semantic weight of a [StatusChip].
enum StatusTone { neutral, positive, warning, negative, info }

/// A small pill that labels a status.
///
/// Used for leave status, attendance status and notification type. Features
/// map their own enums onto a [StatusTone] so this widget stays free of
/// domain knowledge.
class StatusChip extends StatelessWidget {
  const StatusChip({
    required this.label,
    required this.tone,
    super.key,
    this.icon,
    this.isCompact = false,
  });

  final String label;
  final StatusTone tone;
  final IconData? icon;
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final (Color foreground, Color background) = _colorsFor(palette);

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: isCompact ? AppSpacing.sm : AppSpacing.md,
        vertical: isCompact ? AppSpacing.xxs : AppSpacing.xs,
      ),
      decoration: BoxDecoration(
        color: background,
        borderRadius: AppRadius.chip,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (icon != null) ...<Widget>[
            Icon(icon, size: AppSizes.iconSm, color: foreground),
            Gap.wXs,
          ],
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: foreground, fontWeight: FontWeight.w700),
          ),
        ],
      ),
    );
  }

  (Color, Color) _colorsFor(AppPalette palette) => switch (tone) {
    StatusTone.positive => (palette.success, palette.successSurface),
    StatusTone.warning => (palette.warning, palette.warningSurface),
    StatusTone.negative => (palette.danger, palette.dangerSurface),
    StatusTone.info => (palette.info, palette.infoSurface),
    StatusTone.neutral => (palette.textSecondary, palette.surfaceMuted),
  };
}

/// A coloured dot, for dense rows where a full chip would be too heavy.
class StatusDot extends StatelessWidget {
  const StatusDot({required this.tone, super.key, this.size = 8});

  final StatusTone tone;
  final double size;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color color = switch (tone) {
      StatusTone.positive => palette.success,
      StatusTone.warning => palette.warning,
      StatusTone.negative => palette.danger,
      StatusTone.info => palette.info,
      StatusTone.neutral => palette.textTertiary,
    };

    return Container(
      height: size,
      width: size,
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
    );
  }
}
