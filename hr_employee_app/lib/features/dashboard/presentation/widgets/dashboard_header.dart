import 'dart:async';
import 'dart:math' as math;

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_avatar.dart';
import 'package:hr_employee_app/features/attendance/domain/entities/attendance_status.dart';
import 'package:hr_employee_app/features/dashboard/domain/entities/dashboard_summary.dart';
import 'package:material_ui/material_ui.dart';

/// The navy top of the home screen: who you are, what time it is, and the
/// one thing most people open the app to do — punch in or out.
///
/// It paints under the status bar and takes the safe-area inset itself, so
/// the band reaches the top edge. Its foot leaves [overlap] of clearance for
/// the body sheet that rides up over it.
class DashboardHeader extends StatelessWidget {
  const DashboardHeader({
    required this.profile,
    required this.unreadCount,
    required this.attendance,
    required this.isPunching,
    required this.onCheckIn,
    required this.onCheckOut,
    required this.onViewAttendance,
    required this.onNotificationsTap,
    required this.onProfileTap,
    super.key,
  });

  /// How far the body sheet rides up over the header's foot.
  static const double overlap = 24;

  final EmployeeHeadline profile;
  final int unreadCount;

  /// Null before the first punch of the day.
  final TodayAttendance? attendance;

  /// True while a punch is in flight: the button shows a spinner and is
  /// detached, so a second tap cannot double-submit.
  final bool isPunching;

  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;
  final VoidCallback onViewAttendance;
  final VoidCallback onNotificationsTap;
  final VoidCallback onProfileTap;

  @override
  Widget build(BuildContext context) {
    final CheckInAvailability availability =
        attendance?.availability ?? CheckInAvailability.canCheckIn;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.xl + overlap,
          ),
          child: Column(
            children: <Widget>[
              _IdentityRow(
                profile: profile,
                unreadCount: unreadCount,
                onNotificationsTap: onNotificationsTap,
                onProfileTap: onProfileTap,
              ),
              Gap.md,
              const _Clock(),
              Gap.sm,
              _PunchStage(
                availability: availability,
                isPunching: isPunching,
                onCheckIn: onCheckIn,
                onCheckOut: onCheckOut,
              ),
              Gap.sm,
              Text(
                _statusLine(attendance, availability),
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: AppColors.textOnPrimary.withValues(alpha: 0.78),
                  fontWeight: FontWeight.w500,
                ),
              ),
              Gap.lg,
              _TimeTiles(attendance: attendance, onTap: onViewAttendance),
            ],
          ),
        ),
      ),
    );
  }

  static String _statusLine(
    TodayAttendance? today,
    CheckInAvailability availability,
  ) {
    switch (availability) {
      case CheckInAvailability.canCheckIn:
        return "You haven't checked in yet";
      case CheckInAvailability.canCheckOut:
        return today!.isLateArrival
            ? "You're checked in · late today"
            : "You're checked in · have a good day";
      case CheckInAvailability.completed:
        return 'Day complete · see you tomorrow';
      case CheckInAvailability.notRequired:
        return 'No attendance needed today';
    }
  }
}

/// Avatar, greeting and name, and the notification bell.
class _IdentityRow extends StatelessWidget {
  const _IdentityRow({
    required this.profile,
    required this.unreadCount,
    required this.onNotificationsTap,
    required this.onProfileTap,
  });

  final EmployeeHeadline profile;
  final int unreadCount;
  final VoidCallback onNotificationsTap;
  final VoidCallback onProfileTap;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    const Color onBrand = AppColors.textOnPrimary;

