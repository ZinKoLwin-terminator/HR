import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';

part 'grievance_models.freezed.dart';
part 'grievance_models.g.dart';

// Wire shapes of the grievance self-service API
// (`Apex.Hrms.Application/Grievances/GrievanceModels.cs`). JSON is camelCase,
// enums are their exact texts, dates are `yyyy-MM-dd`, `...AtUtc` fields are
// UTC instants and `rowVersion` is base64. See `docs/api_contracts.md` §10.
//
// As with leave, enum and date-only fields are held as `String` and
// converted in `toEntity`, so an unknown status still parses and a calendar
// day is not shifted by a timezone.

/// `GrievanceCategoryOptionDto`.
@freezed
abstract class GrievanceCategoryOptionModel
    with _$GrievanceCategoryOptionModel {
  const factory GrievanceCategoryOptionModel({
    required int id,
    required String name,
    @Default('') String code,
    @Default('') String description,
    @Default(false) bool allowsAnonymous,
    @Default(0) int defaultTargetDays,
  }) = _GrievanceCategoryOptionModel;

  const GrievanceCategoryOptionModel._();

  factory GrievanceCategoryOptionModel.fromJson(Map<String, dynamic> json) =>
      _$GrievanceCategoryOptionModelFromJson(json);

  GrievanceCategory toEntity() => GrievanceCategory(
    id: id,
    name: name,
    code: code,
    description: description,
    allowsAnonymous: allowsAnonymous,
    defaultTargetDays: defaultTargetDays,
  );
}

/// `GrievanceDepartmentOptionDto`.
@freezed
abstract class GrievanceDepartmentOptionModel
    with _$GrievanceDepartmentOptionModel {
  const factory GrievanceDepartmentOptionModel({
    required int id,
    required String name,
  }) = _GrievanceDepartmentOptionModel;

  const GrievanceDepartmentOptionModel._();

  factory GrievanceDepartmentOptionModel.fromJson(Map<String, dynamic> json) =>
      _$GrievanceDepartmentOptionModelFromJson(json);

  GrievanceDepartment toEntity() => GrievanceDepartment(id: id, name: name);
}

/// `GrievanceOptionsDto`: `GET /grievances/options`.
@freezed
abstract class GrievanceOptionsModel with _$GrievanceOptionsModel {
  const factory GrievanceOptionsModel({
    @Default(<GrievanceCategoryOptionModel>[])
    List<GrievanceCategoryOptionModel> categories,
    @Default(<GrievanceDepartmentOptionModel>[])
    List<GrievanceDepartmentOptionModel> departments,
  }) = _GrievanceOptionsModel;

  const GrievanceOptionsModel._();

  factory GrievanceOptionsModel.fromJson(Map<String, dynamic> json) =>
      _$GrievanceOptionsModelFromJson(json);

  GrievanceOptions toEntity() => GrievanceOptions(
    categories: categories
        .map((GrievanceCategoryOptionModel c) => c.toEntity())
        .toList(growable: false),
    departments: departments
        .map((GrievanceDepartmentOptionModel d) => d.toEntity())
        .toList(growable: false),
  );
}

/// `MyGrievanceDto`: one row of `GET /grievances/my`.
@freezed
abstract class MyGrievanceModel with _$MyGrievanceModel {
  const factory MyGrievanceModel({
    required int id,
    required int categoryId,
    required String categoryName,
    required String subject,
    required String status,
    required DateTime createdAtUtc,
    required String rowVersion,
    String? caseNo,
    @Default(false) bool isAnonymous,
    DateTime? submittedAtUtc,

    /// `yyyy-MM-dd`.
    String? targetResolutionDate,
    DateTime? lastUpdateAtUtc,
    @Default(false) bool hasUnreadUpdate,
  }) = _MyGrievanceModel;

  const MyGrievanceModel._();

  factory MyGrievanceModel.fromJson(Map<String, dynamic> json) =>
      _$MyGrievanceModelFromJson(json);

  GrievanceSummary toEntity() => GrievanceSummary(
    id: id,
    caseNo: caseNo,
    categoryId: categoryId,
    categoryName: categoryName,
    subject: subject,
    status: GrievanceStatusMapper.fromApi(status),
    isAnonymous: isAnonymous,
    createdAt: createdAtUtc,
    submittedAt: submittedAtUtc,
    targetResolutionDate: DateFormatter.parseApiDate(targetResolutionDate),
    lastUpdateAt: lastUpdateAtUtc,
    hasUnreadUpdate: hasUnreadUpdate,
    rowVersion: rowVersion,
  );
}

