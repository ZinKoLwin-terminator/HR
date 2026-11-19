import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/services/file_picker_service.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_providers.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:hr_employee_app/features/profile/presentation/providers/profile_controllers.dart';
import 'package:hr_employee_app/features/profile/presentation/widgets/profile_header.dart';
import 'package:material_ui/material_ui.dart';

/// The employee's own record, plus account actions.
///
/// Everything HR controls is read-only here. Only the fields the employee
/// owns are editable, and they are reached through the edit screen rather
/// than being inline-editable, so a save is always deliberate.
class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  Future<void> _changePhoto() async {
    final Result<PickedFile?> picked = await ref
        .read(filePickerServiceProvider)
        .pickImage(
          allowedExtensions: UploadLimits.profilePhotoExtensions,
          maxSizeBytes: UploadLimits.maxProfilePhotoBytes,
        );

    if (!mounted) {
      return;
    }

    final PickedFile? file = picked.valueOrNull;
    final Failure? pickFailure = picked.failureOrNull;

    if (pickFailure != null) {
      AppSnackBar.failure(context, pickFailure);
      return;
    }
    if (file == null) {
      return;
    }

    final bool uploaded = await ref
        .read(profilePhotoControllerProvider.notifier)
        .upload(file.path);

    if (!mounted) {
      return;
    }
    if (uploaded) {
      AppSnackBar.success(context, 'Profile photo updated.');
      return;
    }
    final Failure? failure = ref
        .read(profilePhotoControllerProvider)
        .failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  Future<void> _signOut() async {
    final bool confirmed = await ConfirmDialog.show(
      context,
      title: 'Sign out?',
      message: 'You will need your employee ID and password to sign in again.',
      confirmLabel: 'Sign out',
      isDestructive: true,
      icon: Icons.logout_rounded,
    );

    if (!confirmed || !mounted) {
      return;
    }
    // The router reacts to the session ending; this screen does not navigate.
    await ref.read(authControllerProvider.notifier).signOut();
  }

  void _editDetails() => context.pushNamed(RouteNames.editProfile);

