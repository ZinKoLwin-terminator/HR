import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/widgets/app_branding.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/splash_gate.dart';
import 'package:lottie/lottie.dart';
import 'package:material_ui/material_ui.dart';

/// The Apex HRMS logo reveal, shown while the stored session is inspected.
///
/// Built to the brand spec in `assets/animations/` (README of the splash
/// pack): navy ground, the Lottie centred at ~77% of the width, played once,
/// with a slim loading bar 72dp from the bottom.
///
/// It decides nothing about where to go. When the animation finishes it
/// opens [splashGateProvider]; the router leaves the splash once that gate
/// is open **and** the session has been resolved, whichever is later.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  static const String animationAsset =
      'assets/animations/apex_hrms_splash.json';

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _reveal = AnimationController(vsync: this);

  /// Lottie artboard proportions, 900 × 840.
  static const double _aspectRatio = 900 / 840;

  /// Spec: 300dp on a 390dp-wide phone.
  static const double _maxLogoWidth = 300;
  static const double _logoWidthFraction = 0.77;

  @override
  void dispose() {
    _reveal.dispose();
    super.dispose();
  }

  void _onLoaded(LottieComposition composition) {
    _reveal.duration = composition.duration;

    // Reduce Motion: show the finished logo instead of playing it.
    if (MediaQuery.disableAnimationsOf(context)) {
      _reveal.value = 1;
      _finish();
      return;
    }
    _reveal.forward().whenComplete(_finish);
  }

  void _finish() {
    if (mounted) ref.read(splashGateProvider.notifier).open();
  }

  @override
  Widget build(BuildContext context) {
    final double logoWidth =
        (MediaQuery.sizeOf(context).width * _logoWidthFraction)
            .clamp(0, _maxLogoWidth)
            .toDouble();

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        // Matches the native launch window, so there is no colour jump
        // between the OS splash and this one.
        backgroundColor: AppColors.apexNavy,
        body: Stack(
          children: <Widget>[
            Center(
              child: SizedBox(
                width: logoWidth,
                height: logoWidth / _aspectRatio,
                child: Lottie.asset(
                  SplashScreen.animationAsset,
                  controller: _reveal,
                  repeat: false,
                  fit: BoxFit.contain,
                  onLoaded: _onLoaded,
                  // A missing or corrupt animation must not strand the user
                  // on the splash: show the static lockup and move on.
                  errorBuilder: (BuildContext context, Object error, _) {
                    WidgetsBinding.instance.addPostFrameCallback((_) {
                      _finish();
                    });
                    return AppWordmark(width: logoWidth);
                  },
                ),
              ),
            ),
            const Positioned(
              left: 0,
              right: 0,
              bottom: 72,
              child: SafeArea(top: false, child: _LoadingStatus()),
            ),
          ],
        ),
      ),
    );
  }
}

/// Spec: bar 120 × 3dp, track white at 16%, fill `#2DD4BF`.
class _LoadingStatus extends StatelessWidget {
  const _LoadingStatus();

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Semantics(
      label: 'Preparing your workspace',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          SizedBox(
            width: 120,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(AppRadius.pill),
              child: LinearProgressIndicator(
                minHeight: 3,
                backgroundColor: onBrand.withValues(alpha: 0.16),
                valueColor: const AlwaysStoppedAnimation<Color>(
                  AppColors.summitTealBright,
                ),
              ),
            ),
          ),
          Gap.md,
          ExcludeSemantics(
            child: Text(
              'Preparing your workspace…',
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: onBrand.withValues(alpha: 0.6),
                letterSpacing: 0.6,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
