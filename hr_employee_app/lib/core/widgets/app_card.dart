import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/context_extensions.dart';
import 'package:material_ui/material_ui.dart';

/// The rounded, softly-shadowed surface the design brief calls for.
///
/// Every panel in the app is built from this rather than a bare [Container],
/// so radius, border and shadow stay identical across screens.
class AppCard extends StatelessWidget {
  const AppCard({
    required this.child,
    super.key,
    this.padding,
    this.onTap,
    this.backgroundColor,
    this.borderColor,
    this.showShadow = true,
    this.isRaised = false,
    this.margin,
  });

  final Widget child;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;
  final Color? backgroundColor;
  final Color? borderColor;
  final bool showShadow;

  /// Lifts the card with a deeper shadow.
  ///
  /// For the one card on a screen that overlaps the coloured header: the
  /// default shadow is tuned to separate a card from the neutral scaffold
  /// and disappears against a saturated band.
  final bool isRaised;

  final EdgeInsetsGeometry? margin;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color surface =
        backgroundColor ?? Theme.of(context).colorScheme.surface;

    final Widget content = Padding(
      padding: padding ?? AppSpacing.cardPadding,
      child: child,
    );

    return Container(
      margin: margin,
      decoration: BoxDecoration(
        color: surface,
        borderRadius: AppRadius.card,
        border: Border.all(color: borderColor ?? palette.border),
        boxShadow: showShadow && !context.isDarkMode
            ? <BoxShadow>[
                isRaised
                    ? const BoxShadow(
                        color: AppColors.shadowLift,
                        blurRadius: 28,
                        offset: Offset(0, 8),
                      )
                    : const BoxShadow(
                        color: AppColors.shadow,
                        blurRadius: 12,
                        offset: Offset(0, 2),
                      ),
              ]
            : null,
      ),
      // Clipping keeps the ripple inside the rounded corners.
      child: ClipRRect(
        borderRadius: AppRadius.card,
        // A Material is present whether or not the card itself is tappable.
        // Ink effects paint onto the nearest Material ancestor, so without
        // one *inside* this container the decoration above would hide the
        // splash of any descendant - a ListTile or an InkWell in the card's
        // content would look dead on tap.
        child: Material(
          type: MaterialType.transparency,
          child: onTap == null
              ? content
              : InkWell(onTap: onTap, child: content),
        ),
      ),
    );
  }
}

/// A card whose content is laid out as a titled section.
class AppSectionCard extends StatelessWidget {
  const AppSectionCard({
    required this.title,
    required this.child,
    super.key,
    this.icon,
    this.subtitle,
    this.action,
    this.padding,
    this.isRaised = false,
  });

  final String title;
  final Widget child;
  final IconData? icon;

  /// One line under the title, for a figure that summarises the section.
  final String? subtitle;

  /// Trailing control, typically a "See all" text button.
  final Widget? action;

  final EdgeInsetsGeometry? padding;

  /// Forwarded to [AppCard]; see [AppCard.isRaised].
  final bool isRaised;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return AppCard(
      padding: padding,
      isRaised: isRaised,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              if (icon != null) ...<Widget>[
                // A tinted backplate rather than a bare glyph: it gives the
                // section header a fixed visual anchor, so a card with a
                // "See all" button and one without still start on the same
                // optical line.
                _SectionIcon(icon: icon!),
                Gap.wMd,
              ],
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      title,
                      style: textStyles.titleMedium,
                      overflow: TextOverflow.ellipsis,
                    ),
                    if (subtitle != null) ...<Widget>[
                      Gap.xxs,
                      Text(
                        subtitle!,
                        style: textStyles.bodySmall,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ],
                ),
              ),
              if (action != null) action!,
            ],
          ),
          Gap.lg,
          child,
        ],
      ),
    );
  }
}

/// The tinted square behind a section header's icon.
class _SectionIcon extends StatelessWidget {
  const _SectionIcon({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final Color primary = Theme.of(context).colorScheme.primary;

    return Container(
      height: AppSizes.iconBackplate,
      width: AppSizes.iconBackplate,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        // Alpha rather than a fixed tint so the backplate follows the
        // primary colour through a rebrand, and lifts on the dark theme
        // where a light wash would disappear.
        color: primary.withValues(alpha: context.isDarkMode ? 0.22 : 0.1),
        borderRadius: AppRadius.field,
      ),
      child: Icon(icon, size: AppSizes.iconMd, color: primary),
    );
  }
}

/// A label/value pair, the building block of every detail screen.
class AppInfoRow extends StatelessWidget {
  const AppInfoRow({
    required this.label,
    required this.value,
    super.key,
    this.icon,
    this.valueStyle,
    this.isMultiline = false,
  });

  final String label;
  final String value;
  final IconData? icon;
  final TextStyle? valueStyle;

  /// Stacks the value under the label, for long values such as an address.
  final bool isMultiline;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    final Widget labelWidget = Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        if (icon != null) ...<Widget>[
          Icon(icon, size: AppSizes.iconSm, color: palette.textTertiary),
          Gap.wXs,
        ],
        Flexible(
          child: Text(
            label,
            style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
          ),
        ),
      ],
    );

    final Widget valueWidget = Text(
      value,
      style: valueStyle ?? textStyles.titleSmall,
      textAlign: isMultiline ? TextAlign.start : TextAlign.end,
    );

    if (isMultiline) {
      return Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[labelWidget, Gap.xs, valueWidget],
        ),
      );
    }

    return Padding(
      padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(child: labelWidget),
          Gap.wMd,
          Flexible(child: valueWidget),
        ],
      ),
    );
  }
}
