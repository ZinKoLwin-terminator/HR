import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:material_ui/material_ui.dart';

/// The Apex HRMS mark: a hexagon framing a figure over two rising chevrons.
///
/// Painted from the brand SVGs in `assets/brand/` rather than loaded as an
/// image, so it is sharp at every size and needs no SVG dependency. The same
/// painter renders the launcher icons (see
/// `test/tool/generate_brand_icons_test.dart`), so the icon on the home
/// screen and the logo inside the app cannot drift apart.
///
/// Below [_compactBelow] logical pixels the hexagon's hairlines stop
/// reading, so the mark drops to the favicon form — figure and one chevron —
/// the way the brand sheet does at small sizes.
class AppLogo extends StatelessWidget {
  const AppLogo({super.key, this.size = 64, this.isOnDark = false});

  final double size;

  /// White strokes for the dark auth ground; navy ones elsewhere.
  final bool isOnDark;

  static const double _compactBelow = 28;

  @override
  Widget build(BuildContext context) {
    final ApexMarkStyle style;
    if (size < _compactBelow) {
      style = ApexMarkStyle.favicon;
    } else {
      style = isOnDark ? ApexMarkStyle.onDark : ApexMarkStyle.onLight;
    }

    return Semantics(
      label: 'Apex HRMS',
      image: true,
      child: SizedBox.square(
        dimension: size,
        child: CustomPaint(painter: ApexMarkPainter(style)),
      ),
    );
  }
}

/// The renderings of the mark the brand sheet defines.
enum ApexMarkStyle {
  /// Navy hexagon with a mist inner hairline, on a light surface.
  onLight,

  /// White hexagon, steel-blue base chevron, on a dark surface.
  onDark,

  /// Full-bleed navy square carrying the [onDark] mark — the launcher icon.
  appIcon,

  /// [appIcon] with the mark shrunk into the 80% safe zone a maskable
  /// (adaptive) icon may be cropped to.
  appIconMaskable,

  /// Rounded navy tile with only the figure and one chevron, for 16–32px.
  favicon,
}

/// Paints the mark in a square, from the 64-unit grid of the brand SVGs.
class ApexMarkPainter extends CustomPainter {
  const ApexMarkPainter(this.style);

  final ApexMarkStyle style;

  @override
  void paint(Canvas canvas, Size size) {
    final double side = size.shortestSide;
    canvas.translate(
      (size.width - side) / 2,
      (size.height - side) / 2,
    );

    switch (style) {
      case ApexMarkStyle.onLight:
        _paintMark(canvas, side / 64, isOnDark: false);
      case ApexMarkStyle.onDark:
        _paintMark(canvas, side / 64, isOnDark: true);
      case ApexMarkStyle.appIcon:
        _paintIcon(canvas, side, markScale: 880 / 1024);
      case ApexMarkStyle.appIconMaskable:
        _paintIcon(canvas, side, markScale: 0.66);
      case ApexMarkStyle.favicon:
        _paintFavicon(canvas, side / 64);
    }
  }

  /// `apex-hrms-app-icon.svg`: navy ground, mark centred at [markScale].
  void _paintIcon(Canvas canvas, double side, {required double markScale}) {
    canvas.drawRect(
      Rect.fromLTWH(0, 0, side, side),
      Paint()..color = AppColors.apexNavy,
    );
    final double markSide = side * markScale;
    canvas
      ..save()
      ..translate((side - markSide) / 2, (side - markSide) / 2);
    _paintMark(canvas, markSide / 64, isOnDark: true);
    canvas.restore();
  }

