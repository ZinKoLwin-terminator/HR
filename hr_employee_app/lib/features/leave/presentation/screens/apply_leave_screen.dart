import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/services/file_picker_service.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
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
  const ApplyLeaveScreen({super.key, this.initialLeaveTypeId});

  /// Preselects this type, when the form is opened from a type's tile.
  final String? initialLeaveTypeId;

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

  /// Set for a beat after the request is accepted, so the button can say
  /// "Request sent" before the screen closes.
  bool _isSent = false;
  final List<PickedFile> _attachments = <PickedFile>[];

  /// Set when the date range itself is invalid; the date fields are pickers,
  /// so a `FormField` validator cannot show it.
  String? _dateError;

  @override
  void initState() {
    super.initState();
    _reasonController.addListener(_onReasonChanged);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(applyLeaveControllerProvider.notifier).reset();
      }
    });
    _preselectLeaveType();
  }

  /// Picks [ApplyLeaveScreen.initialLeaveTypeId] once the types load, unless
  /// the user has already chosen one. An unknown id is ignored.
  Future<void> _preselectLeaveType() async {
    final String? id = widget.initialLeaveTypeId;
    if (id == null) {
      return;
    }
    try {
      final List<LeaveType> types = await ref.read(leaveTypesProvider.future);
      final LeaveType? match = types
          .where((LeaveType t) => t.id == id)
          .firstOrNull;
      if (mounted && match != null && _leaveType == null) {
        setState(() => _leaveType = match);
      }
    } on Object {
      // The form shows the load failure itself; nothing to preselect.
    }
  }

  /// Only the empty/non-empty edge matters to the submit button.
  bool _hadReason = false;

  void _onReasonChanged() {
    final bool hasReason = _reasonController.text.trim().isNotEmpty;
    if (hasReason != _hadReason) {
      setState(() => _hadReason = hasReason);
    }
  }

  @override
  void dispose() {
    _reasonController
      ..removeListener(_onReasonChanged)
      ..dispose();
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

    final List<LeaveBalance> balances =
        ref.read(leaveBalancesProvider(DateTime.now().year)).value ??
        const <LeaveBalance>[];

    final LeaveType? chosen = await showModalBottomSheet<LeaveType>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      backgroundColor: AppColors.surface,
      constraints: BoxConstraints(
        maxHeight: MediaQuery.sizeOf(context).height * 0.8,
      ),
      builder: (BuildContext sheetContext) => _LeaveTypeSheet(
        types: available,
        balances: balances,
        selected: _leaveType,
      ),
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
      setState(() => _isSent = true);
      await Future<void>.delayed(const Duration(milliseconds: 900));
      if (!mounted) {
        return;
      }
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

  /// Every required value is present, so a submit can be attempted. The
  /// server-side rules (balance, overlap) still run on submit.
  bool get _isComplete =>
      _leaveType != null &&
      _startDate != null &&
      _endDate != null &&
      _reasonController.text.trim().isNotEmpty;

  @override
  Widget build(BuildContext context) {
    final ApplyLeaveUiState uiState = ref.watch(applyLeaveControllerProvider);
    final bool isSubmitting = uiState.submission.isSubmitting;
    final AsyncValue<List<LeaveType>> types = ref.watch(leaveTypesProvider);
    final AppConfig config = ref.watch(appConfigProvider);

    final List<LeaveBalance>? balances = ref
        .watch(leaveBalancesProvider(DateTime.now().year))
        .value;
    final LeaveBalance? selectedBalance = _leaveType == null
        ? null
        : balances
              ?.where((LeaveBalance b) => b.leaveTypeId == _leaveType!.id)
              .firstOrNull;

    final double? requestedDays = (_startDate != null && _endDate != null)
        ? LeaveApplication(
            leaveTypeId: _leaveType?.id ?? '',
            startDate: _startDate!,
            endDate: _endDate!,
            reason: '',
            dayPortion: _dayPortion,
          ).estimatedDays
        : null;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
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
                child: Form(
                  key: _formKey,
                  child: ListView(
                    padding: EdgeInsets.zero,
                    children: <Widget>[
                      const _ApplyLeaveHeader(),
                      Transform.translate(
                        offset: const Offset(0, -_ApplyLeaveHeader.overlap),
                        child: _Sheet(
                          children: <Widget>[
                            _FormCard(
                              children: <Widget>[
                                _LeaveTypeField(
                                  type: _leaveType,
                                  balance: selectedBalance,
                                  isLoading: types.isLoading,
                                  isEnabled: !isSubmitting && !types.isLoading,
                                  onTap: _pickLeaveType,
                                ),
                                Gap.lg,
                                _DayPortionSelector(
                                  value: _dayPortion,
                                  isEnabled: !isSubmitting,
                                  onChanged: (LeaveDayPortion portion) =>
                                      setState(() {
                                        _dayPortion = portion;
                                        if (portion !=
                                            LeaveDayPortion.fullDay) {
                                          _endDate = _startDate;
                                        }
                                      }),
                                ),
                                Gap.lg,
                                _DateFields(
                                  startDate: _startDate,
                                  endDate: _endDate,
                                  showEnd: !_isSingleDayPortion,
                                  isEnabled: !isSubmitting,
                                  onPickStart: () => _pickDate(isStart: true),
                                  onPickEnd: () => _pickDate(isStart: false),
                                ),
                                if (_dateError != null) ...<Widget>[
                                  Gap.xs,
                                  Text(
                                    _dateError!,
                                    style: Theme.of(context).textTheme.bodySmall
                                        ?.copyWith(
                                          color: context.palette.danger,
                                        ),
                                  ),
                                ],
                                Gap.lg,
                                _DaysPreview(
                                  days: requestedDays,
                                  type: _leaveType,
                                  balance: selectedBalance,
                                ),
                              ],
                            ),
                            SizedBox(height: AppSpacing.md),
                            _FormCard(
                              children: <Widget>[
                                _ReasonField(
                                  controller: _reasonController,
                                  isEnabled: !isSubmitting,
                                  errorText: uiState.submission.errorFor(
                                    'reason',
                                  ),
                                ),
                                Gap.lg,
                                AppTextField(
                                  label: 'Contact during leave (optional)',
                                  controller: _contactController,
                                  isEnabled: !isSubmitting,
                                  keyboardType: TextInputType.phone,
                                  prefixIcon: Icons.phone_outlined,
                                  hint: 'Phone number',
                                  validator: (String? value) =>
                                      Validators.phone(
                                        value,
                                        isRequired: false,
                                      ),
                                ),
                                Gap.lg,
                                AttachmentPicker(
                                  files: _attachments,
                                  isRequired:
                                      _leaveType?.requiresAttachment ?? false,
                                  isEnabled: !isSubmitting,
                                  onAdd: _addAttachment,
                                  onRemove: (PickedFile file) =>
                                      setState(() => _attachments.remove(file)),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            _SubmitBar(
              isSubmitting: isSubmitting,
              isSent: _isSent,
              isEnabled: _isComplete,
              uploadProgress: isSubmitting && _attachments.isNotEmpty
                  ? uiState.uploadProgress
                  : null,
              onSubmit: _submit,
            ),
          ],
        ),
      ),
    );
  }
}

/// Navy band: back, title and the one-line promise of what happens next.
class _ApplyLeaveHeader extends StatelessWidget {
  const _ApplyLeaveHeader();

  /// How far the form sheet rides up over the header's foot.
  static const double overlap = 24;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
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
            AppSpacing.xl + overlap,
          ),
          child: Row(
            children: <Widget>[
              if (Navigator.of(context).canPop()) ...<Widget>[
                Material(
                  color: onBrand.withValues(alpha: 0.12),
                  shape: CircleBorder(
                    side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: IconButton(
                    onPressed: () => Navigator.of(context).maybePop(),
                    icon: const Icon(Icons.arrow_back_rounded, color: onBrand),
                    iconSize: AppSizes.iconMd,
                    tooltip: MaterialLocalizations.of(context)
                        .backButtonTooltip,
                  ),
                ),
                Gap.wMd,
              ],
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      'Apply leave',
                      style: textStyles.titleLarge?.copyWith(
                        color: onBrand,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      'Your request goes to your manager for approval',
                      style: textStyles.bodySmall?.copyWith(
                        color: onBrand.withValues(alpha: 0.72),
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

/// The Cloud sheet that carries the form cards, rounded where it overlaps
/// the header.
class _Sheet extends StatelessWidget {
  const _Sheet({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Theme.of(context).scaffoldBackgroundColor,
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppRadius.xl * 1.2),
        ),
      ),
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          AppSpacing.lg,
          AppSpacing.lg,
          AppSpacing.lg,
          AppSpacing.lg,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: children,
        ),
      ),
    );
  }
}

/// A white, softly raised group of fields.
class _FormCard extends StatelessWidget {
  const _FormCard({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
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
        children: children,
      ),
    );
  }
}

/// Leave type, shown as the chosen type's colour, name and what is left of
/// it — the figure people check before they apply.
class _LeaveTypeField extends StatelessWidget {
  const _LeaveTypeField({
    required this.type,
    required this.balance,
    required this.isLoading,
    required this.isEnabled,
    required this.onTap,
  });

  final LeaveType? type;
  final LeaveBalance? balance;
  final bool isLoading;
  final bool isEnabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final LeaveType? chosen = type;
    final Color accent = chosen == null
        ? palette.textTertiary
        : ColorUtils.fromHex(chosen.colorHex, fallback: AppColors.summitTeal);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const _RequiredLabel(label: 'Leave type'),
        Gap.xs,
        Semantics(
          button: true,
          label: chosen == null
              ? 'Leave type, not selected'
              : 'Leave type, ${chosen.name}',
          child: ExcludeSemantics(
            child: Material(
              color: AppColors.surface,
              shape: RoundedRectangleBorder(
                borderRadius: AppRadius.field,
                side: BorderSide(color: palette.border),
              ),
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: isEnabled ? onTap : null,
                child: Padding(
                  padding: EdgeInsets.symmetric(
                    horizontal: AppSpacing.md,
                    vertical: AppSpacing.sm + 2,
                  ),
                  child: Row(
                    children: <Widget>[
                      _TypeSwatch(color: accent, isMuted: chosen == null),
                      Gap.wMd,
                      Expanded(
                        child: chosen == null
                            ? Text(
                                isLoading
                                    ? 'Loading leave types…'
                                    : 'Select a leave type',
                                style: textStyles.bodyLarge?.copyWith(
                                  color: palette.textTertiary,
                                ),
                              )
                            : Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  Text(
                                    chosen.name,
                                    style: textStyles.titleSmall?.copyWith(
                                      fontWeight: FontWeight.w700,
                                    ),
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                  Text(
                                    _balanceLine(balance),
                                    style: textStyles.bodySmall?.copyWith(
                                      color: palette.textTertiary,
                                    ),
                                  ),
                                ],
                              ),
                      ),
                      Icon(
                        Icons.keyboard_arrow_down_rounded,
                        color: palette.textTertiary,
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  static String _balanceLine(LeaveBalance? balance) {
    if (balance == null) {
      return 'Balance unavailable';
    }
    if (balance.entitledDays <= 0) {
      return 'Unpaid · no balance to draw from';
    }
    return '${DayCountFormatter.formatNumber(balance.remainingDays)} of '
        '${DayCountFormatter.format(balance.entitledDays)} left';
  }
}

/// A leave type's colour as a dot on a tinted rounded square.
class _TypeSwatch extends StatelessWidget {
  const _TypeSwatch({required this.color, this.isMuted = false});

  final Color color;
  final bool isMuted;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 32,
      width: 32,
      decoration: BoxDecoration(
        color: isMuted
            ? context.palette.surfaceMuted
            : color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(AppRadius.sm + 2),
      ),
      alignment: Alignment.center,
      child: Container(
        height: 8,
        width: 8,
        decoration: BoxDecoration(
          color: isMuted ? context.palette.border : color,
          shape: BoxShape.circle,
        ),
      ),
    );
  }
}

/// A label with the red required asterisk, for fields built here rather
/// than through `AppTextField`.
class _RequiredLabel extends StatelessWidget {
  const _RequiredLabel({required this.label, this.trailing});

  final String label;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Text.rich(
          TextSpan(
            children: <InlineSpan>[
              TextSpan(text: label),
              TextSpan(
                text: ' *',
                style: TextStyle(color: context.palette.danger),
              ),
            ],
          ),
          style: Theme.of(context).textTheme.labelMedium,
          semanticsLabel: '$label, required',
        ),
        const Spacer(),
        if (trailing != null) trailing!,
      ],
    );
  }
}

/// Start and end date pickers, side by side. A half day has no end date.
class _DateFields extends StatelessWidget {
  const _DateFields({
    required this.startDate,
    required this.endDate,
    required this.showEnd,
    required this.isEnabled,
    required this.onPickStart,
    required this.onPickEnd,
  });

  final DateTime? startDate;
  final DateTime? endDate;
  final bool showEnd;
  final bool isEnabled;
  final VoidCallback onPickStart;
  final VoidCallback onPickEnd;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(
          child: AppPickerField(
            label: showEnd ? 'Start date' : 'Date',
            isRequired: true,
            isEnabled: isEnabled,
            prefixIcon: Icons.calendar_today_outlined,
            isCompact: true,
            value: startDate == null
                ? null
                : DateFormatter.dayMonthYear(startDate!),
            hint: 'Select date',
            onTap: onPickStart,
          ),
        ),
        if (showEnd) ...<Widget>[
          Gap.wMd,
          Expanded(
            child: AppPickerField(
              label: 'End date',
              isRequired: true,
              isEnabled: isEnabled,
              prefixIcon: Icons.event_outlined,
              isCompact: true,
              value: endDate == null
                  ? null
                  : DateFormatter.dayMonthYear(endDate!),
              hint: 'Select date',
              onTap: onPickEnd,
            ),
          ),
        ],
      ],
    );
  }
}

/// How many days the request takes, and what it leaves of the balance.
///
/// The count is the client's estimate: the server applies the holiday
/// calendar and the type's weekend rules, so its figure is the one of record.
class _DaysPreview extends StatelessWidget {
  const _DaysPreview({
    required this.days,
    required this.type,
    required this.balance,
  });

  final double? days;
  final LeaveType? type;
  final LeaveBalance? balance;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final double? requested = days;
    final LeaveBalance? b = balance;

    final bool isOver =
        requested != null &&
        b != null &&
        b.entitledDays > 0 &&
        requested > b.remainingDays;

    final (String title, String detail) = switch ((requested, type, b)) {
      (null, _, _) => (
        'Pick a type and dates',
        "We'll show your balance after this request.",
      ),
      (final double d, null, _) => (
        'About ${DayCountFormatter.format(d)}',
        'Choose a leave type to check your balance.',
      ),
      (final double d, _, null) => (
        'About ${DayCountFormatter.format(d)}',
        'HR confirms the final count against the holiday calendar.',
      ),
      (final double d, _, final LeaveBalance bal) when bal.entitledDays <= 0 =>
        (
          'About ${DayCountFormatter.format(d)}',
          'Unpaid leave · no balance to draw from.',
        ),
      (final double d, _, final LeaveBalance bal) when isOver => (
        'About ${DayCountFormatter.format(d)}',
        'More than the ${DayCountFormatter.format(bal.remainingDays)} '
            'you have left.',
      ),
      (final double d, _, final LeaveBalance bal) => (
        'About ${DayCountFormatter.format(d)}',
        'Leaves ${DayCountFormatter.formatNumber(bal.remainingDays - d)} of '
            '${DayCountFormatter.format(bal.entitledDays)}. HR confirms the '
            'final count.',
      ),
    };

    final Color tone = isOver ? palette.danger : palette.textPrimary;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: isOver ? palette.dangerSurface : palette.surfaceMuted,
        borderRadius: AppRadius.field,
      ),
      child: Row(
        children: <Widget>[
          Container(
            height: 44,
            width: 44,
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppRadius.sm + 2),
              boxShadow: const <BoxShadow>[
                BoxShadow(
                  color: AppColors.shadow,
                  blurRadius: 6,
                  offset: Offset(0, 2),
                ),
              ],
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(
                  requested == null
                      ? '–'
                      : DayCountFormatter.formatNumber(requested),
                  style: textStyles.titleSmall?.copyWith(
                    fontWeight: FontWeight.w800,
                    color: tone,
                    height: 1.1,
                  ),
                ),
                Text(
                  'DAYS',
                  style: textStyles.labelSmall?.copyWith(
                    fontSize: 8,
                    letterSpacing: 0.8,
                    color: palette.textTertiary,
                    height: 1.1,
                  ),
                ),
              ],
            ),
          ),
          Gap.wMd,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  style: textStyles.labelLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: tone,
                  ),
                ),
                Text(
                  detail,
                  style: textStyles.bodySmall?.copyWith(
                    color: isOver ? palette.danger : palette.textSecondary,
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

/// Reason, with its character count beside the label rather than under the
/// field.
class _ReasonField extends StatelessWidget {
  const _ReasonField({
    required this.controller,
    required this.isEnabled,
    required this.errorText,
  });

  final TextEditingController controller;
  final bool isEnabled;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    const int maxLength = ValidationRules.maxLeaveReasonLength;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _RequiredLabel(
          label: 'Reason',
          trailing: ValueListenableBuilder<TextEditingValue>(
            valueListenable: controller,
            builder: (BuildContext context, TextEditingValue value, _) => Text(
              '${value.text.characters.length} / $maxLength',
              style: textStyles.labelSmall?.copyWith(
                color: palette.textTertiary,
              ),
            ),
          ),
        ),
        Gap.xs,
        TextFormField(
          controller: controller,
          enabled: isEnabled,
          minLines: 3,
          maxLines: 4,
          maxLength: maxLength,
          style: textStyles.bodyLarge,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: Validators.leaveReason,
          decoration: InputDecoration(
            hintText: 'Give your manager enough context to decide.',
            errorText: errorText,
            counterText: '',
          ),
        ),
      ],
    );
  }
}

