import 'package:freezed_annotation/freezed_annotation.dart';

part 'geo_location.freezed.dart';

/// A position captured when attendance is recorded.
///
/// What this type deliberately does **not** have is any notion of a location
/// being "valid", "verified" or "inside the office". The client reports what
/// the device measured, including [isMocked] and [accuracyMeters], and the
/// server decides whether that is acceptable. A client-side geofence check
/// would be trivially bypassed and would turn a fake success into something
/// the user believes.
@freezed
abstract class GeoLocation with _$GeoLocation {
  const factory GeoLocation({
    required double latitude,
    required double longitude,

    /// Radius of confidence reported by the OS, in metres. Sent so the server
    /// can reject a fix that is too imprecise to judge.
    double? accuracyMeters,

    /// Reverse-geocoded label, when one could be resolved. Display only.
    String? address,

    /// True when the platform reports the fix came from a mock provider.
    ///
    /// Forwarded honestly rather than hidden; it is the server's business
    /// what to do about it.
    @Default(false) bool isMocked,
  }) = _GeoLocation;

  const GeoLocation._();

  /// A short `16.8409, 96.1735` label for the detail screen, used when no
  /// address could be resolved.
  String get coordinatesLabel =>
      '${latitude.toStringAsFixed(4)}, ${longitude.toStringAsFixed(4)}';

  /// What to show the user: the address when known, coordinates otherwise.
  String get displayLabel {
    final String? value = address;
    return (value == null || value.trim().isEmpty) ? coordinatesLabel : value;
  }
}
