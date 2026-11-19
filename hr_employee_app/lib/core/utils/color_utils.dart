import 'package:material_ui/material_ui.dart';

/// Converts the API's `#RRGGBB` colour strings into [Color].
///
/// Lives in the presentation half of the app on purpose: the domain carries
/// `colorHex` as a plain string so it stays free of Flutter types, and this
/// is the single place that interprets it.
abstract final class ColorUtils {
  /// Parses `#RRGGBB`, `#AARRGGBB`, or the same without the leading `#`.
  ///
  /// Returns [fallback] for anything it cannot read, so a malformed value
  /// from the server tints one chip wrongly instead of throwing.
  static Color fromHex(String? hex, {required Color fallback}) {
    if (hex == null) {
      return fallback;
    }

    final String cleaned = hex.replaceFirst('#', '').trim();
    if (cleaned.length != 6 && cleaned.length != 8) {
      return fallback;
    }

    // Pad a six-digit value with full opacity.
    final String withAlpha = cleaned.length == 6 ? 'FF$cleaned' : cleaned;
    final int? value = int.tryParse(withAlpha, radix: 16);
    return value == null ? fallback : Color(value);
  }

  /// A very light tint of [color], for an icon backplate or a chip fill.
  static Color surfaceTint(Color color, {required bool isDark}) {
    return color.withValues(alpha: isDark ? 0.22 : 0.10);
  }

  /// Black or white, whichever reads better on [background].
  ///
  /// Uses relative luminance rather than a simple brightness average, so the
  /// result meets contrast expectations for the strongly saturated brand
  /// colours the API may send.
  static Color onColor(Color background) {
    return background.computeLuminance() > 0.5
        ? const Color(0xFF101828)
        : Colors.white;
  }
}
