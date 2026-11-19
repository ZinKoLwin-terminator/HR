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
import 'package:hr_employee_app/core/widgets/app_card.dart';
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
  /// Revealed only on an explicit tap; it is a national identity number.
  bool _isNationalIdRevealed = false;

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

  @override
  Widget build(BuildContext context) {
    final AsyncValue<EmployeeProfile> profile = ref.watch(
      profileControllerProvider,
    );
    final bool isUploadingPhoto = ref
        .watch(profilePhotoControllerProvider)
        .isSubmitting;

    return Scaffold(
      backgroundColor: AppColors.primary,
      body: AsyncValueView<EmployeeProfile>(
        value: profile,
        onRetry: () => ref.invalidate(profileControllerProvider),
        data: (EmployeeProfile data) => RefreshIndicator(
          onRefresh: () =>
              ref.read(profileControllerProvider.notifier).refresh(),
          child: CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: <Widget>[
              SliverToBoxAdapter(
                child: SafeArea(
                  bottom: false,
                  child: ProfileHeader(
                    profile: data,
                    isUploadingPhoto: isUploadingPhoto,
                    onChangePhoto: _changePhoto,
                  ),
                ),
              ),
              SliverToBoxAdapter(
                child: Container(
                  transform: Matrix4.translationValues(0, -AppSpacing.lg, 0),
                  decoration: BoxDecoration(
                    color: Theme.of(context).scaffoldBackgroundColor,
                    borderRadius: BorderRadius.vertical(
                      top: Radius.circular(AppRadius.xl),
                    ),
                  ),
                  padding: EdgeInsets.fromLTRB(
                    AppSpacing.lg,
                    AppSpacing.lg,
                    AppSpacing.lg,
                    AppSpacing.xxxl,
                  ),
                  child: Column(
                    children: <Widget>[
                      _EmploymentCard(profile: data),
                      SizedBox(height: AppSpacing.cardGap),
                      _ContactCard(
                        profile: data,
                        onEdit: () => context.pushNamed(RouteNames.editProfile),
                      ),
                      SizedBox(height: AppSpacing.cardGap),
                      _PersonalCard(
                        profile: data,
                        isNationalIdRevealed: _isNationalIdRevealed,
                        onToggleNationalId: () => setState(
                          () => _isNationalIdRevealed = !_isNationalIdRevealed,
                        ),
                      ),
                      SizedBox(height: AppSpacing.cardGap),
                      _EmergencyContactCard(
                        profile: data,
                        onEdit: () => context.pushNamed(RouteNames.editProfile),
                      ),
                      SizedBox(height: AppSpacing.cardGap),
                      _AccountCard(
                        onChangePassword: () =>
                            context.pushNamed(RouteNames.changePassword),
                        onSignOut: _signOut,
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// HR-controlled employment data. Read-only by design.
class _EmploymentCard extends StatelessWidget {
  const _EmploymentCard({required this.profile});

  final EmployeeProfile profile;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Employment',
      icon: Icons.badge_outlined,
      child: Column(
        children: <Widget>[
          AppInfoRow(label: 'Employee ID', value: profile.employeeId),
          AppInfoRow(label: 'Department', value: profile.department.orDash()),
          AppInfoRow(label: 'Designation', value: profile.designation.orDash()),
          AppInfoRow(
            label: 'Employment type',
            value: profile.employmentType.orDash(),
          ),
          AppInfoRow(
            label: 'Reporting manager',
            value: profile.reportingManagerName.orDash(),
          ),
          AppInfoRow(
            label: 'Work location',
            value: profile.workLocation.orDash(),
          ),
          AppInfoRow(
            label: 'Joined',
            value: profile.dateOfJoining == null
                ? '—'
                : DateFormatter.dayMonthYear(profile.dateOfJoining!),
          ),
          if (profile.yearsOfService != null)
            AppInfoRow(
              label: 'Service',
              value: profile.yearsOfService == 1
                  ? '1 year'
                  : '${profile.yearsOfService} years',
            ),
        ],
      ),
    );
  }
}

/// The self-editable contact fields.
class _ContactCard extends StatelessWidget {
  const _ContactCard({required this.profile, required this.onEdit});

  final EmployeeProfile profile;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Contact',
      icon: Icons.contact_phone_outlined,
      action: TextButton(onPressed: onEdit, child: const Text('Edit')),
      child: Column(
        children: <Widget>[
          AppInfoRow(label: 'Work email', value: profile.email),
          AppInfoRow(label: 'Mobile', value: profile.mobileNumber.orDash()),
          AppInfoRow(
            label: 'Address',
            value: profile.address.orDash(),
            isMultiline: true,
          ),
        ],
      ),
    );
  }
}

/// Personal data. The national ID is masked until revealed.
class _PersonalCard extends StatelessWidget {
  const _PersonalCard({
    required this.profile,
    required this.isNationalIdRevealed,
    required this.onToggleNationalId,
  });

  final EmployeeProfile profile;
  final bool isNationalIdRevealed;
  final VoidCallback onToggleNationalId;

  @override
  Widget build(BuildContext context) {
    final bool hasNationalId = profile.nationalId.isNotNullOrBlank;

    return AppSectionCard(
      title: 'Personal',
      icon: Icons.person_outline_rounded,
      child: Column(
        children: <Widget>[
          AppInfoRow(
            label: 'Date of birth',
            value: profile.dateOfBirth == null
                ? '—'
                : DateFormatter.dayMonthYear(profile.dateOfBirth!),
          ),
          AppInfoRow(label: 'Gender', value: profile.gender.orDash()),
          AppInfoRow(
            label: 'Marital status',
            value: profile.maritalStatus.orDash(),
          ),
          AppInfoRow(label: 'Blood group', value: profile.bloodGroup.orDash()),
          if (hasNationalId)
            Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      'National ID',
                      style: Theme.of(context).textTheme.bodySmall
                          ?.copyWith(color: context.palette.textSecondary),
                    ),
                  ),
                  Text(
                    isNationalIdRevealed
                        ? profile.nationalId!
                        : profile.maskedNationalId!,
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  IconButton(
                    onPressed: onToggleNationalId,
                    visualDensity: VisualDensity.compact,
                    tooltip: isNationalIdRevealed
                        ? 'Hide national ID'
                        : 'Show national ID',
                    icon: Icon(
                      isNationalIdRevealed
                          ? Icons.visibility_off_outlined
                          : Icons.visibility_outlined,
                      size: AppSizes.iconMd,
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _EmergencyContactCard extends StatelessWidget {
  const _EmergencyContactCard({required this.profile, required this.onEdit});

  final EmployeeProfile profile;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    final EmergencyContact? contact = profile.emergencyContact;

    return AppSectionCard(
      title: 'Emergency contact',
      icon: Icons.emergency_outlined,
      action: TextButton(onPressed: onEdit, child: const Text('Edit')),
      child: contact == null
          ? Align(
              alignment: Alignment.centerLeft,
              child: Text(
                'No emergency contact on file. Adding one helps HR reach '
                'someone if you need help at work.',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            )
          : Column(
              children: <Widget>[
                AppInfoRow(label: 'Name', value: contact.displayName),
                AppInfoRow(label: 'Mobile', value: contact.mobileNumber),
                if (contact.alternateNumber.isNotNullOrBlank)
                  AppInfoRow(
                    label: 'Alternate',
                    value: contact.alternateNumber!,
                  ),
                if (contact.address.isNotNullOrBlank)
                  AppInfoRow(
                    label: 'Address',
                    value: contact.address!,
                    isMultiline: true,
                  ),
              ],
            ),
    );
  }
}

class _AccountCard extends StatelessWidget {
  const _AccountCard({required this.onChangePassword, required this.onSignOut});

  final VoidCallback onChangePassword;
  final VoidCallback onSignOut;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return AppCard(
      padding: EdgeInsets.symmetric(vertical: AppSpacing.xs),
      child: Column(
        children: <Widget>[
          ListTile(
            onTap: onChangePassword,
            leading: const Icon(Icons.lock_outline_rounded),
            title: const Text('Change password'),
            trailing: const Icon(Icons.chevron_right_rounded),
          ),
          Divider(height: 1, color: palette.border),
          ListTile(
            onTap: onSignOut,
            leading: Icon(Icons.logout_rounded, color: palette.danger),
            title: Text(
              'Sign out',
              style: Theme.of(context).textTheme.titleSmall
                  ?.copyWith(color: palette.danger),
            ),
          ),
        ],
      ),
    );
  }
}
