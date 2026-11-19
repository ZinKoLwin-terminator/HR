import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:material_ui/material_ui.dart';

/// The standard empty state.
///
/// An empty list is not an error, so it gets its own neutral treatment: what
/// is missing, why, and the action that would fill it.
class AppEmptyView extends StatelessWidget {
  const AppEmptyView({
    required this.title,
    super.key,
    this.message,
    this.icon = Icons.inbox_outlined,
    this.actionLabel,
    this.onAction,
    this.isCompact = false,
  });

  final String title;
  final String? message;
  final IconData icon;

  /// Label of the call to action. Supplied together with [onAction].
  final String? actionLabel;
  final VoidCallback? onAction;

  /// Renders inline inside a card rather than filling the viewport.
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    final Widget content = Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          padding: EdgeInsets.all(isCompact ? AppSpacing.md : AppSpacing.lg),
          decoration: BoxDecoration(
            color: palette.surfaceMuted,
            shape: BoxShape.circle,
          ),
          child: Icon(
            icon,
            size: isCompact ? 24 : 32,
            color: palette.textTertiary,
          ),
        ),
        Gap.lg,
        Text(
          title,
          style: isCompact ? textStyles.titleSmall : textStyles.titleMedium,
          textAlign: TextAlign.center,
        ),
        if (message != null) ...<Widget>[
          Gap.xs,
          Text(
            message!,
            style: textStyles.bodyMedium,
            textAlign: TextAlign.center,
          ),
        ],
        if (actionLabel != null && onAction != null) ...<Widget>[
          Gap.lg,
          AppButton.secondary(
            label: actionLabel!,
            onPressed: onAction,
            expand: false,
          ),
        ],
      ],
    );

    if (isCompact) {
      return Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.lg),
        child: content,
      );
    }

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(AppSpacing.xxl),
        child: content,
      ),
    );
  }
}
