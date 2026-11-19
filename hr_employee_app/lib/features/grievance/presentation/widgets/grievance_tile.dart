import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_status_chip.dart';
import 'package:material_ui/material_ui.dart';

/// One grievance in the list: a strip in the status colour down the left
/// edge, the status, the case number (or "Not sent" for a draft), the
/// subject, and a red dot when HR updated it since the employee last looked.
class GrievanceTile extends StatelessWidget {
  const GrievanceTile({
    required this.grievance,
    required this.onTap,
    super.key,
  });

  final GrievanceSummary grievance;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final GrievanceSummary g = grievance;
    final Color tone = grievanceTone(context, g.status);
    final BorderRadius radius = BorderRadius.circular(AppRadius.lg);

    return Semantics(
      button: true,
      label:
          '${g.reference}, ${g.subject}, ${g.status.label}'
          '${g.hasUnreadUpdate ? ', new update' : ''}',
      child: ExcludeSemantics(
        child: Padding(
          padding: EdgeInsets.only(bottom: AppSpacing.sm),
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: radius,
              boxShadow: const <BoxShadow>[
                BoxShadow(
                  color: AppColors.shadow,
                  blurRadius: 12,
                  offset: Offset(0, 3),
                ),
              ],
            ),
            child: Material(
              color: AppColors.surface,
              borderRadius: radius,
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: onTap,
                child: IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      Container(width: 4, color: tone),
                      Expanded(
                        child: Padding(
                          padding: EdgeInsets.fromLTRB(
                            AppSpacing.md,
                            AppSpacing.md,
                            AppSpacing.md,
                            AppSpacing.md,
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: <Widget>[
                              Row(
                                children: <Widget>[
                                  GrievancePill(
                                    label: grievanceShortLabel(g.status),
                                    color: tone,
                                  ),
                                  Gap.wSm,
                                  Expanded(
                                    child: Text(
                                      // The case number; a draft has none.
                                      g.caseNo ?? 'Not sent',
                                      style: textStyles.labelMedium?.copyWith(
                                        color: palette.textTertiary,
                                        fontWeight: FontWeight.w600,
                                      ),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                  if (g.hasUnreadUpdate)
                                    Container(
                                      key: const ValueKey<String>(
                                        'grievance-unread-dot',
                                      ),
                                      height: 8,
                                      width: 8,
                                      decoration: BoxDecoration(
                                        color: palette.danger,
                                        shape: BoxShape.circle,
                                      ),
                                    ),
                                ],
                              ),
                              Gap.sm,
                              Text(
                                g.subject,
                                style: textStyles.titleSmall?.copyWith(
                                  fontWeight: g.hasUnreadUpdate
                                      ? FontWeight.w800
                                      : FontWeight.w700,
                                ),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ],
                          ),
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
