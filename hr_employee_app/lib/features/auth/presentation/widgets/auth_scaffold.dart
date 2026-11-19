import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/widgets/app_branding.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:material_ui/material_ui.dart';

/// How far a revealed section travels on entry, in logical pixels.
const double _revealOffset = 24;

/// The shared chrome of every signed-out screen.
///
/// Two layers: a deep brand gradient that bleeds behind the status bar, and a
/// glass panel carrying the form. That split is deliberate — the ground does
/// the branding so the form itself can stay plain, which is what keeps a
/// credential entry screen feeling trustworthy rather than decorated.
///
/// Sign-in, forgot-password and reset-password all build from this rather
/// than each assembling their own background, header and scroll behaviour, so
/// the three cannot drift apart. A screen supplies only what is its own: the
/// heading, the card content, and the footer.
class AuthScaffold extends ConsumerStatefulWidget {
  const AuthScaffold({
    required this.title,
    required this.child,
    super.key,
    this.subtitle,
    this.footer,
  });

  /// The panel's heading.
  final String title;

  /// One line under [title] saying what the screen is for.
  final String? subtitle;

  /// Content of the glass panel — usually a `Form`.
  final Widget child;

  /// Usually an [AuthSupportFooter].
  final Widget? footer;

  @override
  ConsumerState<AuthScaffold> createState() => _AuthScaffoldState();
}

class _AuthScaffoldState extends ConsumerState<AuthScaffold>
    with SingleTickerProviderStateMixin {
  /// Drives the entry reveal. One controller for the whole screen, with the
  /// sections staggered through [Interval]s, so the sequence cannot drift out
  /// of step the way several controllers would.
  late final AnimationController _entry;
  late final Animation<double> _brandReveal;
  late final Animation<double> _cardReveal;
  late final Animation<double> _footerReveal;

  @override
  void initState() {
    super.initState();
    _entry = AnimationController(
      duration: const Duration(milliseconds: 700),
      vsync: this,
    );
    _brandReveal = _curve(begin: 0, end: 0.6);
    _cardReveal = _curve(begin: 0.15, end: 0.85);
    _footerReveal = _curve(begin: 0.4, end: 1);
    _entry.forward();
  }

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

    // Honour the platform "reduce motion" setting by showing the finished
    // state immediately, rather than animating something the user has asked
    // not to see.
    final bool animate = !MediaQuery.disableAnimationsOf(context);

    return AnnotatedRegion<SystemUiOverlayStyle>(
      // The gradient runs behind the status bar, so its icons have to be
      // light regardless of the app's theme.
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: _AuthBackdrop(
          child: SafeArea(
            child: Column(
              children: <Widget>[
                if (config.useMockData) const MockDataBanner(),
                Expanded(
                  child: LayoutBuilder(
                    builder: (BuildContext context, BoxConstraints viewport) {
                      return _buildBody(viewport, animate: animate);
                    },
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  /// Lockup, then the form, in one centred column.
  Widget _buildBody(BoxConstraints viewport, {required bool animate}) {
    final EdgeInsets padding = EdgeInsets.fromLTRB(
      AppSpacing.lg,
      AppSpacing.xxl,
      AppSpacing.lg,
      AppSpacing.xxl,
    );

    return SingleChildScrollView(
      padding: padding,
      // Claim the full viewport while the content is shorter than the screen,
      // so it sits centred instead of stranded at the top above an empty
      // third of gradient. Once the keyboard — or an error panel — makes the
      // content taller, the minimum stops binding and the page simply
      // scrolls.
      child: ConstrainedBox(
        constraints: BoxConstraints(
          minHeight: viewport.maxHeight - padding.vertical,
        ),
        child: Center(
          child: ConstrainedBox(
            constraints: BoxConstraints(maxWidth: AppSizes.maxFormWidth),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                _Reveal(
                  animation: _brandReveal,
                  isEnabled: animate,
                  child: const _BrandLockup(),
                ),
                Gap.xxl,
                _buildFormColumn(animate: animate),
              ],
            ),
          ),
        ),
      ),
    );
  }

  /// The panel, and whatever belongs above and below it.
  Widget _buildFormColumn({required bool animate}) {
    // These screens carry no app bar, so the only way back from a pushed one
    // is the control below. It is rendered only when there is something to
    // pop: sign-in is the root of the signed-out stack, and reset-password is
    // usually opened cold from a deep link.
    final bool canGoBack = Navigator.of(context).canPop();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        if (canGoBack) ...<Widget>[const _GlassBackButton(), Gap.lg],

        _Reveal(
          animation: _cardReveal,
          isEnabled: animate,
          // The panel arrives with a touch of scale as well as the lift: it
          // is the object the screen is about, and the extra cue is what
          // makes it read as rising out of the gradient rather than sliding
          // up it.
          scaleFrom: 0.96,
          child: _AuthPanel(
            title: widget.title,
            subtitle: widget.subtitle,
            child: widget.child,
          ),
        ),

        if (widget.footer != null) ...<Widget>[
          Gap.xxl,
          _Reveal(
            animation: _footerReveal,
            isEnabled: animate,
            child: widget.footer!,
          ),
        ],
      ],
    );
  }
}

/// The gradient wash the screens sit on, plus its ornaments.
///
/// Three layers, cheapest first: the auth gradient, a single painter that
/// draws the grid, glows, rings and sheen in one pass, and a scrim that
/// darkens the foot of the screen. The ornaments are painted rather than
/// composed from blurred widgets because a `BackdropFilter` would cost a
/// full-screen blur on every frame of the entry animation for the same
/// result.
class _AuthBackdrop extends StatelessWidget {
  const _AuthBackdrop({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: AppColors.authGradient,
          stops: <double>[0, 0.55, 1],
        ),
      ),
      // Clipped by default, which is what lets the ornaments sit partly
      // off-screen instead of being forced into view.
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          const RepaintBoundary(
            child: CustomPaint(painter: _BackdropOrnaments()),
          ),
          IgnorePointer(
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.bottomCenter,
                  end: Alignment.center,
                  colors: <Color>[
                    AppColors.authGradient.first.withValues(alpha: 0.55),
                    AppColors.authGradient.first.withValues(alpha: 0),
                  ],
                ),
              ),
            ),
          ),
          child,
        ],
      ),
    );
  }
}

