import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:material_ui/material_ui.dart';

/// The page frame shared by the grievance screens: the development banner
/// (in mock mode), a navy header with back, title, subtitle and actions,
/// and the body below.
///
/// [bottom] sits inside the navy band under the title — the list's status
/// filters go there.
class GrievanceScaffold extends ConsumerWidget {
  const GrievanceScaffold({
    required this.title,
    required this.body,
    super.key,
    this.subtitle,
    this.actions = const <Widget>[],
    this.bottom,
    this.bottomBar,
    this.floatingActionButton,
    this.headerFootPadding,
  });

  final String title;
  final String? subtitle;
  final List<Widget> actions;
  final Widget? bottom;
  final Widget body;
  final Widget? bottomBar;
  final Widget? floatingActionButton;

  /// Extra navy under the header, for a card that rides up over it.
  final double? headerFootPadding;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AppConfig config = ref.watch(appConfigProvider);

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        floatingActionButton: floatingActionButton,
        body: Column(
          children: <Widget>[
            if (config.useMockData)
              const SafeArea(
                bottom: false,
                child: MockDataBanner(
                  compact: true,
                  message:
                      'Development build · sample data, not from the HR '
                      'system',
                ),
              ),
            Expanded(
              child: MediaQuery.removePadding(
                context: context,
                removeTop: config.useMockData,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    GrievanceHeader(
                      title: title,
                      subtitle: subtitle,
                      actions: actions,
                      bottom: bottom,
                      footPadding: headerFootPadding,
                    ),
                    Expanded(child: body),
                    ?bottomBar,
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Navy band: a glass back button, the title and a one-line subtitle, any
/// actions, and an optional row beneath.
class GrievanceHeader extends StatelessWidget {
  const GrievanceHeader({
    required this.title,
    super.key,
    this.subtitle,
    this.actions = const <Widget>[],
    this.bottom,
    this.footPadding,
  });

  final String title;
  final String? subtitle;
  final List<Widget> actions;
  final Widget? bottom;
  final double? footPadding;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            0,
            AppSpacing.sm,
            0,
            bottom == null ? AppSpacing.md + (footPadding ?? 0) : 0,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Padding(
                padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
                child: Row(
                  children: <Widget>[
                    if (Navigator.of(context).canPop()) ...<Widget>[
                      GlassIconButton(
                        icon: Icons.arrow_back_rounded,
                        tooltip: MaterialLocalizations.of(context)
                            .backButtonTooltip,
                        onPressed: () => Navigator.of(context).maybePop(),
                      ),
                      Gap.wMd,
                    ],
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Text(
                            title,
                            style: textStyles.titleLarge?.copyWith(
                              color: onBrand,
                              fontWeight: FontWeight.w800,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          if (subtitle != null)
                            Text(
                              subtitle!,
                              style: textStyles.bodySmall?.copyWith(
                                color: onBrand.withValues(alpha: 0.72),
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                        ],
                      ),
                    ),
                    for (final Widget action in actions) ...<Widget>[
                      Gap.wSm,
                      action,
                    ],
                  ],
                ),
              ),
              if (bottom != null) ...<Widget>[
                Gap.md,
                bottom!,
                SizedBox(height: AppSpacing.sm + (footPadding ?? 0)),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// A white icon in a glass circle, for the navy header.
class GlassIconButton extends StatelessWidget {
  const GlassIconButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    super.key,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return Material(
      color: onBrand.withValues(alpha: 0.12),
      shape: CircleBorder(
        side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
      ),
      clipBehavior: Clip.antiAlias,
      child: IconButton(
        onPressed: onPressed,
        tooltip: tooltip,
        iconSize: AppSizes.iconMd,
        color: onBrand,
        disabledColor: onBrand.withValues(alpha: 0.4),
        icon: Icon(icon),
      ),
    );
  }
}
