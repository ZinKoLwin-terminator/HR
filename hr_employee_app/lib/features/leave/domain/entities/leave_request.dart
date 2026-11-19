import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';

part 'leave_request.freezed.dart';

/// A file attached to a leave request.
@freezed
abstract class LeaveAttachment with _$LeaveAttachment {
  const factory LeaveAttachment({
    required String id,
    required String fileName,
    String? contentType,
    @Default(0) int sizeBytes,

    /// Authenticated URL the file can be fetched from.
    String? downloadUrl,
  }) = _LeaveAttachment;

  const LeaveAttachment._();

  /// `148 KB`, for the attachment row.
  String get readableSize {
    if (sizeBytes <= 0) {
      return '';
    }
    if (sizeBytes < 1024) {
      return '$sizeBytes B';
    }
    if (sizeBytes < 1024 * 1024) {
      return '${(sizeBytes / 1024).round()} KB';
    }
    return '${(sizeBytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }

  bool get isPdf =>
      contentType == 'application/pdf' ||
      fileName.toLowerCase().endsWith('.pdf');
}

/// A submitted leave application and its outcome.
@freezed
abstract class LeaveRequest with _$LeaveRequest {
  const factory LeaveRequest({
    required String id,
    required String leaveTypeId,
    required String leaveTypeName,

    /// Calendar days, no time component.
    required DateTime startDate,
    required DateTime endDate,
    required LeaveStatus status,
    required DateTime appliedAt,
    @Default(LeaveDayPortion.fullDay) LeaveDayPortion dayPortion,

    /// Days this request consumes, as calculated by the server. Decimal to
    /// allow half-days.
    @Default(0) double totalDays,
    @Default('') String reason,
    String? contactDuringLeave,
    String? colorHex,

    /// Set once approved or rejected.
    DateTime? decidedAt,
    String? approverName,
    String? decisionRemark,
    @Default(<LeaveAttachment>[]) List<LeaveAttachment> attachments,
  }) = _LeaveRequest;

  const LeaveRequest._();

  bool get isSingleDay =>
      startDate.year == endDate.year &&
      startDate.month == endDate.month &&
      startDate.day == endDate.day;

  bool get hasAttachments => attachments.isNotEmpty;

  /// Whether this request can still be withdrawn.
  bool get isCancellable => status.isCancellable;

  /// Whether the leave is in the future, so a cancellation still has effect.
  bool get isUpcoming => startDate.isAfter(DateTime.now());
}

/// What the apply-leave form submits.
///
/// [attachmentPaths] are local file paths chosen by the user; the data layer
/// turns them into multipart parts. The domain holds paths rather than `File`
/// objects so it stays free of `dart:io`, which keeps it testable and
/// platform-agnostic.
@freezed
abstract class LeaveApplication with _$LeaveApplication {
  const factory LeaveApplication({
    required String leaveTypeId,
    required DateTime startDate,
    required DateTime endDate,
    required String reason,
    @Default(LeaveDayPortion.fullDay) LeaveDayPortion dayPortion,
    String? contactDuringLeave,
    @Default(<String>[]) List<String> attachmentPaths,
  }) = _LeaveApplication;

  const LeaveApplication._();

  /// Inclusive count of calendar days covered.
  int get calendarDayCount =>
      DateTime(endDate.year, endDate.month, endDate.day)
          .difference(DateTime(startDate.year, startDate.month, startDate.day))
          .inDays +
      1;

  /// Days this would consume, as the client estimates it.
  ///
  /// Shown to set expectations before submitting. The server's figure is
  /// authoritative, because only it knows the holiday calendar and whether
  /// weekends count against this leave type.
  double get estimatedDays {
    if (dayPortion != LeaveDayPortion.fullDay) {
      // A half-day request only ever covers one day.
      return 0.5;
    }
    return calendarDayCount.toDouble();
  }
}
