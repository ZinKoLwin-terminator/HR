import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:material_ui/material_ui.dart';

/// The app mark.
///
/// Drawn rather than loaded from an asset so the project builds and runs
/// before any brand assets exist. Replace the body of [build] with an
/// `Image.asset` once the real logo is supplied — nothing else needs to
/// change, because every screen uses this widget rather than an asset path.
class AppLogo extends StatelessWidget {
  const AppLogo({super.key, this.size = 64, this.isOnDark = false});

  final double size;

  /// Inverts the colours for use on the primary-coloured splash screen.
  final bool isOnDark;

  @override
  Widget build(BuildContext context) {
    final Color background = isOnDark ? Colors.white : AppColors.primary;
    final Color foreground = isOnDark ? AppColors.primary : Colors.white;

    return Container(
      height: size,
      width: size,
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(size * 0.28),
      ),
      child: Icon(Icons.badge_outlined, size: size * 0.52, color: foreground),
    );
  }
}

/// Logo, product name and an optional strapline, used on the auth screens.
class AppBrandHeader extends StatelessWidget {
  const AppBrandHeader({
    required this.title,
    super.key,
    this.subtitle,
    this.showLogo = true,
  });

  final String title;
  final String? subtitle;
  final bool showLogo;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        if (showLogo) ...<Widget>[const AppLogo(), Gap.xl],
        Text(title, style: textStyles.headlineLarge),
        if (subtitle != null) ...<Widget>[
          Gap.sm,
          Text(subtitle!, style: textStyles.bodyMedium),
        ],
      ],
    );
  }
}
