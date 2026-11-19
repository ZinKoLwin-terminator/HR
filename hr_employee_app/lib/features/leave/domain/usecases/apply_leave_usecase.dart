import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/domain/repositories/leave_repository.dart';

/// Submits a leave application after checking the rules the client can check.
///
/// The division of labour matters here. These checks exist to save the user a
/// round trip and to give a precise message; the **server remains the
/// authority** on balances, overlaps and the holiday calendar, because the
/// balance may have moved since it was fetched and only the server knows the
/// roster. A rejection from the server is surfaced verbatim rather than
/// second-guessed.
class ApplyLeaveUseCase {
  const ApplyLeaveUseCase(this._repository);

  final LeaveRepository _repository;

  Future<Result<LeaveRequest>> call(
    LeaveApplication application, {

    /// The matching type, when it is already loaded, so attachment and
    /// duration rules can be checked before submitting.
    LeaveType? leaveType,

    /// The current balance for this type, when loaded.
    LeaveBalance? balance,
    void Function(double progress)? onProgress,
  }) async {
    final Failure? rejection = _validate(
      application,
      leaveType: leaveType,
      balance: balance,
    );
    if (rejection != null) {
      return Result<LeaveRequest>.failure(rejection);
    }

    return _repository.apply(application, onProgress: onProgress);
  }

  /// Returns the first problem found, or null when the application is worth
  /// sending.
  Failure? _validate(
    LeaveApplication application, {
    required LeaveType? leaveType,
    required LeaveBalance? balance,
  }) {
    if (application.leaveTypeId.isEmpty) {
      return const ValidationFailure('Choose a leave type.');
    }

    if (application.endDate.isBefore(application.startDate)) {
      return const ValidationFailure(
        'The end date cannot be before the start date.',
      );
    }

    final String reason = application.reason.trim();
    if (reason.length < ValidationRules.minLeaveReasonLength) {
      return const ValidationFailure(
        'Give at least ${ValidationRules.minLeaveReasonLength} characters of '
        'context in the reason.',
      );
    }
    if (reason.length > ValidationRules.maxLeaveReasonLength) {
      return const ValidationFailure(
        'Keep the reason under ${ValidationRules.maxLeaveReasonLength} '
        'characters.',
      );
    }

    if (application.attachmentPaths.length > UploadLimits.maxLeaveAttachments) {
      return const ValidationFailure(
        'Attach at most ${UploadLimits.maxLeaveAttachments} files.',
      );
    }

    if (leaveType != null) {
      if (leaveType.requiresAttachment && application.attachmentPaths.isEmpty) {
        return ValidationFailure(
          '${leaveType.name} requires a supporting document.',
        );
      }

      final int? maxDays = leaveType.maxConsecutiveDays;
      if (maxDays != null && application.calendarDayCount > maxDays) {
        return ValidationFailure(
          '${leaveType.name} allows at most $maxDays consecutive days.',
        );
      }
    }

    // Advisory only: the server re-checks against the live balance.
    if (balance != null && !balance.canAccommodate(application.estimatedDays)) {
      return ValidationFailure(
        'You have ${_formatDays(balance.remainingDays)} of '
        '${balance.leaveTypeName} left, but this request needs '
        '${_formatDays(application.estimatedDays)}.',
      );
    }

    return null;
  }

  String _formatDays(double days) {
    final String value = days == days.roundToDouble()
        ? days.toInt().toString()
        : days.toStringAsFixed(1);
    return days == 1 ? '$value day' : '$value days';
  }
}
