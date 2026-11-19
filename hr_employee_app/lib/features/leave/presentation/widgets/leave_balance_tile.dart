import 'dart:math' as math;

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_overview.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// Colours and caption for one balance, worked out once so the tile and the
/// "View all" sheet cannot describe the same balance differently.
class LeaveBalanceLook {
  const LeaveBalanceLook._({
    required this.balance,
    required this.state,
    required this.accent,
    required this.caption,
    required this.captionColour,
  });

  factory LeaveBalanceLook.of(BuildContext context, LeaveBalance balance) {
    final LeaveBalanceState state = LeaveBalanceState.of(balance);
    final AppPalette palette = context.palette;
    final Color typeColour = ColorUtils.fromHex(
      balance.colorHex,
      fallback: Theme.of(context).colorScheme.primary,
    );
    final String entitled = DayCountFormatter.formatNumber(
      balance.entitledDays,
    );

    final (Color accent, String caption, Color captionColour) = switch (state) {
      LeaveBalanceState.noBalance => (
        palette.textTertiary,
        'No balance',
        palette.textTertiary,
      ),
      LeaveBalanceState.usedUp => (palette.danger, 'Used up', palette.danger),
      LeaveBalanceState.almostUsedUp => (
        typeColour,
        'Almost used up',
        palette.warning,
      ),
      LeaveBalanceState.pending => (
        typeColour,
        'of $entitled · '
            '${DayCountFormatter.formatNumber(balance.pendingDays)} pending',
        palette.warning,
      ),
      LeaveBalanceState.available => (
        typeColour,
        'of ${DayCountFormatter.format(balance.entitledDays)}',
        palette.textSecondary,
      ),
    };
    return LeaveBalanceLook._(
      balance: balance,
      state: state,
      accent: accent,
      caption: caption,
      captionColour: captionColour,
    );
  }

  final LeaveBalance balance;
  final LeaveBalanceState state;

  /// Ring, bar and figure colour: the type's own colour unless the balance
  /// is used up (red) or there is none (grey).
  final Color accent;
  final String caption;
  final Color captionColour;

  /// Share of the entitlement still left, 0 to 1.
  double get leftFraction => balance.entitledDays <= 0
      ? 0
      : (balance.remainingDays / balance.entitledDays).clamp(0.0, 1.0);

  /// The big figure: days left, or a dash when there is no entitlement.
  String get figure => state == LeaveBalanceState.noBalance
      ? '—'
      : DayCountFormatter.formatNumber(math.max(0, balance.remainingDays));
}

/// A ring showing how much of something is left, with a label inside.
class LeaveRing extends StatelessWidget {
  const LeaveRing({
    required this.fraction,
    required this.colour,
    required this.child,
    super.key,
    this.size = 48,
    this.strokeWidth = 5,
  });

  final double fraction;
  final Color colour;
  final Widget child;
  final double size;
  final double strokeWidth;

