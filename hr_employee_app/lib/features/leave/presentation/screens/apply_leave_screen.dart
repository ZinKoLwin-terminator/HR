import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/services/file_picker_service.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/core/widgets/muted_field_theme.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_controllers.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_providers.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/attachment_picker.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:material_ui/material_ui.dart';

/// The leave application form.
///
/// The form owns its own field state; the submission belongs to
/// [ApplyLeaveController]. The estimated day count shown to the user is
/// explicitly labelled as an estimate — only the server knows the holiday
/// calendar and whether weekends count against the chosen type.
class ApplyLeaveScreen extends ConsumerStatefulWidget {
  const ApplyLeaveScreen({super.key});

  @override
  ConsumerState<ApplyLeaveScreen> createState() => _ApplyLeaveScreenState();
}

class _ApplyLeaveScreenState extends ConsumerState<ApplyLeaveScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _reasonController = TextEditingController();
  final TextEditingController _contactController = TextEditingController();

  LeaveType? _leaveType;
  DateTime? _startDate;
  DateTime? _endDate;
  LeaveDayPortion _dayPortion = LeaveDayPortion.fullDay;
  final List<PickedFile> _attachments = <PickedFile>[];

  /// Set when the date range itself is invalid; the date fields are pickers,
  /// so a `FormField` validator cannot show it.
  String? _dateError;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(applyLeaveControllerProvider.notifier).reset();
      }
    });
  }

  @override
  void dispose() {
    _reasonController.dispose();
    _contactController.dispose();
    super.dispose();
  }

  /// A half-day request only ever covers one day, so the end date follows the
  /// start date and the picker for it is hidden.
  bool get _isSingleDayPortion => _dayPortion != LeaveDayPortion.fullDay;

  Future<void> _pickLeaveType() async {
    final AsyncValue<List<LeaveType>> types = ref.read(leaveTypesProvider);
    final List<LeaveType>? available = types.value;

    if (available == null || available.isEmpty) {
      AppSnackBar.info(context, 'Leave types are still loading.');
      return;
    }

    final LeaveType? chosen = await showModalBottomSheet<LeaveType>(
      context: context,
      builder: (BuildContext sheetContext) =>
          _LeaveTypeSheet(types: available, selected: _leaveType),
    );

    if (chosen != null) {
      setState(() {
        _leaveType = chosen;
        // A type that demands a document makes the attachment list required;
        // clearing nothing here, just re-validating on submit.
        _dateError = null;
      });
    }
  }

  Future<void> _pickDate({required bool isStart}) async {
    final DateTime now = DateTime.now();
    final DateTime initial =
        (isStart ? _startDate : _endDate) ?? _startDate ?? now;

    final DateTime? picked = await showDatePicker(
      context: context,
      initialDate: initial,
      // A year back covers a late submission; a year forward covers planning.
      firstDate: DateTime(now.year - 1),
      lastDate: DateTime(now.year + 1, 12, 31),
      helpText: isStart ? 'Select start date' : 'Select end date',
    );

    if (picked == null) {
      return;
    }

    setState(() {
      _dateError = null;
      if (isStart) {
        _startDate = picked;
        // Keep the range coherent rather than letting the user submit an
        // inverted one.
        if (_endDate == null || _endDate!.isBefore(picked)) {
          _endDate = picked;
        }
      } else {
        _endDate = picked;
      }
      if (_isSingleDayPortion) {
        _endDate = _startDate;
      }
    });
  }

  Future<void> _addAttachment() async {
    final Result<List<PickedFile>> result = await ref
        .read(filePickerServiceProvider)
        .pickFiles(
          allowedExtensions: UploadLimits.leaveAttachmentExtensions,
          maxSizeBytes: UploadLimits.maxLeaveAttachmentBytes,
          maxFiles: UploadLimits.maxLeaveAttachments - _attachments.length,
          allowMultiple: true,
        );

    if (!mounted) {
      return;
    }

    result.fold(
      onSuccess: (List<PickedFile> files) {
        if (files.isEmpty) {
          return;
        }
        setState(() {
          final int room =
              UploadLimits.maxLeaveAttachments - _attachments.length;
          _attachments.addAll(files.take(room));
        });
      },
      onFailure: (Failure failure) => AppSnackBar.failure(context, failure),
    );
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();

    final bool isFormValid = _formKey.currentState?.validate() ?? false;

    final String? rangeError = Validators.leaveDateRange(
      startDate: _startDate,
      endDate: _endDate,
    );
    final String? typeError = _leaveType == null
        ? 'Choose a leave type.'
        : null;

    setState(() => _dateError = rangeError ?? typeError);

    if (!isFormValid || rangeError != null || typeError != null) {
      return;
    }

    final LeaveApplication application = LeaveApplication(
      leaveTypeId: _leaveType!.id,
      startDate: _startDate!,
      endDate: _endDate!,
      reason: _reasonController.text.trim(),
      dayPortion: _dayPortion,
      contactDuringLeave: _contactController.text.trim().isEmpty
          ? null
          : _contactController.text.trim(),
      attachmentPaths: _attachments
          .map((PickedFile file) => file.path)
          .toList(growable: false),
    );

    final List<LeaveBalance>? balances = ref
        .read(leaveBalancesProvider(DateTime.now().year))
        .value;
    final LeaveBalance? balance = balances
        ?.where((LeaveBalance b) => b.leaveTypeId == _leaveType!.id)
        .firstOrNull;

    final LeaveRequest? created = await ref
        .read(applyLeaveControllerProvider.notifier)
        .submit(application, leaveType: _leaveType, balance: balance);

    if (!mounted) {
      return;
    }

    if (created != null) {
      AppSnackBar.success(context, 'Leave request submitted.');
      Navigator.of(context).pop();
      return;
    }

    final Failure? failure = ref
        .read(applyLeaveControllerProvider)
        .submission
        .failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  @override
  Widget build(BuildContext context) {
    final ApplyLeaveUiState uiState = ref.watch(applyLeaveControllerProvider);
    final bool isSubmitting = uiState.submission.isSubmitting;
    final AsyncValue<List<LeaveType>> types = ref.watch(leaveTypesProvider);

    final List<LeaveBalance>? balances = ref
        .watch(leaveBalancesProvider(DateTime.now().year))
        .value;
    final LeaveBalance? selectedBalance = _leaveType == null
        ? null
        : balances
              ?.where((LeaveBalance b) => b.leaveTypeId == _leaveType!.id)
              .firstOrNull;

    return Scaffold(
      body: Form(
        key: _formKey,
        child: ListView(
          padding: EdgeInsets.only(bottom: AppSpacing.xxxl),
          children: <Widget>[
            _ApplyLeaveHeader(
              leaveTypeName: _leaveType?.name,
              balance: selectedBalance,
            ),
            Padding(
              padding: AppSpacing.screenHorizontal,
              child: MutedFieldTheme(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    AppSectionCard(
                      title: 'Leave details',
                      subtitle: 'Type, duration and dates',
                      icon: Icons.event_note_outlined,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          AppPickerField(
                            label: 'Leave type',
                            isRequired: true,
                            isEnabled: !isSubmitting && !types.isLoading,
                            prefixIcon: Icons.category_outlined,
                            value: _leaveType?.name,
                            hint: types.isLoading
                                ? 'Loading leave types...'
                                : 'Select a leave type',
                            onTap: _pickLeaveType,
                          ),
                          Gap.lg,

                          _DayPortionSelector(
                            value: _dayPortion,
                            isEnabled: !isSubmitting,
                            onChanged: (LeaveDayPortion portion) =>
                                setState(() {
                                  _dayPortion = portion;
                                  if (portion != LeaveDayPortion.fullDay) {
                                    _endDate = _startDate;
                                  }
                                }),
                          ),
                          Gap.lg,

                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: <Widget>[
                              Expanded(
                                child: AppPickerField(
                                  label: 'Start date',
                                  isRequired: true,
                                  isEnabled: !isSubmitting,
                                  prefixIcon: Icons.calendar_today_outlined,
                                  isCompact: true,
                                  value: _startDate == null
                                      ? null
                                      : DateFormatter.dayMonthYear(_startDate!),
                                  hint: 'Select',
                                  onTap: () => _pickDate(isStart: true),
                                ),
                              ),
                              if (!_isSingleDayPortion) ...<Widget>[
                                Gap.wMd,
                                Expanded(
                                  child: AppPickerField(
                                    label: 'End date',
                                    isRequired: true,
                                    isEnabled: !isSubmitting,
                                    prefixIcon: Icons.event_outlined,
                                    isCompact: true,
                                    value: _endDate == null
                                        ? null
                                        : DateFormatter.dayMonthYear(_endDate!),
                                    hint: 'Select',
                                    onTap: () => _pickDate(isStart: false),
                                  ),
                                ),
                              ],
                            ],
                          ),

                          if (_dateError != null) ...<Widget>[
                            Gap.xs,
                            Text(
                              _dateError!,
                              style: Theme.of(context).textTheme.bodySmall
                                  ?.copyWith(color: context.palette.danger),
                            ),
                          ],

                          if (_startDate != null &&
                              _endDate != null) ...<Widget>[
                            Gap.md,
                            _EstimatedDays(
                              days: LeaveApplication(
                                leaveTypeId: _leaveType?.id ?? '',
                                startDate: _startDate!,
                                endDate: _endDate!,
                                reason: '',
                                dayPortion: _dayPortion,
                              ).estimatedDays,
                            ),
                          ],
                        ],
                      ),
                    ),
                    SizedBox(height: AppSpacing.cardGap),

                    AppSectionCard(
                      title: 'Supporting details',
                      subtitle: 'Help your manager decide quickly',
                      icon: Icons.description_outlined,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          AppTextField(
                            label: 'Reason',
                            controller: _reasonController,
                            isRequired: true,
                            isEnabled: !isSubmitting,
                            maxLines: 4,
                            minLines: 3,
                            maxLength: ValidationRules.maxLeaveReasonLength,
                            hint: 'Give your manager enough context to decide.',
                            validator: Validators.leaveReason,
                            errorText: uiState.submission.errorFor('reason'),
                          ),
                          Gap.lg,

                          AppTextField(
                            label: 'Contact during leave',
                            controller: _contactController,
                            isEnabled: !isSubmitting,
                            keyboardType: TextInputType.phone,
                            prefixIcon: Icons.phone_outlined,
                            hint: 'Optional',
                            validator: (String? value) =>
                                Validators.phone(value, isRequired: false),
                          ),
                          Gap.lg,

                          AttachmentPicker(
                            files: _attachments,
                            isRequired: _leaveType?.requiresAttachment ?? false,
                            isEnabled: !isSubmitting,
                            onAdd: _addAttachment,
                            onRemove: (PickedFile file) =>
                                setState(() => _attachments.remove(file)),
                          ),
                        ],
                      ),
                    ),
                    Gap.xxl,

                    if (isSubmitting && _attachments.isNotEmpty) ...<Widget>[
                      _UploadProgress(progress: uiState.uploadProgress),
                      Gap.lg,
                    ],

                    AppButton(
                      label: 'Submit request',
                      isLoading: isSubmitting,
                      onPressed: _submit,
                    ),
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

/// Brand-coloured header carrying the title and, once a type is chosen, how
/// much of it is left — the figure people check before applying.
class _ApplyLeaveHeader extends StatelessWidget {
  const _ApplyLeaveHeader({required this.leaveTypeName, required this.balance});

  final String? leaveTypeName;
  final LeaveBalance? balance;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color onBrand = Theme.of(context).colorScheme.onPrimary;

    final String chipText = switch ((leaveTypeName, balance)) {
      (null, _) => 'Choose a leave type to see your balance',
      (final String name, final LeaveBalance b) =>
        '$name · ${DayCountFormatter.format(b.remainingDays)} left',
      (final String name, null) => name,
    };

    return Container(
      margin: EdgeInsets.only(bottom: AppSpacing.lg),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: context.palette.brandGradient,
        ),
        borderRadius: BorderRadius.vertical(
          bottom: Radius.circular(AppRadius.xl),
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.xs,
            AppSpacing.xs,
            AppSpacing.lg,
            AppSpacing.xl,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  if (Navigator.of(context).canPop())
                    BackButton(color: onBrand)
                  else
                    SizedBox(width: AppSpacing.md),
                  Expanded(
                    child: Text(
                      'Apply for leave',
                      style: textStyles.titleLarge?.copyWith(color: onBrand),
                    ),
                  ),
                ],
              ),
              Padding(
                padding: EdgeInsets.only(left: AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'Your request goes to your manager for approval.',
                      style: textStyles.bodyMedium?.copyWith(
                        color: onBrand.withValues(alpha: 0.8),
                      ),
                    ),
                    Gap.md,
                    Container(
                      padding: EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.xs + AppSpacing.xxs,
                      ),
                      decoration: BoxDecoration(
                        color: onBrand.withValues(alpha: 0.14),
                        borderRadius: AppRadius.chip,
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Icon(
                            Icons.account_balance_wallet_outlined,
                            size: AppSizes.iconSm,
                            color: onBrand,
                          ),
                          Gap.wSm,
                          Flexible(
                            child: Text(
                              chipText,
                              overflow: TextOverflow.ellipsis,
                              style: textStyles.labelMedium?.copyWith(
                                color: onBrand,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Full day / first half / second half.
class _DayPortionSelector extends StatelessWidget {
  const _DayPortionSelector({
    required this.value,
    required this.onChanged,
    required this.isEnabled,
  });

  final LeaveDayPortion value;
  final ValueChanged<LeaveDayPortion> onChanged;
  final bool isEnabled;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text('Duration', style: theme.textTheme.labelMedium),
        Gap.xs,
        // Styled as a field (same fill, border, radius and height as the
        // pickers around it) rather than Material's default SegmentedButton,
        // whose dark outline and tinted fill clashed with the rest of the form.
        Opacity(
          opacity: isEnabled ? 1 : 0.6,
          child: Container(
            height: AppSizes.fieldHeight,
            padding: EdgeInsets.all(AppSpacing.xs),
            decoration: BoxDecoration(
              color: theme.inputDecorationTheme.fillColor,
              borderRadius: AppRadius.field,
              // Follows the surrounding field outline so it stays in step
              // with the pickers when a parent restyles them.
              border: Border.all(
                color: switch (theme.inputDecorationTheme.enabledBorder) {
                  final OutlineInputBorder b => b.borderSide.color,
                  _ => context.palette.border,
                },
              ),
            ),
            child: Row(
              children: <Widget>[
                for (final LeaveDayPortion portion in LeaveDayPortion.values)
                  Expanded(
                    child: _PortionSegment(
                      label: portion.label,
                      isSelected: portion == value,
                      onTap: isEnabled ? () => onChanged(portion) : null,
                    ),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// One option inside [_DayPortionSelector].
class _PortionSegment extends StatelessWidget {
  const _PortionSegment({
    required this.label,
    required this.isSelected,
    required this.onTap,
  });

  final String label;
  final bool isSelected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final ColorScheme colors = Theme.of(context).colorScheme;
    final BorderRadius radius = BorderRadius.circular(AppRadius.sm);

    return Semantics(
      button: true,
      selected: isSelected,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: radius,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 180),
            curve: Curves.easeOut,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: isSelected ? colors.primary : Colors.transparent,
              borderRadius: radius,
            ),
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: isSelected
                    ? colors.onPrimary
                    : context.palette.textSecondary,
                fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The client's estimate of how many days the request consumes.
///
/// Labelled an estimate on purpose: the server applies the holiday calendar
/// and the leave type's weekend rules, so its figure is the one of record.
class _EstimatedDays extends StatelessWidget {
  const _EstimatedDays({required this.days});

  final double days;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: palette.infoSurface,
        borderRadius: AppRadius.field,
      ),
      child: Row(
        children: <Widget>[
          Icon(
            Icons.info_outline_rounded,
            size: AppSizes.iconMd,
            color: palette.info,
          ),
          Gap.wSm,
          Expanded(
            child: Text(
              'About ${DayCountFormatter.format(days)}. HR confirms the '
              'final count against the holiday calendar.',
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: palette.info),
            ),
          ),
        ],
      ),
    );
  }
}

/// Determinate upload bar, shown only when there are attachments.
class _UploadProgress extends StatelessWidget {
  const _UploadProgress({required this.progress});

  final double progress;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          'Uploading attachments ${(progress * 100).round()}%',
          style: Theme.of(context).textTheme.bodySmall,
        ),
        Gap.xs,
        ClipRRect(
          borderRadius: BorderRadius.circular(AppRadius.pill),
          child: LinearProgressIndicator(
            value: progress == 0 ? null : progress,
            minHeight: 6,
          ),
        ),
      ],
    );
  }
}

/// Bottom sheet listing the selectable leave types.
class _LeaveTypeSheet extends StatelessWidget {
  const _LeaveTypeSheet({required this.types, this.selected});

  final List<LeaveType> types;
  final LeaveType? selected;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Padding(
              padding: AppSpacing.screenHorizontal,
              child: Text(
                'Leave type',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            Gap.md,
            Flexible(
              child: ListView.builder(
                shrinkWrap: true,
                itemCount: types.length,
                itemBuilder: (BuildContext context, int index) {
                  final LeaveType type = types[index];
                  final bool isSelected = type.id == selected?.id;

                  return ListTile(
                    onTap: () => Navigator.of(context).pop(type),
                    title: Text(type.name),
                    subtitle: type.requiresAttachment
                        ? const Text('Requires a supporting document')
                        : null,
                    trailing: isSelected
                        ? Icon(
                            Icons.check_circle_rounded,
                            color: Theme.of(context).colorScheme.primary,
                          )
                        : null,
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
