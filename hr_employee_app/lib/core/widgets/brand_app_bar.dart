import 'package:flutter/services.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// The app's standard page header: the brand gradient with a rounded bottom
/// edge, white title and icons.
///
/// A drop-in replacement for [AppBar] on in-app pages, so every screen opens
/// with the same coloured band as the dashboard instead of a plain white bar.
/// Pass [subtitle] for a one-line summary under the title, and [bottom] for
/// tabs or filters that belong inside the band.
class BrandAppBar extends StatelessWidget implements PreferredSizeWidget {
  const BrandAppBar({
    required this.title,
    super.key,
    this.subtitle,
    this.actions,
    this.automaticallyImplyLeading = true,
    this.bottom,
  });

  final Widget title;
  final String? subtitle;
  final List<Widget>? actions;
  final bool automaticallyImplyLeading;
  final PreferredSizeWidget? bottom;

  /// Space under the toolbar (or [bottom]) before the rounded edge, so the
  /// band does not end flush against the title.
  static const double _bottomInset = 12;
  static const double _subtitleHeight = 20;

  double get _toolbarHeight =>
      kToolbarHeight + (subtitle == null ? 0 : _subtitleHeight);

  double get _bottomHeight =>
      (bottom?.preferredSize.height ?? 0) + _bottomInset;

  @override
  Size get preferredSize => Size.fromHeight(_toolbarHeight + _bottomHeight);

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Color onBrand = theme.colorScheme.onPrimary;
    final TextStyle? titleStyle = theme.textTheme.titleLarge?.copyWith(
      color: onBrand,
    );

    return AppBar(
      automaticallyImplyLeading: automaticallyImplyLeading,
      toolbarHeight: _toolbarHeight,
      backgroundColor: Colors.transparent,
      surfaceTintColor: Colors.transparent,
      foregroundColor: onBrand,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      systemOverlayStyle: SystemUiOverlayStyle.light,
      iconTheme: IconThemeData(color: onBrand),
      actionsIconTheme: IconThemeData(color: onBrand),
      titleTextStyle: titleStyle,
      title: subtitle == null
          ? title
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                DefaultTextStyle.merge(style: titleStyle, child: title),
                Text(
                  subtitle!,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: onBrand.withValues(alpha: 0.8),
                  ),
                ),
              ],
            ),
      actions: actions,
      // SizedBox.expand: the scaffold gives the app bar loose height
      // constraints, and a childless DecoratedBox would collapse to zero
      // height there, leaving white text on the light page background.
      flexibleSpace: SizedBox.expand(
        child: DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: context.palette.brandGradient,
            ),
            borderRadius: BorderRadius.vertical(
              bottom: Radius.circular(AppRadius.xl),
            ),
          ),
        ),
      ),
      bottom: PreferredSize(
        preferredSize: Size.fromHeight(_bottomHeight),
        child: Column(
          children: <Widget>[
            ?bottom,
            const SizedBox(height: _bottomInset),
          ],
        ),
      ),
    );
  }
}
