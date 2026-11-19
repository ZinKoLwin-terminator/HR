import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_header.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:hr_employee_app/features/profile/presentation/providers/profile_controllers.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Edits the parts of the profile the employee owns: their own mobile and
/// address, and their emergency contact.
///
/// What HR manages (name, job title, department, work email) is shown at the
/// top, locked, so it is clear what can be changed here and what cannot.
///
/// Save is offered only once something has changed, and only the sections
/// that changed are sent.
class EditProfileScreen extends ConsumerStatefulWidget {
  const EditProfileScreen({super.key});

  /// The relationships offered as chips. Anything else is "Other", with an
  /// optional word for what it is.
  static const List<String> relationships = <String>[
    'Mother',
    'Father',
    'Spouse',
    'Sibling',
    'Friend',
    'Other',
  ];

  @override
  ConsumerState<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends ConsumerState<EditProfileScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  final TextEditingController _mobileController = TextEditingController();
  final TextEditingController _addressController = TextEditingController();
  final TextEditingController _contactNameController = TextEditingController();
  final TextEditingController _otherRelationController =
      TextEditingController();
  final TextEditingController _contactMobileController =
      TextEditingController();
  final TextEditingController _contactAlternateController =
      TextEditingController();
  final TextEditingController _contactAddressController =
      TextEditingController();

  late final List<TextEditingController> _controllers = <TextEditingController>[
    _mobileController,
    _addressController,
    _contactNameController,
    _otherRelationController,
    _contactMobileController,
    _contactAlternateController,
    _contactAddressController,
  ];

  /// The profile the form was populated from, used to detect what changed.
  EmployeeProfile? _original;

  /// The chip picked, or null when none is.
  String? _relationship;

  /// The emergency contact lives at the employee's address, so theirs is
  /// copied over instead of typed twice.
  bool _sharesMyAddress = false;

  /// Saved; the button says so for a moment before the screen closes.
  bool _isSaved = false;