    return Row(
      children: <Widget>[
        AppAvatar(
          name: profile.fullName,
          imageUrl: profile.profilePhotoUrl,
          size: AppSizes.avatarSm + 4,
          borderColor: onBrand.withValues(alpha: 0.24),
          onTap: onProfileTap,
        ),
        Gap.wSm,
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                _greeting(),
                style: textStyles.bodySmall?.copyWith(
                  color: onBrand.withValues(alpha: 0.72),
                  height: 1.2,
                ),
              ),
              Text(
                profile.fullName,
                style: textStyles.titleMedium?.copyWith(
                  color: onBrand,
                  fontWeight: FontWeight.w700,
                  height: 1.2,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
        Gap.wSm,
        _NotificationBell(unreadCount: unreadCount, onTap: onNotificationsTap),
      ],
    );
  }

  /// Time-of-day greeting, from the device clock.
  static String _greeting() {
    final int hour = DateTime.now().hour;
    if (hour < 12) {
      return 'Good morning';
    }
    if (hour < 17) {
      return 'Good afternoon';
    }
    return 'Good evening';
  }
}

/// Today's date and the time, ticking over on the minute.
class _Clock extends StatefulWidget {
  const _Clock();

  @override
  State<_Clock> createState() => _ClockState();
}

class _ClockState extends State<_Clock> {
  DateTime _now = DateTime.now();
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _scheduleTick();
  }

  /// Wakes on the next minute boundary rather than every second: the face
  /// shows no seconds, so anything more often is wasted rebuilds.
  void _scheduleTick() {
    final DateTime now = DateTime.now();
    final Duration untilNextMinute = Duration(
      seconds: 60 - now.second,
      milliseconds: -now.millisecond,
    );
    _timer = Timer(untilNextMinute, () {
      if (!mounted) {
        return;
      }
      setState(() => _now = DateTime.now());
      _scheduleTick();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Semantics(
      liveRegion: false,
      child: Column(
        children: <Widget>[
          Text(
            DateFormatter.weekdayDayMonthLong(_now),
            style: textStyles.bodySmall?.copyWith(
              color: onBrand.withValues(alpha: 0.72),
            ),
          ),
          Text(
            DateFormatter.time(_now),
            style: textStyles.displaySmall?.copyWith(
              color: onBrand,
              fontWeight: FontWeight.w800,
              letterSpacing: -1,
              height: 1.1,
              fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
            ),
          ),
        ],
      ),
    );
  }
}

/// The round punch button, centred in a nest of faint hexagons.
class _PunchStage extends StatelessWidget {
  const _PunchStage({
    required this.availability,
    required this.isPunching,
    required this.onCheckIn,
    required this.onCheckOut,
  });

  final CheckInAvailability availability;
  final bool isPunching;
  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;

  static const double _buttonSize = 116;
  static const double _stageHeight = 150;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: _stageHeight,
      width: double.infinity,
      child: CustomPaint(
        painter: const _HexagonRings(),
        child: Center(
          child: _PunchHalo(
            size: _buttonSize,
            // Check in, check out and Done all wear the breathing frame;
            // only the day off is still and unframed.
            framed: availability != CheckInAvailability.notRequired,
            breathing: availability != CheckInAvailability.notRequired,
            child: _PunchButton(
              size: _buttonSize,
              availability: availability,
              isPunching: isPunching,
              onCheckIn: onCheckIn,
              onCheckOut: onCheckOut,
            ),
          ),
        ),
      ),
    );
  }
}

/// The frame round the punch button: a teal band hugging it, a dashed ring
/// a little way out, and a faint hairline beyond that.
///
/// When [breathing] (check in, check out and Done), the teal band slowly
/// swells and brightens and settles back, so the control looks alive. It
/// holds still when the system asks for reduced motion.
class _PunchHalo extends StatefulWidget {
  const _PunchHalo({
    required this.size,
    required this.framed,
    required this.breathing,
    required this.child,
  });

  final double size;
  final bool framed;
  final bool breathing;
  final Widget child;

  @override
  State<_PunchHalo> createState() => _PunchHaloState();
}

