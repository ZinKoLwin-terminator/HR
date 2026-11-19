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

/// The app's button, with four states that look different from each other:
///
///   * **default** — the variant's own colours;
///   * **loading** ([isLoading]) — a spinner beside [loadingLabel], on the
///     same colour a shade lighter, and taps ignored;
///   * **success** ([isSuccess]) — teal with a tick and [successLabel], for
///     the moment between a request completing and the screen moving on;
///   * **disabled** ([isEnabled] false, or no [onPressed]) — grey.
///
/// Loading keeps the button's colour on purpose: a faded or grey button reads
/// as "you cannot do this", which is the wrong message while the app is
/// working on what the user just asked for.
///
/// Two behaviours are guaranteed everywhere rather than re-implemented per
/// screen: while loading or showing success the callback cannot fire, which
/// is what actually prevents double submission; and the control keeps its
/// height whatever it shows, so the layout around it does not jump.
class AppButton extends StatelessWidget {
  const AppButton({
    required this.label,
    required this.onPressed,
    super.key,
    this.variant = AppButtonVariant.primary,
    this.icon,
    this.trailingIcon,
    this.isLoading = false,
    this.loadingLabel,
    this.isSuccess = false,
    this.successLabel,
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
    this.loadingLabel,
    this.isSuccess = false,
    this.successLabel,
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
    this.loadingLabel,
    this.isSuccess = false,
    this.successLabel,
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
    this.loadingLabel,
    this.isSuccess = false,
    this.successLabel,
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

  /// What the button says while loading — "Submitting…", "Signing in…".
  /// Falls back to a bare spinner.
  final String? loadingLabel;

  final bool isSuccess;

  /// What the button says on success — "Request sent". Falls back to
  /// [label].
  final String? successLabel;

  final bool isEnabled;

  /// When true the button fills the available width.
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final bool isBusy = isLoading || isSuccess;
    final bool isDisabled = !isEnabled || onPressed == null;

    // While busy the button stays "enabled" so it keeps its colour, but the
    // callback is a no-op and pointer events are ignored below: the request
    // cannot be sent twice.
    final VoidCallback? effectiveOnPressed = isDisabled && !isBusy
        ? null
        : (isBusy ? _ignore : onPressed);

    final Widget child = switch ((isSuccess, isLoading)) {
      (true, _) => _ButtonLabel(
        label: successLabel ?? label,
        icon: Icons.check_rounded,
      ),
      (_, true) => _LoadingLabel(variant: variant, label: loadingLabel),
      _ => _ButtonLabel(label: label, icon: icon, trailingIcon: trailingIcon),
    };

    final ButtonStyle? stateStyle = _stateStyle(context, isSuccess, isLoading);

    final Widget button = switch (variant) {
      AppButtonVariant.primary => FilledButton(
        onPressed: effectiveOnPressed,
        style: stateStyle,
        child: child,
      ),
      AppButtonVariant.secondary => OutlinedButton(
        onPressed: effectiveOnPressed,
        style: stateStyle,
        child: child,
      ),
      AppButtonVariant.text => TextButton(
        onPressed: effectiveOnPressed,
        style: stateStyle,
        child: child,
      ),
      AppButtonVariant.danger => FilledButton(
        onPressed: effectiveOnPressed,
        style: FilledButton.styleFrom(
          backgroundColor: context.palette.danger,
          foregroundColor: Colors.white,
          minimumSize: Size(double.infinity, AppSizes.buttonHeight),
          shape: RoundedRectangleBorder(borderRadius: AppRadius.button),
        ).merge(stateStyle),
        child: child,
      ),
    };

    // Announce the busy and done states so a screen reader does not read a
    // stale label.
    final String spoken = switch ((isSuccess, isLoading)) {
      (true, _) => successLabel ?? '$label, done',
      (_, true) => loadingLabel ?? '$label, in progress',
      _ => label,
    };
    final Widget semantic = Semantics(
      button: true,
      enabled: !isDisabled && !isBusy,
      liveRegion: isBusy,
      label: spoken,
      child: ExcludeSemantics(
        child: IgnorePointer(ignoring: isBusy, child: button),
      ),
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

  static void _ignore() {}

  /// Colour overrides for the loading and success states.
  ButtonStyle? _stateStyle(
    BuildContext context,
    bool isSuccess,
    bool isLoading,
  ) {
    if (isSuccess) {
      final Color teal = Color.lerp(AppColors.summitTeal, Colors.black, 0.22)!;
      return switch (variant) {
        AppButtonVariant.primary || AppButtonVariant.danger => ButtonStyle(
          backgroundColor: WidgetStatePropertyAll<Color>(teal),
          foregroundColor: const WidgetStatePropertyAll<Color>(Colors.white),
        ),
        _ => ButtonStyle(
          foregroundColor: WidgetStatePropertyAll<Color>(teal),
          side: WidgetStatePropertyAll<BorderSide>(BorderSide(color: teal)),
        ),
      };
    }
    if (isLoading &&
        (variant == AppButtonVariant.primary ||
            variant == AppButtonVariant.danger)) {
      // The resting colour, a shade lighter: still clearly the same button,
      // visibly busy.
      final Color base = variant == AppButtonVariant.danger
          ? context.palette.danger
          : (FilledButtonTheme.of(context).style?.backgroundColor
                    ?.resolve(const <WidgetState>{}) ??
                Theme.of(context).colorScheme.primary);
      return ButtonStyle(
        backgroundColor: WidgetStatePropertyAll<Color>(
          Color.lerp(base, Colors.white, 0.12)!,
        ),
      );
    }
    return null;
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

/// A spinner, and the loading label beside it when there is one.
class _LoadingLabel extends StatelessWidget {
  const _LoadingLabel({required this.variant, this.label});

  final AppButtonVariant variant;
  final String? label;

  @override
  Widget build(BuildContext context) {
    final Widget spinner = _ButtonSpinner(variant: variant, isSmall: true);
    if (label == null) {
      return _ButtonSpinner(variant: variant);
    }
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        spinner,
        Gap.wSm,
        Flexible(child: Text(label!, overflow: TextOverflow.ellipsis)),
      ],
    );
  }
}

class _ButtonSpinner extends StatelessWidget {
  const _ButtonSpinner({required this.variant, this.isSmall = false});

  final AppButtonVariant variant;

  /// Sized to sit beside a label rather than stand in for one.
  final bool isSmall;

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
    final double size = isSmall ? 18 : AppSizes.iconLg;

    return SizedBox(
      height: size,
      width: size,
      child: CircularProgressIndicator(
        strokeWidth: isSmall ? 2 : 2.2,
        color: color,
      ),
    );
  }

  /// The label colour the nearest [FilledButtonTheme] would use, or null when
  /// nothing has overridden it.
  Color? _themedForeground(BuildContext context) {
    return FilledButtonTheme.of(context).style?.foregroundColor
        ?.resolve(const <WidgetState>{});
  }
}
