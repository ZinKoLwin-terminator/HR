import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/router/app_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/password_controllers.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Changes the password of the signed-in user.
///
/// Reached from Profile, and also forced by the router when the server has
/// flagged the account with `mustChangePassword`. [isForced] hides the back
/// affordance for that case, so the user cannot skip it.
///
/// The rules are shown as a live checklist under the new password, with a
/// strength bar, and the confirmation says whether it matches as it is typed:
/// the user should never have to submit to find out what is wrong. Update
/// stays disabled until everything is satisfied.
class ChangePasswordScreen extends ConsumerStatefulWidget {
  const ChangePasswordScreen({super.key, this.isForced = false});

  final bool isForced;

  @override
  ConsumerState<ChangePasswordScreen> createState() =>
      _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends ConsumerState<ChangePasswordScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _currentController = TextEditingController();
  final TextEditingController _newController = TextEditingController();
  final TextEditingController _confirmController = TextEditingController();

  @override
  void initState() {
    super.initState();
    // The checklist, match line and button all follow the fields.
    for (final TextEditingController c in <TextEditingController>[
      _currentController,
      _newController,
      _confirmController,
    ]) {
      c.addListener(_onChanged);
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(changePasswordControllerProvider.notifier).reset();
      }
    });
  }

  void _onChanged() => setState(() {});

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  _PasswordCheck get _check => _PasswordCheck(
    current: _currentController.text,
    next: _newController.text,
    confirm: _confirmController.text,
  );

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    // The checklist and match line have already said what is missing; the
    // button is disabled until they are satisfied.
    if (!(_formKey.currentState?.validate() ?? false) || !_check.isReady) {
      return;
    }

    final bool succeeded = await ref
        .read(changePasswordControllerProvider.notifier)
        .submit(
          currentPassword: _currentController.text,
          newPassword: _newController.text,
        );

    if (!mounted) {
      return;
    }

    final SubmissionState state = ref.read(changePasswordControllerProvider);
    if (succeeded) {
      AppSnackBar.success(context, 'Your password has been changed.');
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
      return;
    }
    if (state.failureOrNull != null) {
      AppSnackBar.failure(context, state.failureOrNull!);
    }
  }

  /// The reset-link flow lives on the sign-in screens, which a signed-in
  /// user cannot reach; so this signs out first, then opens it.
  Future<void> _forgotCurrent() async {
    final bool confirmed = await ConfirmDialog.show(
      context,
      title: 'Reset your password?',
      message:
          'You will be signed out, and can then ask for a reset link to be '
          'emailed to you.',
      confirmLabel: 'Sign out and reset',
      icon: Icons.lock_reset_rounded,
    );
    if (!confirmed || !mounted) {
      return;
    }
    final GoRouter router = ref.read(routerProvider);
    await ref.read(authControllerProvider.notifier).signOut();
    router.goNamed(RouteNames.forgotPassword);
  }

  @override
  Widget build(BuildContext context) {
    final SubmissionState submission = ref.watch(
      changePasswordControllerProvider,
    );
    final AppConfig config = ref.watch(appConfigProvider);
    final bool isBusy = submission.isSubmitting;
    final _PasswordCheck check = _check;

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
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    _Header(canGoBack: !widget.isForced),
                    Expanded(
                      child: Form(
                        key: _formKey,
                        child: ListView(
                          padding: AppSpacing.screenPadding,
                          children: <Widget>[
                            if (widget.isForced) ...<Widget>[
                              const _Note(
                                icon: Icons.priority_high_rounded,
                                text:
                                    'Your account is on a temporary password. '
                                    'Set a new one to continue.',
                                isWarning: true,
                              ),
                              Gap.sm,
                            ],
                            const _Note(
                              icon: Icons.smartphone_rounded,
                              text:
                                  'You stay signed in on this phone. Other '
                                  'devices will be signed out.',
                            ),
                            Gap.md,
                            _FormCard(
                              children: <Widget>[
                                AppPasswordField(
                                  label: 'Current password',
                                  controller: _currentController,
                                  isRequired: false,
                                  isEnabled: !isBusy,
                                  textInputAction: TextInputAction.next,
                                  validator: Validators.loginPassword,
                                  errorText: submission.errorFor(
                                    'currentPassword',
                                  ),
                                ),
                                Padding(
                                  padding: EdgeInsets.symmetric(
                                    vertical: AppSpacing.lg,
                                  ),
                                  child: const Divider(
                                    height: 1,
                                    color: AppColors.divider,
                                  ),
                                ),
                                AppPasswordField(
                                  label: 'New password',
                                  controller: _newController,
                                  isRequired: false,
                                  isEnabled: !isBusy,
                                  textInputAction: TextInputAction.next,
                                  // No live error here: the checklist below
                                  // says the same thing, without the red.
                                  errorText: submission.errorFor('newPassword'),
                                  autofillHints: const <String>[
                                    AutofillHints.newPassword,
                                  ],
                                ),
                                Gap.sm,
                                _StrengthMeter(check: check),
                                Gap.sm,
                                _Rule(
                                  label:
                                      'At least '
                                      '${ValidationRules.minPasswordLength} '
                                      'characters',
                                  isMet: check.isLongEnough,
                                ),
                                _Rule(
                                  label: 'A letter and a number',
                                  isMet: check.hasLetterAndNumber,
                                ),
                                _Rule(
                                  label: 'Different from your current password',
                                  isMet: check.isDifferent,
                                ),
                                Gap.lg,
                                _ConfirmField(
                                  controller: _confirmController,
                                  isEnabled: !isBusy,
                                  isMismatched: check.isMismatched,
                                  original: _newController,
                                  onSubmitted: _submit,
                                ),
                                if (check.confirm.isNotEmpty) ...<Widget>[
                                  Gap.sm,
                                  _MatchLine(isMatching: check.isMatching),
                                ],
                              ],
                            ),
                            Gap.md,
                            if (!widget.isForced)
                              Center(
                                child: TextButton(
                                  onPressed: isBusy ? null : _forgotCurrent,
                                  style: TextButton.styleFrom(
                                    foregroundColor: AppColors.link,
                                  ),
                                  child: const Text(
                                    'Forgot your current password?',
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                    _UpdateBar(
                      isBusy: isBusy,
                      isEnabled: check.isReady,
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

/// The rules, worked out from what is typed.
class _PasswordCheck {
  const _PasswordCheck({
    required this.current,
    required this.next,
    required this.confirm,
  });

  final String current;
  final String next;
  final String confirm;

  static final RegExp _letter = RegExp('[A-Za-z]');
  static final RegExp _digit = RegExp(r'\d');
  static final RegExp _symbol = RegExp(r'[^A-Za-z0-9]');

  bool get isLongEnough => next.length >= ValidationRules.minPasswordLength;

  bool get hasLetterAndNumber =>
      _letter.hasMatch(next) && _digit.hasMatch(next);

  bool get isDifferent => next.isNotEmpty && next != current;

  bool get isMatching => confirm.isNotEmpty && confirm == next;

  bool get isMismatched => confirm.isNotEmpty && confirm != next;

  /// Everything the server will check, so Update can be offered.
  bool get isReady =>
      current.isNotEmpty &&
      Validators.newPassword(next) == null &&
      isDifferent &&
      isMatching;

  /// 0 when empty; 1 too short; then 2–4 by how much more it does than the
  /// minimum. A guide, not a promise: the server's policy decides.
  int get score {
    if (next.isEmpty) {
      return 0;
    }
    if (!isLongEnough) {
      return 1;
    }
    int score = 1;
    if (hasLetterAndNumber) {
      score += 1;
    }
    if (next.length >= 12 ||
        _symbol.hasMatch(next) ||
        (next.contains(RegExp('[A-Z]')) && next.contains(RegExp('[a-z]')))) {
      score += 1;
    }
    if (next.length >= 14 && _symbol.hasMatch(next)) {
      score += 1;
    }
    return score.clamp(1, 4);
  }
}

/// Four bars and a word, in the colour of the strength.
class _StrengthMeter extends StatelessWidget {
  const _StrengthMeter({required this.check});

  final _PasswordCheck check;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final int score = check.score;
    final (String word, Color colour) = switch (score) {
      0 => ('', palette.border),
      1 => ('Too short', palette.danger),
      2 => ('Fair', palette.warning),
      3 => ('Good', AppColors.summitTeal),
      _ => ('Strong', palette.success),
    };

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            for (int i = 0; i < 4; i++) ...<Widget>[
              Expanded(
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 200),
                  height: 4,
                  decoration: BoxDecoration(
                    color: i < score ? colour : palette.border,
                    borderRadius: BorderRadius.circular(AppRadius.pill),
                  ),
                ),
              ),
              if (i < 3) const SizedBox(width: 4),
            ],
          ],
        ),
        if (word.isNotEmpty) ...<Widget>[
          Gap.xs,
          Text(
            'Strength: $word',
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: score == 1 ? palette.textTertiary : colour,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ],
    );
  }
}

/// One rule with a tick that fills in green once it is met.
class _Rule extends StatelessWidget {
  const _Rule({required this.label, required this.isMet});

  final String label;
  final bool isMet;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Semantics(
      label: '$label, ${isMet ? 'done' : 'not yet'}',
      child: ExcludeSemantics(
        child: Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Row(
            children: <Widget>[
              AnimatedContainer(
                duration: const Duration(milliseconds: 200),
                height: 16,
                width: 16,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: isMet ? palette.successSurface : palette.surfaceMuted,
                ),
                child: Icon(
                  Icons.check_rounded,
                  size: 11,
                  color: isMet ? palette.success : palette.textTertiary,
                ),
              ),
              Gap.wSm,
              Expanded(
                child: Text(
                  label,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: isMet ? palette.success : palette.textTertiary,
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

/// The confirmation field, outlined red while it does not match.
class _ConfirmField extends StatelessWidget {
  const _ConfirmField({
    required this.controller,
    required this.original,
    required this.isEnabled,
    required this.isMismatched,
    required this.onSubmitted,
  });

  final TextEditingController controller;
  final TextEditingController original;
  final bool isEnabled;
  final bool isMismatched;
  final VoidCallback onSubmitted;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Widget field = AppPasswordField(
      label: 'Confirm new password',
      controller: controller,
      isRequired: false,
      isEnabled: isEnabled,
      textInputAction: TextInputAction.done,
      onFieldSubmitted: (_) => onSubmitted(),
    );
    if (!isMismatched) {
      return field;
    }

    final OutlineInputBorder red = OutlineInputBorder(
      borderRadius: AppRadius.field,
      borderSide: BorderSide(color: context.palette.danger),
    );
    return Theme(
      data: theme.copyWith(
        inputDecorationTheme: theme.inputDecorationTheme.copyWith(
          enabledBorder: red,
          border: red,
          focusedBorder: red.copyWith(
            borderSide: BorderSide(color: context.palette.danger, width: 1.5),
          ),
        ),
      ),
      child: field,
    );
  }
}

class _MatchLine extends StatelessWidget {
  const _MatchLine({required this.isMatching});

  final bool isMatching;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color colour = isMatching ? palette.success : palette.danger;

    return Semantics(
      liveRegion: true,
      child: Row(
        children: <Widget>[
          Container(
            height: 6,
            width: 6,
            decoration: BoxDecoration(color: colour, shape: BoxShape.circle),
          ),
          Gap.wSm,
          Text(
            isMatching ? 'Passwords match' : 'Passwords do not match yet',
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: colour,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

/// Navy band: back (unless the change is forced), title and subtitle.
class _Header extends StatelessWidget {
  const _Header({required this.canGoBack});

  final bool canGoBack;

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
            AppSpacing.lg,
            AppSpacing.sm,
            AppSpacing.lg,
            AppSpacing.md,
          ),
          child: Row(
            children: <Widget>[
              if (canGoBack && Navigator.of(context).canPop()) ...<Widget>[
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
                    tooltip: MaterialLocalizations.of(
                      context,
                    ).backButtonTooltip,
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
                      'Change password',
                      style: textStyles.titleLarge?.copyWith(
                        color: onBrand,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      'Keep your account secure',
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

/// A tinted line of context above the form.
class _Note extends StatelessWidget {
  const _Note({required this.icon, required this.text, this.isWarning = false});

  final IconData icon;
  final String text;
  final bool isWarning;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color tone = isWarning ? palette.warning : AppColors.apexNavy;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: isWarning ? palette.warningSurface : AppColors.primarySurface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, size: AppSizes.iconMd, color: tone),
          Gap.wSm,
          Expanded(
            child: Text(
              text,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: isWarning ? tone : palette.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// White, softly raised card holding the fields.
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

/// Update password, pinned to the foot of the screen.
class _UpdateBar extends StatelessWidget {
  const _UpdateBar({
    required this.isBusy,
    required this.isEnabled,
    required this.onPressed,
  });

  final bool isBusy;
  final bool isEnabled;
  final VoidCallback onPressed;

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
        child: AppButton(
          label: 'Update password',
          loadingLabel: 'Updating…',
          isLoading: isBusy,
          isEnabled: isEnabled,
          onPressed: onPressed,
        ),
      ),
    );
  }
}
