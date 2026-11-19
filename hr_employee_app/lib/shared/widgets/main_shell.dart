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
          Expanded(child: shell),
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
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: context.palette.border)),
      ),
      child: NavigationBar(
        selectedIndex: currentIndex,
        onDestinationSelected: onSelect,
        destinations: <Widget>[
          for (final _NavItem item in _items)
            NavigationDestination(
              label: item.label,
              icon: Icon(item.icon, size: AppSizes.iconLg),
              selectedIcon: Icon(item.selectedIcon, size: AppSizes.iconLg),
              tooltip: item.label,
            ),
        ],
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
