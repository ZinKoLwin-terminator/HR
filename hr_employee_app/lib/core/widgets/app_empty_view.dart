import 'dart:math' as math;

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:material_ui/material_ui.dart';

/// The standard empty state: the brand hexagon holding the subject's icon,
/// what is missing, why, and — when there is one — the action that would
/// fill it.
///
/// An empty list is not an error, so it gets its own calm treatment rather
/// than the error view's. Every list in the app routes through this, so a
/// new list gets the same look for free.
class AppEmptyView extends StatelessWidget {
  const AppEmptyView({
    required this.title,
    super.key,
    this.message,
    this.icon = Icons.inbox_outlined,
    this.actionLabel,
    this.actionIcon,
    this.onAction,
    this.isCompact = false,
    this.tone,
  });

  final String title;
  final String? message;
  final IconData icon;

  /// Label of the call to action. Supplied together with [onAction].
  final String? actionLabel;

  /// Leading icon for the action, such as `Icons.add_rounded`.
  final IconData? actionIcon;

  final VoidCallback? onAction;

  /// Renders inline inside a card rather than filling the viewport.
  final bool isCompact;

  /// Colour of the hexagon and icon. Summit Teal unless the subject has a
  /// colour of its own.
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = tone ?? AppColors.summitTeal;
    final double badge = isCompact ? 64 : 96;

    final Widget content = ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 320),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          ExcludeSemantics(
            child: _HexBadge(size: badge, icon: icon, tone: accent),
          ),
          SizedBox(height: isCompact ? AppSpacing.md : AppSpacing.xl),
          Text(
            title,
            style: (isCompact ? textStyles.titleSmall : textStyles.titleLarge)
                ?.copyWith(
                  fontWeight: FontWeight.w800,
                  color: palette.textPrimary,
                ),
            textAlign: TextAlign.center,
          ),
          if (message != null) ...<Widget>[
            Gap.sm,
            Text(
              message!,
              style: textStyles.bodyMedium?.copyWith(
                color: palette.textSecondary,
                height: 1.45,
              ),
              textAlign: TextAlign.center,
            ),
          ],
          if (actionLabel != null && onAction != null) ...<Widget>[
            SizedBox(height: isCompact ? AppSpacing.md : AppSpacing.xl),
            // Sized to its label: the app theme makes filled buttons
            // full-width, which reads as a form submit rather than an
            // invitation.
            Theme(
              data: Theme.of(context).copyWith(
                filledButtonTheme: FilledButtonThemeData(
                  style: FilledButton.styleFrom(
                    minimumSize: Size(0, AppSizes.buttonHeight),
                    padding: EdgeInsets.symmetric(horizontal: AppSpacing.xxl),
                    shape: RoundedRectangleBorder(
                      borderRadius: AppRadius.button,
                    ),
                  ),
                ),
              ),
              child: AppButton(
                label: actionLabel!,
                icon: actionIcon,
                onPressed: onAction,
                expand: false,
              ),
            ),
          ],
        ],
      ),
    );

    if (isCompact) {
      return Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.lg),
        child: Center(child: content),
      );
    }

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(AppSpacing.xxl),
        child: content,
      ),
    );
  }
}

/// A soft tinted hexagon with a white hexagon inside it, outlined in the
/// tone, and the icon in the middle — the mark's frame, reused.
class _HexBadge extends StatelessWidget {
  const _HexBadge({required this.size, required this.icon, required this.tone});

  final double size;
  final IconData icon;
  final Color tone;

  @override
  Widget build(BuildContext context) {
    return SizedBox.square(
      dimension: size,
      child: CustomPaint(
        painter: _HexBadgePainter(tone: tone),
        child: Center(
          child: Icon(icon, size: size * 0.3, color: tone),
        ),
      ),
    );
  }
}

class _HexBadgePainter extends CustomPainter {
  const _HexBadgePainter({required this.tone});

  final Color tone;

  @override
  void paint(Canvas canvas, Size size) {
    final Offset centre = size.center(Offset.zero);
    final double outer = size.shortestSide / 2;

    canvas
      ..drawPath(
        _hexagon(centre, outer),
        Paint()
          ..isAntiAlias = true
          ..color = tone.withValues(alpha: 0.10),
      )
      ..drawPath(
        _hexagon(centre, outer * 0.7),
        Paint()
          ..isAntiAlias = true
          ..color = AppColors.surface,
      )
      ..drawPath(
        _hexagon(centre, outer * 0.7),
        Paint()
          ..isAntiAlias = true
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.5
          ..strokeJoin = StrokeJoin.round
          ..color = tone.withValues(alpha: 0.45),
      );
  }

  /// Pointy-top, like the mark, with softened corners.
  static Path _hexagon(Offset centre, double radius) {
    final Path path = Path();
    for (int i = 0; i < 6; i++) {
      final double angle = math.pi / 3 * i - math.pi / 2;
      final Offset point =
          centre + Offset(math.cos(angle), math.sin(angle)) * radius;
      if (i == 0) {
        path.moveTo(point.dx, point.dy);
      } else {
        path.lineTo(point.dx, point.dy);
      }
    }
    return path..close();
  }

  @override
  bool shouldRepaint(_HexBadgePainter oldDelegate) => oldDelegate.tone != tone;
}
