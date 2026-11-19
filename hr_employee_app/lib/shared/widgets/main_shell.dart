import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:material_ui/material_ui.dart';

/// The five-tab chrome that wraps the main screens.
///
/// Holds no feature state: the shell is given to it by `GoRouter`, and each
/// branch keeps its own navigation stack.
class MainShell extends ConsumerWidget {
  const MainShell({required this.shell, super.key});

  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AppConfig config = ref.watch(appConfigProvider);

    return Scaffold(
      body: Column(
        children: <Widget>[
          if (config.useMockData)
            const SafeArea(bottom: false, child: MockDataBanner(compact: true)),
          // The banner has taken the status-bar inset; without this every tab
          // whose header pads for the status bar would pad for it twice.
          Expanded(
            child: MediaQuery.removePadding(
              context: context,
              removeTop: config.useMockData,
              child: shell,
            ),
          ),
        ],
      ),
      bottomNavigationBar: _BottomNavigation(
        currentIndex: shell.currentIndex,
        onSelect: _onSelect,
      ),
    );
  }

  /// Switches branch, or pops to the branch root when the active tab is
  /// tapped again — the behaviour users expect from a tab bar.
  void _onSelect(int index) {
    shell.goBranch(index, initialLocation: index == shell.currentIndex);
  }
}

class _BottomNavigation extends StatelessWidget {
  const _BottomNavigation({required this.currentIndex, required this.onSelect});

  final int currentIndex;
  final ValueChanged<int> onSelect;

  static const List<_NavItem> _items = <_NavItem>[
    _NavItem(
      label: 'Home',
      icon: Icons.home_outlined,
      selectedIcon: Icons.home_rounded,
    ),
    _NavItem(
      label: 'Attendance',
      icon: Icons.schedule_outlined,
      selectedIcon: Icons.schedule_rounded,
    ),
    _NavItem(
      label: 'Leave',
      icon: Icons.event_available_outlined,
      selectedIcon: Icons.event_available_rounded,
    ),
    _NavItem(
      label: 'Payslip',
      icon: Icons.receipt_long_outlined,
      selectedIcon: Icons.receipt_long_rounded,
    ),
    _NavItem(
      label: 'Profile',
      icon: Icons.person_outline_rounded,
      selectedIcon: Icons.person_rounded,
    ),
  ];

  @override
  Widget build(BuildContext context) {
    // A floating Apex Navy pill rather than a full-width bar. It sits on
    // the scaffold's own colour, so the margin around it reads as page.
    return ColoredBox(
      color: Theme.of(context).scaffoldBackgroundColor,
      child: SafeArea(
        top: false,
        minimum: EdgeInsets.only(bottom: AppSpacing.sm),
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.xs,
            AppSpacing.lg,
            AppSpacing.xs,
          ),
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: AppColors.apexNavy,
              borderRadius: BorderRadius.circular(AppRadius.pill),
              boxShadow: <BoxShadow>[
                BoxShadow(
                  color: AppColors.apexNavy.withValues(alpha: 0.28),
                  blurRadius: 20,
                  offset: const Offset(0, 8),
                ),
              ],
            ),
            child: Padding(
              padding: const EdgeInsets.all(6),
              child: Row(
                children: <Widget>[
                  for (int i = 0; i < _items.length; i++)
                    Expanded(
                      // The selected tab takes more room for its label.
                      flex: i == currentIndex ? 2 : 1,
                      child: _NavButton(
                        item: _items[i],
                        isSelected: i == currentIndex,
                        onTap: () => onSelect(i),
                      ),
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

/// One tab: icon only, or icon and label in a lighter pill when selected.
class _NavButton extends StatelessWidget {
  const _NavButton({
    required this.item,
    required this.isSelected,
    required this.onTap,
  });

  final _NavItem item;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Semantics(
      button: true,
      selected: isSelected,
      label: item.label,
      child: ExcludeSemantics(
        child: Tooltip(
          message: item.label,
          child: Material(
            color: isSelected
                ? onBrand.withValues(alpha: 0.14)
                : Colors.transparent,
            borderRadius: BorderRadius.circular(AppRadius.pill),
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: SizedBox(
                height: AppSizes.minTouchTarget,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Icon(
                      isSelected ? item.selectedIcon : item.icon,
                      size: AppSizes.iconMd + 2,
                      color: isSelected
                          ? onBrand
                          : onBrand.withValues(alpha: 0.66),
                    ),
                    if (isSelected) ...<Widget>[
                      Gap.wXs,
                      Flexible(
                        child: Text(
                          item.label,
                          maxLines: 1,
                          overflow: TextOverflow.fade,
                          softWrap: false,
                          style: Theme.of(context).textTheme.labelMedium
                              ?.copyWith(
                                color: onBrand,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                      ),
                    ],
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

class _NavItem {
  const _NavItem({
    required this.label,
    required this.icon,
    required this.selectedIcon,
  });

  final String label;
  final IconData icon;
  final IconData selectedIcon;
}
