import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/widgets/app_branding.dart';
import 'package:material_ui/material_ui.dart';

/// Shown while the stored session is being inspected.
///
/// It has no logic of its own: `AuthController.build()` is already running,
/// and the router moves on as soon as that resolves. Keeping the decision in
/// the router rather than here is what stops two places from disagreeing
/// about where the user should go.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.primary,
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const AppLogo(size: 88, isOnDark: true),
            Gap.xl,
            Text(
              AppConstants.appName,
              style: Theme.of(context).textTheme.headlineMedium
                  ?.copyWith(color: Colors.white, fontWeight: FontWeight.w700),
            ),
            Gap.xxxl,
            const SizedBox(
              height: 24,
              width: 24,
              child: CircularProgressIndicator(
                strokeWidth: 2.5,
                valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
