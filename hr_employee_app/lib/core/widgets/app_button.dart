import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// Visual weight of an [AppButton].
enum AppButtonVariant {
  /// Filled. One per screen — the action the screen exists for.
  primary,

  /// Outlined. Secondary actions that sit beside a primary one.
  secondary,

  /// Text only. Tertiary actions such as "Cancel".
  text,

  /// Filled in the danger colour, for destructive actions.
  danger,
}

/// The app's button.
///
/// Wraps the Material buttons so that two behaviours are guaranteed
/// everywhere rather than re-implemented per screen:
///   * while [isLoading] the label is replaced by a spinner **and** the
///     callback is detached, which is what actually prevents double
///     submission;
///   * the control keeps its height when it swaps to the spinner, so the
///     surrounding layout does not jump.
class AppButton extends StatelessWidget {
  const AppButton({
    required this.label,
    required this.onPressed,
    super.key,
    this.variant = AppButtonVariant.primary,
    this.icon,
    this.trailingIcon,
    this.isLoading = false,
    this.isEnabled = true,
    this.expand = true,
  });

  /// Convenience constructor for a secondary button.
  const AppButton.secondary({
    required this.label,
    required this.onPressed,
    super.key,
    this.icon,
    this.trailingIcon,
    this.isLoading = false,
    this.isEnabled = true,
    this.expand = true,
  }) : variant = AppButtonVariant.secondary;

  /// Convenience constructor for a text button.
  const AppButton.text({
    required this.label,
    required this.onPressed,
    super.key,
    this.icon,
    this.trailingIcon,
    this.isLoading = false,
    this.isEnabled = true,
    this.expand = false,
  }) : variant = AppButtonVariant.text;

  /// Convenience constructor for a destructive button.
  const AppButton.danger({
    required this.label,
    required this.onPressed,
    super.key,
    this.icon,
    this.trailingIcon,
    this.isLoading = false,
    this.isEnabled = true,
    this.expand = true,
  }) : variant = AppButtonVariant.danger;

  final String label;
  final VoidCallback? onPressed;
  final AppButtonVariant variant;

  /// Drawn before the label.
  final IconData? icon;

  /// Drawn after the label, for an action that moves the user onwards.
  final IconData? trailingIcon;
  final bool isLoading;
  final bool isEnabled;

  /// When true the button fills the available width.
  final bool expand;

  @override
  Widget build(BuildContext context) {
    // Detaching the callback while loading is what blocks a second tap; the
    // spinner alone would only be cosmetic.
    final VoidCallback? effectiveOnPressed = (!isEnabled || isLoading)
        ? null
        : onPressed;

    final Widget child = isLoading
        ? _ButtonSpinner(variant: variant)
        : _ButtonLabel(label: label, icon: icon, trailingIcon: trailingIcon);

    final Widget button = switch (variant) {
      AppButtonVariant.primary => FilledButton(
        onPressed: effectiveOnPressed,
        child: child,
      ),
      AppButtonVariant.secondary => OutlinedButton(
        onPressed: effectiveOnPressed,
        child: child,
      ),
      AppButtonVariant.text => TextButton(
        onPressed: effectiveOnPressed,
        child: child,
      ),
      AppButtonVariant.danger => FilledButton(
        onPressed: effectiveOnPressed,
        style: FilledButton.styleFrom(
          backgroundColor: context.palette.danger,
          foregroundColor: Colors.white,
          minimumSize: Size(double.infinity, AppSizes.buttonHeight),
          shape: RoundedRectangleBorder(borderRadius: AppRadius.button),
        ),
        child: child,
      ),
    };

    // Announce the busy state so a screen reader does not read a stale label.
    final Widget semantic = Semantics(
      button: true,
      enabled: effectiveOnPressed != null,
      label: isLoading ? '$label, in progress' : label,
      child: ExcludeSemantics(child: button),
    );

    if (expand) {
      return SizedBox(
        width: double.infinity,
        height: AppSizes.buttonHeight,
        child: semantic,
      );
    }
    return semantic;
  }
}

class _ButtonLabel extends StatelessWidget {
  const _ButtonLabel({required this.label, this.icon, this.trailingIcon});

  final String label;
  final IconData? icon;
  final IconData? trailingIcon;

  @override
  Widget build(BuildContext context) {
    if (icon == null && trailingIcon == null) {
      return Text(label, overflow: TextOverflow.ellipsis);
    }
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        if (icon != null) ...<Widget>[
          Icon(icon, size: AppSizes.iconMd),
          Gap.wSm,
        ],
        Flexible(child: Text(label, overflow: TextOverflow.ellipsis)),
        if (trailingIcon != null) ...<Widget>[
          Gap.wSm,
          Icon(trailingIcon, size: AppSizes.iconMd),
        ],
      ],
    );
  }
}

class _ButtonSpinner extends StatelessWidget {
  const _ButtonSpinner({required this.variant});

  final AppButtonVariant variant;

  @override
  Widget build(BuildContext context) {
    final Color color = switch (variant) {
      // Resolved from the button theme rather than assumed to be white: a
      // filled button restyled for a dark surface carries a dark label, and
      // a white spinner on it would be invisible.
      AppButtonVariant.primary => _themedForeground(context) ?? Colors.white,
      AppButtonVariant.danger => Colors.white,
      AppButtonVariant.secondary ||
      AppButtonVariant.text => Theme.of(context).colorScheme.primary,
    };

    return SizedBox(
      height: AppSizes.iconLg,
      width: AppSizes.iconLg,
      child: CircularProgressIndicator(strokeWidth: 2.2, color: color),
    );
  }

  /// The label colour the nearest [FilledButtonTheme] would use, or null when
  /// nothing has overridden it.
  Color? _themedForeground(BuildContext context) {
    return FilledButtonTheme.of(context).style?.foregroundColor
        ?.resolve(const <WidgetState>{});
  }
}
