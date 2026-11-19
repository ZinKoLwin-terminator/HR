import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_screenutil/flutter_screenutil.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/app_router.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:material_ui/material_ui.dart';

/// The application widget.
///
/// Three responsibilities and no more: establish the responsive scale, apply
/// the theme, and hand navigation to the router.
class HrEmployeeApp extends ConsumerWidget {
  const HrEmployeeApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final GoRouter router = ref.watch(routerProvider);

    return ScreenUtilInit(
      designSize: AppSizes.designSize,
      minTextAdapt: true,
      // False because the app is phone-first; allowing split-screen scaling
      // would distort the fixed-height controls.
      splitScreenMode: false,
      builder: (BuildContext context, Widget? child) {
        return MaterialApp.router(
          title: AppConstants.appName,
          debugShowCheckedModeBanner: false,
          routerConfig: router,
          theme: AppTheme.light,
          darkTheme: AppTheme.dark,
          // Pinned to light rather than following the system setting: the
          // brief specifies the light palette, and a phone left on the
          // system-wide dark setting would otherwise show an app nobody
          // signed off. The dark theme is still built and kept in step, so
          // an in-app appearance toggle is a one-line change here.
          themeMode: ThemeMode.light,
          builder: (BuildContext context, Widget? routedChild) {
            return _TextScaleGuard(child: routedChild ?? const SizedBox());
          },
        );
      },
    );
  }
}

/// Clamps the system text scale.
///
/// Honouring the user's font-size setting is required for accessibility, but
/// an unbounded scale breaks fixed-height rows and the bottom navigation
/// labels. Clamping to 1.3 keeps large-text users supported without the
/// layout collapsing.
class _TextScaleGuard extends StatelessWidget {
  const _TextScaleGuard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final MediaQueryData media = MediaQuery.of(context);

    return MediaQuery(
      data: media.copyWith(
        textScaler: media.textScaler.clamp(
          minScaleFactor: 0.9,
          maxScaleFactor: 1.3,
        ),
      ),
      child: child,
    );
  }
}
