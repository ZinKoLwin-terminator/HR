import 'dart:async';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_branding.dart';
import 'package:material_ui/material_ui.dart';

/// First-load state for a whole page: the page's own skeleton, blurred and
/// veiled, with a spinning teal ring around the Apex mark and a short line
/// saying what is happening.
///
/// No card: the ring, mark and words sit straight on the veil, which is why
/// the veil is fairly opaque and the blur fairly strong — the words must not
/// tangle with the skeleton rows behind them.
///
/// The steps advance on a timer and stop at the last one; they describe the
/// kind of work the screen is waiting on, not measured progress. Nothing here
/// knows how far along the request is.
class AppPageLoader extends StatefulWidget {
  const AppPageLoader({
    super.key,
    this.background,
    this.messages = defaultMessages,
  });

  /// What the page will look like, shown blurred behind the loader.
  final Widget? background;

  /// One line per step, shown in turn.
  final List<String> messages;

  static const List<String> defaultMessages = <String>[
    'Connecting to HR…',
    'Fetching your details…',
    'Almost there…',
  ];

  /// How long each step is shown before the next.
  static const Duration stepDuration = Duration(milliseconds: 1400);

  @override
  State<AppPageLoader> createState() => _AppPageLoaderState();
}

class _AppPageLoaderState extends State<AppPageLoader>
    with SingleTickerProviderStateMixin {
  late final AnimationController _spin = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1100),
  );
  Timer? _stepper;
  int _step = 0;

  @override
  void initState() {
    super.initState();
    _stepper = Timer.periodic(AppPageLoader.stepDuration, (Timer timer) {
      if (_step >= widget.messages.length - 1) {
        timer.cancel();
        return;
      }
      setState(() => _step += 1);
    });
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Reduce Motion: a still ring, not a spinning one.
    if (MediaQuery.disableAnimationsOf(context)) {
      _spin.stop();
    } else if (!_spin.isAnimating) {
      _spin.repeat();
    }
  }

  @override
  void dispose() {
    _stepper?.cancel();
    _spin.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final List<String> messages = widget.messages.isEmpty
        ? AppPageLoader.defaultMessages
        : widget.messages;
    final int step = math.min(_step, messages.length - 1);

    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        if (widget.background != null)
          ExcludeSemantics(
            child: IgnorePointer(
              child: ImageFiltered(
                imageFilter: ui.ImageFilter.blur(sigmaX: 7, sigmaY: 7),
                child: widget.background,
              ),
            ),
          ),
        ColoredBox(
          color: Theme.of(context).scaffoldBackgroundColor
              .withValues(alpha: 0.62),
        ),
        Center(
          child: Semantics(
            liveRegion: true,
            label: messages[step],
            child: ExcludeSemantics(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  SizedBox.square(
                    dimension: 92,
                    child: AnimatedBuilder(
                      animation: _spin,
                      builder: (BuildContext context, Widget? child) =>
                          CustomPaint(
                            painter: _RingPainter(
                              turn: _spin.value,
                              track: context.palette.border,
                            ),
                            child: child,
                          ),
                      child: Center(
                        child: Container(
                          height: 68,
                          width: 68,
                          decoration: const BoxDecoration(
                            color: AppColors.surface,
                            shape: BoxShape.circle,
                            boxShadow: <BoxShadow>[
                              BoxShadow(
                                color: AppColors.shadow,
                                blurRadius: 12,
                                offset: Offset(0, 4),
                              ),
                            ],
                          ),
                          alignment: Alignment.center,
                          child: const AppLogo(size: 40),
                        ),
                      ),
                    ),
                  ),
                  Gap.lg,
                  Text(
                    'STEP ${step + 1} OF ${messages.length}',
                    style: textStyles.labelSmall?.copyWith(
                      fontSize: 10,
                      letterSpacing: 1.4,
                      fontWeight: FontWeight.w800,
                      color: Color.lerp(
                        AppColors.summitTeal,
                        Colors.black,
                        0.15,
                      ),
                    ),
                  ),
                  Gap.xs,
                  AnimatedSwitcher(
                    duration: const Duration(milliseconds: 250),
                    child: Text(
                      messages[step],
                      key: ValueKey<int>(step),
                      textAlign: TextAlign.center,
                      style: textStyles.titleSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                        color: context.palette.textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// A light track with a teal arc chasing round it, a dot at the arc's head.
class _RingPainter extends CustomPainter {
  const _RingPainter({required this.turn, required this.track});

  /// 0 to 1, one full revolution.
  final double turn;

  final Color track;

  static const double _stroke = 4;
  static const double _sweep = math.pi * 0.55;

  @override
  void paint(Canvas canvas, Size size) {
    final Offset centre = size.center(Offset.zero);
    final double radius = size.shortestSide / 2 - _stroke;
    final Rect rect = Rect.fromCircle(center: centre, radius: radius);

    canvas.drawCircle(
      centre,
      radius,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _stroke
        ..color = track,
    );

    final double start = turn * 2 * math.pi - math.pi / 2;
    canvas.drawArc(
      rect,
      start,
      _sweep,
      false,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _stroke
        ..strokeCap = StrokeCap.round
        ..shader = SweepGradient(
          startAngle: 0,
          endAngle: _sweep,
          colors: <Color>[
            AppColors.summitTeal.withValues(alpha: 0.1),
            AppColors.summitTeal,
          ],
          transform: GradientRotation(start),
        ).createShader(rect),
    );

    final double head = start + _sweep;
    canvas.drawCircle(
      centre + Offset(math.cos(head), math.sin(head)) * radius,
      _stroke * 1.25,
      Paint()..color = AppColors.summitTeal,
    );
  }

  @override
  bool shouldRepaint(_RingPainter oldDelegate) =>
      oldDelegate.turn != turn || oldDelegate.track != track;
}