/// A grid, glows, rings and a sheen, drawn in one pass over the gradient.
///
/// Every value but the grid pitch is a fraction of the painted size rather
/// than a fixed offset, so the composition holds its shape from a 320pt phone
/// to a desktop window instead of bunching into one corner.
class _BackdropOrnaments extends CustomPainter {
  const _BackdropOrnaments();

  /// The ornaments are white at a low alpha throughout: tinting them would
  /// fight the gradient underneath.
  static const Color _light = AppColors.textOnPrimary;

  /// Pitch of the background grid, in logical pixels. Fixed rather than
  /// proportional — a grid that rescales with the window stops reading as a
  /// grid.
  static const double _gridStep = 64;

  @override
  void paint(Canvas canvas, Size size) {
    _paintGrid(canvas, size);

    _paintGlow(
      canvas,
      Offset(size.width * 1.02, -size.height * 0.03),
      size.width * 0.80,
      0.16,
    );
    _paintGlow(
      canvas,
      Offset(-size.width * 0.20, size.height * 0.28),
      size.width * 0.62,
      0.10,
    );
    _paintGlow(
      canvas,
      Offset(size.width * 0.88, size.height * 1.04),
      size.width * 0.70,
      0.10,
    );

    _paintRings(canvas, size);
    _paintSheen(canvas, size);
  }

