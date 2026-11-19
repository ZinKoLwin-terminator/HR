import 'dart:math' as math;

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/widgets/app_branding.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:material_ui/material_ui.dart';

/// How far a revealed section travels on entry, in logical pixels.
const double _revealOffset = 24;

/// Brand display serif, bundled for the auth headings and the wordmark.
const String _displayFont = 'CormorantGaramond';

/// The shared chrome of every signed-out screen.
///
/// Two parts: an Apex Navy header carrying the brand lockup and the page
/// heading, and a white sheet that rises over its lower edge and holds the
/// form. The header does the branding so the form can stay plain, which is
/// what keeps a credential screen feeling trustworthy rather than decorated.
///
/// The sheet always uses the light theme, whatever the app's brightness: the
/// signed-out world is one fixed look, the same in both modes, and the shared
/// form widgets simply resolve against a white surface.
///
/// Sign-in, forgot-password and reset-password all build from this, so the
/// three cannot drift apart. A screen supplies only what is its own: the
/// heading, the form, and the footer.
class AuthScaffold extends ConsumerStatefulWidget {
  const AuthScaffold({
    required this.title,
    required this.child,
    super.key,
    this.subtitle,
    this.footer,
  });

  /// The page heading, set in the brand serif on the navy header.
  final String title;

  /// One line under [title] saying what the screen is for.
  final String? subtitle;

  /// Content of the sheet — usually a `Form`.
  final Widget child;

  /// Pinned to the foot of the sheet. Usually an [AuthSupportFooter].
  final Widget? footer;

  @override
  ConsumerState<AuthScaffold> createState() => _AuthScaffoldState();
}

