import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/context_extensions.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:material_ui/material_ui.dart';

/// One entry in the quick-action grid.
class QuickAction {
  const QuickAction({
    required this.label,
    required this.icon,
    required this.onTap,
    this.tint,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  /// Colours the tile; defaults to the primary colour.
  ///
  /// Giving each shortcut its own tint is what lets the grid be scanned by
  /// colour once the tiles are familiar, instead of four identical squares
  /// that have to be read.
  final Color? tint;
}

/// Shortcuts to the actions an employee takes most often.
///
/// Laid out as a wrap rather than a `GridView` so it sizes to its content
/// inside the dashboard's scroll view, and so the tile count can change
/// without a fixed row count to maintain.
class QuickActionsGrid extends StatelessWidget {
  const QuickActionsGrid({required this.actions, super.key});

  final List<QuickAction> actions;

  @override
  Widget build(BuildContext context) {
    // Two columns on the narrowest phones, four otherwise, so a label is
    // never clipped.
    final int columns = context.isCompactWidth ? 2 : 4;

    return AppSectionCard(
      title: 'Quick actions',
      icon: Icons.bolt_rounded,
      child: LayoutBuilder(
        builder: (BuildContext context, BoxConstraints constraints) {
          final double spacing = AppSpacing.sm;
          final double tileWidth =
              (constraints.maxWidth - spacing * (columns - 1)) / columns;

          return Wrap(
            spacing: spacing,
            runSpacing: spacing,
            children: <Widget>[
              for (final QuickAction action in actions)
                SizedBox(
                  width: tileWidth,
                  child: _QuickActionTile(action: action),
                ),
            ],
          );
        },
      ),
    );
  }
}

class _QuickActionTile extends StatelessWidget {
  const _QuickActionTile({required this.action});

  final QuickAction action;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color tint = action.tint ?? Theme.of(context).colorScheme.primary;

    // A wash of the tile's own colour rather than the neutral muted grey:
    // the alpha is low enough that four tiles side by side still read as one
    // control group. Lifted on the dark theme, where a light wash vanishes.
    final Color background = tint.withValues(
      alpha: context.isDarkMode ? 0.18 : 0.12,
    );

    return Semantics(
      button: true,
      label: action.label,
      child: ExcludeSemantics(
        child: Material(
          color: background,
          borderRadius: AppRadius.field,
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: action.onTap,
            child: Container(
              // Keeps the whole tile above the minimum touch target, not just
              // the icon.
              constraints: const BoxConstraints(
                minHeight: AppSizes.minTouchTarget + 32,
              ),
              padding: EdgeInsets.symmetric(
                vertical: AppSpacing.md,
                horizontal: AppSpacing.xs,
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Icon(action.icon, size: AppSizes.iconLg, color: tint),
                  Gap.sm,
                  Text(
                    action.label,
                    style: Theme.of(context).textTheme.labelSmall
                        ?.copyWith(color: palette.textPrimary),
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
