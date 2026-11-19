import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/paginated_list_notifier.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/usecases/punch_attendance_usecase.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_providers.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_controller.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// Today's attendance record, or null before the first punch.
class TodayAttendanceController extends AsyncNotifier<AttendanceRecord?> {
  @override
  Future<AttendanceRecord?> build() async {
    final Result<AttendanceRecord?> result = await ref.watch(
      getTodayAttendanceUseCaseProvider,
    )();
    return result.valueOrThrow();
  }

  /// Applies the record returned by a punch, avoiding a refetch.
  void apply(AttendanceRecord record) {
    state = AsyncValue<AttendanceRecord?>.data(record);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

final AsyncNotifierProvider<TodayAttendanceController, AttendanceRecord?>
todayAttendanceControllerProvider =
    AsyncNotifierProvider<TodayAttendanceController, AttendanceRecord?>(
      TodayAttendanceController.new,
    );

/// Records a check-in or check-out.
///
/// Kept apart from [TodayAttendanceController] for the same reason login is
/// kept apart from the session: a failed punch should surface as a message on
/// the button, not replace today's card with an error.
///
/// On success it pushes the new record into every provider that shows it, so
/// the dashboard and the attendance screen update together without either
/// refetching.
class PunchController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  Future<bool> punch(PunchDirection direction, {String? remark}) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final PunchAttendanceUseCase punchAttendance = ref.read(
      punchAttendanceUseCaseProvider,
    );
    final Result<AttendanceRecord> result = await punchAttendance(
      direction: direction,
      remark: remark,
    );

    return result.fold(
      onSuccess: (AttendanceRecord record) {
        _propagate(record);
        state = SubmissionState.success(
          message: direction == PunchDirection.checkIn
              ? 'Checked in.'
              : 'Checked out.',
        );
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  void reset() => state = const SubmissionState.idle();

  /// Fans the confirmed record out to the providers that display it.
  void _propagate(AttendanceRecord record) {
    ref.read(todayAttendanceControllerProvider.notifier).apply(record);

    ref
        .read(dashboardControllerProvider.notifier)
        .applyTodayAttendance(
          TodayAttendance(
            attendanceDate: record.attendanceDate,
            status: record.status,
            checkInAt: record.checkInAt,
            checkOutAt: record.checkOutAt,
            isLateArrival: record.isLateArrival,
            isEarlyDeparture: record.isEarlyDeparture,
            workedMinutes: record.workedMinutes,
          ),
        );

    // History now contains an extra row, and the monthly counts have moved.
    // Invalidated rather than patched: the server owns the aggregation.
    ref.invalidate(attendanceHistoryControllerProvider);
    ref.invalidate(attendanceSummaryProvider);
  }
}

final NotifierProvider<PunchController, SubmissionState>
punchControllerProvider = NotifierProvider<PunchController, SubmissionState>(
  PunchController.new,
);

/// Paginated attendance history, newest first.
class AttendanceHistoryController
    extends PaginatedListNotifier<AttendanceRecord> {
  @override
  Future<Result<PaginatedResponse<AttendanceRecord>>> fetchPage(
    PageRequest page,
  ) {
    return ref.read(getAttendanceHistoryUseCaseProvider)(page: page);
  }

  @override
  Object identityOf(AttendanceRecord item) => item.id;
}

final AsyncNotifierProvider<
  AttendanceHistoryController,
  PaginatedState<AttendanceRecord>
>
attendanceHistoryControllerProvider =
    AsyncNotifierProvider<
      AttendanceHistoryController,
      PaginatedState<AttendanceRecord>
    >(AttendanceHistoryController.new);

/// The month a summary is requested for.
class AttendancePeriod {
  const AttendancePeriod({required this.year, required this.month});

  /// The current month, in local time.
  factory AttendancePeriod.current() {
    final DateTime now = DateTime.now();
    return AttendancePeriod(year: now.year, month: now.month);
  }

  final int year;
  final int month;

  DateTime get asDate => DateTime(year, month);

  /// The previous month, rolling the year over correctly.
  AttendancePeriod previous() {
    final DateTime shifted = DateTime(year, month - 1);
    return AttendancePeriod(year: shifted.year, month: shifted.month);
  }

  AttendancePeriod next() {
    final DateTime shifted = DateTime(year, month + 1);
    return AttendancePeriod(year: shifted.year, month: shifted.month);
  }

  /// True when this period is the current calendar month or later, used to
  /// stop the month picker running into the future.
  bool get isCurrentOrFuture {
    final DateTime now = DateTime.now();
    return year > now.year || (year == now.year && month >= now.month);
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AttendancePeriod && year == other.year && month == other.month;

  @override
  int get hashCode => Object.hash(year, month);
}

/// The monthly summary for a period.
///
/// A `FutureProvider.family` rather than an `AsyncNotifier`: it is a pure
/// read with no commands, and the family key gives per-month caching for
/// free as the user pages through months.
///
/// The type is left to inference here and on every other family in the app:
/// Riverpod 3 does not export `FutureProviderFamily`, so it cannot be named.
final attendanceSummaryProvider =
    FutureProvider.family<AttendanceSummary, AttendancePeriod>((
      Ref ref,
      AttendancePeriod period,
    ) async {
      final Result<AttendanceSummary> result = await ref.watch(
        getMonthlyAttendanceSummaryUseCaseProvider,
      )(year: period.year, month: period.month);
      return result.valueOrThrow();
    });

/// A single attendance record, for the detail screen.
final attendanceDetailProvider =
    FutureProvider.family<AttendanceRecord, String>((Ref ref, String id) async {
      final Result<AttendanceRecord> result = await ref.watch(
        getAttendanceDetailUseCaseProvider,
      )(id);
      return result.valueOrThrow();
    });
