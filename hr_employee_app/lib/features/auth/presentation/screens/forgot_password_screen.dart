import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/password_controllers.dart';
import 'package:hr_employee_app/features/auth/presentation/widgets/auth_scaffold.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Requests a password reset email.
///
/// On success the screen switches to a confirmation panel rather than popping,
/// because the user now has to leave the app to read their email and may come
/// back to re-read the instructions. The heading changes with it, so the
/// whole screen — not just the card — reflects which step the user is on.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() =>
      _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _emailController = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(forgotPasswordControllerProvider.notifier).reset();
      }
    });
  }

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    await ref
        .read(forgotPasswordControllerProvider.notifier)
        .submit(_emailController.text);

    if (!mounted) {
      return;
    }
    final SubmissionState state = ref.read(forgotPasswordControllerProvider);
    if (state.failureOrNull != null) {
      AppSnackBar.failure(context, state.failureOrNull!);
    }
  }

  @override
  Widget build(BuildContext context) {
    final SubmissionState submission = ref.watch(
      forgotPasswordControllerProvider,
    );

    if (submission.isSuccess) {
      final String email = _emailController.text.trim();
      return AuthScaffold(
        title: 'Check your email',
        // Worded so it is true whether or not the address has an account —
        // the API deliberately does not reveal which, and neither does this
        // screen.
        subtitle:
            'If $email is on file, a reset link is on its way. The link '
            'expires shortly, so use it soon.',
        footer: const AuthSupportFooter(),
        child: const _SentConfirmation(),
      );
    }

    return AuthScaffold(
      title: 'Forgot your password?',
      subtitle:
          'Enter the email address on your HR record. If it matches an '
          'account, we will send you a link to set a new password.',
      footer: const AuthSupportFooter(),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            AppTextField(
              label: 'Work email',
              controller: _emailController,
              hint: 'name@example.com',
              isRequired: true,
              prefixIcon: Icons.mail_outline_rounded,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.done,
              isEnabled: !submission.isSubmitting,
              validator: Validators.email,
              errorText: submission.errorFor('email'),
              autofillHints: const <String>[AutofillHints.email],
              onFieldSubmitted: (_) => _submit(),
            ),
            Gap.xl,
            AuthPrimaryButton(
              label: 'Send reset link',
              loadingLabel: 'Sending…',
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

/// The card shown once the request has been accepted.
///
/// It carries no copy of its own: the heading above it already says what
/// happened, so repeating it here would just be the same sentence twice.
class _SentConfirmation extends StatelessWidget {
  const _SentConfirmation();

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Center(
          child: Container(
            padding: EdgeInsets.all(AppSpacing.lg),
            decoration: BoxDecoration(
              color: palette.successSurface,
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.mark_email_read_outlined,
              size: 40,
              color: palette.success,
            ),
          ),
        ),
        Gap.lg,
        Text(
          'Still nothing after a few minutes? Check your spam folder, then '
          'try again.',
          style: textStyles.bodyMedium,
          textAlign: TextAlign.center,
        ),
        Gap.xl,
        AppButton.secondary(
          label: 'Back to sign in',
          onPressed: () => Navigator.of(context).pop(),
        ),
      ],
    );
  }
}
