import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/muted_field_theme.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:hr_employee_app/features/profile/presentation/providers/profile_controllers.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Edits the parts of the profile the employee owns.
///
/// Only the self-editable fields appear. Department, designation and the rest
/// are HR-controlled, so they are absent rather than shown disabled — a
/// greyed-out field invites a request the server would reject.
///
/// Only the sections that actually changed are sent, so opening the screen
/// and saving without edits costs nothing.
class EditProfileScreen extends ConsumerStatefulWidget {
  const EditProfileScreen({super.key});

  @override
  ConsumerState<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends ConsumerState<EditProfileScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  final TextEditingController _mobileController = TextEditingController();
  final TextEditingController _addressController = TextEditingController();
  final TextEditingController _contactNameController = TextEditingController();
  final TextEditingController _contactRelationController =
      TextEditingController();
  final TextEditingController _contactMobileController =
      TextEditingController();
  final TextEditingController _contactAlternateController =
      TextEditingController();
  final TextEditingController _contactAddressController =
      TextEditingController();

  /// The profile the form was populated from, used to detect what changed.
  EmployeeProfile? _original;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      ref.read(editProfileControllerProvider.notifier).reset();
      _populateFrom(ref.read(profileControllerProvider).value);
    });
  }

  /// Fills the fields once, from the loaded profile.
  void _populateFrom(EmployeeProfile? profile) {
    if (profile == null || _original != null) {
      return;
    }
    final EmergencyContact? contact = profile.emergencyContact;

    setState(() {
      _original = profile;
      _mobileController.text = profile.mobileNumber ?? '';
      _addressController.text = profile.address ?? '';
      _contactNameController.text = contact?.name ?? '';
      _contactRelationController.text = contact?.relationship ?? '';
      _contactMobileController.text = contact?.mobileNumber ?? '';
      _contactAlternateController.text = contact?.alternateNumber ?? '';
      _contactAddressController.text = contact?.address ?? '';
    });
  }

  @override
  void dispose() {
    _mobileController.dispose();
    _addressController.dispose();
    _contactNameController.dispose();
    _contactRelationController.dispose();
    _contactMobileController.dispose();
    _contactAlternateController.dispose();
    _contactAddressController.dispose();
    super.dispose();
  }

  /// The contact update, or null when nothing in that section changed.
  ContactUpdate? _contactChanges() {
    final EmployeeProfile? original = _original;
    if (original == null) {
      return null;
    }

    final String mobile = _mobileController.text.trim();
    final String address = _addressController.text.trim();

    final bool changed =
        mobile != (original.mobileNumber ?? '').trim() ||
        address != (original.address ?? '').trim();

    return changed
        ? ContactUpdate(
            mobileNumber: mobile,
            address: address.isEmpty ? null : address,
          )
        : null;
  }

  /// The emergency contact update, or null when unchanged.
  ///
  /// Also returns null when the whole section is blank and was blank before,
  /// so an employee with no emergency contact is not forced to invent one.
  EmergencyContact? _emergencyChanges() {
    final EmployeeProfile? original = _original;
    if (original == null) {
      return null;
    }

    final String name = _contactNameController.text.trim();
    final String mobile = _contactMobileController.text.trim();
    final String relationship = _contactRelationController.text.trim();
    final String alternate = _contactAlternateController.text.trim();
    final String address = _contactAddressController.text.trim();

    final bool isBlank = name.isEmpty && mobile.isEmpty;
    if (isBlank) {
      return null;
    }

    final EmergencyContact? existing = original.emergencyContact;
    final bool changed =
        name != (existing?.name ?? '').trim() ||
        mobile != (existing?.mobileNumber ?? '').trim() ||
        relationship != (existing?.relationship ?? '').trim() ||
        alternate != (existing?.alternateNumber ?? '').trim() ||
        address != (existing?.address ?? '').trim();

    return changed
        ? EmergencyContact(
            name: name,
            mobileNumber: mobile,
            relationship: relationship.isEmpty ? null : relationship,
            alternateNumber: alternate.isEmpty ? null : alternate,
            address: address.isEmpty ? null : address,
          )
        : null;
  }

  Future<void> _save() async {
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final bool saved = await ref
        .read(editProfileControllerProvider.notifier)
        .save(
          contact: _contactChanges(),
          emergencyContact: _emergencyChanges(),
        );

    if (!mounted) {
      return;
    }
    if (saved) {
      AppSnackBar.success(context, 'Profile updated.');
      Navigator.of(context).pop();
      return;
    }
    final Failure? failure = ref
        .read(editProfileControllerProvider)
        .failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<EmployeeProfile> profile = ref.watch(
      profileControllerProvider,
    );
    final SubmissionState submission = ref.watch(editProfileControllerProvider);
    final bool isSaving = submission.isSubmitting;

    // Populate once the profile arrives, if the screen opened before it did.
    ref.listen<AsyncValue<EmployeeProfile>>(profileControllerProvider, (
      AsyncValue<EmployeeProfile>? previous,
      AsyncValue<EmployeeProfile> next,
    ) {
      _populateFrom(next.value);
    });

    return Scaffold(
      appBar: const BrandAppBar(title: Text('Edit profile')),
      body: SafeArea(
        child: AsyncValueView<EmployeeProfile>(
          value: profile,
          onRetry: () => ref.invalidate(profileControllerProvider),
          data: (EmployeeProfile data) => Form(
            key: _formKey,
            child: MutedFieldTheme(
              child: ListView(
                padding: AppSpacing.screenPadding,
                children: <Widget>[
                  AppSectionCard(
                    title: 'Your contact details',
                    icon: Icons.contact_phone_outlined,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        const _SectionNote(
                          'HR uses these to reach you. Department and job title are '
                          'managed by HR.',
                        ),

                        AppTextField(
                          label: 'Mobile number',
                          controller: _mobileController,
                          isRequired: true,
                          isEnabled: !isSaving,
                          keyboardType: TextInputType.phone,
                          prefixIcon: Icons.phone_outlined,
                          validator: Validators.phone,
                          errorText: submission.errorFor('mobileNumber'),
                        ),
                        Gap.lg,

                        AppTextField(
                          label: 'Address',
                          controller: _addressController,
                          isEnabled: !isSaving,
                          maxLines: 3,
                          minLines: 2,
                          maxLength: ValidationRules.maxRemarkLength,
                          prefixIcon: Icons.home_outlined,
                          validator: (String? value) => Validators.maxLength(
                            value,
                            ValidationRules.maxRemarkLength,
                            field: 'Address',
                          ),
                          errorText: submission.errorFor('address'),
                        ),
                      ],
                    ),
                  ),
                  SizedBox(height: AppSpacing.cardGap),

                  AppSectionCard(
                    title: 'Emergency contact',
                    icon: Icons.emergency_outlined,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        const _SectionNote(
                          'Who HR should call if something happens at work.',
                        ),

                        AppTextField(
                          label: 'Full name',
                          controller: _contactNameController,
                          isEnabled: !isSaving,
                          prefixIcon: Icons.person_outline_rounded,
                          errorText: submission.errorFor('name'),
                        ),
                        Gap.lg,

                        AppTextField(
                          label: 'Relationship',
                          controller: _contactRelationController,
                          isEnabled: !isSaving,
                          hint: 'Mother, spouse, friend',
                          prefixIcon: Icons.diversity_3_outlined,
                        ),
                        Gap.lg,

                        AppTextField(
                          label: 'Mobile number',
                          controller: _contactMobileController,
                          isEnabled: !isSaving,
                          keyboardType: TextInputType.phone,
                          prefixIcon: Icons.phone_outlined,
                          validator: (String? value) {
                            // Required only once a name has been entered, so the
                            // section can be left empty altogether.
                            final bool hasName = _contactNameController.text
                                .trim()
                                .isNotEmpty;
                            return Validators.phone(value, isRequired: hasName);
                          },
                          errorText: submission.errorFor('mobileNumber'),
                        ),
                        Gap.lg,

                        AppTextField(
                          label: 'Alternate number',
                          controller: _contactAlternateController,
                          isEnabled: !isSaving,
                          keyboardType: TextInputType.phone,
                          prefixIcon: Icons.phone_forwarded_outlined,
                          validator: (String? value) =>
                              Validators.phone(value, isRequired: false),
                        ),
                        Gap.lg,

                        AppTextField(
                          label: 'Address',
                          controller: _contactAddressController,
                          isEnabled: !isSaving,
                          maxLines: 3,
                          minLines: 2,
                          maxLength: ValidationRules.maxRemarkLength,
                          prefixIcon: Icons.home_outlined,
                        ),
                      ],
                    ),
                  ),
                  Gap.xxl,

                  AppButton(
                    label: 'Save changes',
                    isLoading: isSaving,
                    onPressed: _save,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A short explanation at the top of a section card.
class _SectionNote extends StatelessWidget {
  const _SectionNote(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: AppSpacing.lg),
      child: Text(text, style: Theme.of(context).textTheme.bodySmall),
    );
  }
}