/// Pinned to the foot of the screen, so the action is always in reach on a
/// long form.
class _SubmitBar extends StatelessWidget {
  const _SubmitBar({
    required this.isSubmitting,
    required this.isSent,
    required this.isEnabled,
    required this.uploadProgress,
    required this.onSubmit,
  });

  final bool isSubmitting;

  /// The request was accepted; the screen is about to close.
  final bool isSent;

  /// False until every required value is present.
  final bool isEnabled;

  /// Non-null while attachments are uploading.
  final double? uploadProgress;

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
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.md,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              if (uploadProgress != null) ...<Widget>[
                _UploadProgress(progress: uploadProgress!),
                Gap.md,
              ],
              AppButton(
                label: 'Submit request',
                loadingLabel: 'Submitting…',
                successLabel: 'Request sent',
                trailingIcon: Icons.send_rounded,
                isLoading: isSubmitting,
                isSuccess: isSent,
                isEnabled: isEnabled,
                onPressed: onSubmit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Full day / first half / second half, as a segmented control on a muted
/// track.
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
        Opacity(
          opacity: isEnabled ? 1 : 0.6,
          child: Container(
            height: AppSizes.fieldHeight - 4,
            padding: EdgeInsets.all(AppSpacing.xs),
            decoration: BoxDecoration(
              color: context.palette.surfaceMuted,
              borderRadius: AppRadius.field,
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
    final BorderRadius radius = BorderRadius.circular(AppRadius.sm + 2);

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
              color: isSelected ? AppColors.apexNavy : Colors.transparent,
              borderRadius: radius,
            ),
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: isSelected
                    ? AppColors.textOnPrimary
                    : context.palette.textSecondary,
                fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
              ),
            ),
          ),
        ),
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

