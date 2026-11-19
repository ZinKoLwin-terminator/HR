import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
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

/// Shortcuts to the actions an employee takes most often: one row of white
/// cards, icon above label, each icon in its own tint so the row can be
/// scanned by colour once it is familiar.
class QuickActionsGrid extends StatelessWidget {
  const QuickActionsGrid({required this.actions, super.key});

  final List<QuickAction> actions;

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          for (int i = 0; i < actions.length; i++) ...<Widget>[
            Expanded(child: _QuickActionTile(action: actions[i])),
            if (i < actions.length - 1) SizedBox(width: AppSpacing.sm),
          ],
        ],
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
    final BorderRadius radius = BorderRadius.circular(AppRadius.lg);

    return Semantics(
      button: true,
      label: action.label,
      child: ExcludeSemantics(
        child: DecoratedBox(
          decoration: BoxDecoration(
            borderRadius: radius,
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: AppColors.shadow,
                blurRadius: 14,
                offset: Offset(0, 4),
              ),
            ],
          ),
          child: Material(
            color: AppColors.surface,
            borderRadius: radius,
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: action.onTap,
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Icon(action.icon, size: AppSizes.iconLg, color: tint),
                    Gap.md,
                    Text(
                      action.label,
                      style: Theme.of(context).textTheme.labelMedium
                          ?.copyWith(
                            color: palette.textPrimary,
                            fontWeight: FontWeight.w700,
                          ),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
