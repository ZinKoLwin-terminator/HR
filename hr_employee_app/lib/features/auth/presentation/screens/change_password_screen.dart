import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/muted_field_theme.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/password_controllers.dart';
import 'package:hr_employee_app/features/auth/presentation/widgets/password_policy_hint.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Changes the password of the signed-in user.
///
/// Reached from Profile, and also forced by the router when the server has
/// flagged the account with `mustChangePassword`. [isForced] hides the back
/// affordance for that case, so the user cannot skip it.
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
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(changePasswordControllerProvider.notifier).reset();
      }
    });
  }

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false)) {
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

  @override
  Widget build(BuildContext context) {
    final SubmissionState submission = ref.watch(
      changePasswordControllerProvider,
    );
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Scaffold(
      appBar: BrandAppBar(
        title: const Text('Change password'),
        automaticallyImplyLeading: !widget.isForced,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: AppSpacing.screenPadding,
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                if (widget.isForced) ...<Widget>[
                  const _ForcedChangeNotice(),
                  Gap.xl,
                ],

                AppSectionCard(
                  title: 'Update your password',
                  icon: Icons.lock_reset_rounded,
                  child: MutedFieldTheme(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        Text(
                          'You will stay signed in on this device. Other devices will '
                          'be signed out.',
                          style: textStyles.bodyMedium,
                        ),
                        Gap.lg,

                        AppPasswordField(
                          label: 'Current password',
                          controller: _currentController,
                          isEnabled: !submission.isSubmitting,
                          textInputAction: TextInputAction.next,
                          validator: Validators.loginPassword,
                          errorText: submission.errorFor('currentPassword'),
                        ),
                        Gap.lg,

                        AppPasswordField(
                          label: 'New password',
                          controller: _newController,
                          isEnabled: !submission.isSubmitting,
                          textInputAction: TextInputAction.next,
                          validator: Validators.newPassword,
                          errorText: submission.errorFor('newPassword'),
                          autofillHints: const <String>[
                            AutofillHints.newPassword,
                          ],
                        ),
                        Gap.sm,
                        const PasswordPolicyHint(),
                        Gap.lg,

                        AppPasswordField(
                          label: 'Confirm new password',
                          controller: _confirmController,
                          isEnabled: !submission.isSubmitting,
                          textInputAction: TextInputAction.done,
                          validator: (String? value) =>
                              Validators.confirmPassword(
                                value,
                                _newController.text,
                              ),
                          onFieldSubmitted: (_) => _submit(),
                        ),
                      ],
                    ),
                  ),
                ),
                Gap.xxl,

                AppButton(
                  label: 'Change password',
                  isLoading: submission.isSubmitting,
                  onPressed: _submit,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Explains why the user cannot leave this screen.
class _ForcedChangeNotice extends StatelessWidget {
  const _ForcedChangeNotice();

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: palette.warningSurface,
        borderRadius: AppRadius.field,
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            Icons.priority_high_rounded,
            size: AppSizes.iconMd,
            color: palette.warning,
          ),
          Gap.wSm,
          Expanded(
            child: Text(
              'Your account is on a temporary password. Set a new one to '
              'continue.',
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: palette.warning),
            ),
          ),
        ],
      ),
    );
  }
}