class _PunchHaloState extends State<_PunchHalo>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1400),
  );

  late final Animation<double> _breath = CurvedAnimation(
    parent: _controller,
    curve: Curves.easeInOut,
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _sync();
  }

  @override
  void didUpdateWidget(_PunchHalo oldWidget) {
    super.didUpdateWidget(oldWidget);
    _sync();
  }

  void _sync() {
    final bool reduceMotion = MediaQuery.disableAnimationsOf(context);
    if (widget.breathing && !reduceMotion) {
      if (!_controller.isAnimating) {
        _controller.repeat(reverse: true);
      }
    } else {
      _controller
        ..stop()
        ..value = 0;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return CustomPaint(
      painter: _HaloPainter(
        breath: _breath,
        buttonRadius: widget.size / 2,
        framed: widget.framed,
      ),
      child: widget.child,
    );
  }
}

class _HaloPainter extends CustomPainter {
  _HaloPainter({
    required this.breath,
    required this.buttonRadius,
    required this.framed,
  }) : super(repaint: breath);

  /// 0 at rest, 1 at the top of a breath.
  final Animation<double> breath;
  final double buttonRadius;
  final bool framed;

  @override
  void paint(Canvas canvas, Size size) {
    if (!framed) {
      return;
    }
    const Color onBrand = AppColors.textOnPrimary;
    final Offset centre = size.center(Offset.zero);
    final double t = breath.value;

    // Teal band: at rest it sits just inside the dashed ring; on a breath it
    // thickens out to meet it and brightens.
    final double bandWidth = 6 + 4 * t;
    canvas.drawCircle(
      centre,
      buttonRadius + bandWidth / 2,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = bandWidth
        ..color = AppColors.summitTeal.withValues(alpha: 0.55 + 0.35 * t),
    );

    // Dashed ring: short arcs round the circle.
    const int dashes = 72;
    const double sweep = 2 * math.pi / dashes;
    final Rect dashedRect = Rect.fromCircle(
      center: centre,
      radius: buttonRadius + 16,
    );
    final Paint dashPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1
      ..color = onBrand.withValues(alpha: 0.30);
    for (int i = 0; i < dashes; i++) {
      canvas.drawArc(dashedRect, i * sweep, sweep * 0.5, false, dashPaint);
    }

    canvas.drawCircle(
      centre,
      buttonRadius + 26,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..color = onBrand.withValues(alpha: 0.12),
    );
  }

  @override
  bool shouldRepaint(_HaloPainter oldDelegate) =>
      oldDelegate.framed != framed || oldDelegate.buttonRadius != buttonRadius;
}

/// Concentric hexagon hairlines — the mark's frame, repeated outward.
class _HexagonRings extends CustomPainter {
  const _HexagonRings();

  @override
  void paint(Canvas canvas, Size size) {
    final Offset centre = size.center(Offset.zero);
    const List<(double, double)> rings = <(double, double)>[
      (74, 0.12),
      (102, 0.08),
      (132, 0.05),
    ];
    for (final (double radius, double alpha) in rings) {
      canvas.drawPath(
        _hexagon(centre, radius),
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.2
          ..strokeJoin = StrokeJoin.round
          ..color = AppColors.textOnPrimary.withValues(alpha: alpha),
      );
    }
  }

  static Path _hexagon(Offset centre, double radius) {
    final Path path = Path();
    for (int i = 0; i < 6; i++) {
      final double angle = math.pi / 3 * i - math.pi / 2;
      final Offset point =
          centre + Offset(math.cos(angle), math.sin(angle)) * radius;
      if (i == 0) {
        path.moveTo(point.dx, point.dy);
      } else {
        path.lineTo(point.dx, point.dy);
      }
    }
    return path..close();
  }

  @override
  bool shouldRepaint(_HexagonRings oldDelegate) => false;
}

/// One round control for all four states of [CheckInAvailability].
///
/// Check in is Summit Teal, the brightest thing on the screen; check out is
/// white, so the two cannot be confused at a glance; the finished and
/// day-off states are glass and do nothing when tapped.
class _PunchButton extends StatelessWidget {
  const _PunchButton({
    required this.size,
    required this.availability,
    required this.isPunching,
    required this.onCheckIn,
    required this.onCheckOut,
  });

