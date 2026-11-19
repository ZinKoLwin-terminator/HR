import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/location_service.dart';
import 'package:hr_employee_app/features/attendance/data/datasources/attendance_mock_datasource.dart';
import 'package:hr_employee_app/features/attendance/data/datasources/attendance_remote_datasource.dart';
import 'package:hr_employee_app/features/attendance/data/repositories/attendance_repository_impl.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_location_policy.dart';
import 'package:hr_employee_app/features/attendance/domain/repositories/attendance_repository.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/attendance_usecases.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/punch_attendance_usecase.dart';

/// Dependency injection for the attendance feature.

final Provider<LocationService> locationServiceProvider =
    Provider<LocationService>((Ref ref) => const GeolocatorLocationService());

/// Whether a position is collected, and whether it is mandatory.
///
/// ---------------------------------------------------------------------------
/// PLACEHOLDER. This is tenant configuration and belongs on the server; there
/// is no endpoint for it yet (`docs/api_contracts.md` §9, question 3). It is
/// read from a `--dart-define` so the behaviour can be exercised, and this is
/// the single line to change once tenant settings exist.
/// ---------------------------------------------------------------------------
final Provider<AttendanceLocationPolicy> attendanceLocationPolicyProvider =
    Provider<AttendanceLocationPolicy>((Ref ref) {
      const String configured = String.fromEnvironment(
        'ATTENDANCE_LOCATION_POLICY',
        defaultValue: 'optional',
      );
      return AttendanceLocationPolicy.fromName(configured);
    });

final Provider<AttendanceRemoteDataSource> attendanceRemoteDataSourceProvider =
    Provider<AttendanceRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return const AttendanceMockDataSource();
      }
      return AttendanceRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<AttendanceRepository> attendanceRepositoryProvider =
    Provider<AttendanceRepository>(
      (Ref ref) => AttendanceRepositoryImpl(
        remote: ref.watch(attendanceRemoteDataSourceProvider),
        deviceId: ref.watch(deviceInfoProvider).deviceId,
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<PunchAttendanceUseCase> punchAttendanceUseCaseProvider =
    Provider<PunchAttendanceUseCase>(
      (Ref ref) => PunchAttendanceUseCase(
        repository: ref.watch(attendanceRepositoryProvider),
        locationService: ref.watch(locationServiceProvider),
        policy: ref.watch(attendanceLocationPolicyProvider),
      ),
    );

final Provider<GetTodayAttendanceUseCase> getTodayAttendanceUseCaseProvider =
    Provider<GetTodayAttendanceUseCase>(
      (Ref ref) =>
          GetTodayAttendanceUseCase(ref.watch(attendanceRepositoryProvider)),
    );

final Provider<GetAttendanceHistoryUseCase>
getAttendanceHistoryUseCaseProvider = Provider<GetAttendanceHistoryUseCase>(
  (Ref ref) =>
      GetAttendanceHistoryUseCase(ref.watch(attendanceRepositoryProvider)),
);

final Provider<GetMonthlyAttendanceSummaryUseCase>
getMonthlyAttendanceSummaryUseCaseProvider =
    Provider<GetMonthlyAttendanceSummaryUseCase>(
      (Ref ref) => GetMonthlyAttendanceSummaryUseCase(
        ref.watch(attendanceRepositoryProvider),
      ),
    );

final Provider<GetAttendanceDetailUseCase> getAttendanceDetailUseCaseProvider =
    Provider<GetAttendanceDetailUseCase>(
      (Ref ref) =>
          GetAttendanceDetailUseCase(ref.watch(attendanceRepositoryProvider)),
    );
