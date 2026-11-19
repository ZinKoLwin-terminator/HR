import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/app_error_view.dart';
import 'package:hr_employee_app/core/widgets/app_page_loader.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_controllers.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/anonymity_explainer.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/declaration_dialog.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_header.dart';
import 'package:material_ui/material_ui.dart';

/// New grievance, or edit a draft.
///
/// A draft needs a category and a subject; submitting also needs the
/// minimum lengths and the declaration, confirmed in a dialog. The anonymous
/// switch is offered only for categories that accept it, with an honest
/// account of what HR will and will not see, and only until the draft is
/// first saved. The rules are checked here first with the API's wording; the
/// server's refusal, if any, is put on the field it names.
class GrievanceFormScreen extends ConsumerStatefulWidget {
  const GrievanceFormScreen({super.key, this.grievanceId});

  /// The draft to edit; null for a new grievance.
  final int? grievanceId;

  @override
  ConsumerState<GrievanceFormScreen> createState() =>
      _GrievanceFormScreenState();
}

class _GrievanceFormScreenState extends ConsumerState<GrievanceFormScreen> {
  final TextEditingController _subject = TextEditingController();
  final TextEditingController _description = TextEditingController();
  final TextEditingController _location = TextEditingController();
  final TextEditingController _person = TextEditingController();

  int? _categoryId;

  /// Shown when the draft's category is no longer offered.
  String? _categoryName;
  DateTime? _incidentDate;
  int? _departmentId;
  bool _isAnonymous = false;

  /// Whether the draft being edited has been put into the fields.
  bool _filled = false;

  /// Field (API spelling) to message, from the last local or server check.
  Map<String, String> _errors = const <String, String>{};
  String? _formError;

