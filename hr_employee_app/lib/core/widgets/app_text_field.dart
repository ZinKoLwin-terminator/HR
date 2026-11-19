import 'package:flutter/services.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:material_ui/material_ui.dart';

/// The app's text input.
///
/// Renders the label above the field rather than as a floating label, which
/// keeps row heights consistent whether or not a field has content, and makes
/// the required marker readable at a glance.
class AppTextField extends StatelessWidget {
  const AppTextField({
    required this.label,
    super.key,
    this.controller,
    this.initialValue,
    this.hint,
    this.helperText,
    this.errorText,
    this.validator,
    this.onChanged,
    this.onFieldSubmitted,
    this.keyboardType,
    this.textInputAction,
    this.inputFormatters,
    this.prefixIcon,
    this.suffix,
    this.isRequired = false,
    this.isEnabled = true,
    this.isReadOnly = false,
    this.obscureText = false,
    this.autofocus = false,
    this.maxLines = 1,
    this.minLines,
    this.maxLength,
    this.autofillHints,
    this.focusNode,
    this.onTap,
  });

  final String label;
  final TextEditingController? controller;
  final String? initialValue;
  final String? hint;
  final String? helperText;

  /// A server-side error for this field, surfaced from a `ValidationFailure`.
  ///
  /// Takes precedence over [validator] so a 400 response can mark the field
  /// without the local validator having to know about it.
  final String? errorText;

  final FormFieldValidator<String>? validator;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onFieldSubmitted;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final List<TextInputFormatter>? inputFormatters;
  final IconData? prefixIcon;
  final Widget? suffix;
  final bool isRequired;
  final bool isEnabled;
  final bool isReadOnly;
  final bool obscureText;
  final bool autofocus;
  final int maxLines;
  final int? minLines;
  final int? maxLength;
  final List<String>? autofillHints;
  final FocusNode? focusNode;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        _FieldLabel(
          label: label,
          isRequired: isRequired,
          style: textStyles.labelMedium,
          requiredColor: palette.danger,
        ),
        Gap.xs,
        TextFormField(
          controller: controller,
          initialValue: controller == null ? initialValue : null,
          focusNode: focusNode,
          enabled: isEnabled,
          readOnly: isReadOnly,
          obscureText: obscureText,
          autofocus: autofocus,
          maxLines: obscureText ? 1 : maxLines,
          minLines: minLines,
          maxLength: maxLength,
          keyboardType: keyboardType,
          textInputAction: textInputAction,
          inputFormatters: inputFormatters,
          autofillHints: autofillHints,
          validator: validator,
          onChanged: onChanged,
          onFieldSubmitted: onFieldSubmitted,
          onTap: onTap,
          style: textStyles.bodyLarge,
          // Validate as the user corrects a field, but not before they have
          // had a chance to type anything.
          autovalidateMode: AutovalidateMode.onUserInteraction,
          decoration: InputDecoration(
            hintText: hint,
            helperText: helperText,
            errorText: errorText,
            counterText: '',
            prefixIcon: prefixIcon == null
                ? null
                : Icon(prefixIcon, size: AppSizes.iconLg),
            suffixIcon: suffix,
            suffixIconConstraints: const BoxConstraints(
              minWidth: AppSizes.minTouchTarget,
              minHeight: AppSizes.minTouchTarget,
            ),
          ),
        ),
      ],
    );
  }
}

/// A password field with a show/hide toggle.
///
/// Stateful because the toggle is pure view state that no provider needs to
/// know about.
class AppPasswordField extends StatefulWidget {
  const AppPasswordField({
    required this.label,
    super.key,
    this.controller,
    this.hint,
    this.helperText,
    this.errorText,
    this.validator,
    this.onChanged,
    this.onFieldSubmitted,
    this.textInputAction,
    this.isRequired = true,
    this.isEnabled = true,
    this.autofocus = false,
    this.autofillHints,
    this.focusNode,
  });

  final String label;
  final TextEditingController? controller;
  final String? hint;
  final String? helperText;
  final String? errorText;
  final FormFieldValidator<String>? validator;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onFieldSubmitted;
  final TextInputAction? textInputAction;
  final bool isRequired;
  final bool isEnabled;
  final bool autofocus;
  final List<String>? autofillHints;
  final FocusNode? focusNode;