/// `MyGrievanceTimelineEntryDto`.
@freezed
abstract class MyGrievanceTimelineEntryModel
    with _$MyGrievanceTimelineEntryModel {
  const factory MyGrievanceTimelineEntryModel({
    required String fromStatus,
    required String toStatus,
    required String action,
    required String actor,
    required DateTime occurredAtUtc,
    String? reason,
  }) = _MyGrievanceTimelineEntryModel;

  const MyGrievanceTimelineEntryModel._();

  factory MyGrievanceTimelineEntryModel.fromJson(Map<String, dynamic> json) =>
      _$MyGrievanceTimelineEntryModelFromJson(json);

  GrievanceTimelineEntry toEntity() => GrievanceTimelineEntry(
    fromStatus: GrievanceStatusMapper.fromApi(fromStatus),
    toStatus: GrievanceStatusMapper.fromApi(toStatus),
    action: action,
    actor: actor,
    reason: reason,
    occurredAt: occurredAtUtc,
  );
}

/// `MyGrievanceMessageDto`.
@freezed
abstract class MyGrievanceMessageModel with _$MyGrievanceMessageModel {
  const factory MyGrievanceMessageModel({
    required int id,
    required String direction,
    required String kind,
    required String body,
    required String actor,
    required DateTime createdAtUtc,
    int? inReplyToId,
  }) = _MyGrievanceMessageModel;

  const MyGrievanceMessageModel._();

  factory MyGrievanceMessageModel.fromJson(Map<String, dynamic> json) =>
      _$MyGrievanceMessageModelFromJson(json);

  GrievanceMessage toEntity() => GrievanceMessage(
    id: id,
    direction: GrievanceMessageDirectionMapper.fromApi(direction),
    kind: GrievanceMessageKindMapper.fromApi(kind),
    body: body,
    actor: actor,
    inReplyToId: inReplyToId,
    createdAt: createdAtUtc,
  );
}

/// `MyGrievanceAttachmentDto`.
@freezed
abstract class MyGrievanceAttachmentModel with _$MyGrievanceAttachmentModel {
  const factory MyGrievanceAttachmentModel({
    required int id,
    required String fileName,
    required String uploadedBy,
    required DateTime uploadedAtUtc,
    @Default('') String contentType,
    @Default(0) int sizeBytes,
  }) = _MyGrievanceAttachmentModel;

  const MyGrievanceAttachmentModel._();

  factory MyGrievanceAttachmentModel.fromJson(Map<String, dynamic> json) =>
      _$MyGrievanceAttachmentModelFromJson(json);

  GrievanceAttachment toEntity() => GrievanceAttachment(
    id: id,
    fileName: fileName,
    contentType: contentType,
    sizeBytes: sizeBytes,
    uploadedBy: uploadedBy,
    uploadedAt: uploadedAtUtc,
  );
}

/// `MyGrievanceDetailDto`: `GET /grievances/my/{id}` and the answer to every
/// write.
@freezed
abstract class MyGrievanceDetailModel with _$MyGrievanceDetailModel {
  const factory MyGrievanceDetailModel({
    required int id,
    required int categoryId,
    required String categoryName,
    required String subject,
    required String status,
    required DateTime createdAtUtc,
    required String rowVersion,
    String? caseNo,
    @Default(false) bool isAnonymous,
    DateTime? submittedAtUtc,
    String? targetResolutionDate,
    DateTime? lastUpdateAtUtc,
    @Default(false) bool hasUnreadUpdate,
    @Default('') String description,

    /// `yyyy-MM-dd`.
    String? incidentDate,
    String? location,
    String? involvedPersonText,
    int? involvedDepartmentId,
    String? involvedDepartmentName,
    DateTime? declarationAcceptedAtUtc,
    DateTime? acknowledgedAtUtc,
    DateTime? resolvedAtUtc,
    DateTime? closedAtUtc,
    String? rejectionReason,
    String? resolutionSummary,
    @Default(<MyGrievanceTimelineEntryModel>[])
    List<MyGrievanceTimelineEntryModel> timeline,
    @Default(<MyGrievanceMessageModel>[])
    List<MyGrievanceMessageModel> messages,
    @Default(<MyGrievanceAttachmentModel>[])
    List<MyGrievanceAttachmentModel> attachments,
  }) = _MyGrievanceDetailModel;