  bool get _isNew => widget.grievanceId == null;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _isNew) {
        ref.read(grievanceFormControllerProvider.notifier).start(null);
      }
    });
  }

  @override
  void dispose() {
    _subject.dispose();
    _description.dispose();
    _location.dispose();
    _person.dispose();
    super.dispose();
  }

  void _fill(GrievanceDetail draft) {
    _filled = true;
    _categoryId = draft.categoryId;
    _categoryName = draft.categoryName;
    _subject.text = draft.subject;
    _description.text = draft.description;
    _incidentDate = draft.incidentDate;
    _location.text = draft.location ?? '';
    _person.text = draft.involvedPersonText ?? '';
    _departmentId = draft.involvedDepartmentId;
    _isAnonymous = draft.isAnonymous;
    // After this frame: the controller is a provider and must not change
    // while the tree builds.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(grievanceFormControllerProvider.notifier).start(draft);
      }
    });
  }

  GrievanceDraftInput _input(GrievanceCategory? category) =>
      GrievanceDraftInput(
        categoryId: _categoryId ?? 0,
        subject: _subject.text,
        description: _description.text,
        incidentDate: _incidentDate,
        location: _location.text,
        involvedPersonText: _person.text,
        involvedDepartmentId: _departmentId,
        isAnonymous: (category != null && !category.allowsAnonymous)
            ? false
            : _isAnonymous,
      );

  DateTime get _today {
    final DateTime now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  /// Runs the local rules. Returns true when the form may be sent.
  bool _check(GrievanceCategory? category, {required bool forSubmission}) {
    final Map<String, String> errors = GrievanceRules.violations(
      _input(category),
      today: _today,
      category: category,
      forSubmission: forSubmission,
      // Asked for in the dialog that follows.
      declarationAccepted: true,
    );
    setState(() {
      _errors = errors;
      _formError = errors.isEmpty
          ? null
          : (forSubmission
                ? 'Please complete the highlighted fields before submitting.'
                : 'A draft needs a category and a subject.');
    });
    return errors.isEmpty;
  }

  /// Puts a server refusal on the fields it names, and says it.
  void _showFailure(Failure failure) {
    setState(() {
      if (failure is ValidationFailure && failure.fieldErrors.isNotEmpty) {
        _errors = <String, String>{
          for (final MapEntry<String, List<String>> e
              in failure.fieldErrors.entries)
            if (e.value.isNotEmpty) e.key: e.value.first,
        };
      }
      _formError = failure.message;
    });
    AppSnackBar.failure(context, failure);
  }

  Future<void> _saveDraft(GrievanceCategory? category) async {
    FocusScope.of(context).unfocus();
    if (!_check(category, forSubmission: false)) {
      return;
    }
    final GrievanceDetail? saved = await ref
        .read(grievanceFormControllerProvider.notifier)
        .saveDraft(_input(category), category: category);
    if (!mounted) {
      return;
    }
    if (saved != null) {
      AppSnackBar.success(
        context,
        'Draft saved. Nothing has been sent to HR yet.',
      );
      return;
    }
    final Failure? failure = ref
        .read(grievanceFormControllerProvider)
        .submission
        .failureOrNull;
    if (failure != null) {
      _showFailure(failure);
    }
  }

  Future<void> _submit(GrievanceCategory? category) async {
    FocusScope.of(context).unfocus();
    if (!_check(category, forSubmission: true)) {
      return;
    }
    final bool confirmed = await GrievanceDeclarationDialog.show(
      context,
      isAnonymous: _input(category).isAnonymous,
    );
    if (!confirmed || !mounted) {
      return;
    }
    final GrievanceDetail? submitted = await ref
        .read(grievanceFormControllerProvider.notifier)
        .submit(
          _input(category),
          declarationAccepted: true,
          category: category,
        );
    if (!mounted) {
      return;
    }
    if (submitted != null) {
      AppSnackBar.success(
        context,
        submitted.caseNo == null
            ? 'Grievance submitted.'
            : 'Grievance ${submitted.caseNo} submitted.',
      );
      context.pushReplacementNamed(
        RouteNames.grievanceDetail,
        pathParameters: <String, String>{
          RouteParams.grievanceId: '${submitted.id}',
        },
      );
      return;
    }
    final Failure? failure = ref
        .read(grievanceFormControllerProvider)
        .submission
        .failureOrNull;
    if (failure != null) {
      _showFailure(failure);
    }
  }

  Future<void> _pickCategory(GrievanceOptions options, bool isLocked) async {
    final GrievanceCategory? chosen =
        await showModalBottomSheet<GrievanceCategory>(
          context: context,
          isScrollControlled: true,
          showDragHandle: true,
          backgroundColor: AppColors.surface,
          constraints: BoxConstraints(
            maxHeight: MediaQuery.sizeOf(context).height * 0.8,
          ),
          builder: (BuildContext sheetContext) => _CategorySheet(
            categories: options.categories,
            selectedId: _categoryId,
            // An existing anonymous draft cannot move to a named-only
            // category: its anonymity is fixed.
            onlyAnonymous: isLocked && _isAnonymous,
          ),
        );
    if (chosen == null) {
      return;
    }
    setState(() {
      _categoryId = chosen.id;
      _categoryName = chosen.name;
      if (!isLocked && !chosen.allowsAnonymous) {
        _isAnonymous = false;
      }
      _errors = Map<String, String>.of(_errors)
        ..remove('categoryId')
        ..remove('isAnonymous');
    });
  }

  Future<void> _pickDepartment(GrievanceOptions options) async {
    final _DepartmentChoice? chosen =
        await showModalBottomSheet<_DepartmentChoice>(
          context: context,
          showDragHandle: true,
          isScrollControlled: true,
          backgroundColor: AppColors.surface,
          constraints: BoxConstraints(
            maxHeight: MediaQuery.sizeOf(context).height * 0.7,
          ),
          builder: (BuildContext sheetContext) => SafeArea(
            child: ListView(
              shrinkWrap: true,
              children: <Widget>[
                ListTile(
                  title: const Text('None'),
                  selected: _departmentId == null,
                  onTap: () =>
                      Navigator.of(sheetContext)
                          .pop(const _DepartmentChoice(null)),
                ),
                for (final GrievanceDepartment d in options.departments)
                  ListTile(
                    title: Text(d.name),
                    selected: d.id == _departmentId,
                    onTap: () =>
                        Navigator.of(sheetContext).pop(_DepartmentChoice(d.id)),
                  ),
              ],
            ),
          ),
        );
    if (chosen != null) {
      setState(() => _departmentId = chosen.id);
    }
  }

  Future<void> _pickIncidentDate() async {
    final DateTime today = _today;
    final DateTime? picked = await showDatePicker(
      context: context,
      initialDate: _incidentDate ?? today,
      firstDate: DateTime(today.year - 5),
      // Not after today.
      lastDate: today,
      helpText: 'When did it happen?',
    );
    if (picked != null) {
      setState(() {
        _incidentDate = picked;
        _errors = Map<String, String>.of(_errors)..remove('incidentDate');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<GrievanceOptions> options = ref.watch(
      grievanceOptionsProvider,
    );
    final int? id = widget.grievanceId;
    final AsyncValue<GrievanceDetail>? draft = id == null
        ? null
        : ref.watch(grievanceDetailProvider(id));

    return GrievanceScaffold(
      title: _isNew ? 'New grievance' : 'Edit draft',
      subtitle: _isNew
          ? 'Tell HR what happened'
          : 'Finish your draft and submit it when it is ready',
      body: _body(options, draft),
    );
  }

  Widget _body(
    AsyncValue<GrievanceOptions> options,
    AsyncValue<GrievanceDetail>? draft,
  ) {
    final Object? error = options.error ?? draft?.error;
    if (error != null && (options.value == null || draft?.value == null)) {
      return AppErrorView(
        failure: error is Failure ? error : const UnexpectedFailure(),
        onRetry: () {
          ref.invalidate(grievanceOptionsProvider);
          if (widget.grievanceId != null) {
            ref.invalidate(grievanceDetailProvider(widget.grievanceId!));
          }
        },
      );
    }
    final GrievanceOptions? loaded = options.value;
    if (loaded == null || (draft != null && draft.value == null)) {
      return const AppPageLoader(
        messages: <String>[
          'Fetching the categories…',
          'Preparing the form…',
          'Almost there…',
        ],
      );
    }

    final GrievanceDetail? existing = draft?.value;
    if (existing != null && !existing.isDraft) {
      return AppEmptyView(
        icon: Icons.lock_outline_rounded,
        title: 'This grievance has been submitted',
        message:
            'A submitted grievance can no longer be edited. Open it to follow '
            'its progress or to send HR a message.',
        actionLabel: 'Open the grievance',
        onAction: () => context.pushReplacementNamed(
          RouteNames.grievanceDetail,
          pathParameters: <String, String>{
            RouteParams.grievanceId: '${existing.id}',
          },
        ),
      );
    }
    if (existing != null && !_filled) {
      _fill(existing);
    }
    return _form(loaded);
  }

  Widget _form(GrievanceOptions options) {
    final GrievanceFormState formState = ref.watch(
      grievanceFormControllerProvider,
    );
    final bool isBusy = formState.submission.isSubmitting;
    // Fixed once the draft exists on the server.
    final bool isLocked = formState.saved != null || !_isNew;
    final GrievanceCategory? category = options.categoryById(_categoryId);
    final bool anonymousAllowed = category?.allowsAnonymous ?? true;
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String? departmentName = _departmentId == null
        ? null
        : options.departments
              .where((GrievanceDepartment d) => d.id == _departmentId)
              .firstOrNull
              ?.name;

    return Column(
      children: <Widget>[
        Expanded(
          child: ListView(
            padding: AppSpacing.screenPadding,
            children: <Widget>[
              const _PrivacyNote(),
              Gap.md,
              _Card(
                title: 'What happened',
                children: <Widget>[
                  AppPickerField(
                    key: const ValueKey<String>('grievance-category-field'),
                    label: 'Category',
                    isRequired: true,
                    isEnabled: !isBusy,
                    prefixIcon: Icons.category_outlined,
                    value: category?.name ?? _categoryName,
                    hint: 'Choose a category',
                    errorText: _errors['categoryId'],
                    onTap: () => _pickCategory(options, isLocked),
                  ),
                  if (category != null &&
                      category.description.isNotEmpty) ...<Widget>[
                    Gap.xs,
                    Text(
                      category.description,
                      style: textStyles.bodySmall?.copyWith(
                        color: palette.textSecondary,
                      ),
                    ),
                  ],
                  Gap.lg,
                  _CountedField(
                    fieldKey: const ValueKey<String>('grievance-subject-field'),
                    label: 'Subject',
                    isRequired: true,
                    controller: _subject,
                    maxLength: GrievanceRules.subjectMaxLength,
                    isEnabled: !isBusy,
                    hint: 'A short summary',
                    errorText: _errors['subject'],
                  ),
                  Gap.lg,
                  _CountedField(
                    fieldKey: const ValueKey<String>(
                      'grievance-description-field',
                    ),
                    label: 'Description',
                    isRequired: true,
                    controller: _description,
                    maxLength: GrievanceRules.descriptionMaxLength,
                    isEnabled: !isBusy,
                    minLines: 5,
                    maxLines: 10,
                    hint:
                        'What happened, when and where, and who was involved.',
                    minLength: GrievanceRules.descriptionMinLength,
                    errorText: _errors['description'],
                  ),
                  Gap.lg,
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Expanded(
                        child: AppPickerField(
                          key: const ValueKey<String>(
                            'grievance-incident-field',
                          ),
                          label: 'Incident date (optional)',
                          isEnabled: !isBusy,
                          isCompact: true,
                          prefixIcon: Icons.event_outlined,
                          value: _incidentDate == null
                              ? null
                              : DateFormatter.dayMonthYear(_incidentDate!),
                          hint: 'Not after today',
                          errorText: _errors['incidentDate'],
                          onTap: _pickIncidentDate,
                        ),
                      ),
                      Gap.wMd,
                      Expanded(
                        child: AppPickerField(
                          key: const ValueKey<String>(
                            'grievance-department-field',
                          ),
                          label: 'Department (optional)',
                          isEnabled: !isBusy,
                          isCompact: true,
                          prefixIcon: Icons.apartment_outlined,
                          value: departmentName,
                          hint: 'None',
                          errorText: _errors['involvedDepartmentId'],
                          onTap: () => _pickDepartment(options),
                        ),
                      ),
                    ],
                  ),
                  if (_incidentDate != null)
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton(
                        onPressed: isBusy
                            ? null
                            : () => setState(() => _incidentDate = null),
                        child: const Text('Clear date'),
                      ),
                    ),
                  Gap.md,
                  AppTextField(
                    key: const ValueKey<String>('grievance-location-field'),
                    label: 'Location (optional)',
                    controller: _location,
                    isEnabled: !isBusy,
                    maxLength: GrievanceRules.shortTextMaxLength,
                    prefixIcon: Icons.place_outlined,
                    hint: 'e.g. Meeting room B',
                    errorText: _errors['location'],
                  ),
                  Gap.lg,
                  AppTextField(
                    key: const ValueKey<String>('grievance-person-field'),
                    label: 'Person involved (optional)',
                    controller: _person,
                    isEnabled: !isBusy,
                    maxLength: GrievanceRules.shortTextMaxLength,
                    prefixIcon: Icons.person_outline_rounded,
                    hint: 'A name or a role',
                    errorText: _errors['involvedPersonText'],
                  ),
                ],
              ),
              Gap.md,
              _Card(
                title: 'Who is reporting',
                children: <Widget>[
                  AnonymityExplainer(
                    isAnonymous: _isAnonymous && anonymousAllowed,
                    isAllowed: anonymousAllowed,
                    isLocked: isLocked,
                    isEnabled: !isBusy,
                    errorText: _errors['isAnonymous'],
                    onChanged: (bool value) =>
                        setState(() => _isAnonymous = value),
                  ),
                ],
              ),
              if (_formError != null) ...<Widget>[
                Gap.md,
                Text(
                  _formError!,
                  key: const ValueKey<String>('grievance-form-error'),
                  style: textStyles.bodyMedium?.copyWith(color: palette.danger),
                ),
              ],
            ],
          ),
        ),
        _ActionBar(
          isBusy: isBusy,
          onSaveDraft: () => _saveDraft(category),
          onSubmit: () => _submit(category),
        ),
      ],
    );
  }
}

