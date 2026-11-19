import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:material_ui/material_ui.dart';

/// States the password rules up front.
///
/// Showing the policy before submission is a usability choice: a validator
/// that only complains after a failed attempt makes the user guess.
class PasswordPolicyHint extends StatelessWidget {
  const PasswordPolicyHint({super.key});

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Icon(
          Icons.info_outline_rounded,
          size: AppSizes.iconSm,
          color: palette.textTertiary,
        ),
        Gap.wXs,
        Expanded(
          child: Text(
            'At least ${ValidationRules.minPasswordLength} characters, '
            'including a letter and a number.',
            style: Theme.of(context).textTheme.bodySmall
                ?.copyWith(color: palette.textTertiary),
          ),
        ),
      ],
    );
  }
}