  void _paintGrid(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..strokeWidth = 1
      ..color = _light.withValues(alpha: 0.03);

    for (double x = 0; x < size.width; x += _gridStep) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), paint);
    }
    for (double y = 0; y < size.height; y += _gridStep) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), paint);
    }
  }

  void _paintGlow(Canvas canvas, Offset centre, double radius, double alpha) {
    final Rect bounds = Rect.fromCircle(center: centre, radius: radius);
    final Paint paint = Paint()
      ..shader = RadialGradient(
        colors: <Color>[
          _light.withValues(alpha: alpha),
          _light.withValues(alpha: 0),
        ],
      ).createShader(bounds);

    canvas.drawCircle(centre, radius, paint);
  }

  /// Concentric hairlines centred just off the top-right corner.
  ///
  /// They give the gradient a focal point without putting anything behind the
  /// form, which is why the centre sits outside the canvas.
  void _paintRings(Canvas canvas, Size size) {
    final Offset centre = Offset(size.width * 0.96, size.height * 0.05);

    for (int i = 0; i < 4; i++) {
      final Paint paint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.2
        ..color = _light.withValues(alpha: 0.11 - i * 0.022);

      canvas.drawCircle(centre, size.width * (0.28 + i * 0.17), paint);
    }
  }

  /// A single band of light running across the upper half.
  void _paintSheen(Canvas canvas, Size size) {
    final Path band = Path()
      ..moveTo(-size.width * 0.15, size.height * 0.50)
      ..lineTo(size.width * 0.62, -size.height * 0.06)
      ..lineTo(size.width * 0.92, size.height * 0.04)
      ..lineTo(size.width * 0.16, size.height * 0.64)
      ..close();

    final Paint paint = Paint()
      ..shader = LinearGradient(
        begin: Alignment.bottomLeft,
        end: Alignment.topRight,
        colors: <Color>[
          _light.withValues(alpha: 0),
          _light.withValues(alpha: 0.06),
          _light.withValues(alpha: 0),
        ],
      ).createShader(Offset.zero & size);

    canvas.drawPath(band, paint);
  }

  @override
  bool shouldRepaint(_BackdropOrnaments oldDelegate) => false;
}

