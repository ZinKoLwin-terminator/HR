import 'dart:math' as math;

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_record.dart';
import 'package:hr_employee_app/features/attendance/presentation/providers/attendance_controllers.dart';
import 'package:material_ui/material_ui.dart';

/// The navy top of the attendance screen: title, month switcher, and the
/// month's rate, hours worked and overtime.
///
/// The figures come from the server, not from the loaded history page: the
/// list is paginated, so counting it client-side would report whatever
/// happened to be fetched.
class AttendanceMonthHeader extends StatelessWidget {
  const AttendanceMonthHeader({
    required this.period,
    required this.summary,
    required this.onPreviousMonth,
    required this.onNextMonth,
    super.key,
  });

  /// How much of [AttendanceStatTiles] sits on the navy: the screen paints
  /// a navy strip this tall behind the top of the tiles.
  static const double overlap = 36;

  final AttendancePeriod period;

  /// Null while loading, so the header keeps its shape.
  final AttendanceSummary? summary;

  final VoidCallback onPreviousMonth;
  final VoidCallback onNextMonth;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AttendanceSummary? data = summary;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: ClipRect(
        child: CustomPaint(
          painter: const _CornerHexagons(),
          child: SafeArea(
            bottom: false,
            child: Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.md,
                AppSpacing.lg,
                AppSpacing.lg,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          'Attendance',
                          style: textStyles.titleLarge?.copyWith(
                            color: onBrand,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      _MonthPill(
                        period: period,
                        onPrevious: onPreviousMonth,
                        onNext: onNextMonth,
                      ),
                    ],
                  ),
                  Gap.lg,
                  Row(
                    children: <Widget>[
                      _RateRing(rate: data?.attendanceRate),
                      Gap.wLg,
                      Expanded(
                        child: _HeaderStat(
                          label: 'Worked',
                          value: data == null
                              ? '—'
                              : DateFormatter.duration(data.totalWorkedMinutes),
                        ),
                      ),
                      Expanded(
                        child: _HeaderStat(
                          label: 'Overtime',
                          value: data == null
                              ? '—'
                              : DateFormatter.duration(
                                  data.totalOvertimeMinutes,
                                ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// `‹ Nov 2026 ›` in a glass pill. Nothing exists in the future, so the
/// forward arrow is disabled at the current month.
class _MonthPill extends StatelessWidget {
  const _MonthPill({
    required this.period,
    required this.onPrevious,
    required this.onNext,
  });

  final AttendancePeriod period;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final bool canGoForward = !period.isCurrentOrFuture;

    Widget arrow(IconData icon, String tooltip, VoidCallback? onTap) {
      return IconButton(
        onPressed: onTap,
        icon: Icon(icon),
        iconSize: AppSizes.iconMd,
        color: onBrand,
        disabledColor: onBrand.withValues(alpha: 0.3),
        tooltip: tooltip,
        visualDensity: VisualDensity.compact,
      );
    }

    return Container(
      decoration: BoxDecoration(
        color: onBrand.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(AppRadius.pill),
        border: Border.all(color: onBrand.withValues(alpha: 0.14)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          arrow(Icons.chevron_left_rounded, 'Previous month', onPrevious),
          Text(
            DateFormatter.shortMonthYear(period.asDate),
            style: Theme.of(context).textTheme.labelLarge
                ?.copyWith(color: onBrand, fontWeight: FontWeight.w700),
          ),
          arrow(
            Icons.chevron_right_rounded,
            'Next month',
            canGoForward ? onNext : null,
          ),
        ],
      ),
    );
  }
}

/// Attendance rate as a teal ring with the percentage inside.
class _RateRing extends StatelessWidget {
  const _RateRing({required this.rate});

  /// Null while loading or when the month has no working days.
  final double? rate;

  static const double _size = 68;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final double? value = rate;

    return Semantics(
      label: value == null
          ? 'Attendance rate unavailable'
          : 'Attendance rate ${(value * 100).round()} percent',
      child: ExcludeSemantics(
        child: SizedBox.square(
          dimension: _size,
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              CircularProgressIndicator(
                value: value ?? 0,
                strokeWidth: 6,
                strokeCap: StrokeCap.round,
                backgroundColor: onBrand.withValues(alpha: 0.14),
                valueColor: const AlwaysStoppedAnimation<Color>(
                  AppColors.summitTealBright,
                ),
              ),
              Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      value == null ? '—' : '${(value * 100).round()}%',
                      style: textStyles.titleMedium?.copyWith(
                        color: onBrand,
                        fontWeight: FontWeight.w800,
                        height: 1.1,
                      ),
                    ),
                    Text(
                      'Rate',
                      style: textStyles.labelSmall?.copyWith(
                        color: onBrand.withValues(alpha: 0.66),
                        fontSize: 10,
                        height: 1.1,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HeaderStat extends StatelessWidget {
  const _HeaderStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: textStyles.labelSmall?.copyWith(
            color: onBrand.withValues(alpha: 0.66),
          ),
        ),
        Gap.xxs,
        Text(
          value,
          style: textStyles.titleMedium?.copyWith(
            color: onBrand,
            fontWeight: FontWeight.w800,
          ),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ],
    );
  }
}

/// Two hairline hexagons off the top-right corner.
class _CornerHexagons extends CustomPainter {
  const _CornerHexagons();

  @override
  void paint(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2
      ..color = AppColors.textOnPrimary.withValues(alpha: 0.06);
    final Offset centre = Offset(size.width * 0.95, size.height * 0.55);
    for (final double radius in <double>[110, 160]) {
      final Path path = Path();
      for (int i = 0; i < 6; i++) {
        final double angle = math.pi / 3 * i - math.pi / 2;
        final Offset p =
            centre + Offset(math.cos(angle), math.sin(angle)) * radius;
        if (i == 0) {
          path.moveTo(p.dx, p.dy);
        } else {
          path.lineTo(p.dx, p.dy);
        }
      }
      canvas.drawPath(path..close(), paint);
    }
  }

  @override
  bool shouldRepaint(_CornerHexagons oldDelegate) => false;
}

/// Present / absent / leave / late for the month, as four tinted tiles on a
/// white card that rides up over the header.
class AttendanceStatTiles extends StatelessWidget {
  const AttendanceStatTiles({required this.summary, super.key});

  /// Null while loading; tiles show an em dash rather than a misleading 0.
  final AttendanceSummary? summary;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final AttendanceSummary? data = summary;

    return Container(
      padding: EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadowLift,
            blurRadius: 20,
            offset: Offset(0, 6),
          ),
        ],
      ),
      child: Row(
        children: <Widget>[
          _StatTile(
            label: 'Present',
            value: data?.presentDays,
            tone: palette.success,
            surface: palette.successSurface,
          ),
          Gap.wSm,
          _StatTile(
            label: 'Absent',
            value: data?.absentDays,
            tone: palette.danger,
            surface: palette.dangerSurface,
          ),
          Gap.wSm,
          _StatTile(
            label: 'Leave',
            value: data?.leaveDays,
            tone: palette.info,
            surface: palette.infoSurface,
          ),
          Gap.wSm,
          _StatTile(
            label: 'Late',
            value: data?.lateArrivals,
            tone: palette.warning,
            surface: palette.warningSurface,
          ),
        ],
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({
    required this.label,
    required this.value,
    required this.tone,
    required this.surface,
  });

  final String label;
  final int? value;
  final Color tone;
  final Color surface;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Expanded(
      child: Semantics(
        label: '$label, ${value ?? 'loading'}',
        child: ExcludeSemantics(
          child: Container(
            padding: EdgeInsets.symmetric(vertical: AppSpacing.sm + 2),
            decoration: BoxDecoration(
              color: surface,
              borderRadius: AppRadius.field,
            ),
            child: Column(
              children: <Widget>[
                Text(
                  value?.toString() ?? '—',
                  style: textStyles.titleLarge?.copyWith(
                    color: tone,
                    fontWeight: FontWeight.w800,
                    height: 1.1,
                  ),
                ),
                Gap.xxs,
                Text(
                  label,
                  style: textStyles.labelSmall?.copyWith(
                    color: Color.lerp(tone, Colors.black, 0.2),
                    fontWeight: FontWeight.w600,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