  @override
  State<AppPasswordField> createState() => _AppPasswordFieldState();
}

class _AppPasswordFieldState extends State<AppPasswordField> {
  bool _isObscured = true;

  @override
  Widget build(BuildContext context) {
    return AppTextField(
      label: widget.label,
      controller: widget.controller,
      hint: widget.hint,
      helperText: widget.helperText,
      errorText: widget.errorText,
      validator: widget.validator,
      onChanged: widget.onChanged,
      onFieldSubmitted: widget.onFieldSubmitted,
      textInputAction: widget.textInputAction,
      isRequired: widget.isRequired,
      isEnabled: widget.isEnabled,
      autofocus: widget.autofocus,
      autofillHints: widget.autofillHints,
      focusNode: widget.focusNode,
      obscureText: _isObscured,
      prefixIcon: Icons.lock_outline_rounded,
      keyboardType: TextInputType.visiblePassword,
      suffix: IconButton(
        onPressed: () => setState(() => _isObscured = !_isObscured),
        icon: Icon(
          _isObscured
              ? Icons.visibility_outlined
              : Icons.visibility_off_outlined,
          size: AppSizes.iconLg,
        ),
        tooltip: _isObscured ? 'Show password' : 'Hide password',
      ),
    );
  }
}

/// A read-only field that opens a picker when tapped.
///
/// Used for dates, leave types and any other value chosen rather than typed.
class AppPickerField extends StatelessWidget {
  const AppPickerField({
    required this.label,
    required this.value,
    required this.onTap,
    super.key,
    this.hint,
    this.errorText,
    this.prefixIcon,
    this.isRequired = false,
    this.isEnabled = true,
    this.isCompact = false,
  });

  final String label;

  /// The chosen value, or null when nothing has been selected yet.
  final String? value;

  final VoidCallback onTap;
  final String? hint;
  final String? errorText;
  final IconData? prefixIcon;
  final bool isRequired;
  final bool isEnabled;

  /// For half-width fields such as a start/end date pair: drops the dropdown
  /// arrow and tightens the icon gutter so a value like `18 Nov 2026` fits
  /// instead of being ellipsised. The prefix icon already signals a picker.
  final bool isCompact;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool hasValue = value != null && value!.isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        _FieldLabel(
          label: label,
          isRequired: isRequired,
          style: textStyles.labelMedium,
          requiredColor: palette.danger,
        ),
        Gap.xs,
        InkWell(
          onTap: isEnabled ? onTap : null,
          borderRadius: AppRadius.field,
          child: InputDecorator(
            isEmpty: !hasValue,
            decoration: InputDecoration(
              errorText: errorText,
              enabled: isEnabled,
              prefixIcon: prefixIcon == null
                  ? null
                  : Icon(
                      prefixIcon,
                      size: isCompact ? AppSizes.iconMd : AppSizes.iconLg,
                    ),
              prefixIconConstraints: isCompact
                  ? BoxConstraints(minWidth: AppSpacing.xxl + AppSpacing.md)
                  : null,
              contentPadding: isCompact
                  ? EdgeInsets.symmetric(
                      horizontal: AppSpacing.sm,
                      vertical: AppSpacing.md,
                    )
                  : null,
              suffixIcon: isCompact
                  ? null
                  : Icon(
                      Icons.keyboard_arrow_down_rounded,
                      size: AppSizes.iconLg,
                      color: palette.textTertiary,
                    ),
            ),
            child: Text(
              hasValue ? value! : (hint ?? 'Select'),
              maxLines: 1,
              style: hasValue
                  ? textStyles.bodyLarge
                  : textStyles.bodyLarge?.copyWith(color: palette.textTertiary),
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ),
      ],
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel({
    required this.label,
    required this.isRequired,
    required this.requiredColor,
    this.style,
  });

  final String label;
  final bool isRequired;
  final Color requiredColor;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    if (!isRequired) {
      return Text(label, style: style);
    }
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          TextSpan(text: label),
          TextSpan(
            text: ' *',
            style: TextStyle(color: requiredColor),
          ),
        ],
      ),
      style: style,
      // The asterisk is decorative; the label already says what the field is.
      semanticsLabel: '$label, required',
    );
  }
}