class _DepartmentChoice {
  const _DepartmentChoice(this.id);

  final int? id;
}

/// A white card with a heading.
class _Card extends StatelessWidget {
  const _Card({required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            title,
            style: Theme.of(context).textTheme.titleSmall
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          Gap.md,
          ...children,
        ],
      ),
    );
  }
}

/// A text field with its character count beside the label, as the leave
/// form's reason does.
class _CountedField extends StatelessWidget {
  const _CountedField({
    required this.fieldKey,
    required this.label,
    required this.controller,
    required this.maxLength,
    required this.isEnabled,
    this.isRequired = false,
    this.minLines,
    this.maxLines = 1,
    this.hint,
    this.minLength,
    this.errorText,
  });

  final Key fieldKey;
  final String label;
  final TextEditingController controller;
  final int maxLength;
  final bool isEnabled;
  final bool isRequired;
  final int? minLines;
  final int maxLines;
  final String? hint;

  /// When set, a live note under the field says how many more characters
  /// are needed to submit.
  final int? minLength;

  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text.rich(
              TextSpan(
                children: <InlineSpan>[
                  TextSpan(text: label),
                  if (isRequired)
                    TextSpan(
                      text: ' *',
                      style: TextStyle(color: palette.danger),
                    ),
                ],
              ),
              style: textStyles.labelMedium,
            ),
            const Spacer(),
            ValueListenableBuilder<TextEditingValue>(
              valueListenable: controller,
              builder: (BuildContext context, TextEditingValue value, _) =>
                  Text(
                    '${value.text.characters.length} / $maxLength',
                    style: textStyles.labelSmall?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
            ),
          ],
        ),
        Gap.xs,
        TextField(
          key: fieldKey,
          controller: controller,
          enabled: isEnabled,
          minLines: minLines,
          maxLines: maxLines,
          maxLength: maxLength,
          style: textStyles.bodyLarge,
          decoration: InputDecoration(
            hintText: hint,
            errorText: errorText,
            errorMaxLines: 3,
            counterText: '',
          ),
        ),
        if (minLength != null && errorText == null)
          ValueListenableBuilder<TextEditingValue>(
            valueListenable: controller,
            builder: (BuildContext context, TextEditingValue value, _) {
              final int missing =
                  minLength! - value.text.trim().characters.length;
              if (missing <= 0) {
                return const SizedBox.shrink();
              }
              return Padding(
                padding: EdgeInsets.only(top: AppSpacing.xs),
                child: Text(
                  '• $missing more character${missing == 1 ? '' : 's'} '
                  'needed to submit',
                  style: textStyles.labelSmall?.copyWith(
                    color: palette.textTertiary,
                  ),
                ),
              );
            },
          ),
      ],
    );
  }
}

