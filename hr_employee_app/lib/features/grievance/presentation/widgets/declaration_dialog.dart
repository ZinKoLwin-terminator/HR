import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// "Submit this grievance?" with the declaration the API requires.
///
/// Submit stays disabled until the box is ticked, so a submission always
/// carries `declarationAccepted: true`. Returns true only when confirmed.
abstract final class GrievanceDeclarationDialog {
  static const String declaration =
      'I confirm that what I have written is true to the best of my '
      'knowledge, and I understand that HR may need to look into it with the '
      'people concerned.';

  static Future<bool> show(
    BuildContext context, {
    required bool isAnonymous,
  }) async {
    final bool? result = await showDialog<bool>(
      context: context,
      builder: (BuildContext dialogContext) =>
          _DeclarationDialog(isAnonymous: isAnonymous),
    );
    return result ?? false;
  }
}

class _DeclarationDialog extends StatefulWidget {
  const _DeclarationDialog({required this.isAnonymous});

  final bool isAnonymous;

  @override
  State<_DeclarationDialog> createState() => _DeclarationDialogState();
}

class _DeclarationDialogState extends State<_DeclarationDialog> {
  bool _accepted = false;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return AlertDialog(
      title: const Text('Submit this grievance?'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              widget.isAnonymous
                  ? 'HR will receive it without your name. You cannot edit '
                        'it after submitting.'
                  : 'HR will receive it with your name. You cannot edit it '
                        'after submitting.',
            ),
            Gap.md,
            CheckboxListTile(
              key: const ValueKey<String>('grievance-declaration-checkbox'),
              value: _accepted,
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              title: Text(
                GrievanceDeclarationDialog.declaration,
                style: textStyles.bodySmall,
              ),
              onChanged: (bool? value) =>
                  setState(() => _accepted = value ?? false),
            ),
            if (!_accepted)
              Text(
                'Please confirm the declaration to submit.',
                style: textStyles.bodySmall?.copyWith(
                  color: palette.textSecondary,
                ),
              ),
          ],
        ),
      ),
      actionsPadding: EdgeInsets.fromLTRB(
        AppSpacing.lg,
        0,
        AppSpacing.lg,
        AppSpacing.lg,
      ),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          key: const ValueKey<String>('grievance-declaration-submit'),
          onPressed: _accepted ? () => Navigator.of(context).pop(true) : null,
          style: FilledButton.styleFrom(
            minimumSize: const Size(0, AppSizes.minTouchTarget),
            padding: EdgeInsets.symmetric(horizontal: AppSpacing.xl),
          ),
          child: const Text('Submit'),
        ),
      ],
    );
  }
}