  @override
  Widget build(BuildContext context) {
    return SizedBox.square(
      dimension: size,
      child: CustomPaint(
        painter: _RingPainter(
          fraction: fraction,
          colour: colour,
          track: colour.withValues(alpha: 0.14),
          strokeWidth: strokeWidth,
        ),
        child: Center(child: child),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  const _RingPainter({
    required this.fraction,
    required this.colour,
    required this.track,
    required this.strokeWidth,
  });

  final double fraction;
  final Color colour;
  final Color track;
  final double strokeWidth;

  @override
  void paint(Canvas canvas, Size size) {
    final Rect rect = (Offset.zero & size).deflate(strokeWidth / 2);
    canvas.drawArc(
      rect,
      0,
      math.pi * 2,
      false,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = strokeWidth
        ..color = track,
    );
    if (fraction <= 0) {
      return;
    }
    canvas.drawArc(
      rect,
      -math.pi / 2,
      math.pi * 2 * fraction,
      false,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = strokeWidth
        ..strokeCap = StrokeCap.round
        ..color = colour,
    );
  }

  @override
  bool shouldRepaint(_RingPainter oldDelegate) =>
      oldDelegate.fraction != fraction ||
      oldDelegate.colour != colour ||
      oldDelegate.track != track;
}

/// One leave type in the leave screen's horizontal strip: a ring with the
/// days left, the type's name, and a caption coloured by its state.
class LeaveTypeTile extends StatelessWidget {
  const LeaveTypeTile({
    required this.balance,
    required this.width,
    required this.onTap,
    super.key,
  });

  final LeaveBalance balance;
  final double width;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final LeaveBalanceLook look = LeaveBalanceLook.of(context, balance);
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String name = shortLeaveTypeName(balance.leaveTypeName);

    return SizedBox(
      width: width,
      child: Semantics(
        button: true,
        label:
            '$name: ${look.figure} days left, ${look.caption}. Apply for '
            '$name leave',
        child: ExcludeSemantics(
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surface,
              borderRadius: AppRadius.card,
              boxShadow: const <BoxShadow>[
                BoxShadow(
                  color: AppColors.shadowLift,
                  blurRadius: 18,
                  offset: Offset(0, 6),
                ),
              ],
            ),
            child: Material(
              type: MaterialType.transparency,
              borderRadius: AppRadius.card,
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: onTap,
                child: Padding(
                  padding: EdgeInsets.symmetric(
                    horizontal: AppSpacing.xs + 2,
                    vertical: AppSpacing.md,
                  ),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      LeaveRing(
                        fraction: look.leftFraction,
                        colour: look.accent,
                        child: Text(
                          look.figure,
                          style: textStyles.titleSmall?.copyWith(
                            fontWeight: FontWeight.w800,
                            color: look.state == LeaveBalanceState.usedUp
                                ? look.accent
                                : context.palette.textPrimary,
                          ),
                        ),
                      ),
                      Gap.sm,
                      Text(
                        name,
                        style: textStyles.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      Gap.xxs,
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        child: Text(
                          look.caption,
                          style: textStyles.labelSmall?.copyWith(
                            color: look.captionColour,
                            fontWeight: FontWeight.w500,
                          ),
                          maxLines: 1,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Every leave type as a list, in a bottom sheet. Tapping one calls
/// [onSelect] after the sheet has closed.
Future<void> showLeaveTypesSheet(
  BuildContext context, {
  required List<LeaveBalance> balances,
  required ValueChanged<LeaveBalance> onSelect,
}) async {
  final LeaveBalance? chosen = await showModalBottomSheet<LeaveBalance>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    useRootNavigator: true,
    backgroundColor: Theme.of(context).colorScheme.surface,
    constraints: BoxConstraints(
      maxHeight: MediaQuery.sizeOf(context).height * 0.85,
    ),
    builder: (BuildContext sheetContext) =>
        _LeaveTypesSheet(balances: balances),
  );
  if (chosen != null) {
    onSelect(chosen);
  }
}

class _LeaveTypesSheet extends StatelessWidget {
  const _LeaveTypesSheet({required this.balances});

  final List<LeaveBalance> balances;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return SafeArea(
      top: false,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Padding(
            padding: EdgeInsets.fromLTRB(
              AppSpacing.lg,
              0,
              AppSpacing.lg,
              AppSpacing.xs,
            ),
            child: Text('Your leave types', style: textStyles.titleMedium),
          ),
          Padding(
            padding: EdgeInsets.fromLTRB(
              AppSpacing.lg,
              0,
              AppSpacing.lg,
              AppSpacing.sm,
            ),
            child: Text(
              'Days left this year. Tap a type to apply.',
              style: textStyles.bodySmall,
            ),
          ),
          Flexible(
            child: ListView.separated(
              shrinkWrap: true,
              padding: EdgeInsets.fromLTRB(
                AppSpacing.sm,
                0,
                AppSpacing.sm,
                AppSpacing.lg,
              ),
              itemCount: balances.length,
              separatorBuilder: (BuildContext context, int index) => Divider(
                height: 1,
                indent: AppSpacing.sm,
                endIndent: AppSpacing.sm,
                color: context.palette.border,
              ),
              itemBuilder: (BuildContext context, int index) => _LeaveTypeRow(
                balance: balances[index],
                onTap: () => Navigator.of(context).pop(balances[index]),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LeaveTypeRow extends StatelessWidget {
  const _LeaveTypeRow({required this.balance, required this.onTap});

  final LeaveBalance balance;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final LeaveBalanceLook look = LeaveBalanceLook.of(context, balance);
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isDark = Theme.of(context).brightness == Brightness.dark;
    final bool hasEntitlement = look.state != LeaveBalanceState.noBalance;

    return InkWell(
      onTap: onTap,
      borderRadius: AppRadius.field,
      child: Padding(
        padding: EdgeInsets.symmetric(
          horizontal: AppSpacing.sm,
          vertical: AppSpacing.md,
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: AppSizes.iconBackplate,
              height: AppSizes.iconBackplate,
              decoration: BoxDecoration(
                color: ColorUtils.surfaceTint(look.accent, isDark: isDark),
                borderRadius: AppRadius.field,
              ),
              child: Center(
                child: Container(
                  width: 10,
                  height: 10,
                  decoration: BoxDecoration(
                    color: look.accent,
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
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          balance.leaveTypeName,
                          style: textStyles.titleSmall,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      Gap.wSm,
                      Text.rich(
                        TextSpan(
                          children: <InlineSpan>[
                            TextSpan(
                              text: look.figure,
                              style: textStyles.titleSmall?.copyWith(
                                fontWeight: FontWeight.w800,
                                color: look.state == LeaveBalanceState.usedUp
                                    ? palette.danger
                                    : palette.textPrimary,
                              ),
                            ),
                            if (hasEntitlement)
                              TextSpan(
                                text:
                                    ' / ${DayCountFormatter.format(balance.entitledDays)}',
                                style: textStyles.bodySmall?.copyWith(
                                  color: palette.textTertiary,
                                ),
                              ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  Gap.xs,
                  if (hasEntitlement) ...<Widget>[
                    ClipRRect(
                      borderRadius: BorderRadius.circular(AppRadius.pill),
                      child: LinearProgressIndicator(
                        value: look.leftFraction,
                        minHeight: 6,
                        backgroundColor: palette.surfaceMuted,
                        valueColor: AlwaysStoppedAnimation<Color>(look.accent),
                      ),
                    ),
                    Gap.xs,
                  ],
                  Text(
                    switch (look.state) {
                      LeaveBalanceState.available =>
                        '${DayCountFormatter.format(balance.usedDays)} used',
                      // The figure beside it already says "of 14".
                      LeaveBalanceState.pending =>
                        '${DayCountFormatter.format(balance.pendingDays)} pending',
                      _ => look.caption,
                    },
                    style: textStyles.labelSmall?.copyWith(
                      color: look.state == LeaveBalanceState.available
                          ? palette.textTertiary
                          : look.captionColour,
                    ),
                  ),
                ],
              ),
            ),
            Gap.wXs,
            Icon(
              Icons.chevron_right_rounded,
              size: AppSizes.iconMd,
              color: palette.textTertiary,
            ),
          ],
        ),
      ),
    );
  }
}
