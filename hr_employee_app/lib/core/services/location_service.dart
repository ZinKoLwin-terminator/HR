import 'package:geolocator/geolocator.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';

/// Acquires the device position.
///
/// Declared as an interface so attendance logic can be unit-tested without a
/// platform channel, and so a different provider can be substituted later.
///
/// What it reports is what the OS measured — including [GeoLocation.isMocked]
/// and the accuracy radius. It never judges whether a position is acceptable:
/// that is the server's decision, and a client-side geofence would be both
/// bypassable and misleading.
abstract interface class LocationService {
  /// Whether the user has granted a location permission.
  Future<LocationAuthorizationState> checkPermission();

  /// Prompts for permission. Returns the resulting state.
  Future<LocationAuthorizationState> requestPermission();

  /// Whether the device's location services are switched on at all.
  Future<bool> isServiceEnabled();

  /// Reads the current position.
  ///
  /// Returns a [DevicePermissionFailure] when permission is missing and a
  /// [LocationFailure] when services are off or no fix could be obtained in
  /// time. It does not prompt — call [requestPermission] first, so the
  /// prompt happens at a moment the user understands.
  Future<Result<GeoLocation>> getCurrentLocation({Duration? timeout});

  /// Opens the OS settings page, for a permanently denied permission.
  Future<void> openSettings();
}

/// Permission state, reduced to the three cases the app acts on.
enum LocationAuthorizationState {
  granted,

  /// Refused, but the OS will prompt again.
  denied,

  /// Refused for good; only a trip to system settings can change it.
  permanentlyDenied;

  bool get isGranted => this == LocationAuthorizationState.granted;
}

/// [LocationService] backed by `geolocator`.
class GeolocatorLocationService implements LocationService {
  const GeolocatorLocationService();

  /// How long to wait for a fix before giving up.
  ///
  /// Bounded because an indoor check-in can otherwise hang indefinitely while
  /// the user stares at a spinner.
  static const Duration _defaultTimeout = Duration(seconds: 12);

  @override
  Future<LocationAuthorizationState> checkPermission() async {
    return _map(await Geolocator.checkPermission());
  }

  @override
  Future<LocationAuthorizationState> requestPermission() async {
    return _map(await Geolocator.requestPermission());
  }

  @override
  Future<bool> isServiceEnabled() => Geolocator.isLocationServiceEnabled();

  @override
  Future<Result<GeoLocation>> getCurrentLocation({Duration? timeout}) async {
    final LocationAuthorizationState permission = await checkPermission();
    if (!permission.isGranted) {
      return Result<GeoLocation>.failure(
        DevicePermissionFailure(
          permission == LocationAuthorizationState.permanentlyDenied
              ? 'Location access is turned off for this app. Enable it in '
                    'Settings to record attendance.'
              : 'Location access is needed to record attendance.',
          isPermanentlyDenied:
              permission == LocationAuthorizationState.permanentlyDenied,
        ),
      );
    }

    if (!await isServiceEnabled()) {
      return const Result<GeoLocation>.failure(
        LocationFailure(
          'Location services are switched off. Turn them on and try again.',
        ),
      );
    }

    try {
      final Position position = await Geolocator.getCurrentPosition(
        locationSettings: LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: timeout ?? _defaultTimeout,
        ),
      );

      return Result<GeoLocation>.success(
        GeoLocation(
          latitude: position.latitude,
          longitude: position.longitude,
          accuracyMeters: position.accuracy,
          // Reported as the OS gives it. Reverse geocoding is not performed:
          // it would need another dependency and a network round trip, and
          // the server can resolve an address from the coordinates if it
          // wants one. Add the `geocoding` package here if a device-side
          // address is ever required.
          isMocked: position.isMocked,
        ),
      );
    } on Object catch (_) {
      // Covers the timeout and the various platform-specific position
      // errors, none of which the user can act on differently.
      return const Result<GeoLocation>.failure(
        LocationFailure(
          'Could not get your location. Move somewhere with a clearer signal '
          'and try again.',
        ),
      );
    }
  }

  @override
  Future<void> openSettings() async {
    await Geolocator.openAppSettings();
  }

  static LocationAuthorizationState _map(LocationPermission permission) {
    return switch (permission) {
      LocationPermission.always ||
      LocationPermission.whileInUse => LocationAuthorizationState.granted,
      LocationPermission.deniedForever =>
        LocationAuthorizationState.permanentlyDenied,
      LocationPermission.denied ||
      LocationPermission.unableToDetermine => LocationAuthorizationState.denied,
    };
  }
}
