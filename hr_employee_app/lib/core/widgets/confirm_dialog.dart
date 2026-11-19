import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// A yes/no confirmation dialog.
///
/// Returns true only when the user picks the confirming action; dismissing by
/// tapping outside or pressing back returns false, never null, so callers do
/// not have to handle a third case.
abstract final class ConfirmDialog {
  static Future<bool> show(
    BuildContext context, {
    required String title,
    required String message,
    String confirmLabel = 'Confirm',
    String cancelLabel = 'Cancel',
    bool isDestructive = false,
    IconData? icon,
  }) async {
    final bool? result = await showDialog<bool>(
      context: context,
      builder: (BuildContext dialogContext) => _ConfirmDialogBody(
        title: title,
        message: message,
        confirmLabel: confirmLabel,
        cancelLabel: cancelLabel,
        isDestructive: isDestructive,
        icon: icon,
      ),
    );
    return result ?? false;
  }
}

class _ConfirmDialogBody extends StatelessWidget {
  const _ConfirmDialogBody({
    required this.title,
    required this.message,
    required this.confirmLabel,
    required this.cancelLabel,
    required this.isDestructive,
    this.icon,
  });

  final String title;
  final String message;
  final String confirmLabel;
  final String cancelLabel;
  final bool isDestructive;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color accent = isDestructive
        ? palette.danger
        : Theme.of(context).colorScheme.primary;

    return AlertDialog(
      icon: icon == null
          ? null
          : Container(
              padding: EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: isDestructive
                    ? palette.dangerSurface
                    : palette.infoSurface,
                shape: BoxShape.circle,
              ),
              child: Icon(icon, color: accent, size: AppSizes.iconLg),
            ),
      title: Text(title),
      content: Text(message),
      actionsPadding: EdgeInsets.fromLTRB(
        AppSpacing.lg,
        0,
        AppSpacing.lg,
        AppSpacing.lg,
      ),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: Text(cancelLabel),
        ),
        FilledButton(
          onPressed: () => Navigator.of(context).pop(true),
          style: FilledButton.styleFrom(
            backgroundColor: accent,
            // The dialog button sizes to its label; the global theme stretches
            // buttons to full width, which is wrong here.
            minimumSize: const Size(0, AppSizes.minTouchTarget),
            padding: EdgeInsets.symmetric(horizontal: AppSpacing.xl),
          ),
          child: Text(confirmLabel),
        ),
      ],
    );
  }
}
