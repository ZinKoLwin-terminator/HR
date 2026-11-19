import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/services/location_service.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';
import 'package:hr_employee_app/features/attendance/domain/repositories/attendance_repository.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/repositories/auth_repository.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/domain/repositories/leave_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:mocktail/mocktail.dart';

/// Shared test doubles and fixtures.
///
/// `mocktail` is used rather than `mockito`: it needs no code generation, so
/// the test suite runs without a `build_runner` pass and a new mock is one
/// line rather than an annotation plus a rebuild.

class MockAuthRepository extends Mock implements AuthRepository {}

class MockAttendanceRepository extends Mock implements AttendanceRepository {}

class MockLeaveRepository extends Mock implements LeaveRepository {}

class MockLocationService extends Mock implements LocationService {}

/// A logger that discards everything, so tests do not print.
AppLogger silentLogger() => AppLogger(verbose: false);

/// Configuration for tests.
///
/// Mock data is off: a test overrides the repository directly, so routing
/// through the fixture data sources would only obscure what is under test.
const AppConfig testConfig = AppConfig(
  environment: AppEnvironment.development,
  apiBaseUrl: 'https://test.example.com/api/v1',
  useMockData: false,
  enableVerboseLogging: false,
  connectTimeout: Duration(seconds: 5),
  receiveTimeout: Duration(seconds: 5),
  sendTimeout: Duration(seconds: 5),
  defaultPageSize: 20,
);

/// Registers fallbacks for the types passed to mocked methods.
///
/// `mocktail` needs one per type used with `any()`. Called once from each
/// test file's `setUpAll`.
void registerCommonFallbacks() {
  registerFallbackValue(
    const LoginCredentials(
      userName: 'fallback',
      password: 'fallback',
      deviceId: 'fallback',
      deviceName: 'fallback',
    ),
  );
  registerFallbackValue(AttendancePunch(capturedAt: DateTime(2026)));
  registerFallbackValue(
    LeaveApplication(
      leaveTypeId: 'fallback',
      startDate: DateTime(2026),
      endDate: DateTime(2026),
      reason: 'fallback',
    ),
  );
  registerFallbackValue(const PageRequest());
  registerFallbackValue(
    const PasswordChangeRequest(
      currentPassword: 'fallback',
      newPassword: 'fallback',
    ),
  );
  registerFallbackValue(
    const PasswordResetRequest(
      email: 'fallback',
      token: 'fallback',
      newPassword: 'fallback',
    ),
  );
}

/// Fixtures used across tests.
abstract final class TestData {
  static const AuthenticatedUser user = AuthenticatedUser(
    userId: 'u-1',
    employeeId: 'EMP-00421',
    fullName: 'Aye Chan Moe',
    email: 'aye.chan@example.com',
    department: 'Finance',
    designation: 'Senior Accountant',
  );

  static const GeoLocation location = GeoLocation(
    latitude: 16.8409,
    longitude: 96.1735,
    accuracyMeters: 12.4,
  );

  static AttendanceRecord attendance({
    String id = 'att-1',
    DateTime? date,
    AttendanceStatus status = AttendanceStatus.present,
    DateTime? checkInAt,
    DateTime? checkOutAt,
  }) {
    return AttendanceRecord(
      id: id,
      attendanceDate: date ?? DateTime(2026, 3, 14),
      status: status,
      checkInAt: checkInAt,
      checkOutAt: checkOutAt,
    );
  }

  static const LeaveType annualLeave = LeaveType(
    id: 'lt-1',
    name: 'Annual Leave',
    code: 'AL',
    maxConsecutiveDays: 14,
  );

  static const LeaveType medicalLeave = LeaveType(
    id: 'lt-2',
    name: 'Medical Leave',
    code: 'ML',
    requiresAttachment: true,
  );

  static const LeaveBalance annualBalance = LeaveBalance(
    leaveTypeId: 'lt-1',
    leaveTypeName: 'Annual Leave',
    year: 2026,
    entitledDays: 14,
    usedDays: 5,
    remainingDays: 9,
  );

  static LeaveRequest leaveRequest({
    String id = 'lr-1',
    LeaveStatus status = LeaveStatus.pending,
  }) {
    return LeaveRequest(
      id: id,
      leaveTypeId: 'lt-1',
      leaveTypeName: 'Annual Leave',
      startDate: DateTime(2026, 4, 2),
      endDate: DateTime(2026, 4, 3),
      status: status,
      appliedAt: DateTime(2026, 3, 14),
      totalDays: 2,
      reason: 'Family matters out of town.',
    );
  }

  static LeaveApplication leaveApplication({
    String leaveTypeId = 'lt-1',
    DateTime? startDate,
    DateTime? endDate,
    String reason = 'Family matters out of town, back on Monday.',
    List<String> attachmentPaths = const <String>[],
    LeaveDayPortion dayPortion = LeaveDayPortion.fullDay,
  }) {
    return LeaveApplication(
      leaveTypeId: leaveTypeId,
      startDate: startDate ?? DateTime(2026, 4, 2),
      endDate: endDate ?? DateTime(2026, 4, 3),
      reason: reason,
      dayPortion: dayPortion,
      attachmentPaths: attachmentPaths,
    );
  }

  /// A one-page response, for repository and controller tests.
  static PaginatedResponse<T> page<T>(List<T> items, {bool hasNext = false}) {
    return PaginatedResponse<T>(
      items: items,
      pageNumber: 1,
      pageSize: 20,
      totalCount: items.length,
      totalPages: 1,
      hasNextPage: hasNext,
      hasPreviousPage: false,
    );
  }
}

/// Convenience matchers for [Result].
extension ResultTestX<T> on Result<T> {
  /// The value, or a test failure if this is an error.
  T get requireValue {
    final T? value = valueOrNull;
    if (value == null) {
      throw StateError('Expected a success but got $this');
    }
    return value;
  }
}
