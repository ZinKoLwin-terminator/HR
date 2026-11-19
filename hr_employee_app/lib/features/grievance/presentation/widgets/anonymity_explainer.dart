import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// What the form tells the employee about reporting anonymously.
///
/// The same honest account as the web app (`grievance-anonymity.ts`): an
/// anonymous case is linked to its submitter only by a keyed pseudonym, HR
/// sees no name, department or position — and some details can still give a
/// person away, which the form says plainly instead of promising more than
/// the system can give.
abstract final class GrievanceAnonymityTexts {
  static const List<String> hrSees = <String>[
    'The category, subject, description, incident date and location you '
        'write.',
    'The person and department you name as involved, and the files you '
        'attach.',
    'Your messages, signed "Anonymous submitter".',
  ];

  static const List<String> hrDoesNotSee = <String>[
    'Your name, employee code, department or position.',
    'Your user account: the history and messages carry no account or '
        'employee id.',
  ];

  static const List<String> residualRisks = <String>[
    'Timing: when you submit or reply can be matched to who was at work or '
        'online.',
    'Writing style and details only you would know can point to you.',
    'In a small department, the facts alone may narrow it down to a few '
        'people.',
  ];

  static const String notAllowed =
      'This category does not accept anonymous grievances: HR needs to know '
      'who is concerned to look into it.';

  static const String named =
      'HR will see your name. Your employee code, department and position '
      'are recorded from your employee record when you submit.';
}

/// The anonymous switch, with what HR will and will not see.
///
/// On an existing draft the choice is shown, not offered: it was fixed when
/// the draft was created.
class AnonymityExplainer extends StatelessWidget {
  const AnonymityExplainer({
    required this.isAnonymous,
    required this.isAllowed,
    required this.isLocked,
    required this.isEnabled,
    required this.onChanged,
    super.key,
    this.errorText,
  });

  final bool isAnonymous;

  /// False when the chosen category refuses anonymous reports.
  final bool isAllowed;

  /// True on an existing draft.
  final bool isLocked;
  final bool isEnabled;
  final ValueChanged<bool> onChanged;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        if (isLocked)
          Text.rich(
            key: const ValueKey<String>('grievance-anonymity-fixed'),
            TextSpan(
              children: <InlineSpan>[
                TextSpan(
                  text: isAnonymous
                      ? 'You are reporting anonymously. '
                      : 'You are reporting with your name. ',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                const TextSpan(
                  text: 'Start a new draft to change how you report.',
                ),
              ],
            ),
            style: textStyles.bodyMedium,
          )
        else ...<Widget>[
          // Two choices side by side rather than a switch: "anonymous" is
          // not an on/off setting but one of two ways of reporting, and
          // both deserve a word on what they mean.
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                Expanded(
                  child: _ReportingOption(
                    key: const ValueKey<String>('grievance-named-option'),
                    title: 'With my name',
                    caption: 'HR can contact you directly',
                    isSelected: !(isAnonymous && isAllowed),
                    onTap: isEnabled ? () => onChanged(false) : null,
                  ),
                ),
                Gap.wSm,
                Expanded(
                  child: _ReportingOption(
                    key: const ValueKey<String>('grievance-anonymous-switch'),
                    title: 'Anonymously',
                    caption: isAllowed
                        ? 'HR will not see who you are'
                        : 'Not offered for this category',
                    isSelected: isAnonymous && isAllowed,
                    onTap: (isEnabled && isAllowed)
                        ? () => onChanged(true)
                        : null,
                  ),
                ),
              ],
            ),
          ),
          Gap.sm,
          Text(
            isAllowed
                ? 'You cannot change this after the draft is saved.'
                : GrievanceAnonymityTexts.notAllowed,
            style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
          ),
        ],
        if (errorText != null) ...<Widget>[
          Gap.xs,
          Text(
            errorText!,
            style: textStyles.bodySmall?.copyWith(color: palette.danger),
          ),
        ],
        Gap.sm,
        if (isAnonymous) ...<Widget>[
          const _Bullets(
            icon: Icons.visibility_outlined,
            title: 'HR will see',
            lines: GrievanceAnonymityTexts.hrSees,
          ),
          Gap.sm,
          const _Bullets(
            icon: Icons.visibility_off_outlined,
            title: 'HR will not see',
            lines: GrievanceAnonymityTexts.hrDoesNotSee,
          ),
          Gap.sm,
          const _Bullets(
            icon: Icons.warning_amber_rounded,
            title: 'What can still identify you',
            lines: GrievanceAnonymityTexts.residualRisks,
          ),
        ] else
          Container(
            padding: EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: palette.surfaceMuted,
              borderRadius: AppRadius.field,
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Icon(
                  Icons.info_outline_rounded,
                  size: AppSizes.iconSm,
                  color: palette.textTertiary,
                ),
                Gap.wSm,
                Expanded(
                  child: Text(
                    GrievanceAnonymityTexts.named,
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

class _Bullets extends StatelessWidget {
  const _Bullets({
    required this.icon,
    required this.title,
    required this.lines,
  });

  final IconData icon;
  final String title;
  final List<String> lines;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: palette.surfaceMuted,
        borderRadius: AppRadius.field,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Icon(icon, size: AppSizes.iconSm, color: palette.textSecondary),
              Gap.wXs,
              Expanded(
                child: Text(
                  title,
                  style: textStyles.labelLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          Gap.xs,
          for (final String line in lines)
            Padding(
              padding: EdgeInsets.only(top: AppSpacing.xxs),
              child: Text('• $line', style: textStyles.bodySmall),
            ),
        ],
      ),
    );
  }
}

/// One way of reporting, as a selectable card: navy outline and a light
/// tint when chosen, greyed when not offered.
class _ReportingOption extends StatelessWidget {
  const _ReportingOption({
    required this.title,
    required this.caption,
    required this.isSelected,
    required this.onTap,
    super.key,
  });

  final String title;
  final String caption;
  final bool isSelected;

  /// Null when this option cannot be chosen.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isOffered = onTap != null || isSelected;

    return Semantics(
      button: true,
      selected: isSelected,
      enabled: onTap != null,
      label: '$title. $caption',
      child: ExcludeSemantics(
        child: Opacity(
          opacity: isOffered ? 1 : 0.5,
          child: Material(
            color: isSelected ? AppColors.primarySurface : AppColors.surface,
            shape: RoundedRectangleBorder(
              borderRadius: AppRadius.field,
              side: BorderSide(
                color: isSelected ? AppColors.apexNavy : palette.border,
                width: isSelected ? 1.5 : 1,
              ),
            ),
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      style: textStyles.labelLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                        color: isSelected
                            ? AppColors.apexNavy
                            : palette.textPrimary,
                      ),
                    ),
                    Gap.xxs,
                    Text(
                      caption,
                      style: textStyles.bodySmall?.copyWith(
                        color: palette.textSecondary,
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
