import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:material_ui/material_ui.dart';

/// A heading above a group of content, with an optional trailing action.
class SectionHeader extends StatelessWidget {
  const SectionHeader({
    required this.title,
    super.key,
    this.subtitle,
    this.actionLabel,
    this.onAction,
    this.padding,
  });

  final String title;
  final String? subtitle;

  /// Label of the trailing text button, typically "See all".
  final String? actionLabel;
  final VoidCallback? onAction;

  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Padding(
      padding: padding ?? EdgeInsets.only(bottom: AppSpacing.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(title, style: textStyles.titleMedium),
                if (subtitle != null) ...<Widget>[
                  Gap.xxs,
                  Text(subtitle!, style: textStyles.bodySmall),
                ],
              ],
            ),
          ),
          if (actionLabel != null && onAction != null)
            TextButton(
              onPressed: onAction,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(actionLabel!),
                  Icon(Icons.chevron_right_rounded, size: AppSizes.iconMd),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
