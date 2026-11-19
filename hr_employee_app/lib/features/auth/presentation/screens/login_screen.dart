import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/validators/validators.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/app_text_field.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_state.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/login_controller.dart';
import 'package:hr_employee_app/features/auth/presentation/widgets/auth_scaffold.dart';
import 'package:hr_employee_app/features/auth/presentation/widgets/mock_credentials_hint.dart';
import 'package:hr_employee_app/shared/helpers/mock_support.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// Sign-in screen.
///
/// The screen owns only view concerns — controllers, focus and form keys. The
/// request itself belongs to [LoginController]; nothing here calls a
/// repository or an API, and the background, header and scroll behaviour come
/// from [AuthScaffold] rather than being rebuilt here.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _userNameController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  final FocusNode _passwordFocus = FocusNode();

  bool _rememberMe = false;

  @override
  void initState() {
    super.initState();
    // Clear any error left from a previous visit, and restore the saved
    // identifier. Deferred because providers must not be mutated during the
    // first build.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      ref.read(loginControllerProvider.notifier).reset();

      final String? remembered = ref.read(rememberedUserNameProvider);
      if (remembered != null && remembered.isNotEmpty) {
        setState(() {
          _userNameController.text = remembered;
          _rememberMe = true;
        });
      }

      _explainExpiredSessionIfNeeded();
    });
  }

  /// Tells the user why they were signed out, rather than dropping them on a
  /// login screen with no explanation.
  void _explainExpiredSessionIfNeeded() {
    final AuthState? auth = ref.read(authControllerProvider).value;
    if (auth is AuthUnauthenticated && auth.wasSessionExpired) {
      AppSnackBar.info(context, 'Your session expired. Please sign in again.');
    }
  }

  @override
  void dispose() {
    _userNameController.dispose();
    _passwordController.dispose();
    _passwordFocus.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final bool succeeded = await ref
        .read(loginControllerProvider.notifier)
        .submit(
          userName: _userNameController.text,
          password: _passwordController.text,
          rememberMe: _rememberMe,
        );

    if (!mounted || succeeded) {
      // On success the router reacts to the new auth state and navigates;
      // this screen does not push a route itself.
      return;
    }

    final Failure? failure = ref.read(loginControllerProvider).failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  /// Fills the form with the fixture credentials and signs in.
  ///
  /// It types into the same controllers the user would, rather than calling
  /// the controller directly, so the demo takes exactly the path a typed
  /// sign-in takes — validation included.
  Future<void> _useDemo() async {
    setState(() {
      _userNameController.text = MockSupport.employeeId;
      _passwordController.text = MockSupport.acceptedPassword;
    });
    await _submit();
  }

  @override
  Widget build(BuildContext context) {
    final SubmissionState submission = ref.watch(loginControllerProvider);
    final AppConfig config = ref.watch(appConfigProvider);

    return AuthScaffold(
      title: 'Welcome back',
      subtitle: 'Sign in to your workspace to continue.',
      footer: const AuthSupportFooter(),
      child: Form(
        key: _formKey,
        child: _LoginFields(
          submission: submission,
          userNameController: _userNameController,
          passwordController: _passwordController,
          passwordFocus: _passwordFocus,
          rememberMe: _rememberMe,
          onRememberMeChanged: (bool value) =>
              setState(() => _rememberMe = value),
          onSubmit: _submit,
          onUseDemo: config.useMockData ? _useDemo : null,
        ),
      ),
    );
  }
}

/// Everything inside the form card.
///
/// Split out of [_LoginScreenState.build] so the screen's build method reads as
/// the page layout rather than as one 150-line widget tree.
class _LoginFields extends StatelessWidget {
  const _LoginFields({
    required this.submission,
    required this.userNameController,
    required this.passwordController,
    required this.passwordFocus,
    required this.rememberMe,
    required this.onRememberMeChanged,
    required this.onSubmit,
    required this.onUseDemo,
  });

  final SubmissionState submission;
  final TextEditingController userNameController;
  final TextEditingController passwordController;
  final FocusNode passwordFocus;
  final bool rememberMe;
  final ValueChanged<bool> onRememberMeChanged;
  final VoidCallback onSubmit;

  /// Signs in with the fixture credentials. Null outside mock mode, which is
  /// what keeps the demo route out of a production build entirely.
  final VoidCallback? onUseDemo;

  @override
  Widget build(BuildContext context) {
    final bool isBusy = submission.isSubmitting;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        AppTextField(
          label: 'Employee ID or email',
          controller: userNameController,
          hint: 'EMP-00421',
          isRequired: true,
          prefixIcon: Icons.person_outline_rounded,
          keyboardType: TextInputType.emailAddress,
          textInputAction: TextInputAction.next,
          isEnabled: !isBusy,
          validator: Validators.loginIdentifier,
          errorText: submission.errorFor('userName'),
          autofillHints: const <String>[AutofillHints.username],
          onFieldSubmitted: (_) => passwordFocus.requestFocus(),
        ),
        Gap.lg,

        AppPasswordField(
          label: 'Password',
          controller: passwordController,
          focusNode: passwordFocus,
          isEnabled: !isBusy,
          textInputAction: TextInputAction.done,
          validator: Validators.loginPassword,
          errorText: submission.errorFor('password'),
          autofillHints: const <String>[AutofillHints.password],
          onFieldSubmitted: (_) => onSubmit(),
        ),
        Gap.sm,

        // Both children are flexible rather than one being pinned by a
        // Spacer: at a large system text scale the two labels together can
        // exceed the width, and a Spacer would let the row overflow instead
        // of letting the labels shrink.
        Row(
          children: <Widget>[
            Flexible(
              child: _RememberMeToggle(
                value: rememberMe,
                isEnabled: !isBusy,
                onChanged: onRememberMeChanged,
              ),
            ),
            Flexible(
              child: Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: isBusy
                      ? null
                      : () => context.pushNamed(RouteNames.forgotPassword),
                  child: const Text(
                    'Forgot password?',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
            ),
          ],
        ),
        Gap.lg,

        AuthPrimaryButton(
          label: 'Sign in',
          isSubmitting: isBusy,
          onPressed: onSubmit,
        ),

        if (submission.failureOrNull != null) ...<Widget>[
          Gap.lg,
          AuthInlineFailure(failure: submission.failureOrNull!),
        ],

        if (onUseDemo != null) ...<Widget>[
          Gap.xl,
          const AuthOrDivider(label: 'Or try it out'),
          Gap.lg,
          MockCredentialsHint(onUseDemo: isBusy ? null : onUseDemo),
        ],
      ],
    );
  }
}

/// Checkbox plus label, as one target so the label is tappable too.
class _RememberMeToggle extends StatelessWidget {
  const _RememberMeToggle({
    required this.value,
    required this.onChanged,
    required this.isEnabled,
  });

  final bool value;
  final ValueChanged<bool> onChanged;
  final bool isEnabled;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: isEnabled ? () => onChanged(!value) : null,
      borderRadius: AppRadius.field,
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.xs),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            SizedBox.square(
              dimension: AppSizes.iconLg,
              child: Checkbox(
                value: value,
                onChanged: isEnabled
                    ? (bool? next) => onChanged(next ?? false)
                    : null,
                visualDensity: VisualDensity.compact,
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                // Shape and border come from the panel's theme rather than
                // from here: they belong to the glass surface this sits on,
                // not to the remember-me control.
              ),
            ),
            Gap.wSm,
            Flexible(
              child: Text(
                'Remember me',
                style: Theme.of(context).textTheme.bodyMedium,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
