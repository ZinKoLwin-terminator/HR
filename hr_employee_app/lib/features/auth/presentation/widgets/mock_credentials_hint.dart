import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/dashed_border.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:material_ui/material_ui.dart';

/// The way into the app while it runs on fixtures.
///
/// Only rendered when `AppConfig.useMockData` is true, which is impossible in
/// a production build. The password it displays guards nothing — it exists so
/// the fixture login has both a success and a failure path to demonstrate.
///
/// Drawn with a dashed border on purpose: it is scaffolding around the form,
/// not part of it, and should look provisional next to the real controls.
class MockCredentialsHint extends StatelessWidget {
  const MockCredentialsHint({super.key, this.onUseDemo});

  /// Fills the form with the fixture credentials and submits it. Null while a
  /// request is already in flight.
  final VoidCallback? onUseDemo;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;

    return CustomPaint(
      foregroundPainter: DashedBorderPainter(
        color: palette.textTertiary.withValues(alpha: 0.55),
        radius: AppRadius.lg,
      ),
      child: Container(
        padding: EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: AppColors.cloud,
          borderRadius: BorderRadius.circular(AppRadius.lg),
        ),
        child: Row(
          children: <Widget>[
            Container(
              height: AppSizes.avatarSm + 4,
              width: AppSizes.avatarSm + 4,
              decoration: BoxDecoration(
                color: AppColors.summitTeal.withValues(alpha: 0.12),
                borderRadius: AppRadius.field,
              ),
              child: Icon(
                Icons.science_outlined,
                size: AppSizes.iconMd,
                color: AppColors.summitTeal,
              ),
            ),
            Gap.wMd,
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    'Demo workspace',
                    style: textStyles.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                  Gap.xxs,
                  // Shown as code: these are meant to be typed verbatim.
                  SelectableText(
                    '${MockSupport.employeeId} · ${MockSupport.acceptedPassword}',
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textSecondary,
                      letterSpacing: 0.4,
                      fontFeatures: const <FontFeature>[
                        FontFeature.tabularFigures(),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            Gap.wSm,
            OutlinedButton(
              onPressed: onUseDemo,
              style: OutlinedButton.styleFrom(
                // The app's outlined button is a full-width form control;
                // this one sits at the end of a row, so it sets its own
                // metrics rather than inheriting that minimum width.
                minimumSize: const Size(0, AppSizes.minTouchTarget),
                padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
                backgroundColor: AppColors.surface,
                foregroundColor: AppColors.ink,
                side: BorderSide(color: palette.border),
                textStyle: textStyles.labelLarge,
                shape: RoundedRectangleBorder(borderRadius: AppRadius.field),
              ),
              child: const Text('Use demo'),
            ),
          ],
        ),
      ),
    );
  }
}
