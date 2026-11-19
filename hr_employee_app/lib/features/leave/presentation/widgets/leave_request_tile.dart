import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/widgets/status_chip.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_overview.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_status_chip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:intl/intl.dart';
import 'package:material_ui/material_ui.dart';

/// One leave request as a row inside a grouped card.
///
/// The leading square is tinted with the leave type's colour. With
/// [showDateBox] it carries the start day and month, for the month-grouped
/// all-requests list; without it, a plain colour dot, for the short recent
/// list where the dates are read from the text.
class LeaveRequestRow extends StatelessWidget {
  const LeaveRequestRow({
    required this.request,
    required this.onTap,
    super.key,
    this.showDateBox = true,
  });

  final LeaveRequest request;
  final VoidCallback onTap;
  final bool showDateBox;

  static final DateFormat _weekdayDay = DateFormat('EEE d');
  static final DateFormat _weekdayDayMonth = DateFormat('EEE d MMM');
  static final DateFormat _monthShort = DateFormat('MMM');

  /// `Mon 24 – Tue 25 Nov · 2 days`, `Wed 14 Oct · first half · 0.5 days`.
  static String whenLine(LeaveRequest request) {
    final DateTime start = request.startDate;
    final DateTime end = request.endDate;
    final String dates;
    if (request.isSingleDay) {
      dates = _weekdayDayMonth.format(start);
    } else if (start.year == end.year && start.month == end.month) {
      dates = '${_weekdayDay.format(start)} – ${_weekdayDayMonth.format(end)}';
    } else {
      dates =
          '${_weekdayDayMonth.format(start)} – ${_weekdayDayMonth.format(end)}';
    }
    final String portion = request.dayPortion == LeaveDayPortion.fullDay
        ? ''
        : ' · ${request.dayPortion.label.toLowerCase()}';
    return '$dates$portion · ${DayCountFormatter.format(request.totalDays)}';
  }

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isDark = Theme.of(context).brightness == Brightness.dark;
    final Color accent = ColorUtils.fromHex(
      request.colorHex,
      fallback: Theme.of(context).colorScheme.primary,
    );
    final (String? note, Color noteColour) = _note(palette);

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.md,
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: ColorUtils.surfaceTint(accent, isDark: isDark),
                borderRadius: AppRadius.field,
              ),
              child: showDateBox
                  ? Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: <Widget>[
                        Text(
                          '${request.startDate.day}',
                          style: textStyles.titleMedium?.copyWith(
                            color: accent,
                            fontWeight: FontWeight.w800,
                            height: 1.05,
                          ),
                        ),
                        Text(
                          _monthShort.format(request.startDate).toUpperCase(),
                          style: textStyles.labelSmall?.copyWith(
                            color: accent,
                            fontSize: 9,
                            fontWeight: FontWeight.w700,
                            letterSpacing: 0.4,
                            height: 1.1,
                          ),
                        ),
                      ],
                    )
                  : Center(
                      child: Container(
                        width: 10,
                        height: 10,
                        decoration: BoxDecoration(
                          color: accent,
                          shape: BoxShape.circle,
                        ),
                      ),
                    ),
            ),
            Gap.wMd,
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          sentenceLeaveTypeName(request.leaveTypeName),
                          style: textStyles.titleSmall?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      Gap.wSm,
                      StatusChip(
                        label: request.status.label,
                        tone: LeaveStatusChip.toneFor(request.status),
                        isCompact: true,
                      ),
                    ],
                  ),
                  Gap.xxs,
                  Text(
                    whenLine(request),
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textSecondary,
                    ),
                  ),
                  if (note != null) ...<Widget>[
                    Gap.xxs,
                    Text(
                      note,
                      style: textStyles.bodySmall?.copyWith(color: noteColour),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// The one extra line a status earns: who it is waiting on, or why it was
  /// turned down. A rejection reason is shown in full rather than behind a
  /// tap, because it is the first thing the employee wants to read.
  (String?, Color) _note(AppPalette palette) {
    final String? remark = request.decisionRemark?.trim();
    return switch (request.status) {
      LeaveStatus.pending => ('Waiting for your manager', palette.textTertiary),
      LeaveStatus.cancelled => (
        'You cancelled this request',
        palette.textTertiary,
      ),
      LeaveStatus.rejected => (
        (remark == null || remark.isEmpty)
            ? 'Not approved by your manager'
            : 'Manager: ${remark[0].toLowerCase()}${remark.substring(1)}',
        palette.danger,
      ),
      LeaveStatus.approved ||
      LeaveStatus.unknown => (null, palette.textTertiary),
    };
  }
}

/// A white rounded card holding [LeaveRequestRow]s with hairlines between.
class LeaveRequestGroupCard extends StatelessWidget {
  const LeaveRequestGroupCard({required this.children, super.key});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: AppRadius.card,
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadow,
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        type: MaterialType.transparency,
        child: Column(
          children: <Widget>[
            for (int i = 0; i < children.length; i++) ...<Widget>[
              if (i > 0)
                Divider(
                  height: 1,
                  indent: AppSpacing.lg,
                  endIndent: AppSpacing.lg,
                  color: context.palette.border,
                ),
              children[i],
            ],
          ],
        ),
      ),
    );
  }
}
