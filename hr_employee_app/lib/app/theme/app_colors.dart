import 'package:material_ui/material_ui.dart';

/// The application palette.
///
/// Colours are defined once here and consumed through [ThemeData] or the
/// [AppPalette] theme extension. Widgets must not hold literal colour values,
/// so that a brand change is a single-file edit.
abstract final class AppColors {
  // --- Apex HRMS brand sheet -----------------------------------------
  // The theme colours from the brand sheet. Everything below derives from
  // these, so the UI and the logo are one palette rather than two blues
  // that nearly match.
  static const Color apexNavy = Color(0xFF0B3B66);
  static const Color summitTeal = Color(0xFF14B8A6);
  static const Color summitTealBright = Color(0xFF2DD4BF);
  static const Color steelBlue = Color(0xFF4F86B8);
  static const Color mistBlue = Color(0xFFA5C4DD);
  static const Color ink = Color(0xFF0F172A);
  static const Color cloud = Color(0xFFF8FAFC);

  // --- Brand roles ----------------------------------------------------
  /// Primary actions and active states: Apex Navy.
  ///
  /// The two stops of the brand gradient: [primary] is its top and
  /// [primaryDark] its bottom, so a filled button and the header it sits
  /// under are the same colour.
  static const Color primary = apexNavy;
  static const Color primaryDark = Color(0xFF072845);
  static const Color primaryLight = steelBlue;

  /// Mist Blue at a whisper, for selected rows and icon backplates.
  static const Color primarySurface = Color(0xFFEAF1F7);

  /// Summit Teal, used sparingly for highlights.
  static const Color accent = summitTeal;

  /// The pale end of the accent, for text and strokes that have to stay
  /// legible on the dark auth ground where [accent] itself goes muddy.
  static const Color accentLight = Color(0xFF5EEAD4);

  /// The brand gradient, painted top to bottom behind a screen header.
  ///
  /// Two stops, both from the brand family and both dark enough to carry
  /// white text at either end: the band runs under the status bar, so a
  /// stop that went pale would leave the clock unreadable.
  static const List<Color> brandGradient = <Color>[primary, primaryDark];

  /// The ground the signed-out screens are painted on.
  ///
  /// Its own gradient rather than [brandGradient], and the same one in both
  /// app themes: the auth screens are a dark, glass-panelled world, and a
  /// translucent form panel needs a deeper ground to read against than a
  /// two-stop header band gives it. The brand colour is kept as the middle
  /// stop, so it is the same identity at a lower key rather than a second
  /// palette.
  static const List<Color> authGradient = <Color>[
    Color(0xFF06223D),
    primary,
    Color(0xFF031426),
  ];

  // --- Neutrals -------------------------------------------------------
  static const Color scaffoldBackground = cloud;
  static const Color surface = Color(0xFFFFFFFF);
  static const Color surfaceMuted = Color(0xFFF2F4F7);
  static const Color border = Color(0xFFE4E7EC);
  static const Color divider = Color(0xFFEDEFF2);

  static const Color textPrimary = ink;
  static const Color textSecondary = Color(0xFF475467);
  static const Color textTertiary = Color(0xFF98A2B3);
  static const Color textOnPrimary = Color(0xFFFFFFFF);

  // --- Semantic -------------------------------------------------------
  static const Color success = Color(0xFF12B76A);
  static const Color successSurface = Color(0xFFE7F8F0);

  static const Color warning = Color(0xFFF79009);
  static const Color warningSurface = Color(0xFFFEF4E6);

  static const Color error = Color(0xFFD92D20);
  static const Color errorSurface = Color(0xFFFEECEB);

  static const Color info = Color(0xFF2E90FA);
  static const Color infoSurface = Color(0xFFEAF4FF);

  // --- Dark theme -----------------------------------------------------
  static const Color darkScaffoldBackground = Color(0xFF0B1220);
  static const Color darkSurface = Color(0xFF141C2C);
  static const Color darkSurfaceMuted = Color(0xFF1C2638);
  static const Color darkBorder = Color(0xFF2A3447);
  static const Color darkTextPrimary = Color(0xFFF2F4F7);
  static const Color darkTextSecondary = Color(0xFFB2BCCC);
  static const Color darkTextTertiary = Color(0xFF7A869A);

  /// Dark counterpart of [brandGradient].
  ///
  /// Same shape, far dimmer: on a dark theme the cards that overlap the
  /// header have to stay the brightest things on the screen, which a
  /// full-strength brand gradient would not allow.
  static const List<Color> darkBrandGradient = <Color>[
    Color(0xFF06223D),
    Color(0xFF031426),
  ];

  /// Shadow used by raised cards. Kept very soft: the design brief calls for
  /// subtle elevation rather than heavy drop shadows.
  static const Color shadow = Color(0x140F172A);

  /// Shadow for a surface that floats on the brand gradient rather than on
  /// the neutral scaffold, where [shadow] is too faint to separate the two.
  static const Color shadowRaised = Color(0x330F172A);

  /// Shadow for a card that overlaps a coloured header.
  ///
  /// Between the other two: [shadow] leaves the card looking pasted onto
  /// the band, while [shadowRaised] - tuned for a panel on a full-screen
  /// gradient - reads as a hard shelf under a card that is mostly sitting
  /// on the neutral page.
  static const Color shadowLift = Color(0x1C0F172A);
}

