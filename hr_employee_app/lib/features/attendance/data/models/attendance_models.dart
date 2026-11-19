import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/attendance/data/models/attendance_status_mapper.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';

part 'attendance_models.freezed.dart';
part 'attendance_models.g.dart';

/// Wire shape of a captured position. See `docs/api_contracts.md` §4.
@freezed
abstract class GeoLocationModel with _$GeoLocationModel {
  const factory GeoLocationModel({
    required double latitude,
    required double longitude,
    double? accuracyMeters,
    String? address,
    @Default(false) bool isMocked,
  }) = _GeoLocationModel;

  const GeoLocationModel._();

  factory GeoLocationModel.fromJson(Map<String, dynamic> json) =>
      _$GeoLocationModelFromJson(json);

  factory GeoLocationModel.fromEntity(GeoLocation location) => GeoLocationModel(
    latitude: location.latitude,
    longitude: location.longitude,
    accuracyMeters: location.accuracyMeters,
    address: location.address,
    isMocked: location.isMocked,
  );

  GeoLocation toEntity() => GeoLocation(
    latitude: latitude,
    longitude: longitude,
    accuracyMeters: accuracyMeters,
    address: address,
    isMocked: isMocked,
  );
}

/// Wire shape of one day of attendance.
@freezed
abstract class AttendanceRecordModel with _$AttendanceRecordModel {
  const factory AttendanceRecordModel({
    required String id,

    /// `yyyy-MM-dd`.
    required String attendanceDate,

    /// One of `Present`, `Absent`, `OnLeave`, `Holiday`, `WeeklyOff`,
    /// `HalfDay`.
    required String status,
    DateTime? checkInAt,
    DateTime? checkOutAt,
    GeoLocationModel? checkInLocation,
    GeoLocationModel? checkOutLocation,
    DateTime? shiftStartAt,
    DateTime? shiftEndAt,
    @Default(false) bool isLateArrival,
    @Default(0) int lateByMinutes,
    @Default(false) bool isEarlyDeparture,
    @Default(0) int earlyByMinutes,
    int? workedMinutes,
    @Default(0) int overtimeMinutes,
    String? remark,
  }) = _AttendanceRecordModel;

  const AttendanceRecordModel._();

  factory AttendanceRecordModel.fromJson(Map<String, dynamic> json) =>
      _$AttendanceRecordModelFromJson(json);

  AttendanceRecord toEntity() => AttendanceRecord(
    id: id,
    // Falls back to the check-in day, then to today, so an unparseable date
    // does not discard an otherwise usable record.
    attendanceDate:
        DateFormatter.parseApiDate(attendanceDate) ??
        (checkInAt == null
            ? DateFormatter.dateOnly(DateTime.now())
            : DateFormatter.dateOnly(checkInAt!.toLocal())),
    status: AttendanceStatusMapper.fromApi(status),
    checkInAt: checkInAt,
    checkOutAt: checkOutAt,
    checkInLocation: checkInLocation?.toEntity(),
    checkOutLocation: checkOutLocation?.toEntity(),
    shiftStartAt: shiftStartAt,
    shiftEndAt: shiftEndAt,
    isLateArrival: isLateArrival,
    lateByMinutes: lateByMinutes,
    isEarlyDeparture: isEarlyDeparture,
    earlyByMinutes: earlyByMinutes,
    workedMinutes: workedMinutes,
    overtimeMinutes: overtimeMinutes,
    remark: remark,
  );
}

/// Wire shape of a monthly summary.
@freezed
abstract class AttendanceSummaryModel with _$AttendanceSummaryModel {
  const factory AttendanceSummaryModel({
    required int year,
    required int month,
    @Default(0) int totalWorkingDays,
    @Default(0) int presentDays,
    @Default(0) int absentDays,
    @Default(0) int leaveDays,
    @Default(0) int holidayDays,
    @Default(0) int lateArrivals,
    @Default(0) int earlyDepartures,
    @Default(0) int totalWorkedMinutes,
    @Default(0) int totalOvertimeMinutes,
  }) = _AttendanceSummaryModel;

  const AttendanceSummaryModel._();

  factory AttendanceSummaryModel.fromJson(Map<String, dynamic> json) =>
      _$AttendanceSummaryModelFromJson(json);

  AttendanceSummary toEntity() => AttendanceSummary(
    year: year,
    month: month,
    totalWorkingDays: totalWorkingDays,
    presentDays: presentDays,
    absentDays: absentDays,
    leaveDays: leaveDays,
    holidayDays: holidayDays,
    lateArrivals: lateArrivals,
    earlyDepartures: earlyDepartures,
    totalWorkedMinutes: totalWorkedMinutes,
    totalOvertimeMinutes: totalOvertimeMinutes,
  );
}

/// Request body for check-in and check-out.
@freezed
abstract class AttendancePunchRequestModel with _$AttendancePunchRequestModel {
  const factory AttendancePunchRequestModel({
    required DateTime capturedAt,
    required String deviceId,

    /// Null when no position was captured. The server decides whether that is
    /// acceptable for this tenant.
    GeoLocationModel? location,
    String? remark,
  }) = _AttendancePunchRequestModel;

  const AttendancePunchRequestModel._();

  factory AttendancePunchRequestModel.fromJson(Map<String, dynamic> json) =>
      _$AttendancePunchRequestModelFromJson(json);

  factory AttendancePunchRequestModel.fromEntity(
    AttendancePunch punch, {
    required String deviceId,
  }) => AttendancePunchRequestModel(
    capturedAt: punch.capturedAt,
    deviceId: deviceId,
    location: punch.location == null
        ? null
        : GeoLocationModel.fromEntity(punch.location!),
    remark: punch.remark,
  );
}
