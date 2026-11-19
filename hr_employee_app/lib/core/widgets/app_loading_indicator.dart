import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// The app's loading spinner, centred in the available space.
class AppLoadingIndicator extends StatelessWidget {
  const AppLoadingIndicator({super.key, this.message, this.size});

  final String? message;
  final double? size;

  @override
  Widget build(BuildContext context) {
    final double dimension = size ?? 28;

    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          SizedBox(
            height: dimension,
            width: dimension,
            child: const CircularProgressIndicator(strokeWidth: 2.5),
          ),
          if (message != null) ...<Widget>[
            Gap.md,
            Text(
              message!,
              style: Theme.of(context).textTheme.bodySmall,
              textAlign: TextAlign.center,
            ),
          ],
        ],
      ),
    );
  }
}

/// A grey block that stands in for content while it loads.
///
/// Preferred over a spinner for list and card content: the page keeps its
/// shape, so nothing jumps when the data arrives.
class AppSkeleton extends StatefulWidget {
  const AppSkeleton({
    required this.height,
    super.key,
    this.width,
    this.borderRadius,
  });

  /// A single line of text.
  const AppSkeleton.text({super.key, this.width, double? height})
    : height = height ?? 14,
      borderRadius = null;

  final double height;
  final double? width;
  final BorderRadius? borderRadius;

  @override
  State<AppSkeleton> createState() => _AppSkeletonState();
}

class _AppSkeletonState extends State<AppSkeleton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1100),
  )..repeat(reverse: true);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final Color base = context.palette.surfaceMuted;

    return FadeTransition(
      opacity: Tween<double>(
        begin: 0.45,
        end: 1,
      ).animate(CurvedAnimation(parent: _controller, curve: Curves.easeInOut)),
      child: Container(
        height: widget.height,
        width: widget.width ?? double.infinity,
        decoration: BoxDecoration(
          color: base,
          borderRadius: widget.borderRadius ?? AppRadius.field,
        ),
      ),
    );
  }
}

/// A card-shaped skeleton, used while a list of cards loads.
class AppSkeletonCard extends StatelessWidget {
  const AppSkeletonCard({super.key, this.lines = 3});

  final int lines;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: AppSpacing.cardPadding,
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: AppRadius.card,
        border: Border.all(color: context.palette.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          for (int i = 0; i < lines; i++) ...<Widget>[
            AppSkeleton.text(
              // Vary the widths so the block reads as text, not as bars.
              width: i == 0 ? 140 : (i.isEven ? double.infinity : 200),
            ),
            if (i < lines - 1) Gap.sm,
          ],
        ],
      ),
    );
  }
}

/// A full-screen blocking overlay, used while a submission is in flight.
///
/// Only for operations the user must not interrupt, such as a check-in that
/// has already reached the server.
class AppBlockingLoader extends StatelessWidget {
  const AppBlockingLoader({super.key, this.message});

  final String? message;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black.withValues(alpha: 0.35),
      child: Center(
        child: Container(
          padding: EdgeInsets.all(AppSpacing.xxl),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surface,
            borderRadius: AppRadius.card,
          ),
          child: AppLoadingIndicator(message: message),
        ),
      ),
    );
  }
}