  /// `apex-hrms-mark.svg` / `apex-hrms-mark-white.svg`.
  void _paintMark(Canvas canvas, double unit, {required bool isOnDark}) {
    final Color frame = isOnDark ? Colors.white : AppColors.apexNavy;
    final Color base = isOnDark ? AppColors.steelBlue : AppColors.mistBlue;

    canvas.drawPath(
      _hexagon(unit, top: 6, upper: 19, lower: 45, bottom: 58, left: 9.5),
      _stroke(frame, 1.3 * unit),
    );
    if (!isOnDark) {
      canvas.drawPath(
        _hexagon(
          unit,
          top: 9.4,
          upper: 20.7,
          lower: 43.3,
          bottom: 54.6,
          left: 12.4,
        ),
        _stroke(AppColors.mistBlue, 0.4 * unit),
      );
    }
    canvas
      ..drawPath(
        _chevron(unit, 17.7, 46.2, 35.4),
        _stroke(base, 2.6 * unit),
      )
      ..drawPath(
        _chevron(unit, 21.4, 38.9, 30.6),
        _stroke(AppColors.summitTeal, 2.6 * unit),
      )
      ..drawCircle(
        Offset(32 * unit, 23.4 * unit),
        3.6 * unit,
        Paint()
          ..isAntiAlias = true
          ..color = frame,
      );
  }

  /// `apex-hrms-favicon.svg`.
  void _paintFavicon(Canvas canvas, double unit) {
    canvas
      ..drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromLTWH(0, 0, 64 * unit, 64 * unit),
          Radius.circular(14 * unit),
        ),
        Paint()
          ..isAntiAlias = true
          ..color = AppColors.apexNavy,
      )
      ..drawPath(
        _chevron(unit, 17, 45, 33),
        _stroke(AppColors.summitTealBright, 6 * unit),
      )
      ..drawCircle(
        Offset(32 * unit, 20 * unit),
        6.5 * unit,
        Paint()
          ..isAntiAlias = true
          ..color = Colors.white,
      );
  }

  /// A pointy-top hexagon, symmetric about x = 32.
  static Path _hexagon(
    double unit, {
    required double top,
    required double upper,
    required double lower,
    required double bottom,
    required double left,
  }) {
    final double right = 64 - left;
    return Path()
      ..moveTo(32 * unit, top * unit)
      ..lineTo(right * unit, upper * unit)
      ..lineTo(right * unit, lower * unit)
      ..lineTo(32 * unit, bottom * unit)
      ..lineTo(left * unit, lower * unit)
      ..lineTo(left * unit, upper * unit)
      ..close();
  }

  /// A chevron from (left, foot) up to (32, apex) and down to the mirror.
  static Path _chevron(double unit, double left, double foot, double apex) {
    return Path()
      ..moveTo(left * unit, foot * unit)
      ..lineTo(32 * unit, apex * unit)
      ..lineTo((64 - left) * unit, foot * unit);
  }

  static Paint _stroke(Color color, double width) {
    return Paint()
      ..isAntiAlias = true
      ..style = PaintingStyle.stroke
      ..strokeWidth = width
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..color = color;
  }

  @override
  bool shouldRepaint(ApexMarkPainter oldDelegate) => oldDelegate.style != style;
}

/// The full white Apex HRMS lockup — mark over the APEX wordmark and HRMS
/// rule — for the dark splash.
///
/// An image rather than painted, because the wordmark is set in a serif the
/// app does not bundle.
class AppWordmark extends StatelessWidget {
  const AppWordmark({super.key, this.width = 220});

  final double width;

  static const String _asset = 'assets/images/apex_hrms_logo_white.png';

  @override
  Widget build(BuildContext context) {
    // The source is 2408px wide; decode it at the size it is shown rather
    // than holding a ~20MB bitmap for a screen that lasts a second.
    final double pixelRatio = MediaQuery.devicePixelRatioOf(context);

    return Image.asset(
      _asset,
      width: width,
      cacheWidth: (width * pixelRatio).round(),
      fit: BoxFit.contain,
      filterQuality: FilterQuality.medium,
      semanticLabel: 'Apex HRMS',
    );
  }
}

/// Logo, product name and an optional strapline, used on the auth screens.
class AppBrandHeader extends StatelessWidget {
  const AppBrandHeader({
    required this.title,
    super.key,
    this.subtitle,
    this.showLogo = true,
  });

  final String title;
  final String? subtitle;
  final bool showLogo;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        if (showLogo) ...<Widget>[const AppLogo(), Gap.xl],
        Text(title, style: textStyles.headlineLarge),
        if (subtitle != null) ...<Widget>[
          Gap.sm,
          Text(subtitle!, style: textStyles.bodyMedium),
        ],
      ],
    );
  }
}
