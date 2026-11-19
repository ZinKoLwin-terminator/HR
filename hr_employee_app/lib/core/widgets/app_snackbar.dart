import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:material_ui/material_ui.dart';

/// Transient feedback messages.
///
/// All four helpers go through one builder so tone, icon and duration stay
/// consistent. Any in-flight snack bar is replaced rather than queued, so a
/// user who taps twice does not sit through two messages.
abstract final class AppSnackBar {
  static void success(BuildContext context, String message) =>
      _show(context, message, _SnackTone.success);

  static void info(BuildContext context, String message) =>
      _show(context, message, _SnackTone.info);

  static void warning(BuildContext context, String message) =>
      _show(context, message, _SnackTone.warning);

  static void error(BuildContext context, String message) =>
      _show(context, message, _SnackTone.error);

  /// Shows a [Failure] using its own user-facing message.
  static void failure(BuildContext context, Failure failure) {
    _show(context, failure.message, _SnackTone.error);
  }

  static void _show(
    BuildContext context,
    String message,
    _SnackTone tone, {
    SnackBarAction? action,
  }) {
    final AppPalette palette = context.palette;
    final ScaffoldMessengerState messenger = ScaffoldMessenger.of(context);

    final Color accent = switch (tone) {
      _SnackTone.success => palette.success,
      _SnackTone.info => palette.info,
      _SnackTone.warning => palette.warning,
      _SnackTone.error => palette.danger,
    };

    final IconData icon = switch (tone) {
      _SnackTone.success => Icons.check_circle_outline_rounded,
      _SnackTone.info => Icons.info_outline_rounded,
      _SnackTone.warning => Icons.warning_amber_rounded,
      _SnackTone.error => Icons.error_outline_rounded,
    };

    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          duration: tone == _SnackTone.error
              ? const Duration(seconds: 5)
              : const Duration(seconds: 3),
          action: action,
          content: Row(
            children: <Widget>[
              Icon(icon, color: accent, size: AppSizes.iconLg),
              Gap.wMd,
              Expanded(
                child: Text(
                  message,
                  style: Theme.of(context).snackBarTheme.contentTextStyle,
                ),
              ),
            ],
          ),
        ),
      );
  }
}

enum _SnackTone { success, info, warning, error }