/// Bottom sheet for choosing a leave type: searchable, with each type's
/// balance as a bar so the choice can be made without leaving the form.
class _LeaveTypeSheet extends StatefulWidget {
  const _LeaveTypeSheet({
    required this.types,
    required this.balances,
    this.selected,
  });

  final List<LeaveType> types;
  final List<LeaveBalance> balances;
  final LeaveType? selected;

  @override
  State<_LeaveTypeSheet> createState() => _LeaveTypeSheetState();
}

class _LeaveTypeSheetState extends State<_LeaveTypeSheet> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String query = _query.trim().toLowerCase();
    final List<LeaveType> visible = widget.types
        .where((LeaveType t) => t.name.toLowerCase().contains(query))
        .toList(growable: false);

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          // Lift the sheet with the keyboard while searching.
          bottom: MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Padding(
              padding: EdgeInsets.fromLTRB(AppSpacing.lg, 0, AppSpacing.xs, 0),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      'Choose leave type',
                      style: textStyles.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(context).pop(),
                    icon: const Icon(Icons.close_rounded),
                    tooltip: MaterialLocalizations.of(context)
                        .closeButtonTooltip,
                  ),
                ],
              ),
            ),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
              child: TextField(
                onChanged: (String value) => setState(() => _query = value),
                textInputAction: TextInputAction.search,
                decoration: InputDecoration(
                  hintText: 'Search leave types',
                  prefixIcon: Icon(
                    Icons.search_rounded,
                    size: AppSizes.iconMd,
                    color: context.palette.textTertiary,
                  ),
                  isDense: true,
                ),
              ),
            ),
            Gap.sm,
            Flexible(
              child: visible.isEmpty
                  ? Padding(
                      padding: EdgeInsets.all(AppSpacing.xl),
                      child: Text(
                        'No leave type matches "${_query.trim()}".',
                        textAlign: TextAlign.center,
                        style: textStyles.bodySmall,
                      ),
                    )
                  : ListView.builder(
                      shrinkWrap: true,
                      padding: EdgeInsets.only(bottom: AppSpacing.md),
                      itemCount: visible.length,
                      itemBuilder: (BuildContext context, int index) {
                        final LeaveType type = visible[index];
                        return _LeaveTypeRow(
                          type: type,
                          balance: widget.balances
                              .where(
                                (LeaveBalance b) => b.leaveTypeId == type.id,
                              )
                              .firstOrNull,
                          isSelected: type.id == widget.selected?.id,
                          onTap: () => Navigator.of(context).pop(type),
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

/// One leave type in the picker: swatch, name, balance bar and a radio.
class _LeaveTypeRow extends StatelessWidget {
  const _LeaveTypeRow({
    required this.type,
    required this.balance,
    required this.isSelected,
    required this.onTap,
  });

  final LeaveType type;
  final LeaveBalance? balance;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = ColorUtils.fromHex(
      type.colorHex,
      fallback: AppColors.summitTeal,
    );
    final LeaveBalance? b = balance;
    final bool hasQuota = b != null && b.entitledDays > 0;
    final bool isExhausted = hasQuota && b.remainingDays <= 0;

    final String figure = switch (b) {
      null => '',
      final LeaveBalance bal when bal.entitledDays <= 0 => 'Unpaid',
      final LeaveBalance bal =>
        '${DayCountFormatter.formatNumber(bal.remainingDays)} / '
            '${DayCountFormatter.formatNumber(bal.entitledDays)}',
    };

    return Semantics(
      button: true,
      selected: isSelected,
      label: '${type.name}${figure.isEmpty ? '' : ', $figure'}',
      child: ExcludeSemantics(
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.sm,
            ),
            child: Row(
              children: <Widget>[
                _TypeSwatch(color: accent),
                Gap.wMd,
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      Row(
                        children: <Widget>[
                          Expanded(
                            child: Text(
                              type.name,
                              style: textStyles.labelLarge?.copyWith(
                                color: palette.textPrimary,
                                fontWeight: FontWeight.w600,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          Text(
                            figure,
                            style: textStyles.labelSmall?.copyWith(
                              color: isExhausted
                                  ? palette.danger
                                  : palette.textTertiary,
                              fontWeight: isExhausted
                                  ? FontWeight.w700
                                  : FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      ClipRRect(
                        borderRadius: BorderRadius.circular(AppRadius.pill),
                        child: LinearProgressIndicator(
                          value: hasQuota
                              ? (b.remainingDays / b.entitledDays).clamp(
                                  0.0,
                                  1.0,
                                )
                              : (b == null ? 0 : 1),
                          minHeight: 3,
                          backgroundColor: palette.surfaceMuted,
                          valueColor: AlwaysStoppedAnimation<Color>(
                            hasQuota ? accent : palette.border,
                          ),
                        ),
                      ),
                      if (type.requiresAttachment) ...<Widget>[
                        Gap.xxs,
                        Text(
                          'Needs a supporting document',
                          style: textStyles.labelSmall?.copyWith(
                            color: palette.textTertiary,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                Gap.wMd,
                _RadioDot(isSelected: isSelected),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _RadioDot extends StatelessWidget {
  const _RadioDot({required this.isSelected});

  final bool isSelected;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      height: 20,
      width: 20,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: isSelected ? AppColors.apexNavy : Colors.transparent,
        border: Border.all(
          color: isSelected ? AppColors.apexNavy : context.palette.border,
          width: 1.5,
        ),
      ),
      child: isSelected
          ? const Icon(
              Icons.check_rounded,
              size: 13,
              color: AppColors.textOnPrimary,
            )
          : null,
    );
  }
}