/// Who can read the grievance, said before anything is typed.
class _PrivacyNote extends StatelessWidget {
  const _PrivacyNote();

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.primarySurface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            Icons.shield_outlined,
            size: AppSizes.iconMd,
            color: AppColors.apexNavy,
          ),
          Gap.wSm,
          Expanded(
            child: Text(
              'Only HR can read what you write here. You can save a draft '
              'and finish it later.',
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: palette.textSecondary),
            ),
          ),
        ],
      ),
    );
  }
}

/// Save draft and Submit, pinned to the foot of the screen.
class _ActionBar extends StatelessWidget {
  const _ActionBar({
    required this.isBusy,
    required this.onSaveDraft,
    required this.onSubmit,
  });

  final bool isBusy;
  final VoidCallback onSaveDraft;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: context.palette.border)),
      ),
      child: SafeArea(
        top: false,
        minimum: EdgeInsets.all(AppSpacing.md),
        child: Row(
          children: <Widget>[
            Expanded(
              child: AppButton.secondary(
                key: const ValueKey<String>('grievance-save-draft'),
                label: 'Save draft',
                isEnabled: !isBusy,
                onPressed: onSaveDraft,
              ),
            ),
            Gap.wMd,
            Expanded(
              child: AppButton(
                key: const ValueKey<String>('grievance-submit'),
                label: 'Submit to HR',
                loadingLabel: 'Submitting…',
                trailingIcon: Icons.send_rounded,
                isLoading: isBusy,
                onPressed: onSubmit,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The categories, each with its description; named-only ones say so.
class _CategorySheet extends StatelessWidget {
  const _CategorySheet({
    required this.categories,
    required this.selectedId,
    required this.onlyAnonymous,
  });

  final List<GrievanceCategory> categories;
  final int? selectedId;
  final bool onlyAnonymous;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final AppPalette palette = context.palette;

    return SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
            child: Text(
              'Choose a category',
              style: textStyles.titleMedium?.copyWith(
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          Gap.sm,
          Flexible(
            child: ListView(
              shrinkWrap: true,
              children: <Widget>[
                for (final GrievanceCategory c in categories)
                  ListTile(
                    enabled: !onlyAnonymous || c.allowsAnonymous,
                    selected: c.id == selectedId,
                    title: Text(
                      c.allowsAnonymous ? c.name : '${c.name} (named only)',
                    ),
                    subtitle: c.description.isEmpty
                        ? null
                        : Text(
                            c.description,
                            style: textStyles.bodySmall?.copyWith(
                              color: palette.textSecondary,
                            ),
                          ),
                    onTap: () => Navigator.of(context).pop(c),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