/// Semantic colours that have no slot in [ColorScheme].
///
/// Exposed as a [ThemeExtension] so light and dark variants resolve
/// automatically and widgets never branch on brightness themselves.
@immutable
class AppPalette extends ThemeExtension<AppPalette> {
  const AppPalette({
    required this.success,
    required this.successSurface,
    required this.warning,
    required this.warningSurface,
    required this.info,
    required this.infoSurface,
    required this.danger,
    required this.dangerSurface,
    required this.textPrimary,
    required this.textSecondary,
    required this.textTertiary,
    required this.border,
    required this.surfaceMuted,
    required this.brandGradient,
  });

  static const AppPalette light = AppPalette(
    success: AppColors.success,
    successSurface: AppColors.successSurface,
    warning: AppColors.warning,
    warningSurface: AppColors.warningSurface,
    info: AppColors.info,
    infoSurface: AppColors.infoSurface,
    danger: AppColors.error,
    dangerSurface: AppColors.errorSurface,
    textPrimary: AppColors.textPrimary,
    textSecondary: AppColors.textSecondary,
    textTertiary: AppColors.textTertiary,
    border: AppColors.border,
    surfaceMuted: AppColors.surfaceMuted,
    brandGradient: AppColors.brandGradient,
  );

  static const AppPalette dark = AppPalette(
    success: Color(0xFF32D583),
    successSurface: Color(0xFF10291F),
    warning: Color(0xFFFDB022),
    warningSurface: Color(0xFF2B2113),
    info: Color(0xFF53B1FD),
    infoSurface: Color(0xFF132436),
    danger: Color(0xFFF97066),
    dangerSurface: Color(0xFF2E1715),
    textPrimary: AppColors.darkTextPrimary,
    textSecondary: AppColors.darkTextSecondary,
    textTertiary: AppColors.darkTextTertiary,
    border: AppColors.darkBorder,
    surfaceMuted: AppColors.darkSurfaceMuted,
    brandGradient: AppColors.darkBrandGradient,
  );

  final Color success;
  final Color successSurface;
  final Color warning;
  final Color warningSurface;
  final Color info;
  final Color infoSurface;
  final Color danger;
  final Color dangerSurface;
  final Color textPrimary;
  final Color textSecondary;
  final Color textTertiary;
  final Color border;
  final Color surfaceMuted;

  /// Stops of the brand gradient, from the top of the band to its bottom.
  final List<Color> brandGradient;

  @override
  AppPalette copyWith({
    Color? success,
    Color? successSurface,
    Color? warning,
    Color? warningSurface,
    Color? info,
    Color? infoSurface,
    Color? danger,
    Color? dangerSurface,
    Color? textPrimary,
    Color? textSecondary,
    Color? textTertiary,
    Color? border,
    Color? surfaceMuted,
    List<Color>? brandGradient,
  }) {
    return AppPalette(
      success: success ?? this.success,
      successSurface: successSurface ?? this.successSurface,
      warning: warning ?? this.warning,
      warningSurface: warningSurface ?? this.warningSurface,
      info: info ?? this.info,
      infoSurface: infoSurface ?? this.infoSurface,
      danger: danger ?? this.danger,
      dangerSurface: dangerSurface ?? this.dangerSurface,
      textPrimary: textPrimary ?? this.textPrimary,
      textSecondary: textSecondary ?? this.textSecondary,
      textTertiary: textTertiary ?? this.textTertiary,
      border: border ?? this.border,
      surfaceMuted: surfaceMuted ?? this.surfaceMuted,
      brandGradient: brandGradient ?? this.brandGradient,
    );
  }

  @override
  AppPalette lerp(ThemeExtension<AppPalette>? other, double t) {
    if (other is! AppPalette) {
      return this;
    }
    return AppPalette(
      success: Color.lerp(success, other.success, t)!,
      successSurface: Color.lerp(successSurface, other.successSurface, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
      warningSurface: Color.lerp(warningSurface, other.warningSurface, t)!,
      info: Color.lerp(info, other.info, t)!,
      infoSurface: Color.lerp(infoSurface, other.infoSurface, t)!,
      danger: Color.lerp(danger, other.danger, t)!,
      dangerSurface: Color.lerp(dangerSurface, other.dangerSurface, t)!,
      textPrimary: Color.lerp(textPrimary, other.textPrimary, t)!,
      textSecondary: Color.lerp(textSecondary, other.textSecondary, t)!,
      textTertiary: Color.lerp(textTertiary, other.textTertiary, t)!,
      border: Color.lerp(border, other.border, t)!,
      surfaceMuted: Color.lerp(surfaceMuted, other.surfaceMuted, t)!,
      brandGradient: _lerpGradient(brandGradient, other.brandGradient, t),
    );
  }

  /// Interpolates two gradients stop by stop.
  ///
  /// Falls back to a straight swap at the halfway point when the two have a
  /// different number of stops, which is the only sane reading of "halfway
  /// between a two-stop and a three-stop gradient".
  static List<Color> _lerpGradient(List<Color> from, List<Color> to, double t) {
    if (from.length != to.length) {
      return t < 0.5 ? from : to;
    }
    return List<Color>.generate(
      from.length,
      (int i) => Color.lerp(from[i], to[i], t)!,
      growable: false,
    );
  }
}
