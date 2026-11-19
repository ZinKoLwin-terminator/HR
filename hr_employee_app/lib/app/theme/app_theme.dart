import 'package:flutter/foundation.dart' show defaultTargetPlatform;
import 'package:flutter/services.dart';
import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:material_ui/material_ui.dart';

/// Re-exported so a widget that imports this file to reach `context.palette`
/// also gets [AppPalette] and [AppColors] in scope.
///
/// This file is the theme barrel: one import gives a widget everything it
/// needs to style itself, instead of three that must be kept in step.
export 'package:hr_employee_app/app/theme/app_colors.dart';

/// Builds the light and dark [ThemeData].
///
/// Component styling lives here rather than on individual widgets, so a
/// button, card or input looks the same everywhere without each screen
/// repeating the decoration.
abstract final class AppTheme {
  static ThemeData get light => _build(isDark: false);

  static ThemeData get dark => _build(isDark: true);

  static ThemeData _build({required bool isDark}) {
    final ColorScheme colorScheme = isDark
        ? const ColorScheme.dark(
            primary: AppColors.primaryLight,
            onPrimary: AppColors.textOnPrimary,
            secondary: AppColors.accent,
            surface: AppColors.darkSurface,
            onSurface: AppColors.darkTextPrimary,
            error: AppColors.error,
          )
        : const ColorScheme.light(
            primary: AppColors.primary,
            onPrimary: AppColors.textOnPrimary,
            secondary: AppColors.accent,
            surface: AppColors.surface,
            onSurface: AppColors.textPrimary,
            error: AppColors.error,
          );

    final AppPalette palette = isDark ? AppPalette.dark : AppPalette.light;

    // The type scale is merged onto Material's Typography *here*, rather
    // than being left to `ThemeData` to do internally.
    //
    // `ThemeData` merges whatever text theme it is handed onto Typography,
    // and that merge is what supplies the platform font family — Roboto on
    // Android, the system face on iOS. But a component theme (an AppBar
    // title, a button label, a dialog) takes a `TextStyle` verbatim, with no
    // such merge. Styling those slots straight from `AppTextStyles`, whose
    // `fontFamily` is deliberately null, would leave exactly those strings
    // with no font family while every other string in the app had one.
    //
    // Doing the merge up front means every slot below is styled from a
    // text theme that already carries the family.
    final Typography typography = Typography.material2021(
      platform: defaultTargetPlatform,
      colorScheme: colorScheme,
    );
    final TextTheme textTheme = (isDark ? typography.white : typography.black)
        .merge(AppTextStyles.textTheme(isDark: isDark));

    final Color scaffoldBackground = isDark
        ? AppColors.darkScaffoldBackground
        : AppColors.scaffoldBackground;

    return ThemeData(
      useMaterial3: true,
      brightness: isDark ? Brightness.dark : Brightness.light,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: scaffoldBackground,
      textTheme: textTheme,
      fontFamily: AppTextStyles.fontFamily,
      extensions: <ThemeExtension<dynamic>>[palette],
      splashFactory: InkSparkle.splashFactory,

      appBarTheme: AppBarTheme(
        backgroundColor: scaffoldBackground,
        surfaceTintColor: Colors.transparent,
        foregroundColor: palette.textPrimary,
        elevation: 0,
        scrolledUnderElevation: 0.5,
        centerTitle: false,
        titleTextStyle: textTheme.titleLarge,
        systemOverlayStyle: isDark
            ? SystemUiOverlayStyle.light
            : SystemUiOverlayStyle.dark,
      ),

      cardTheme: CardThemeData(
        color: colorScheme.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: AppRadius.card,
          side: BorderSide(color: palette.border),
        ),
      ),

      dividerTheme: DividerThemeData(
        color: palette.border,
        thickness: 1,
        space: 1,
      ),

      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: Size(double.infinity, AppSizes.buttonHeight),
          backgroundColor: colorScheme.primary,
          foregroundColor: colorScheme.onPrimary,
          disabledBackgroundColor: colorScheme.primary.withValues(alpha: 0.4),
          disabledForegroundColor: colorScheme.onPrimary.withValues(alpha: 0.8),
          textStyle: textTheme.labelLarge,
          shape: RoundedRectangleBorder(borderRadius: AppRadius.button),
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: Size(double.infinity, AppSizes.buttonHeight),
          foregroundColor: colorScheme.primary,
          side: BorderSide(color: palette.border),
          textStyle: textTheme.labelLarge,
          shape: RoundedRectangleBorder(borderRadius: AppRadius.button),
        ),
      ),

      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: colorScheme.primary,
          textStyle: textTheme.labelMedium,
          minimumSize: const Size(0, AppSizes.minTouchTarget),
          padding: EdgeInsets.symmetric(horizontal: AppSpacing.sm),
        ),
      ),

      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: isDark ? AppColors.darkSurfaceMuted : AppColors.surface,
        contentPadding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.md,
        ),
        hintStyle: textTheme.bodyMedium?.copyWith(color: palette.textTertiary),
        labelStyle: textTheme.labelMedium,
        errorStyle: textTheme.bodySmall?.copyWith(color: palette.danger),
        border: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: palette.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: palette.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: colorScheme.primary, width: 1.5),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: palette.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: palette.danger, width: 1.5),
        ),
        disabledBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: palette.border.withValues(alpha: 0.5)),
        ),
      ),

      // No `bottomNavigationBarTheme`: the shell uses the Material 3
      // `NavigationBar`, configured below. Styling the Material 2 widget as
      // well would be dead configuration that drifts out of step.
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: colorScheme.surface,
        surfaceTintColor: Colors.transparent,
        indicatorColor: isDark
            ? AppColors.primary.withValues(alpha: 0.25)
            : AppColors.primarySurface,
        elevation: 0,
        height: AppSizes.bottomNavHeight,
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        labelTextStyle: WidgetStateProperty.resolveWith<TextStyle>((
          Set<WidgetState> states,
        ) {
          final bool selected = states.contains(WidgetState.selected);
          return AppTextStyles.labelSmall.copyWith(
            color: selected ? colorScheme.primary : palette.textTertiary,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
          );
        }),
      ),

      chipTheme: ChipThemeData(
        backgroundColor: palette.surfaceMuted,
        labelStyle: AppTextStyles.labelSmall,
        side: BorderSide.none,
        shape: RoundedRectangleBorder(borderRadius: AppRadius.chip),
        padding: EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.xs,
        ),
      ),

      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: colorScheme.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: AppRadius.sheet),
        showDragHandle: true,
      ),

      dialogTheme: DialogThemeData(
        backgroundColor: colorScheme.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: AppRadius.card),
        titleTextStyle: textTheme.titleLarge,
        contentTextStyle: textTheme.bodyMedium,
      ),

      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: isDark
            ? AppColors.darkSurfaceMuted
            : AppColors.ink,
        contentTextStyle: textTheme.bodyMedium?.copyWith(color: Colors.white),
        shape: RoundedRectangleBorder(borderRadius: AppRadius.field),
        insetPadding: EdgeInsets.all(AppSpacing.lg),
      ),

      listTileTheme: ListTileThemeData(
        contentPadding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
        titleTextStyle: textTheme.titleSmall,
        subtitleTextStyle: textTheme.bodySmall,
        iconColor: palette.textSecondary,
        minVerticalPadding: AppSpacing.md,
      ),

      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: colorScheme.primary,
        linearTrackColor: palette.surfaceMuted,
        circularTrackColor: palette.surfaceMuted,
      ),

      tabBarTheme: TabBarThemeData(
        labelColor: colorScheme.primary,
        unselectedLabelColor: palette.textSecondary,
        labelStyle: AppTextStyles.titleSmall,
        unselectedLabelStyle: AppTextStyles.titleSmall.copyWith(
          fontWeight: FontWeight.w500,
        ),
        indicatorSize: TabBarIndicatorSize.tab,
        dividerColor: palette.border,
      ),

      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith<Color>(
          (Set<WidgetState> states) => states.contains(WidgetState.selected)
              ? colorScheme.onPrimary
              : Colors.white,
        ),
        trackColor: WidgetStateProperty.resolveWith<Color>(
          (Set<WidgetState> states) => states.contains(WidgetState.selected)
              ? colorScheme.primary
              : palette.border,
        ),
      ),
    );
  }
}

/// Reads the semantic palette without repeating the lookup everywhere.
extension AppPaletteX on BuildContext {
  AppPalette get palette =>
      Theme.of(this).extension<AppPalette>() ?? AppPalette.light;
}
