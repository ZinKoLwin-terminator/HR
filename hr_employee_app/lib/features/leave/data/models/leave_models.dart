import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';

part 'leave_models.freezed.dart';
part 'leave_models.g.dart';

/// Wire shape of a leave type. See `docs/api_contracts.md` §5.
///
/// Note the enum and date fields: they are typed as `String` here and
/// converted in `toEntity`. Holding the raw value means a status the client
/// has never heard of still parses, and date-only fields are not silently
/// shifted by a timezone.
@freezed
abstract class LeaveTypeModel with _$LeaveTypeModel {
  const factory LeaveTypeModel({
    required String id,
    required String name,
    String? code,
    @Default(false) bool requiresAttachment,
    int? maxConsecutiveDays,
    String? colorHex,
    @Default(true) bool isActive,
  }) = _LeaveTypeModel;

  const LeaveTypeModel._();

  factory LeaveTypeModel.fromJson(Map<String, dynamic> json) =>
      _$LeaveTypeModelFromJson(json);

  LeaveType toEntity() => LeaveType(
    id: id,
    name: name,
    code: code,
    requiresAttachment: requiresAttachment,
    maxConsecutiveDays: maxConsecutiveDays,
    colorHex: colorHex,
    isActive: isActive,
  );
}

/// Wire shape of a leave balance.
@freezed
abstract class LeaveBalanceModel with _$LeaveBalanceModel {
  const factory LeaveBalanceModel({
    required String leaveTypeId,
    required String leaveTypeName,
    int? year,

    /// Short type code, e.g. `AL`. Decides which types the leave screen's
    /// headline total adds up.
    String? code,
    @Default(0) num entitledDays,
    @Default(0) num usedDays,
    @Default(0) num pendingDays,
    @Default(0) num remainingDays,
    String? colorHex,
  }) = _LeaveBalanceModel;

  const LeaveBalanceModel._();

  factory LeaveBalanceModel.fromJson(Map<String, dynamic> json) =>
      _$LeaveBalanceModelFromJson(json);

  /// [fallbackYear] covers the dashboard payload, which omits `year` because
  /// it only ever reports the current one.
  LeaveBalance toEntity({int? fallbackYear}) => LeaveBalance(
    leaveTypeId: leaveTypeId,
    leaveTypeName: leaveTypeName,
    year: year ?? fallbackYear ?? DateTime.now().year,
    code: code,
    entitledDays: entitledDays.toDouble(),
    usedDays: usedDays.toDouble(),
    pendingDays: pendingDays.toDouble(),
    remainingDays: remainingDays.toDouble(),
    colorHex: colorHex,
  );
}

/// Wire shape of a leave attachment.
@freezed
abstract class LeaveAttachmentModel with _$LeaveAttachmentModel {
  const factory LeaveAttachmentModel({
    required String id,
    required String fileName,
    String? contentType,
    @Default(0) int sizeBytes,
    String? downloadUrl,
  }) = _LeaveAttachmentModel;

  const LeaveAttachmentModel._();

  factory LeaveAttachmentModel.fromJson(Map<String, dynamic> json) =>
      _$LeaveAttachmentModelFromJson(json);

  LeaveAttachment toEntity() => LeaveAttachment(
    id: id,
    fileName: fileName,
    contentType: contentType,
    sizeBytes: sizeBytes,
    downloadUrl: downloadUrl,
  );
}

/// Wire shape of a leave request.
@freezed
abstract class LeaveRequestModel with _$LeaveRequestModel {
  const factory LeaveRequestModel({
    required String id,
    required String leaveTypeId,
    required String leaveTypeName,

    /// `yyyy-MM-dd`.
    required String startDate,
    required String endDate,

    /// One of `Pending`, `Approved`, `Rejected`, `Cancelled`.
    required String status,
    required DateTime appliedAt,
    @Default('FullDay') String dayPortion,
    @Default(0) num totalDays,
    @Default('') String reason,
    String? contactDuringLeave,
    String? colorHex,
    DateTime? decidedAt,
    String? approverName,
    String? decisionRemark,
    @Default(<LeaveAttachmentModel>[]) List<LeaveAttachmentModel> attachments,
  }) = _LeaveRequestModel;

  const LeaveRequestModel._();

  factory LeaveRequestModel.fromJson(Map<String, dynamic> json) =>
      _$LeaveRequestModelFromJson(json);

  /// Converts to the domain entity.
  ///
  /// A date that fails to parse falls back to [appliedAt] rather than
  /// throwing: one malformed row should not empty the whole list.
  LeaveRequest toEntity() => LeaveRequest(
    id: id,
    leaveTypeId: leaveTypeId,
    leaveTypeName: leaveTypeName,
    startDate: DateFormatter.parseApiDate(startDate) ?? appliedAt,
    endDate: DateFormatter.parseApiDate(endDate) ?? appliedAt,
    status: LeaveStatusMapper.fromApi(status),
    appliedAt: appliedAt,
    dayPortion: LeaveDayPortionMapper.fromApi(dayPortion),
    totalDays: totalDays.toDouble(),
    reason: reason,
    contactDuringLeave: contactDuringLeave,
    colorHex: colorHex,
    decidedAt: decidedAt,
    approverName: approverName,
    decisionRemark: decisionRemark,
    attachments: attachments
        .map((LeaveAttachmentModel model) => model.toEntity())
        .toList(growable: false),
  );
}