class _AuthScaffoldState extends ConsumerState<AuthScaffold>
    with SingleTickerProviderStateMixin {
  /// One controller for the whole entry, staggered through [Interval]s.
  late final AnimationController _entry = AnimationController(
    duration: const Duration(milliseconds: 650),
    vsync: this,
  )..forward();
  late final Animation<double> _headerReveal = _curve(begin: 0, end: 0.6);
  late final Animation<double> _sheetReveal = _curve(begin: 0.2, end: 1);

  Animation<double> _curve({required double begin, required double end}) {
    return CurvedAnimation(
      parent: _entry,
      curve: Interval(begin, end, curve: Curves.easeOutCubic),
    );
  }

  @override
  void dispose() {
    _entry.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final AppConfig config = ref.watch(appConfigProvider);

    // Honour "reduce motion" by showing the finished state at once.
    final bool animate = !MediaQuery.disableAnimationsOf(context);
    final bool canGoBack = Navigator.of(context).canPop();

    return AnnotatedRegion<SystemUiOverlayStyle>(
      // The navy header runs under the status bar.
      value: SystemUiOverlayStyle.light,
      child: Theme(
        data: _sheetTheme(),
        child: Scaffold(
          backgroundColor: AppColors.surface,
          body: Column(
            children: <Widget>[
              if (config.useMockData)
                const SafeArea(
                  bottom: false,
                  child: MockDataBanner(
                    compact: true,
                    message:
                        'Development build · sample data, not from the HR '
                        'system',
                  ),
                ),
              Expanded(
                child: MediaQuery.removePadding(
                  context: context,
                  // The banner, when shown, already sits below the status
                  // bar; without it the header takes the inset itself.
                  removeTop: config.useMockData,
                  child: CustomScrollView(
                    slivers: <Widget>[
                      SliverFillRemaining(
                        hasScrollBody: false,
                        // White behind everything: the sheet rides up over
                        // the header, and what it leaves uncovered at the
                        // bottom must read as more sheet, not as navy.
                        child: ColoredBox(
                          color: AppColors.surface,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: <Widget>[
                              _Reveal(
                                animation: _headerReveal,
                                isEnabled: animate,
                                child: _AuthHeader(
                                  title: widget.title,
                                  subtitle: widget.subtitle,
                                  canGoBack: canGoBack,
                                ),
                              ),
                              Expanded(
                                child: _Reveal(
                                  animation: _sheetReveal,
                                  isEnabled: animate,
                                  child: _AuthSheet(
                                    footer: widget.footer,
                                    child: widget.child,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The light theme, with quieter field icons and checkbox, as on the brand
/// sign-in design.
ThemeData _sheetTheme() {
  final ThemeData light = AppTheme.light;
  final AppPalette palette = light.extension<AppPalette>()!;

  return light.copyWith(
    inputDecorationTheme: light.inputDecorationTheme.copyWith(
      prefixIconColor: palette.textTertiary,
      suffixIconColor: palette.textSecondary,
    ),
    checkboxTheme: light.checkboxTheme.copyWith(
      side: BorderSide(color: palette.textTertiary, width: 1.5),
    ),
  );
}

/// Lockup, then the heading, on navy with a pair of faint hexagons.
class _AuthHeader extends StatelessWidget {
  const _AuthHeader({
    required this.title,
    required this.subtitle,
    required this.canGoBack,
  });

  final String title;
  final String? subtitle;
  final bool canGoBack;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: ClipRect(child: _content(textStyles, onBrand)),
    );
  }

  Widget _content(TextTheme textStyles, Color onBrand) {
    return CustomPaint(
      painter: const _HeaderHexagons(),
      child: SafeArea(
        bottom: false,
        child: Center(
          child: Container(
            // Full width up to the form width, so the header's content
            // lines up with the form's left edge instead of centring.
            width: double.infinity,
            constraints: BoxConstraints(maxWidth: AppSizes.maxFormWidth),
            child: Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.xxl,
                AppSpacing.xl,
                AppSpacing.xxl,
                // Room for the sheet's rounded top to overlap.
                AppSpacing.xxl + _AuthSheet.overlap,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  if (canGoBack) ...<Widget>[const _BackButton(), Gap.lg],
                  const AppLockup(),
                  SizedBox(height: AppSpacing.xxxl + AppSpacing.sm),
                  Text(
                    title,
                    style: TextStyle(
                      fontFamily: _displayFont,
                      fontSize: 38,
                      height: 1.1,
                      fontWeight: FontWeight.w600,
                      color: onBrand,
                    ),
                  ),
                  if (subtitle != null) ...<Widget>[
                    Gap.sm,
                    Text(
                      subtitle!,
                      style: textStyles.bodyMedium?.copyWith(
                        color: onBrand.withValues(alpha: 0.78),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Two hairline hexagons off the right edge — the mark's frame, at a scale
/// where it reads as texture rather than as a second logo.
class _HeaderHexagons extends CustomPainter {
  const _HeaderHexagons();

  @override
  void paint(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2
      ..color = AppColors.textOnPrimary.withValues(alpha: 0.07);

    final Offset centre = Offset(size.width * 0.98, size.height * 0.42);
    final double radius = math.max(size.height * 0.55, 150);
    canvas
      ..drawPath(_hexagon(centre, radius), paint)
      ..drawPath(_hexagon(centre, radius * 0.66), paint);
  }

  /// Pointy-top, like the mark.
  static Path _hexagon(Offset centre, double radius) {
    final Path path = Path();
    for (int i = 0; i < 6; i++) {
      final double angle = math.pi / 3 * i - math.pi / 2;
      final Offset point =
          centre + Offset(math.cos(angle), math.sin(angle)) * radius;
      i == 0
          ? path.moveTo(point.dx, point.dy)
          : path.lineTo(point.dx, point.dy);
    }
    return path..close();
  }

  @override
  bool shouldRepaint(_HeaderHexagons oldDelegate) => false;
}

/// The white sheet: form first, footer pinned to its foot.
class _AuthSheet extends StatelessWidget {
  const _AuthSheet({required this.child, this.footer});

  /// How far the sheet's top rides up over the header.
  static const double overlap = 24;

  final Widget child;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    return Transform.translate(
      offset: const Offset(0, -overlap),
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.vertical(
            top: Radius.circular(AppRadius.xl * 1.4),
          ),
        ),
        child: SafeArea(
          top: false,
          child: Center(
            child: ConstrainedBox(
              constraints: BoxConstraints(maxWidth: AppSizes.maxFormWidth),
              child: Padding(
                padding: EdgeInsets.fromLTRB(
                  AppSpacing.xxl,
                  AppSpacing.xxl + AppSpacing.xs,
                  AppSpacing.xxl,
                  AppSpacing.lg,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    child,
                    if (footer != null) ...<Widget>[
                      // Pushes the footer to the foot of a tall screen, and
                      // collapses to a fixed gap once the content is taller.
                      const Spacer(),
                      SizedBox(height: AppSpacing.xxxl),
                      footer!,
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

/// White back arrow on the navy header, for the pushed auth screens.
class _BackButton extends StatelessWidget {
  const _BackButton();

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Material(
      color: onBrand.withValues(alpha: 0.10),
      shape: CircleBorder(
        side: BorderSide(color: onBrand.withValues(alpha: 0.20)),
      ),
      clipBehavior: Clip.antiAlias,
      child: IconButton(
        onPressed: () => Navigator.of(context).pop(),
        icon: const Icon(Icons.arrow_back_rounded, color: onBrand),
        iconSize: AppSizes.iconLg,
        tooltip: MaterialLocalizations.of(context).backButtonTooltip,
        constraints: const BoxConstraints.tightFor(
          width: AppSizes.minTouchTarget,
          height: AppSizes.minTouchTarget,
        ),
      ),
    );
  }
}

/// The white Apex HRMS lockup for a navy ground: the mark, the APEX
/// wordmark in the brand serif, and HRMS spaced out beneath it.
class AppLockup extends StatelessWidget {
  const AppLockup({super.key});

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Semantics(
      label: 'Apex HRMS',
      container: true,
      child: ExcludeSemantics(
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const AppLogo(size: 44, isOnDark: true),
            Gap.wMd,
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const Text(
                  'APEX',
                  style: TextStyle(
                    fontFamily: _displayFont,
                    fontSize: 24,
                    height: 1,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 5,
                    color: onBrand,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  'HRMS',
                  style: TextStyle(
                    fontSize: 9,
                    height: 1,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 4,
                    color: onBrand.withValues(alpha: 0.82),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// The primary action of an auth sheet: full-width Apex Navy with a soft
/// navy lift.
///
/// A theme override around [AppButton] rather than a button of its own, so
/// it keeps the shared behaviour — the callback detaches while the request
/// is in flight, and the control holds its height when the label swaps to
/// the spinner. The lift is dropped while submitting: a disabled control that
/// still looks raised reads as tappable when it is not.
class AuthPrimaryButton extends StatelessWidget {
  const AuthPrimaryButton({
    required this.label,
    required this.isSubmitting,
    required this.onPressed,
    super.key,
    this.loadingLabel,
  });

  final String label;

  /// Shown beside the spinner while submitting — "Signing in…".
  final String? loadingLabel;

  final bool isSubmitting;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    const Color fill = AppColors.apexNavy;
    const Color ink = AppColors.textOnPrimary;

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: AppRadius.button,
        boxShadow: isSubmitting
            ? null
            : <BoxShadow>[
                BoxShadow(
                  color: fill.withValues(alpha: 0.30),
                  blurRadius: 24,
                  offset: const Offset(0, 12),
                  spreadRadius: -10,
                ),
              ],
      ),
      child: Theme(
        data: theme.copyWith(
          filledButtonTheme: FilledButtonThemeData(
            style: FilledButton.styleFrom(
              minimumSize: Size(double.infinity, AppSizes.buttonHeight),
              backgroundColor: fill,
              disabledBackgroundColor: fill.withValues(alpha: 0.55),
              foregroundColor: ink,
              disabledForegroundColor: ink.withValues(alpha: 0.8),
              shadowColor: Colors.transparent,
              elevation: 0,
              textStyle: theme.textTheme.labelLarge,
              shape: RoundedRectangleBorder(borderRadius: AppRadius.button),
            ),
          ),
        ),
        child: AppButton(
          label: label,
          loadingLabel: loadingLabel,
          trailingIcon: Icons.arrow_forward_rounded,
          isLoading: isSubmitting,
          onPressed: onPressed,
        ),
      ),
    );
  }
}

/// A labelled rule, separating the form from an alternative route into the
/// app.
class AuthOrDivider extends StatelessWidget {
  const AuthOrDivider({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Widget rule = Expanded(
      child: Container(height: 1, color: palette.border),
    );

    return Row(
      children: <Widget>[
        rule,
        Padding(
          padding: EdgeInsets.symmetric(horizontal: AppSpacing.md),
          child: Text(
            label,
            style: Theme.of(context).textTheme.bodySmall
                ?.copyWith(color: palette.textTertiary),
          ),
        ),
        rule,
      ],
    );
  }
}

/// A persistent error panel inside an auth sheet.
///
/// Shown in addition to the snack bar: the snack bar disappears, and a user
/// retyping a password should still be able to see what went wrong.
class AuthInlineFailure extends StatelessWidget {
  const AuthInlineFailure({required this.failure, super.key});

  final Failure failure;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: palette.dangerSurface,
        borderRadius: AppRadius.field,
        border: Border.all(color: palette.danger.withValues(alpha: 0.35)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            Icons.error_outline_rounded,
            size: AppSizes.iconMd,
            color: palette.danger,
          ),
          Gap.wSm,
          Expanded(
            child: Text(
              failure.message,
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: palette.danger),
            ),
          ),
        ],
      ),
    );
  }
}

/// Trust line and support route, at the foot of the sheet.
class AuthSupportFooter extends StatelessWidget {
  const AuthSupportFooter({super.key});

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;

    return Column(
      children: <Widget>[
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(
              Icons.verified_user_outlined,
              size: AppSizes.iconSm,
              color: palette.textSecondary,
            ),
            Gap.wXs,
            Flexible(
              child: Text(
                'Encrypted, company-managed sign-in',
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textSecondary,
                ),
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
        Gap.xxs,
        Text(
          'Having trouble? Contact your HR administrator.',
          style: textStyles.bodySmall?.copyWith(color: palette.textTertiary),
          textAlign: TextAlign.center,
        ),
      ],
    );
  }
}

/// Fades and lifts a section into place on first build.
///
/// Opacity and a translation rather than a `SlideTransition`: the section
/// keeps its layout slot throughout, so the page does not reflow as each
/// part arrives.
class _Reveal extends StatelessWidget {
  const _Reveal({
    required this.animation,
    required this.child,
    required this.isEnabled,
  });

  final Animation<double> animation;
  final Widget child;

  /// When false the child is rendered in its finished state.
  final bool isEnabled;

  @override
  Widget build(BuildContext context) {
    if (!isEnabled) {
      return child;
    }

    return AnimatedBuilder(
      animation: animation,
      child: child,
      builder: (BuildContext context, Widget? built) {
        final double progress = animation.value.clamp(0.0, 1.0);
        return Opacity(
          opacity: progress,
          child: Transform.translate(
            offset: Offset(0, (1 - progress) * _revealOffset),
            child: built,
          ),
        );
      },
    );
  }
}
