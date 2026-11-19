import 'package:flutter_screenutil/flutter_screenutil.dart';
import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:material_ui/material_ui.dart';

/// The type scale.
///
/// Sizes use `.sp` so they respond to both the design scale and the user's
/// system font-size setting, which matters for accessibility. Prefer reading
/// styles from `Theme.of(context).textTheme`; this class is the single place
/// that defines them.
abstract final class AppTextStyles {
  /// Set to null to use the platform default (Roboto on Android, SF on iOS).
  /// Point this at a bundled brand face when one is supplied.
  static const String? fontFamily = null;

  static TextStyle get displayLarge => TextStyle(
    fontFamily: fontFamily,
    fontSize: 32.sp,
    fontWeight: FontWeight.w700,
    height: 1.25,
    letterSpacing: -0.5,
  );

  static TextStyle get headlineLarge => TextStyle(
    fontFamily: fontFamily,
    fontSize: 24.sp,
    fontWeight: FontWeight.w700,
    height: 1.3,
    letterSpacing: -0.3,
  );

  static TextStyle get headlineMedium => TextStyle(
    fontFamily: fontFamily,
    fontSize: 20.sp,
    fontWeight: FontWeight.w600,
    height: 1.3,
  );

  static TextStyle get titleLarge => TextStyle(
    fontFamily: fontFamily,
    fontSize: 18.sp,
    fontWeight: FontWeight.w600,
    height: 1.35,
  );

  static TextStyle get titleMedium => TextStyle(
    fontFamily: fontFamily,
    fontSize: 16.sp,
    fontWeight: FontWeight.w600,
    height: 1.4,
  );

  static TextStyle get titleSmall => TextStyle(
    fontFamily: fontFamily,
    fontSize: 14.sp,
    fontWeight: FontWeight.w600,
    height: 1.4,
  );

  static TextStyle get bodyLarge => TextStyle(
    fontFamily: fontFamily,
    fontSize: 16.sp,
    fontWeight: FontWeight.w400,
    height: 1.5,
  );

  static TextStyle get bodyMedium => TextStyle(
    fontFamily: fontFamily,
    fontSize: 14.sp,
    fontWeight: FontWeight.w400,
    height: 1.5,
  );

  static TextStyle get bodySmall => TextStyle(
    fontFamily: fontFamily,
    fontSize: 12.sp,
    fontWeight: FontWeight.w400,
    height: 1.45,
  );

  static TextStyle get labelLarge => TextStyle(
    fontFamily: fontFamily,
    fontSize: 15.sp,
    fontWeight: FontWeight.w600,
    height: 1.2,
    letterSpacing: 0.1,
  );

  static TextStyle get labelMedium => TextStyle(
    fontFamily: fontFamily,
    fontSize: 13.sp,
    fontWeight: FontWeight.w500,
    height: 1.2,
  );

  static TextStyle get labelSmall => TextStyle(
    fontFamily: fontFamily,
    fontSize: 11.sp,
    fontWeight: FontWeight.w600,
    height: 1.2,
    letterSpacing: 0.4,
  );

  /// Tabular figures for money and durations, so digits align in columns and
  /// an amount does not jitter as it changes.
  static TextStyle get numeric => TextStyle(
    fontFamily: fontFamily,
    fontSize: 16.sp,
    fontWeight: FontWeight.w600,
    height: 1.3,
    fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
  );

  static TextStyle get numericLarge => TextStyle(
    fontFamily: fontFamily,
    fontSize: 28.sp,
    fontWeight: FontWeight.w700,
    height: 1.2,
    letterSpacing: -0.5,
    fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
  );

  /// Assembles the [TextTheme] for a given brightness.
  static TextTheme textTheme({required bool isDark}) {
    final Color primary = isDark
        ? AppColors.darkTextPrimary
        : AppColors.textPrimary;
    final Color secondary = isDark
        ? AppColors.darkTextSecondary
        : AppColors.textSecondary;

    return TextTheme(
      displayLarge: displayLarge.copyWith(color: primary),
      headlineLarge: headlineLarge.copyWith(color: primary),
      headlineMedium: headlineMedium.copyWith(color: primary),
      titleLarge: titleLarge.copyWith(color: primary),
      titleMedium: titleMedium.copyWith(color: primary),
      titleSmall: titleSmall.copyWith(color: primary),
      bodyLarge: bodyLarge.copyWith(color: primary),
      bodyMedium: bodyMedium.copyWith(color: secondary),
      bodySmall: bodySmall.copyWith(color: secondary),
      labelLarge: labelLarge.copyWith(color: primary),
      labelMedium: labelMedium.copyWith(color: secondary),
      labelSmall: labelSmall.copyWith(color: secondary),
    );
  }
}