  Future<void> _openSection(_Section section) {
    return showModalBottomSheet<void>(
      context: context,
      // Above the tab bar, so the sheet and its scrim cover the whole screen.
      useRootNavigator: true,
      isScrollControlled: true,
      showDragHandle: false,
      backgroundColor: AppColors.surface,
      clipBehavior: Clip.antiAlias,
      constraints: BoxConstraints(
        maxHeight: MediaQuery.sizeOf(context).height * 0.85,
      ),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppRadius.xl * 1.2),
        ),
      ),
      builder: (BuildContext sheetContext) => _DetailsSheet(
        section: section,
        onEdit: section.isSelfEditable
            ? () {
                Navigator.of(sheetContext).pop();
                _editDetails();
              }
            : null,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<EmployeeProfile> profile = ref.watch(
      profileControllerProvider,
    );
    final bool isUploadingPhoto = ref
        .watch(profilePhotoControllerProvider)
        .isSubmitting;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: AsyncValueView<EmployeeProfile>(
          value: profile,
          loadingMessages: const <String>[
            'Fetching your profile…',
            'Gathering your details…',
            'Almost there…',
          ],
          onRetry: () => ref.invalidate(profileControllerProvider),
          data: (EmployeeProfile data) {
            final List<_Section> sections = _Section.allFor(data);

            return RefreshIndicator(
              onRefresh: () =>
                  ref.read(profileControllerProvider.notifier).refresh(),
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: EdgeInsets.only(bottom: AppSpacing.xxxl),
                children: <Widget>[
                  _Header(onEdit: _editDetails),
                  // The identity card straddles the header's foot: a navy
                  // strip behind its top half continues the band.
                  Stack(
                    children: <Widget>[
                      const Positioned(
                        top: 0,
                        left: 0,
                        right: 0,
                        height: 72,
                        child: ColoredBox(color: AppColors.apexNavy),
                      ),
                      Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: AppSpacing.lg,
                        ),
                        child: ProfileHeader(
                          profile: data,
                          isUploadingPhoto: isUploadingPhoto,
                          onChangePhoto: _changePhoto,
                        ),
                      ),
                    ],
                  ),
                  Padding(
                    padding: EdgeInsets.fromLTRB(
                      AppSpacing.lg,
                      AppSpacing.md,
                      AppSpacing.lg,
                      0,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        _GroupedCard(
                          children: <Widget>[
                            for (final _Section section in sections)
                              _SectionRow(
                                section: section,
                                onTap: () => _openSection(section),
                              ),
                          ],
                        ),
                        SizedBox(height: AppSpacing.md),
                        _GroupedCard(
                          children: <Widget>[
                            _ActionRow(
                              icon: Icons.forum_outlined,
                              label: 'My grievances',
                              onTap: () =>
                                  context.pushNamed(RouteNames.grievances),
                            ),
                            _ActionRow(
                              icon: Icons.lock_outline_rounded,
                              label: 'Change password',
                              onTap: () =>
                                  context.pushNamed(RouteNames.changePassword),
                            ),
                            _ActionRow(
                              icon: Icons.logout_rounded,
                              label: 'Sign out',
                              isDestructive: true,
                              onTap: _signOut,
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

/// Navy band: the title and the way into editing the details the employee
/// owns.
class _Header extends StatelessWidget {
  const _Header({required this.onEdit});

  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.lg,
          ),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Profile',
                  style: Theme.of(context).textTheme.titleLarge
                      ?.copyWith(color: onBrand, fontWeight: FontWeight.w800),
                ),
              ),
              Material(
                color: onBrand.withValues(alpha: 0.12),
                shape: CircleBorder(
                  side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
                ),
                clipBehavior: Clip.antiAlias,
                child: IconButton(
                  onPressed: onEdit,
                  tooltip: 'Edit my details',
                  iconSize: AppSizes.iconMd,
                  icon: const Icon(Icons.edit_outlined, color: onBrand),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// One fact in a details sheet.
class _Fact {
  const _Fact(this.label, this.value, {this.isLong = false, this.masked});

  final String label;
  final String value;

  /// Shown under its label rather than beside it — an address, say.
  final bool isLong;

  /// When set, [value] is hidden behind this until the eye is tapped.
  final String? masked;
}

/// A profile section: its colour and icon, a one-line summary for the list,
/// and the full set of facts for the sheet.
class _Section {
  const _Section({
    required this.title,
    required this.summary,
    required this.icon,
    required this.tint,
    required this.facts,
    required this.isSelfEditable,
    this.emptyMessage,
  });

  final String title;
  final String summary;
  final IconData icon;
  final Color tint;
  final List<_Fact> facts;

  /// Contact and emergency contact are the employee's own to change; the
  /// rest is managed by HR.
  final bool isSelfEditable;

  /// Shown in the sheet when there are no facts.
  final String? emptyMessage;

  static String _dash(String? value) => value.orDash();

  static String _join(List<String?> parts) =>
      parts.whereType<String>().where((String s) => s.isNotBlank).join(' · ');

  static List<_Section> allFor(EmployeeProfile p) {
    final EmergencyContact? contact = p.emergencyContact;

    return <_Section>[
      _Section(
        title: 'Employment',
        summary: _join(<String?>[
          p.employmentType,
          if (p.reportingManagerName.isNotNullOrBlank)
            'reports to ${p.reportingManagerName}',
        ]),
        icon: Icons.work_outline_rounded,
        tint: AppColors.apexNavy,
        isSelfEditable: false,
        facts: <_Fact>[
          _Fact('Department', _dash(p.department)),
          _Fact('Designation', _dash(p.designation)),
          _Fact('Employment type', _dash(p.employmentType)),
          _Fact('Reporting manager', _dash(p.reportingManagerName)),
          _Fact('Work location', _dash(p.workLocation)),
          if (p.confirmationDate != null)
            _Fact('Confirmed', DateFormatter.dayMonthYear(p.confirmationDate!)),
        ],
      ),
      _Section(
        title: 'Contact',
        summary: p.email,
        icon: Icons.alternate_email_rounded,
        tint: AppColors.summitTeal,
        isSelfEditable: true,
        facts: <_Fact>[
          _Fact('Work email', p.email),
          _Fact('Mobile', _dash(p.mobileNumber)),
          _Fact('Address', _dash(p.address), isLong: true),
        ],
      ),
      _Section(
        title: 'Personal',
        summary: _join(<String?>[
          if (p.dateOfBirth != null)
            'Born ${DateFormatter.dayMonthYear(p.dateOfBirth!)}',
          p.gender,
          p.maritalStatus,
        ]),
        icon: Icons.person_outline_rounded,
        tint: AppColors.violet,
        isSelfEditable: false,
        facts: <_Fact>[
          _Fact(
            'Date of birth',
            p.dateOfBirth == null
                ? '—'
                : DateFormatter.dayMonthYear(p.dateOfBirth!),
          ),
          _Fact('Gender', _dash(p.gender)),
          _Fact('Marital status', _dash(p.maritalStatus)),
          _Fact('Blood group', _dash(p.bloodGroup)),
          if (p.nationalId.isNotNullOrBlank)
            _Fact('National ID', p.nationalId!, masked: p.maskedNationalId),
        ],
      ),
      _Section(
        title: 'Emergency contact',
        summary: contact == null
            ? 'Not added yet'
            : _join(<String?>[contact.name, contact.relationship]),
        icon: Icons.emergency_outlined,
        tint: AppColors.error,
        isSelfEditable: true,
        emptyMessage:
            'No emergency contact on file. Adding one helps HR reach '
            'someone if you need help at work.',
        facts: contact == null
            ? const <_Fact>[]
            : <_Fact>[
                _Fact('Name', contact.name),
                _Fact('Relationship', _dash(contact.relationship)),
                _Fact('Mobile', contact.mobileNumber),
                if (contact.alternateNumber.isNotNullOrBlank)
                  _Fact('Alternate', contact.alternateNumber!),
                if (contact.address.isNotNullOrBlank)
                  _Fact('Address', contact.address!, isLong: true),
              ],
      ),
    ];
  }
}

/// A section's icon: its colour as a dot on a tinted rounded square.
class _SectionIcon extends StatelessWidget {
  const _SectionIcon({required this.tint, this.isOnWhite = false});

  final Color tint;

  /// On the sheet header the tile is white, so it reads on the tint.
  final bool isOnWhite;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 36,
      width: 36,
      decoration: BoxDecoration(
        color: isOnWhite ? AppColors.surface : tint.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadius.sm + 2),
      ),
      alignment: Alignment.center,
      child: Container(
        height: 10,
        width: 10,
        decoration: BoxDecoration(
          color: tint,
          borderRadius: BorderRadius.circular(3),
        ),
      ),
    );
  }
}

/// A section in the list: icon, title, summary and a › — it opens a sheet.
class _SectionRow extends StatelessWidget {
  const _SectionRow({required this.section, required this.onTap});

  final _Section section;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Semantics(
      button: true,
      label: '${section.title}, ${section.summary}',
      child: ExcludeSemantics(
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.md,
            ),
            child: Row(
              children: <Widget>[
                _SectionIcon(tint: section.tint),
                Gap.wMd,
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        section.title,
                        style: textStyles.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      if (section.summary.isNotEmpty)
                        Text(
                          section.summary,
                          style: textStyles.bodySmall?.copyWith(
                            color: palette.textTertiary,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                    ],
                  ),
                ),
                Icon(
                  Icons.chevron_right_rounded,
                  size: AppSizes.iconMd,
                  color: palette.textTertiary,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Change password / sign out.
class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.icon,
    required this.label,
    required this.onTap,
    this.isDestructive = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool isDestructive;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color ink = isDestructive ? palette.danger : palette.textPrimary;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.md + 2,
        ),
        child: Row(
          children: <Widget>[
            Icon(icon, size: AppSizes.iconMd, color: ink),
            Gap.wMd,
            Expanded(
              child: Text(
                label,
                style: Theme.of(context).textTheme.labelLarge
                    ?.copyWith(color: ink, fontWeight: FontWeight.w700),
              ),
            ),
            if (!isDestructive)
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

/// One white card holding several rows, divided by hairlines.
class _GroupedCard extends StatelessWidget {
  const _GroupedCard({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final BorderRadius radius = BorderRadius.circular(AppRadius.xl);

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: radius,
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadow,
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: Material(
        color: AppColors.surface,
        borderRadius: radius,
        clipBehavior: Clip.antiAlias,
        child: Column(
          children: <Widget>[
            for (int i = 0; i < children.length; i++) ...<Widget>[
              children[i],
              if (i < children.length - 1)
                Divider(
                  height: 1,
                  indent: AppSpacing.md,
                  endIndent: AppSpacing.md,
                  color: AppColors.divider,
                ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The bottom sheet for one section: a tinted header with the title and
/// summary, the facts in a bordered card, and either an Edit button or the
/// "managed by HR" note. Closes on × or a tap outside.
class _DetailsSheet extends StatefulWidget {
  const _DetailsSheet({required this.section, required this.onEdit});

  final _Section section;

  /// Null for the sections HR manages.
  final VoidCallback? onEdit;

  @override
  State<_DetailsSheet> createState() => _DetailsSheetState();
}

class _DetailsSheetState extends State<_DetailsSheet> {
  /// Masked facts the employee has chosen to reveal in this sheet.
  final Set<String> _revealed = <String>{};

  @override
  Widget build(BuildContext context) {
    final _Section section = widget.section;
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return SafeArea(
      top: false,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Container(
            color: Color.alphaBlend(
              section.tint.withValues(alpha: 0.08),
              AppColors.surface,
            ),
            padding: EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.sm,
              AppSpacing.sm,
              AppSpacing.md,
            ),
            child: Column(
              children: <Widget>[
                // Drag handle.
                Container(
                  height: 4,
                  width: 36,
                  margin: EdgeInsets.only(bottom: AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: palette.border,
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
                Row(
                  children: <Widget>[
                    _SectionIcon(tint: section.tint, isOnWhite: true),
                    Gap.wMd,
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            section.title,
                            style: textStyles.titleMedium?.copyWith(
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          if (section.summary.isNotEmpty)
                            Text(
                              section.summary,
                              style: textStyles.bodySmall?.copyWith(
                                color: palette.textSecondary,
                              ),
                            ),
                        ],
                      ),
                    ),
                    Material(
                      color: AppColors.surface,
                      shape: const CircleBorder(),
                      child: IconButton(
                        onPressed: () => Navigator.of(context).pop(),
                        icon: const Icon(Icons.close_rounded),
                        iconSize: AppSizes.iconMd,
                        tooltip: MaterialLocalizations.of(context)
                            .closeButtonTooltip,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          Flexible(
            child: SingleChildScrollView(
              padding: EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  if (section.facts.isEmpty)
                    Text(
                      section.emptyMessage ?? 'Nothing on file yet.',
                      style: textStyles.bodySmall?.copyWith(
                        color: palette.textSecondary,
                      ),
                    )
                  else
                    Container(
                      decoration: BoxDecoration(
                        borderRadius: AppRadius.field,
                        border: Border.all(color: palette.border),
                      ),
                      child: Column(
                        children: <Widget>[
                          for (
                            int i = 0;
                            i < section.facts.length;
                            i++
                          ) ...<Widget>[
                            _FactRow(
                              fact: section.facts[i],
                              isRevealed: _revealed.contains(
                                section.facts[i].label,
                              ),
                              onToggle: () => setState(() {
                                final String key = section.facts[i].label;
                                if (!_revealed.remove(key)) {
                                  _revealed.add(key);
                                }
                              }),
                            ),
                            if (i < section.facts.length - 1)
                              const Divider(
                                height: 1,
                                color: AppColors.divider,
                              ),
                          ],
                        ],
                      ),
                    ),
                  Gap.md,
                  if (widget.onEdit != null)
                    AppButton(
                      label: section.facts.isEmpty
                          ? 'Add ${section.title.toLowerCase()}'
                          : 'Edit ${section.title.toLowerCase()}',
                      icon: Icons.edit_outlined,
                      onPressed: widget.onEdit!,
                    )
                  else
                    Container(
                      padding: EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.cloud,
                        borderRadius: AppRadius.field,
                      ),
                      child: Row(
                        children: <Widget>[
                          Icon(
                            Icons.lock_outline_rounded,
                            size: AppSizes.iconSm,
                            color: palette.textTertiary,
                          ),
                          Gap.wSm,
                          Expanded(
                            child: Text(
                              'Managed by HR. Contact your HR administrator '
                              'to change these.',
                              style: textStyles.bodySmall?.copyWith(
                                color: palette.textSecondary,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Label on the left and value on the right — or the value under the label
/// for long text — with an eye for masked values.
class _FactRow extends StatelessWidget {
  const _FactRow({
    required this.fact,
    required this.isRevealed,
    required this.onToggle,
  });

  final _Fact fact;
  final bool isRevealed;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isMasked = fact.masked != null && !isRevealed;

    final Text label = Text(
      fact.label,
      style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
    );
    final Text value = Text(
      isMasked ? fact.masked! : fact.value,
      textAlign: fact.isLong ? TextAlign.start : TextAlign.end,
      style: textStyles.labelLarge?.copyWith(fontWeight: FontWeight.w700),
      semanticsLabel: isMasked ? '${fact.label} hidden' : null,
    );

    return Padding(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: fact.masked != null ? AppSpacing.xs : AppSpacing.md,
      ),
      child: fact.isLong
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[label, Gap.xxs, value],
            )
          : Row(
              children: <Widget>[
                label,
                Gap.wMd,
                Expanded(child: value),
                if (fact.masked != null)
                  IconButton(
                    onPressed: onToggle,
                    visualDensity: VisualDensity.compact,
                    tooltip: isRevealed
                        ? 'Hide ${fact.label}'
                        : 'Show ${fact.label}',
                    icon: Icon(
                      isRevealed
                          ? Icons.visibility_off_outlined
                          : Icons.visibility_outlined,
                      size: AppSizes.iconMd,
                    ),
                  ),
              ],
            ),
    );
  }
}
