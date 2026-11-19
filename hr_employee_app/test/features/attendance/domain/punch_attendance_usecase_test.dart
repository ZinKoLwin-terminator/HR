import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/services/location_service.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_location_policy.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/punch_attendance_usecase.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/test_doubles.dart';

/// The location policy is the most consequential rule in the app: it decides
/// whether a punch is submitted without a position, and getting it wrong
/// either blocks legitimate check-ins or silently drops evidence the employer
/// requires. Each branch is covered.
void main() {
  late MockAttendanceRepository repository;
  late MockLocationService locationService;

  setUpAll(registerCommonFallbacks);

  setUp(() {
    repository = MockAttendanceRepository();
    locationService = MockLocationService();

    when(() => repository.checkIn(any())).thenAnswer(
      (_) async => Result<AttendanceRecord>.success(TestData.attendance()),
    );
    when(() => repository.checkOut(any())).thenAnswer(
      (_) async => Result<AttendanceRecord>.success(TestData.attendance()),
    );
  });

  PunchAttendanceUseCase build(AttendanceLocationPolicy policy) {
    return PunchAttendanceUseCase(
      repository: repository,
      locationService: locationService,
      policy: policy,
    );
  }

  void grantPermission() {
    when(locationService.checkPermission)
        .thenAnswer((_) async => LocationAuthorizationState.granted);
  }

  void stubLocation(Result<GeoLocation> result) {
    when(() => locationService.getCurrentLocation())
        .thenAnswer((_) async => result);
  }

  AttendancePunch capturedPunch() {
    return verify(() => repository.checkIn(captureAny())).captured.single
        as AttendancePunch;
  }

  group('policy: disabled', () {
    test('submits with no location and never asks for permission', () async {
      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.disabled,
      )(direction: PunchDirection.checkIn);

      expect(result.isSuccess, isTrue);
      expect(capturedPunch().location, isNull);
      verifyNever(locationService.checkPermission);
      verifyNever(() => locationService.getCurrentLocation());
    });
  });

  group('policy: optional', () {
    test('attaches a position when one is available', () async {
      grantPermission();
      stubLocation(const Result<GeoLocation>.success(TestData.location));

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.optional,
      )(direction: PunchDirection.checkIn);

      expect(result.isSuccess, isTrue);
      expect(capturedPunch().location, TestData.location);
    });

    test('still submits when permission is refused', () async {
      when(locationService.checkPermission)
          .thenAnswer((_) async => LocationAuthorizationState.denied);
      when(locationService.requestPermission)
          .thenAnswer((_) async => LocationAuthorizationState.denied);

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.optional,
      )(direction: PunchDirection.checkIn);

      expect(result.isSuccess, isTrue);
      expect(capturedPunch().location, isNull);
    });

    test('still submits when no fix can be obtained', () async {
      grantPermission();
      stubLocation(
        const Result<GeoLocation>.failure(LocationFailure('No signal')),
      );

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.optional,
      )(direction: PunchDirection.checkIn);

      expect(result.isSuccess, isTrue);
      expect(capturedPunch().location, isNull);
    });
  });

  group('policy: required', () {
    test('submits with the position when one is available', () async {
      grantPermission();
      stubLocation(const Result<GeoLocation>.success(TestData.location));

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.required,
      )(direction: PunchDirection.checkIn);

      expect(result.isSuccess, isTrue);
      expect(capturedPunch().location, TestData.location);
    });

    test('refuses to submit when permission is denied', () async {
      when(locationService.checkPermission)
          .thenAnswer((_) async => LocationAuthorizationState.denied);
      when(locationService.requestPermission)
          .thenAnswer((_) async => LocationAuthorizationState.denied);

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.required,
      )(direction: PunchDirection.checkIn);

      expect(result.isFailure, isTrue);
      expect(result.failureOrNull, isA<DevicePermissionFailure>());
      // The punch must not reach the server at all.
      verifyNever(() => repository.checkIn(any()));
    });

    test(
      'flags a permanently denied permission for the settings prompt',
      () async {
        when(
          locationService.checkPermission,
        ).thenAnswer((_) async => LocationAuthorizationState.permanentlyDenied);

        final Result<AttendanceRecord> result = await build(
          AttendanceLocationPolicy.required,
        )(direction: PunchDirection.checkIn);

        final Failure? failure = result.failureOrNull;
        expect(failure, isA<DevicePermissionFailure>());
        expect(
          (failure! as DevicePermissionFailure).isPermanentlyDenied,
          isTrue,
        );
        // A permanently denied permission is not re-requested; the OS would
        // not show a prompt anyway.
        verifyNever(locationService.requestPermission);
      },
    );

    test('refuses to submit when no fix can be obtained', () async {
      grantPermission();
      stubLocation(
        const Result<GeoLocation>.failure(LocationFailure('No signal')),
      );

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.required,
      )(direction: PunchDirection.checkIn);

      expect(result.isFailure, isTrue);
      expect(result.failureOrNull, isA<LocationFailure>());
      verifyNever(() => repository.checkIn(any()));
    });
  });

  group('direction', () {
    test('check-out calls checkOut, not checkIn', () async {
      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.disabled,
      )(direction: PunchDirection.checkOut);

      expect(result.isSuccess, isTrue);
      verify(() => repository.checkOut(any())).called(1);
      verifyNever(() => repository.checkIn(any()));
    });
  });

  group('honesty', () {
    test('a mocked position is forwarded, not suppressed', () async {
      // The client reports what the OS measured; whether a mock provider is
      // acceptable is the server's decision.
      grantPermission();
      stubLocation(
        const Result<GeoLocation>.success(
          GeoLocation(latitude: 1, longitude: 2, isMocked: true),
        ),
      );

      await build(AttendanceLocationPolicy.required)(
        direction: PunchDirection.checkIn,
      );

      expect(capturedPunch().location?.isMocked, isTrue);
    });

    test('a server rejection is surfaced, never reported as success', () async {
      when(() => repository.checkIn(any())).thenAnswer(
        (_) async => const Result<AttendanceRecord>.failure(
          ConflictFailure('You are outside the allowed area.'),
        ),
      );

      final Result<AttendanceRecord> result = await build(
        AttendanceLocationPolicy.disabled,
      )(direction: PunchDirection.checkIn);

      expect(result.isFailure, isTrue);
      expect(
        result.failureOrNull!.message,
        'You are outside the allowed area.',
      );
    });
  });
}