/// Logo, product name and strapline, drawn straight on the gradient above
/// the panel.
class _BrandLockup extends StatelessWidget {
  const _BrandLockup();

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    const Color onBrand = AppColors.textOnPrimary;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          padding: EdgeInsets.all(AppSpacing.sm),
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: <Color>[
                onBrand.withValues(alpha: 0.26),
                onBrand.withValues(alpha: 0.08),
              ],
            ),
            borderRadius: BorderRadius.circular(AppRadius.xl),
            border: Border.all(color: onBrand.withValues(alpha: 0.28)),
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: AppColors.authGradient.first.withValues(alpha: 0.55),
                blurRadius: 24,
                offset: const Offset(0, 10),
                spreadRadius: -8,
              ),
            ],
          ),
          child: const AppLogo(size: 44, isOnDark: true),
        ),
        Gap.wMd,
        Flexible(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                AppConstants.appName,
                style: textStyles.titleLarge?.copyWith(
                  color: onBrand,
                  fontWeight: FontWeight.w700,
                  letterSpacing: -0.2,
                ),
                overflow: TextOverflow.ellipsis,
              ),
              Gap.xxs,
              Text(
                'Employee self-service portal',
                style: textStyles.labelSmall?.copyWith(
                  color: onBrand.withValues(alpha: 0.72),
                  letterSpacing: 0.9,
                ),
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// The glass panel the form sits on.
///
/// Its own container rather than the shared `AppCard`: this panel floats on
/// the auth gradient instead of on the neutral scaffold, so it is translucent
/// rather than opaque and carries a deeper shadow than any in-app panel.
///
/// Its content runs under a dark theme whatever the app's brightness. That is
/// the whole trick of this screen: the shared form widgets — fields, labels,
/// checkbox, links, error panel — are not restyled here, they simply resolve
/// against a dark surface, so the signed-out world can be dark without any
/// widget having to know it is on glass.
class _AuthPanel extends StatelessWidget {
  const _AuthPanel({required this.title, required this.child, this.subtitle});

  final String title;
  final String? subtitle;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final BorderRadius radius = BorderRadius.circular(AppRadius.xl * 1.4);

    return Container(
      padding: EdgeInsets.all(AppSpacing.xl),
      decoration: BoxDecoration(
        borderRadius: radius,
        // Each stop is a white sheen blended over the ground's darkest
        // colour, rather than white alone. A panel made only of translucent
        // white has no body of its own: its own shadow reads straight
        // through it as a second, offset slab.
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[
            Color.alphaBlend(
              onBrand.withValues(alpha: 0.14),
              AppColors.authGradient.first.withValues(alpha: 0.58),
            ),
            Color.alphaBlend(
              onBrand.withValues(alpha: 0.06),
              AppColors.authGradient.first.withValues(alpha: 0.70),
            ),
          ],
        ),
        border: Border.all(color: onBrand.withValues(alpha: 0.16)),
        // Not inset at all, and offset by less than the blur: a shadow drawn
        // smaller than the panel would show its own corners through the
        // translucent fill, as a second slab floating inside the first. This
        // one covers the panel edge to edge, so all that reads through is an
        // even darkening, and the lift is the blur that escapes below.
        boxShadow: <BoxShadow>[
          BoxShadow(
            color: AppColors.authGradient.first.withValues(alpha: 0.45),
            blurRadius: 28,
            offset: const Offset(0, 10),
          ),
        ],
      ),
      child: Theme(
        data: _surfaceTheme(),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              title,
              style: Theme.of(context).textTheme.headlineLarge
                  ?.copyWith(color: onBrand),
            ),
            if (subtitle != null) ...<Widget>[
              Gap.xs,
              Text(
                subtitle!,
                style: Theme.of(context).textTheme.bodyMedium
                    ?.copyWith(color: onBrand.withValues(alpha: 0.72)),
              ),
            ],
            Gap.xl,
            child,
          ],
        ),
      ),
    );
  }

  /// The app's dark theme, retuned for glass.
  ///
  /// Only the surfaces change: fills and resting borders become translucent
  /// white so the gradient reads through them. Focus, error and disabled
  /// states are left exactly as the app defines them, so a field signals its
  /// state the same way here as it does on every other screen.
  ThemeData _surfaceTheme() {
    const Color onBrand = AppColors.textOnPrimary;
    final ThemeData dark = AppTheme.dark;

    final OutlineInputBorder resting = OutlineInputBorder(
      borderRadius: AppRadius.field,
      borderSide: BorderSide(color: onBrand.withValues(alpha: 0.18)),
    );

    return dark.copyWith(
      inputDecorationTheme: dark.inputDecorationTheme.copyWith(
        fillColor: onBrand.withValues(alpha: 0.07),
        border: resting,
        enabledBorder: resting,
        focusedBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: const BorderSide(
            color: AppColors.accentLight,
            width: 1.5,
          ),
        ),
      ),
      checkboxTheme: dark.checkboxTheme.copyWith(
        // Rounded and light-bordered, to match the fields it sits under
        // rather than Material's default square box. The radius is off the
        // scale on purpose: the box Material draws is 18pt across, and
        // `AppRadius.sm` on something that small rounds it into a circle,
        // which reads as a radio button.
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
        side: BorderSide(color: onBrand.withValues(alpha: 0.38), width: 1.5),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.accentLight,
          textStyle: dark.textTheme.labelMedium,
          minimumSize: const Size(0, AppSizes.minTouchTarget),
          padding: EdgeInsets.symmetric(horizontal: AppSpacing.sm),
        ),
      ),
    );
  }
}

/// A back control sized for the gradient rather than for an app bar.
class _GlassBackButton extends StatelessWidget {
  const _GlassBackButton();

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Align(
      alignment: Alignment.centerLeft,
      child: Material(
        color: onBrand.withValues(alpha: 0.12),
        shape: CircleBorder(
          side: BorderSide(color: onBrand.withValues(alpha: 0.22)),
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
      ),
    );
  }
}

/// The primary action of an auth panel: white on the dark ground, which is
/// the highest contrast the screen has to offer and so reads as the one thing
/// to do here.
///
/// The restyle is a theme override around [AppButton] rather than a button of
/// its own, so the control keeps the two behaviours that matter — the
/// callback is detached while the request is in flight, and the control holds
/// its height when the label swaps to the spinner. The lift is dropped while
/// submitting: a disabled control that still looks raised reads as tappable
/// when it is not.
class AuthPrimaryButton extends StatelessWidget {
  const AuthPrimaryButton({
    required this.label,
    required this.isSubmitting,
    required this.onPressed,
    super.key,
  });

