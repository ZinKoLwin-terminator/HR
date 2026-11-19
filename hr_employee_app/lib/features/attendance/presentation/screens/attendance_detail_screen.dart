import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_text_styles.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/geo_location.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:hr_employee_app/features/attendance/presentation/widgets/attendance_status_chip.dart';
import 'package:material_ui/material_ui.dart';

/// One day of attendance in full.
class AttendanceDetailScreen extends ConsumerWidget {
  const AttendanceDetailScreen({required this.attendanceId, super.key});

  final String attendanceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<AttendanceRecord> record = ref.watch(
      attendanceDetailProvider(attendanceId),
    );

    return Scaffold(
      appBar: const BrandAppBar(title: Text('Attendance detail')),
      body: SafeArea(
        child: AsyncValueView<AttendanceRecord>(
          value: record,
          onRetry: () => ref.invalidate(attendanceDetailProvider(attendanceId)),
          data: (AttendanceRecord data) => SingleChildScrollView(
            padding: AppSpacing.screenPadding,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _DateHeader(record: data),
                SizedBox(height: AppSpacing.cardGap),
                _TimesCard(record: data),
                SizedBox(height: AppSpacing.cardGap),
                if (data.checkInLocation != null ||
                    data.checkOutLocation != null) ...<Widget>[
                  _LocationCard(record: data),
                  SizedBox(height: AppSpacing.cardGap),
                ],
                if (data.remark.isNotNullOrBlank) _RemarkCard(record: data),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _DateHeader extends StatelessWidget {
  const _DateHeader({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return AppCard(
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  DateFormatter.fullDate(record.attendanceDate),
                  style: textStyles.titleMedium,
                ),
                if (record.shiftStartAt != null &&
                    record.shiftEndAt != null) ...<Widget>[
                  Gap.xs,
                  Text(
                    'Shift ${DateFormatter.time(record.shiftStartAt!)} - '
                    '${DateFormatter.time(record.shiftEndAt!)}',
                    style: textStyles.bodySmall,
                  ),
                ],
              ],
            ),
          ),
          AttendanceStatusChip(status: record.status),
        ],
      ),
    );
  }
}

class _TimesCard extends StatelessWidget {
  const _TimesCard({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Times',
      icon: Icons.schedule_outlined,
      child: Column(
        children: <Widget>[
          AppInfoRow(
            label: 'Check in',
            value: record.checkInAt == null
                ? '—'
                : DateFormatter.time(record.checkInAt!),
            valueStyle: AppTextStyles.numeric,
          ),
          if (record.isLateArrival)
            _FlagRow(
              label: 'Late arrival',
              detail: '${record.lateByMinutes} minutes',
            ),
          AppInfoRow(
            label: 'Check out',
            value: record.checkOutAt == null
                ? '—'
                : DateFormatter.time(record.checkOutAt!),
            valueStyle: AppTextStyles.numeric,
          ),
          if (record.isEarlyDeparture)
            _FlagRow(
              label: 'Early departure',
              detail: '${record.earlyByMinutes} minutes',
            ),
          Divider(height: AppSpacing.lg, color: context.palette.border),
          AppInfoRow(
            label: 'Worked',
            value: DateFormatter.duration(record.workedMinutes),
            valueStyle: AppTextStyles.numeric,
          ),
          if (record.overtimeMinutes > 0)
            AppInfoRow(
              label: 'Overtime',
              value: DateFormatter.duration(record.overtimeMinutes),
              valueStyle: AppTextStyles.numeric,
            ),
        ],
      ),
    );
  }
}

/// A late/early row, rendered with a warning chip rather than plain text so
/// it is visibly an exception.
class _FlagRow extends StatelessWidget {
  const _FlagRow({required this.label, required this.detail});

  final String label;
  final String detail;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.symmetric(vertical: AppSpacing.xs),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              label,
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: context.palette.textSecondary),
            ),
          ),
          StatusChip(label: detail, tone: StatusTone.warning, isCompact: true),
        ],
      ),
    );
  }
}

/// Where the punches were recorded.
///
/// Reports what the device measured and nothing more. There is deliberately
/// no "verified" or "inside office" indicator: the server decides whether a
/// location is acceptable, and showing a client-side verdict here would be
/// both unverifiable and misleading.
class _LocationCard extends StatelessWidget {
  const _LocationCard({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Recorded location',
      icon: Icons.place_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          if (record.checkInLocation != null)
            _LocationRow(
              label: 'At check in',
              location: record.checkInLocation!,
            ),
          if (record.checkOutLocation != null) ...<Widget>[
            Gap.md,
            _LocationRow(
              label: 'At check out',
              location: record.checkOutLocation!,
            ),
          ],
        ],
      ),
    );
  }
}

class _LocationRow extends StatelessWidget {
  const _LocationRow({required this.label, required this.location});

  final String label;
  final GeoLocation location;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(label, style: textStyles.bodySmall),
        Gap.xxs,
        Text(location.displayLabel, style: textStyles.titleSmall),
        Gap.xs,
        Row(
          children: <Widget>[
            if (location.accuracyMeters != null)
              Text(
                'Accuracy ${location.accuracyMeters!.round()} m',
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textTertiary,
                ),
              ),
            // Surfaced honestly rather than hidden; what to do about a mocked
            // fix is the server's call.
            if (location.isMocked) ...<Widget>[
              Gap.wSm,
              const StatusChip(
                label: 'Mock location',
                tone: StatusTone.negative,
                isCompact: true,
              ),
            ],
          ],
        ),
      ],
    );
  }
}

class _RemarkCard extends StatelessWidget {
  const _RemarkCard({required this.record});

  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Remark',
      icon: Icons.sticky_note_2_outlined,
      child: Align(
        alignment: Alignment.centerLeft,
        child: Text(
          record.remark.orDash(),
          style: Theme.of(context).textTheme.bodyMedium,
        ),
      ),
    );
  }
}