  final double size;
  final CheckInAvailability availability;
  final bool isPunching;
  final VoidCallback onCheckIn;
  final VoidCallback onCheckOut;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    final (
      String label,
      IconData icon,
      Gradient? fill,
      Color glass,
      Color ink,
      VoidCallback? onTap,
    ) = switch (availability) {
      CheckInAvailability.canCheckIn => (
        'Check in',
        Icons.fingerprint_rounded,
        const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[AppColors.summitTealBright, AppColors.summitTeal],
        ),
        Colors.transparent,
        onBrand,
        onCheckIn,
      ),
      CheckInAvailability.canCheckOut => (
        'Check out',
        Icons.fingerprint_rounded,
        const LinearGradient(colors: <Color>[onBrand, AppColors.cloud]),
        Colors.transparent,
        AppColors.apexNavy,
        onCheckOut,
      ),
      CheckInAvailability.completed => (
        'Done',
        Icons.fingerprint_rounded,
        null,
        // Lighter glass than "Day off", so it reads against the teal band.
        onBrand.withValues(alpha: 0.22),
        onBrand,
        null,
      ),
      CheckInAvailability.notRequired => (
        'Day off',
        Icons.beach_access_outlined,
        null,
        onBrand.withValues(alpha: 0.12),
        onBrand,
        null,
      ),
    };
    final bool isActionable = onTap != null;
    final bool isFramed = availability != CheckInAvailability.notRequired;

    return Semantics(
      button: isActionable,
      enabled: isActionable && !isPunching,
      label: label,
      child: ExcludeSemantics(
        child: Container(
          height: size,
          width: size,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: fill,
            color: fill == null ? glass : null,
            // Punch states are framed by the halo's teal band, so the
            // button itself has no edge or glow of its own.
            border: isFramed
                ? null
                : Border.all(
                    color: onBrand.withValues(
                      alpha: isActionable ? 0.28 : 0.18,
                    ),
                    width: isActionable ? 4 : 1.5,
                  ),
            boxShadow: isActionable && !isFramed
                ? <BoxShadow>[
                    BoxShadow(
                      color:
                          (availability == CheckInAvailability.canCheckIn
                                  ? AppColors.summitTeal
                                  : onBrand)
                              .withValues(alpha: 0.35),
                      blurRadius: 36,
                      spreadRadius: 2,
                    ),
                  ]
                : null,
          ),
          child: Material(
            type: MaterialType.transparency,
            shape: const CircleBorder(),
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: isPunching ? null : onTap,
              child: Center(
                // While punching: the spinner where the icon was, and what is
                // happening where the label was — the circle keeps its shape.
                child: isPunching
                    ? Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          SizedBox.square(
                            dimension: 28,
                            child: CircularProgressIndicator(
                              strokeWidth: 2.5,
                              color: ink,
                            ),
                          ),
                          Gap.sm,
                          Text(
                            availability == CheckInAvailability.canCheckOut
                                ? 'Checking out'
                                : 'Checking in',
                            style: Theme.of(context).textTheme.labelMedium
                                ?.copyWith(
                                  color: ink,
                                  fontWeight: FontWeight.w700,
                                ),
                          ),
                        ],
                      )
                    : Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Icon(icon, size: 30, color: ink),
                          Gap.xs,
                          Text(
                            label,
                            style: Theme.of(context).textTheme.titleSmall
                                ?.copyWith(
                                  color: ink,
                                  fontWeight: FontWeight.w700,
                                ),
                          ),
                        ],
                      ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Check-in, check-out and worked time, as three glass tiles. Tapping any of
/// them opens the attendance history.
class _TimeTiles extends StatelessWidget {
  const _TimeTiles({required this.attendance, required this.onTap});

  final TodayAttendance? attendance;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final TodayAttendance? today = attendance;

    return Semantics(
      button: true,
      label: 'Attendance history',
      child: Row(
        children: <Widget>[
          Expanded(
            child: _TimeTile(
              label: 'Check in',
              value: today?.checkInAt == null
                  ? null
                  : DateFormatter.time(today!.checkInAt!),
              flag: (today?.isLateArrival ?? false) ? 'Late' : null,
              onTap: onTap,
            ),
          ),
          Gap.wSm,
          Expanded(
            child: _TimeTile(
              label: 'Check out',
              value: today?.checkOutAt == null
                  ? null
                  : DateFormatter.time(today!.checkOutAt!),
              flag: (today?.isEarlyDeparture ?? false) ? 'Early' : null,
              onTap: onTap,
            ),
          ),
          Gap.wSm,
          Expanded(
            child: _TimeTile(
              label: 'Worked',
              value: (today?.workedMinutes ?? 0) > 0
                  ? DateFormatter.duration(today!.workedMinutes)
                  : null,
              onTap: onTap,
            ),
          ),
        ],
      ),
    );
  }
}