  @override
  void initState() {
    super.initState();
    for (final TextEditingController controller in _controllers) {
      controller.addListener(_onEdited);
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      ref.read(editProfileControllerProvider.notifier).reset();
      _populateFrom(ref.read(profileControllerProvider).value);
    });
  }

  void _onEdited() {
    if (_sharesMyAddress &&
        _contactAddressController.text != _addressController.text) {
      // Re-enters this listener once, after which the two agree.
      _contactAddressController.text = _addressController.text;
      return;
    }
    if (mounted) {
      setState(() {});
    }
  }

  /// Fills the fields once, from the loaded profile.
  void _populateFrom(EmployeeProfile? profile) {
    if (profile == null || _original != null) {
      return;
    }
    final EmergencyContact? contact = profile.emergencyContact;
    final String relation = (contact?.relationship ?? '').trim();
    final String ownAddress = (profile.address ?? '').trim();
    final String contactAddress = (contact?.address ?? '').trim();

    setState(() {
      _original = profile;
      _mobileController.text = profile.mobileNumber ?? '';
      _addressController.text = profile.address ?? '';
      _contactNameController.text = contact?.name ?? '';
      _contactMobileController.text = contact?.mobileNumber ?? '';
      _contactAlternateController.text = contact?.alternateNumber ?? '';
      _contactAddressController.text = contact?.address ?? '';

      final String? known = EditProfileScreen.relationships
          .where((String r) => r.toLowerCase() == relation.toLowerCase())
          .firstOrNull;
      if (known != null) {
        _relationship = known;
      } else if (relation.isNotEmpty) {
        // A relationship from before the chips, kept under Other.
        _relationship = 'Other';
        _otherRelationController.text = relation;
      }
      _sharesMyAddress = ownAddress.isNotEmpty && ownAddress == contactAddress;
    });
  }

  @override
  void dispose() {
    for (final TextEditingController controller in _controllers) {
      controller.dispose();
    }
    super.dispose();
  }

  /// The relationship as it will be saved.
  String get _relationshipValue {
    if (_relationship == 'Other') {
      final String other = _otherRelationController.text.trim();
      return other.isEmpty ? 'Other' : other;
    }
    return _relationship ?? '';
  }

  /// Whether anything in the emergency section has been filled in, which
  /// makes its required fields required.
  bool get _hasEmergencyContact =>
      _original?.emergencyContact != null ||
      _contactNameController.text.trim().isNotEmpty ||
      _contactMobileController.text.trim().isNotEmpty ||
      _relationship != null;

  /// The required fields still empty, by label.
  List<String> get _missing => <String>[
    if (_mobileController.text.trim().isEmpty) 'your mobile number',
    if (_hasEmergencyContact) ...<String>[
      if (_contactNameController.text.trim().isEmpty) 'contact name',
      if (_relationship == null) 'relationship',
      if (_contactMobileController.text.trim().isEmpty) 'contact mobile',
    ],
  ];

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
  /// Also null when the whole section is blank, so an employee with no
  /// emergency contact is not forced to invent one.
  EmergencyContact? _emergencyChanges() {
    final EmployeeProfile? original = _original;
    if (original == null) {
      return null;
    }

    final String name = _contactNameController.text.trim();
    final String mobile = _contactMobileController.text.trim();
    final String relationship = _relationshipValue;
    final String alternate = _contactAlternateController.text.trim();
    final String address = _contactAddressController.text.trim();

    if (name.isEmpty && mobile.isEmpty) {
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

  bool get _isDirty =>
      !_isSaved && (_contactChanges() != null || _emergencyChanges() != null);

  void _setSharesMyAddress(bool value) {
    setState(() {
      _sharesMyAddress = value;
      if (value) {
        _contactAddressController.text = _addressController.text;
      }
    });
  }

  Future<void> _save() async {
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false) || _missing.isNotEmpty) {
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
      setState(() => _isSaved = true);
      await Future<void>.delayed(const Duration(milliseconds: 900));
      if (!mounted) {
        return;
      }
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

  /// Leaves, asking first if there are changes that would be lost.
  Future<void> _leave() async {
    if (_isDirty) {
      final bool discard = await ConfirmDialog.show(
        context,
        title: 'Discard changes?',
        message: 'What you changed here has not been saved.',
        confirmLabel: 'Discard',
        cancelLabel: 'Keep editing',
        isDestructive: true,
      );
      if (!discard || !mounted) {
        return;
      }
    }
    // Pops past the PopScope, which only guards the system back gesture.
    _isSaved = true;
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<EmployeeProfile> profile = ref.watch(
      profileControllerProvider,
    );
    final SubmissionState submission = ref.watch(editProfileControllerProvider);
    final bool isSaving = submission.isSubmitting;
    final bool isLocked = isSaving || _isSaved;
    final bool isDirty = _isDirty;

    // Populate once the profile arrives, if the screen opened before it did.
    ref.listen<AsyncValue<EmployeeProfile>>(profileControllerProvider, (
      AsyncValue<EmployeeProfile>? previous,
      AsyncValue<EmployeeProfile> next,
    ) {
      _populateFrom(next.value);
    });

    return PopScope(
      canPop: !isDirty,
      onPopInvokedWithResult: (bool didPop, Object? result) {
        if (!didPop) {
          _leave();
        }
      },
      child: GrievanceScaffold(
        title: 'Edit profile',
        subtitle: 'Contact and emergency details',
        bottomBar: _original == null
            ? null
            : _SaveBar(
                isDirty: isDirty,
                missing: isDirty ? _missing : const <String>[],
                isSaving: isSaving,
                isSaved: _isSaved,
                onCancel: isLocked ? null : _leave,
                onSave: _save,
              ),
        body: AsyncValueView<EmployeeProfile>(
          value: profile,
          loadingMessages: const <String>[
            'Fetching your profile…',
            'Preparing the form…',
            'Almost there…',
          ],
          onRetry: () => ref.invalidate(profileControllerProvider),
          data: (EmployeeProfile data) => Form(
            key: _formKey,
            child: _OutlinedFieldTheme(
              child: ListView(
                padding: AppSpacing.screenPadding,
                children: <Widget>[
                  _ManagedByHr(profile: data),
                  Gap.md,
                  _Section(
                    title: 'Your contact details',
                    note: 'HR uses these to reach you.',
                    children: <Widget>[
                      AppTextField(
                        label: 'Mobile number',
                        controller: _mobileController,
                        isRequired: true,
                        isEnabled: !isLocked,
                        keyboardType: TextInputType.phone,
                        prefixIcon: Icons.phone_outlined,
                        validator: Validators.phone,
                        errorText: submission.errorFor('mobileNumber'),
                      ),
                      Gap.lg,
                      _OptionalField(
                        label: 'Home address',
                        controller: _addressController,
                        isEnabled: !isLocked,
                        errorText: submission.errorFor('address'),
                      ),
                    ],
                  ),
                  Gap.md,
                  _Section(
                    title: 'Emergency contact',
                    note: 'Who HR should call if something happens at work.',
                    children: <Widget>[
                      AppTextField(
                        label: 'Full name',
                        controller: _contactNameController,
                        isRequired: true,
                        isEnabled: !isLocked,
                        prefixIcon: Icons.person_outline_rounded,
                        errorText: submission.errorFor('name'),
                      ),
                      Gap.lg,
                      _RelationshipPicker(
                        selected: _relationship,
                        isEnabled: !isLocked,
                        onSelected: (String? value) =>
                            setState(() => _relationship = value),
                      ),
                      if (_relationship == 'Other') ...<Widget>[
                        Gap.md,
                        AppTextField(
                          label: 'What is their relationship to you?',
                          controller: _otherRelationController,
                          isRequired: false,
                          isEnabled: !isLocked,
                          hint: 'Cousin, neighbour, guardian…',
                          prefixIcon: Icons.diversity_3_outlined,
                        ),
                      ],
                      Gap.lg,
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Expanded(
                            child: AppTextField(
                              label: 'Mobile',
                              controller: _contactMobileController,
                              isRequired: true,
                              isEnabled: !isLocked,
                              keyboardType: TextInputType.phone,
                              prefixIcon: Icons.phone_outlined,
                              validator: (String? value) => Validators.phone(
                                value,
                                isRequired: _hasEmergencyContact,
                              ),
                            ),
                          ),
                          Gap.wMd,
                          Expanded(
                            child: AppTextField(
                              label: 'Alternate',
                              showOptional: true,
                              controller: _contactAlternateController,
                              isRequired: false,
                              isEnabled: !isLocked,
                              hint: 'Another number',
                              keyboardType: TextInputType.phone,
                              prefixIcon: Icons.phone_outlined,
                              validator: (String? value) =>
                                  Validators.phone(value, isRequired: false),
                            ),
                          ),
                        ],
                      ),
                      Gap.sm,
                      Material(
                        // The ink draws on its own Material, not the card's.
                        type: MaterialType.transparency,
                        child: InkWell(
                          onTap: isLocked
                              ? null
                              : () => _setSharesMyAddress(!_sharesMyAddress),
                          borderRadius: BorderRadius.circular(AppRadius.sm),
                          child: Padding(
                            padding: EdgeInsets.symmetric(
                              vertical: AppSpacing.xs,
                            ),
                            child: Row(
                              children: <Widget>[
                                SizedBox.square(
                                  dimension: 24,
                                  child: Checkbox(
                                    value: _sharesMyAddress,
                                    materialTapTargetSize:
                                        MaterialTapTargetSize.shrinkWrap,
                                    visualDensity: VisualDensity.compact,
                                    onChanged: isLocked
                                        ? null
                                        : (bool? value) => _setSharesMyAddress(
                                            value ?? false,
                                          ),
                                  ),
                                ),
                                Gap.wSm,
                                Text(
                                  'Lives at my address',
                                  style: Theme.of(context).textTheme.bodyMedium,
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                      Gap.sm,
                      _OptionalField(
                        label: 'Address',
                        controller: _contactAddressController,
                        // Mirrors the employee's own while ticked.
                        isEnabled: !isLocked,
                        isReadOnly: _sharesMyAddress,
                      ),
                    ],
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

/// What HR manages, locked, so it is clear it cannot be changed here.
class _ManagedByHr extends StatelessWidget {
  const _ManagedByHr({required this.profile});

  final EmployeeProfile profile;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String detail = <String>[
      if ((profile.designation ?? '').isNotEmpty) profile.designation!,
      if ((profile.department ?? '').isNotEmpty) profile.department!,
      profile.email,
    ].join(' · ');

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.primarySurface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Container(
                height: 40,
                width: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: AppColors.apexNavy,
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: Text(
                  profile.fullName.initials,
                  style: textStyles.labelLarge?.copyWith(
                    color: AppColors.textOnPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              Gap.wMd,
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      profile.fullName,
                      style: textStyles.titleSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      detail,
                      style: textStyles.bodySmall?.copyWith(
                        color: palette.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              Gap.wSm,
              Tooltip(
                message: 'Managed by HR',
                child: Icon(
                  Icons.lock_outline_rounded,
                  size: AppSizes.iconSm,
                  color: AppColors.apexNavy,
                ),
              ),
            ],
          ),
          Gap.sm,
          Text(
            'Name, job title, department and work email are managed by HR.',
            style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
          ),
        ],
      ),
    );
  }
}

/// A white card with a heading, a line on what the section is for, and its
/// fields.
class _Section extends StatelessWidget {
  const _Section({
    required this.title,
    required this.note,
    required this.children,
  });

  final String title;
  final String note;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Container(
      padding: EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadow,
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            title,
            style: textStyles.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          Text(
            note,
            style: textStyles.bodySmall?.copyWith(
              color: context.palette.textTertiary,
            ),
          ),
          Gap.lg,
          ...children,
        ],
      ),
    );
  }
}

/// An optional multi-line address field.
class _OptionalField extends StatelessWidget {
  const _OptionalField({
    required this.label,
    required this.controller,
    required this.isEnabled,
    this.isReadOnly = false,
    this.errorText,
  });

  final String label;
  final TextEditingController controller;
  final bool isEnabled;
  final bool isReadOnly;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    return AppTextField(
      label: label,
      showOptional: true,
      controller: controller,
      isRequired: false,
      isEnabled: isEnabled,
      isReadOnly: isReadOnly,
      maxLines: 3,
      minLines: 2,
      maxLength: ValidationRules.maxRemarkLength,
      prefixIcon: Icons.home_outlined,
      errorText: errorText,
      validator: (String? value) => Validators.maxLength(
        value,
        ValidationRules.maxRemarkLength,
        field: label,
      ),
    );
  }
}

/// The relationship, picked from chips rather than typed.
class _RelationshipPicker extends StatelessWidget {
  const _RelationshipPicker({
    required this.selected,
    required this.isEnabled,
    required this.onSelected,
  });

  final String? selected;
  final bool isEnabled;
  final ValueChanged<String?> onSelected;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text.rich(
          TextSpan(
            children: <InlineSpan>[
              const TextSpan(text: 'Relationship'),
              TextSpan(
                text: ' *',
                style: TextStyle(color: palette.danger),
              ),
            ],
          ),
          style: textStyles.labelMedium,
          semanticsLabel: 'Relationship, required',
        ),
        Gap.sm,
        Wrap(
          spacing: AppSpacing.sm,
          runSpacing: AppSpacing.sm,
          children: <Widget>[
            for (final String relationship in EditProfileScreen.relationships)
              ChoiceChip(
                label: Text(relationship),
                selected: selected == relationship,
                showCheckmark: false,
                onSelected: isEnabled
                    ? (bool isOn) => onSelected(isOn ? relationship : null)
                    : null,
                selectedColor: AppColors.apexNavy,
                backgroundColor: AppColors.surface,
                side: BorderSide(
                  color: selected == relationship
                      ? AppColors.apexNavy
                      : palette.border,
                ),
                shape: const StadiumBorder(),
                visualDensity: VisualDensity.compact,
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                labelPadding: EdgeInsets.symmetric(horizontal: AppSpacing.xs),
                labelStyle: textStyles.labelMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: selected == relationship
                      ? AppColors.textOnPrimary
                      : palette.textSecondary,
                ),
              ),
          ],
        ),
      ],
    );
  }
}

/// Cancel and Save, pinned to the foot, with a line on why Save is or is not
/// ready.
class _SaveBar extends StatelessWidget {
  const _SaveBar({
    required this.isDirty,
    required this.missing,
    required this.isSaving,
    required this.isSaved,
    required this.onCancel,
    required this.onSave,
  });

  final bool isDirty;
  final List<String> missing;
  final bool isSaving;
  final bool isSaved;
  final VoidCallback? onCancel;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isMissing = missing.isNotEmpty;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: palette.border)),
      ),
      child: SafeArea(
        top: false,
        minimum: EdgeInsets.all(AppSpacing.md),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            if (isDirty && !isSaving) ...<Widget>[
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Icon(
                    isMissing
                        ? Icons.error_outline_rounded
                        : Icons.edit_note_rounded,
                    size: AppSizes.iconSm,
                    color: isMissing ? palette.danger : palette.textTertiary,
                  ),
                  Gap.wXs,
                  Flexible(
                    child: Text(
                      isMissing
                          ? 'Fill in ${missing.join(', ')}'
                          : 'You have unsaved changes',
                      style: textStyles.labelSmall?.copyWith(
                        color: isMissing
                            ? palette.danger
                            : palette.textTertiary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
              Gap.sm,
            ],
            Row(
              children: <Widget>[
                Expanded(
                  flex: 2,
                  child: AppButton.secondary(
                    label: 'Cancel',
                    onPressed: onCancel,
                  ),
                ),
                Gap.wMd,
                Expanded(
                  flex: 3,
                  child: AppButton(
                    label: 'Save changes',
                    loadingLabel: 'Saving…',
                    successLabel: 'Saved',
                    isLoading: isSaving,
                    isSuccess: isSaved,
                    isEnabled: isDirty && !isMissing,
                    onPressed: onSave,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Fields as white boxes with a border, so they read as editable rather
/// than greyed out.
class _OutlinedFieldTheme extends StatelessWidget {
  const _OutlinedFieldTheme({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final OutlineInputBorder outline = OutlineInputBorder(
      borderRadius: AppRadius.field,
      borderSide: BorderSide(color: context.palette.border),
    );

    return Theme(
      data: theme.copyWith(
        inputDecorationTheme: theme.inputDecorationTheme.copyWith(
          fillColor: AppColors.surface,
          border: outline,
          enabledBorder: outline,
          disabledBorder: outline,
        ),
      ),
      child: child,
    );
  }
}