  const MyGrievanceDetailModel._();

  factory MyGrievanceDetailModel.fromJson(Map<String, dynamic> json) =>
      _$MyGrievanceDetailModelFromJson(json);

  /// The list row of this case, which the mock store derives from the
  /// detail it keeps.
  MyGrievanceModel toSummaryModel() => MyGrievanceModel(
    id: id,
    caseNo: caseNo,
    categoryId: categoryId,
    categoryName: categoryName,
    subject: subject,
    status: status,
    isAnonymous: isAnonymous,
    createdAtUtc: createdAtUtc,
    submittedAtUtc: submittedAtUtc,
    targetResolutionDate: targetResolutionDate,
    lastUpdateAtUtc: lastUpdateAtUtc,
    hasUnreadUpdate: hasUnreadUpdate,
    rowVersion: rowVersion,
  );

  GrievanceDetail toEntity() => GrievanceDetail(
    id: id,
    caseNo: caseNo,
    categoryId: categoryId,
    categoryName: categoryName,
    subject: subject,
    status: GrievanceStatusMapper.fromApi(status),
    isAnonymous: isAnonymous,
    createdAt: createdAtUtc,
    submittedAt: submittedAtUtc,
    targetResolutionDate: DateFormatter.parseApiDate(targetResolutionDate),
    lastUpdateAt: lastUpdateAtUtc,
    hasUnreadUpdate: hasUnreadUpdate,
    rowVersion: rowVersion,
    description: description,
    incidentDate: DateFormatter.parseApiDate(incidentDate),
    location: location,
    involvedPersonText: involvedPersonText,
    involvedDepartmentId: involvedDepartmentId,
    involvedDepartmentName: involvedDepartmentName,
    declarationAcceptedAt: declarationAcceptedAtUtc,
    acknowledgedAt: acknowledgedAtUtc,
    resolvedAt: resolvedAtUtc,
    closedAt: closedAtUtc,
    rejectionReason: rejectionReason,
    resolutionSummary: resolutionSummary,
    timeline: timeline
        .map((MyGrievanceTimelineEntryModel e) => e.toEntity())
        .toList(growable: false),
    messages: messages
        .map((MyGrievanceMessageModel m) => m.toEntity())
        .toList(growable: false),
    attachments: attachments
        .map((MyGrievanceAttachmentModel a) => a.toEntity())
        .toList(growable: false),
  );
}

/// Request bodies, written by hand: they are flat and their exact spelling
/// is the contract.
abstract final class GrievanceRequestBodies {
  /// `GrievanceDraftCreateRequest`, or `GrievanceDraftUpdateRequest` when
  /// [rowVersion] is given.
  static Map<String, dynamic> draft(
    GrievanceDraftInput input, {
    String? rowVersion,
  }) {
    final GrievanceDraftInput d = input.normalized();
    return <String, dynamic>{
      'rowVersion': ?rowVersion,
      'categoryId': d.categoryId,
      'subject': d.subject,
      'description': d.description,
      'incidentDate': d.incidentDate == null
          ? null
          : DateFormatter.toApiDate(d.incidentDate!),
      'location': d.location,
      'involvedPersonText': d.involvedPersonText,
      'involvedDepartmentId': d.involvedDepartmentId,
      'isAnonymous': d.isAnonymous,
    };
  }

  /// `SubmitGrievanceRequest`.
  static Map<String, dynamic> submit({
    required String rowVersion,
    required bool declarationAccepted,
  }) => <String, dynamic>{
    'rowVersion': rowVersion,
    'declarationAccepted': declarationAccepted,
  };

  /// `MyGrievanceMessageRequest`.
  static Map<String, dynamic> message({
    required String rowVersion,
    required String body,
    int? inReplyToId,
  }) => <String, dynamic>{
    'rowVersion': rowVersion,
    'body': body,
    'inReplyToId': inReplyToId,
  };

  /// `VersionedBody`, for the delete.
  static Map<String, dynamic> versioned(String rowVersion) => <String, dynamic>{
    'rowVersion': rowVersion,
  };
}
