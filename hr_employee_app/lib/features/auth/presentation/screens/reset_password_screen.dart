import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/password_controllers.dart';
import 'package:hr_employee_app/features/auth/presentation/widgets/auth_scaffold.dart';
import 'package:hr_employee_app/features/auth/presentation/widgets/password_policy_hint.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Sets a new password from the emailed reset link.
///
/// [email] and [token] arrive as query parameters on a deep link such as
/// `hrapp://reset-password?email=...&token=...`. They are passed in by the
/// router rather than read from storage, because this screen is reachable
/// without a session.
class ResetPasswordScreen extends ConsumerStatefulWidget {
  const ResetPasswordScreen({
    required this.email,
    required this.token,
    super.key,
  });

  final String email;
  final String token;

  @override
  ConsumerState<ResetPasswordScreen> createState() =>
      _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends ConsumerState<ResetPasswordScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _passwordController = TextEditingController();
  final TextEditingController _confirmController = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(resetPasswordControllerProvider.notifier).reset();
      }
    });
  }

  @override
  void dispose() {
    _passwordController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final bool succeeded = await ref
        .read(resetPasswordControllerProvider.notifier)
        .submit(
          email: widget.email,
          token: widget.token,
          newPassword: _passwordController.text,
        );

    if (!mounted) {
      return;
    }

    final SubmissionState state = ref.read(resetPasswordControllerProvider);
    if (succeeded) {
      AppSnackBar.success(
        context,
        'Password updated. Sign in with your new password.',
      );
      // Replace rather than push: the reset link is now spent, so there is
      // nothing to come back to.
      context.goNamed(RouteNames.login);
      return;
    }
    if (state.failureOrNull != null) {
      AppSnackBar.failure(context, state.failureOrNull!);
    }
  }

  @override
  Widget build(BuildContext context) {
    final SubmissionState submission = ref.watch(
      resetPasswordControllerProvider,
    );
    final bool isLinkUsable =
        widget.token.isNotEmpty && widget.email.isNotEmpty;

    if (!isLinkUsable) {
      return AuthScaffold(
        title: 'This link is incomplete',
        subtitle: 'Open the link from your email again, or request a new one.',
        footer: const AuthSupportFooter(),
        child: _InvalidLink(
          onRequestNew: () => context.goNamed(RouteNames.forgotPassword),
        ),
      );
    }

    return AuthScaffold(
      title: 'Choose a new password',
      subtitle: 'Setting a new password for ${widget.email}.',
      footer: const AuthSupportFooter(),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            AppPasswordField(
              label: 'New password',
              controller: _passwordController,
              isEnabled: !submission.isSubmitting,
              textInputAction: TextInputAction.next,
              validator: Validators.newPassword,
              errorText: submission.errorFor('newPassword'),
              autofillHints: const <String>[AutofillHints.newPassword],
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
                  Validators.confirmPassword(value, _passwordController.text),
              onFieldSubmitted: (_) => _submit(),
            ),
            Gap.xl,

            AuthPrimaryButton(
              label: 'Update password',
              isSubmitting: submission.isSubmitting,
              onPressed: _submit,
            ),

            if (submission.failureOrNull != null) ...<Widget>[
              Gap.lg,
              AuthInlineFailure(failure: submission.failureOrNull!),
            ],
          ],
        ),
      ),
    );
  }
}

/// Shown when the deep link is missing its email or token.
class _InvalidLink extends StatelessWidget {
  const _InvalidLink({required this.onRequestNew});

  final VoidCallback onRequestNew;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const Center(child: Icon(Icons.link_off_rounded, size: 48)),
        Gap.xl,
        AuthPrimaryButton(
          label: 'Request a new link',
          isSubmitting: false,
          onPressed: onRequestNew,
        ),
      ],
    );
  }
}
