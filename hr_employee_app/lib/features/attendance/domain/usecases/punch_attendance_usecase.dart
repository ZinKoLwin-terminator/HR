import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/services/location_service.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_location_policy.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';
import 'package:hr_employee_app/features/attendance/domain/repositories/attendance_repository.dart';

/// Which end of the day is being recorded.
enum PunchDirection { checkIn, checkOut }

/// Records a check-in or check-out.
///
/// The only place the location policy is applied, so the rule cannot drift
/// between the dashboard's quick action and the attendance screen's button.
///
/// What it does **not** do, by design:
///   * it never decides whether a position is inside the office — the server
///     owns geofencing, and a client-side check would be bypassable;
///   * it never reports success unless the server confirmed it;
///   * under [AttendanceLocationPolicy.required] it refuses to submit without
///     a position rather than quietly sending none.
class PunchAttendanceUseCase {
  const PunchAttendanceUseCase({
    required this._repository,
    required this._locationService,
    required this._policy,
  });

  final AttendanceRepository _repository;
  final LocationService _locationService;
  final AttendanceLocationPolicy _policy;

  Future<Result<AttendanceRecord>> call({
    required PunchDirection direction,
    String? remark,
  }) async {
    final Result<GeoLocation?> location = await _resolveLocation();

    // Only a mandatory policy turns a location problem into a failed punch.
    final Failure? locationFailure = location.failureOrNull;
    if (locationFailure != null) {
      return Result<AttendanceRecord>.failure(locationFailure);
    }

    final AttendancePunch punch = AttendancePunch(
      capturedAt: DateTime.now().toUtc(),
      location: location.valueOrNull,
      remark: remark,
    );

    return switch (direction) {
      PunchDirection.checkIn => _repository.checkIn(punch),
      PunchDirection.checkOut => _repository.checkOut(punch),
    };
  }

  /// Obtains a position according to the policy.
  ///
  /// Returns a success carrying null when no position is needed, or when one
  /// could not be obtained and the policy tolerates that.
  Future<Result<GeoLocation?>> _resolveLocation() async {
    if (!_policy.collectsLocation) {
      return const Result<GeoLocation?>.success(null);
    }

    // Ask for permission at the moment of use, which is when the reason for
    // it is obvious to the user.
    LocationAuthorizationState permission = await _locationService
        .checkPermission();
    if (permission == LocationAuthorizationState.denied) {
      permission = await _locationService.requestPermission();
    }

    if (!permission.isGranted) {
      if (_policy.isMandatory) {
        return Result<GeoLocation?>.failure(
          DevicePermissionFailure(
            permission == LocationAuthorizationState.permanentlyDenied
                ? 'Your employer requires a location for attendance. Enable '
                      'location access for this app in Settings.'
                : 'Your employer requires a location for attendance.',
            isPermanentlyDenied:
                permission == LocationAuthorizationState.permanentlyDenied,
          ),
        );
      }
      // Optional policy: proceed without a position.
      return const Result<GeoLocation?>.success(null);
    }

    final Result<GeoLocation> fix = await _locationService.getCurrentLocation();

    return fix.fold(
      onSuccess: Result<GeoLocation?>.success,
      onFailure: (Failure failure) => _policy.isMandatory
          ? Result<GeoLocation?>.failure(failure)
          : const Result<GeoLocation?>.success(null),
    );
  }
}
