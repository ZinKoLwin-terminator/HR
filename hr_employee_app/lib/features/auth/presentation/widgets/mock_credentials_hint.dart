import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:material_ui/material_ui.dart';

/// The way into the app while it runs on fixtures.
///
/// Only rendered when `AppConfig.useMockData` is true, which is impossible in
/// a production build. The password it displays guards nothing — it exists so
/// the fixture login has both a success and a failure path to demonstrate.
///
/// It names no backend state of its own: the banner at the top of the screen
/// already says the data is sample data, so this row only has to answer "what
/// do I type?" — and [onUseDemo] answers it by typing for the user.
class MockCredentialsHint extends StatelessWidget {
  const MockCredentialsHint({super.key, this.onUseDemo});

  /// Fills the form with the fixture credentials and submits it. Null while a
  /// request is already in flight.
  final VoidCallback? onUseDemo;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    const Color onBrand = AppColors.textOnPrimary;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: onBrand.withValues(alpha: 0.06),
        borderRadius: AppRadius.card,
        border: Border.all(color: onBrand.withValues(alpha: 0.14)),
      ),
      // Title and action on one line, credentials on their own below them:
      // side by side, the chips are squeezed into a column narrow enough to
      // stack them, which reads as two unrelated values rather than as one
      // pair to type.
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                height: AppSizes.avatarSm,
                width: AppSizes.avatarSm,
                decoration: BoxDecoration(
                  color: AppColors.accentLight.withValues(alpha: 0.18),
                  borderRadius: AppRadius.field,
                  border: Border.all(
                    color: AppColors.accentLight.withValues(alpha: 0.32),
                  ),
                ),
                child: Icon(
                  Icons.science_outlined,
                  size: AppSizes.iconMd,
                  color: AppColors.accentLight,
                ),
              ),
              Gap.wSm,
              Expanded(
                child: Text(
                  'Demo workspace',
                  style: textStyles.titleSmall?.copyWith(color: onBrand),
                  overflow: TextOverflow.ellipsis,
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
                  padding: EdgeInsets.symmetric(horizontal: AppSpacing.md),
                  foregroundColor: onBrand,
                  disabledForegroundColor: onBrand.withValues(alpha: 0.4),
                  side: BorderSide(color: onBrand.withValues(alpha: 0.28)),
                  textStyle: textStyles.labelMedium,
                  shape: RoundedRectangleBorder(borderRadius: AppRadius.chip),
                ),
                child: const Text('Use demo'),
              ),
            ],
          ),
          Gap.sm,
          Wrap(
            spacing: AppSpacing.xs,
            runSpacing: AppSpacing.xs,
            children: const <Widget>[
              _CredentialChip(value: MockSupport.employeeId),
              _CredentialChip(value: MockSupport.acceptedPassword),
            ],
          ),
        ],
      ),
    );
  }
}

/// One fixture value, shown as code because it is meant to be typed verbatim.
class _CredentialChip extends StatelessWidget {
  const _CredentialChip({required this.value});

  final String value;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xxs,
      ),
      decoration: BoxDecoration(
        color: onBrand.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppRadius.sm),
        border: Border.all(color: onBrand.withValues(alpha: 0.14)),
      ),
      child: SelectableText(
        value,
        style: Theme.of(context).textTheme.bodySmall?.copyWith(
          color: onBrand.withValues(alpha: 0.86),
          fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
        ),
      ),
    );
  }
}
