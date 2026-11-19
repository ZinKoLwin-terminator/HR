import 'package:flutter/widgets.dart';
import 'package:flutter_screenutil/flutter_screenutil.dart';

/// The 4-point spacing scale used throughout the app.
///
/// Values are scaled with `flutter_screenutil` so layouts hold their
/// proportions across phone sizes. Use these instead of literal numbers; a
/// value that is not on the scale is almost always a mistake.
abstract final class AppSpacing {
  static double get xxs => 2.w;
  static double get xs => 4.w;
  static double get sm => 8.w;
  static double get md => 12.w;
  static double get lg => 16.w;
  static double get xl => 20.w;
  static double get xxl => 24.w;
  static double get xxxl => 32.w;
  static double get huge => 48.w;

  /// Horizontal padding applied to the content of every screen.
  static EdgeInsets get screenHorizontal =>
      EdgeInsets.symmetric(horizontal: lg);

  /// Padding for the body of a screen, including a comfortable top gap.
  static EdgeInsets get screenPadding => EdgeInsets.fromLTRB(lg, lg, lg, xxxl);

  /// Internal padding of a card.
  static EdgeInsets get cardPadding => EdgeInsets.all(lg);

  /// Gap between stacked cards in a list.
  static double get cardGap => md;
}

/// Corner radii. Rounded cards are part of the design brief.
abstract final class AppRadius {
  static double get sm => 8.r;
  static double get md => 12.r;
  static double get lg => 16.r;
  static double get xl => 20.r;
  static double get pill => 999.r;

  static BorderRadius get card => BorderRadius.circular(lg);
  static BorderRadius get field => BorderRadius.circular(md);
  static BorderRadius get button => BorderRadius.circular(md);
  static BorderRadius get chip => BorderRadius.circular(pill);
  static BorderRadius get sheet =>
      BorderRadius.vertical(top: Radius.circular(xl));
}

/// Fixed sizes that recur across screens.
abstract final class AppSizes {
  /// Minimum tappable square. Below this, targets fail accessibility review.
  static const double minTouchTarget = 48;

  static double get buttonHeight => 52.h;
  static double get fieldHeight => 52.h;
  static double get bottomNavHeight => 64.h;
  static double get appBarHeight => 56.h;
  static double get avatarSm => 36.r;
  static double get avatarMd => 48.r;
  static double get avatarLg => 88.r;
  static double get iconSm => 16.r;
  static double get iconMd => 20.r;
  static double get iconLg => 24.r;

  /// Square tinted plate behind an icon in a section header or a tile.
  static double get iconBackplate => 36.r;

  /// Design canvas the screenutil scale factors are derived from.
  static const Size designSize = Size(390, 844);

  /// Widest a single-column form is allowed to get.
  ///
  /// Scaled like every other size here, which is what keeps it meaningful: a
  /// fixed 460 would stop capping anything the moment the rest of the layout
  /// scaled past it, and the form's padding would overrun its own width.
  static double get maxFormWidth => 460.w;
}

/// Shorthand gap widgets, so a column does not need a `SizedBox` literal.
abstract final class Gap {
  static Widget get xxs => SizedBox(height: AppSpacing.xxs);

  static Widget get xs => SizedBox(height: AppSpacing.xs);
  static Widget get sm => SizedBox(height: AppSpacing.sm);
  static Widget get md => SizedBox(height: AppSpacing.md);
  static Widget get lg => SizedBox(height: AppSpacing.lg);
  static Widget get xl => SizedBox(height: AppSpacing.xl);
  static Widget get xxl => SizedBox(height: AppSpacing.xxl);
  static Widget get xxxl => SizedBox(height: AppSpacing.xxxl);

  static Widget get wXs => SizedBox(width: AppSpacing.xs);
  static Widget get wSm => SizedBox(width: AppSpacing.sm);
  static Widget get wMd => SizedBox(width: AppSpacing.md);
  static Widget get wLg => SizedBox(width: AppSpacing.lg);
  static Widget get wXl => SizedBox(width: AppSpacing.xl);
}