class _TimeTile extends StatelessWidget {
  const _TimeTile({
    required this.label,
    required this.value,
    required this.onTap,
    this.flag,
  });

  final String label;

  /// Null when nothing is recorded yet; shown as an em dash, not "00:00".
  final String? value;

  final String? flag;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Material(
      color: onBrand.withValues(alpha: 0.08),
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.field,
        side: BorderSide(color: onBrand.withValues(alpha: 0.10)),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: EdgeInsets.symmetric(
            horizontal: AppSpacing.sm + 2,
            vertical: AppSpacing.sm + 2,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Flexible(
                    child: Text(
                      label,
                      style: textStyles.labelSmall?.copyWith(
                        color: onBrand.withValues(alpha: 0.66),
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  if (flag != null) ...<Widget>[
                    Gap.wXs,
                    Text(
                      flag!,
                      style: textStyles.labelSmall?.copyWith(
                        color: AppColors.warning,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ],
              ),
              Gap.xxs,
              Text(
                value ?? '—',
                style: textStyles.titleSmall?.copyWith(
                  color: onBrand,
                  fontWeight: FontWeight.w700,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Bell in a glass circle, with an unread badge.
class _NotificationBell extends StatelessWidget {
  const _NotificationBell({required this.unreadCount, required this.onTap});

  final int unreadCount;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final bool hasUnread = unreadCount > 0;

    return Semantics(
      button: true,
      label: hasUnread ? 'Notifications, $unreadCount unread' : 'Notifications',
      child: ExcludeSemantics(
        child: Material(
          color: onBrand.withValues(alpha: 0.12),
          shape: RoundedRectangleBorder(
            borderRadius: AppRadius.field,
            side: BorderSide(color: onBrand.withValues(alpha: 0.14)),
          ),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: onTap,
            child: SizedBox.square(
              dimension: 40,
              child: Stack(
                clipBehavior: Clip.none,
                alignment: Alignment.center,
                children: <Widget>[
                  Icon(
                    Icons.notifications_none_rounded,
                    color: onBrand,
                    size: AppSizes.iconMd,
                  ),
                  if (hasUnread)
                    Positioned(
                      top: 5,
                      right: 5,
                      child: Container(
                        constraints: const BoxConstraints(
                          minWidth: 16,
                          minHeight: 16,
                        ),
                        padding: const EdgeInsets.symmetric(horizontal: 3),
                        decoration: BoxDecoration(
                          color: AppColors.error,
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(
                            color: AppColors.apexNavy,
                            width: 1.5,
                          ),
                        ),
                        child: Center(
                          child: Text(
                            unreadCount > 99 ? '99+' : '$unreadCount',
                            style: const TextStyle(
                              color: onBrand,
                              fontSize: 9,
                              fontWeight: FontWeight.w700,
                              height: 1,
                            ),
                          ),
                        ),
                      ),
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
