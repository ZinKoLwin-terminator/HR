import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// Restyles every field below it for use inside a white card: fields take a muted grey fill with no
/// outline, so the form reads as grouped panels rather than a stack of
/// white boxes. The focus and error outlines from the app theme still apply.
class MutedFieldTheme extends StatelessWidget {
  const MutedFieldTheme({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final OutlineInputBorder quiet = OutlineInputBorder(
      borderRadius: AppRadius.field,
      borderSide: const BorderSide(color: Colors.transparent),
    );

    return Theme(
      data: theme.copyWith(
        inputDecorationTheme: theme.inputDecorationTheme.copyWith(
          fillColor: context.palette.surfaceMuted,
          border: quiet,
          enabledBorder: quiet,
          disabledBorder: quiet,
        ),
      ),
      child: child,
    );
  }
}