  final String label;
  final bool isSubmitting;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    const Color fill = AppColors.textOnPrimary;
    const Color ink = AppColors.primaryDark;

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: AppRadius.button,
        // Kept tight enough to fall inside the panel's padding: a shadow
        // wider than the padding is clipped square at the panel's edge, which
        // reads as a stray bar rather than as a lift.
        boxShadow: isSubmitting
            ? null
            : <BoxShadow>[
                BoxShadow(
                  color: AppColors.authGradient.first.withValues(alpha: 0.45),
                  blurRadius: 16,
                  offset: const Offset(0, 7),
                  spreadRadius: -6,
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
              disabledForegroundColor: ink.withValues(alpha: 0.7),
              shadowColor: Colors.transparent,
              elevation: 0,
              textStyle: theme.textTheme.labelLarge,
              shape: RoundedRectangleBorder(borderRadius: AppRadius.button),
            ),
          ),
        ),
        child: AppButton(
          label: label,
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
    const Color onBrand = AppColors.textOnPrimary;

    final Widget rule = Expanded(
      child: Container(height: 1, color: onBrand.withValues(alpha: 0.14)),
    );

    return Row(
      children: <Widget>[
        rule,
        Padding(
          padding: EdgeInsets.symmetric(horizontal: AppSpacing.md),
          child: Text(
            label.toUpperCase(),
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: onBrand.withValues(alpha: 0.55),
              letterSpacing: 1.2,
            ),
          ),
        ),
        rule,
      ],
    );
  }
}

/// A persistent error panel inside an auth panel.
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

/// Trust line and support route, drawn on the gradient below the panel.
class AuthSupportFooter extends StatelessWidget {
  const AuthSupportFooter({super.key});

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    const Color onBrand = AppColors.textOnPrimary;

    return Column(
      children: <Widget>[
        // A pill rather than a bare line: it reads as a badge the product is
        // claiming, which is the point of saying this on a sign-in screen.
        Align(
          child: Container(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm,
            ),
            decoration: BoxDecoration(
              color: onBrand.withValues(alpha: 0.10),
              borderRadius: AppRadius.chip,
              border: Border.all(color: onBrand.withValues(alpha: 0.20)),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Icon(
                  Icons.lock_outline_rounded,
                  size: AppSizes.iconSm,
                  color: onBrand.withValues(alpha: 0.82),
                ),
                Gap.wXs,
                Flexible(
                  child: Text(
                    'Encrypted, company-managed sign-in',
                    style: textStyles.labelSmall?.copyWith(
                      color: onBrand.withValues(alpha: 0.82),
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
          ),
        ),
        Gap.md,
        Text(
          'Having trouble? Contact your HR administrator.',
          style: textStyles.bodySmall?.copyWith(
            color: onBrand.withValues(alpha: 0.64),
          ),
          textAlign: TextAlign.center,
        ),
      ],
    );
  }
}

/// Fades and lifts a section into place on first build.
///
/// Opacity and a translation rather than a `SlideTransition` inside a
/// `Column`: the section keeps its layout slot throughout, so the page does
/// not reflow as each part arrives.
class _Reveal extends StatelessWidget {
  const _Reveal({
    required this.animation,
    required this.child,
    required this.isEnabled,
    this.scaleFrom = 1,
  });

  final Animation<double> animation;
  final Widget child;

  /// When false the child is rendered in its finished state, with no
  /// animation at all.
  final bool isEnabled;

  /// Scale the child starts at. Left at 1 for the sections where a lift alone
  /// is the right amount of movement.
  final double scaleFrom;

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
        final double scale = scaleFrom + (1 - scaleFrom) * progress;

        return Opacity(
          opacity: progress,
          child: Transform.translate(
            offset: Offset(0, (1 - progress) * _revealOffset),
            child: Transform.scale(scale: scale, child: built),
          ),
        );
      },
    );
  }
}
