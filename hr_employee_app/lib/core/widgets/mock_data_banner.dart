import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// A permanent, unmissable marker that the screen is showing fixtures.
///
/// The app ships with `AppConfig.useMockData` enabled because no backend
/// contract has been agreed yet. Fixtures are useful for building the UI and
/// actively dangerous if anyone mistakes them for payroll or attendance data,
/// so every screen that can show mock data carries this banner.
///
/// It is deliberately not dismissible, and `AppConfig` forces
/// `useMockData` to false in production builds, so this can never ship to an
/// employee.
class MockDataBanner extends StatelessWidget {
  const MockDataBanner({super.key, this.compact = false, this.message});

  /// A slimmer variant for screens that are already dense.
  final bool compact;

  /// Replaces the default wording, for screens with their own voice (the
  /// sign-in screens say "development build").
  final String? message;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Container(
      width: double.infinity,
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: compact ? AppSpacing.xs : AppSpacing.sm,
      ),
      color: palette.warningSurface,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Icon(
            Icons.science_outlined,
            size: AppSizes.iconSm,
            color: palette.warning,
          ),
          Gap.wSm,
          Flexible(
            child: Text(
              message ??
                  (compact
                      ? 'Sample data - not from the HR system'
                      : 'Sample data for development. These figures are made up '
                            'and do not come from the HR system.'),
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: palette.warning,
                fontWeight: FontWeight.w700,
              ),
              textAlign: TextAlign.center,
            ),
          ),
        ],
      ),
    );
  }
}
