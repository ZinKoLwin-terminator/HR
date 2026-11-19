import 'package:hr_employee_app/core/constants/app_constants.dart';

/// Reusable form validators.
///
/// Each returns null when the value is acceptable and an end-user message
/// otherwise, matching the signature `TextFormField.validator` expects.
///
/// Client-side validation exists to give fast feedback, not to enforce rules —
/// the server re-validates everything.
abstract final class Validators {
  /// Matches the overwhelming majority of real addresses without trying to
  /// implement RFC 5322, which is not practical and rejects valid mail.
  static final RegExp _email = RegExp(r'^[\w.+-]+@[\w-]+(\.[\w-]+)+$');

  /// Employee IDs in the form `EMP-00421`, plus a plain alphanumeric fallback.
  static final RegExp _employeeId = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._-]{2,}$');

  /// Digits, spaces and the usual separators, 7 to 20 characters.
  static final RegExp _phone = RegExp(r'^\+?[0-9\s().-]{7,20}$');

  static final RegExp _hasLetter = RegExp(r'[A-Za-z]');
  static final RegExp _hasDigit = RegExp(r'[0-9]');

  /// Fails when the field is empty or whitespace only.
  static String? required(String? value, {String field = 'This field'}) {
    if (value == null || value.trim().isEmpty) {
      return '$field is required.';
    }
    return null;
  }

  static String? email(String? value) {
    final String? empty = required(value, field: 'Email');
    if (empty != null) {
      return empty;
    }
    if (!_email.hasMatch(value!.trim())) {
      return 'Enter a valid email address.';
    }
    return null;
  }

  /// The login field accepts either an employee ID or a work email.
  static String? loginIdentifier(String? value) {
    final String? empty = required(value, field: 'Employee ID or email');
    if (empty != null) {
      return empty;
    }
    final String trimmed = value!.trim();
    if (trimmed.length < ValidationRules.minUserNameLength) {
      return 'Enter at least ${ValidationRules.minUserNameLength} characters.';
    }
    if (trimmed.contains('@')) {
      return _email.hasMatch(trimmed) ? null : 'Enter a valid email address.';
    }
    return _employeeId.hasMatch(trimmed)
        ? null
        : 'Enter a valid employee ID or email.';
  }

  /// Used on the login screen, where only presence matters — the server
  /// decides whether the password is correct.
  static String? loginPassword(String? value) {
    if (value == null || value.isEmpty) {
      return 'Password is required.';
    }
    return null;
  }

  /// Used when setting a new password, where the policy must be met.
  static String? newPassword(String? value) {
    if (value == null || value.isEmpty) {
      return 'Password is required.';
    }
    if (value.length < ValidationRules.minPasswordLength) {
      return 'Use at least ${ValidationRules.minPasswordLength} characters.';
    }
    if (value.length > ValidationRules.maxPasswordLength) {
      return 'Use at most ${ValidationRules.maxPasswordLength} characters.';
    }
    if (!_hasLetter.hasMatch(value) || !_hasDigit.hasMatch(value)) {
      return 'Include at least one letter and one number.';
    }
    return null;
  }

  static String? confirmPassword(String? value, String? original) {
    if (value == null || value.isEmpty) {
      return 'Confirm your new password.';
    }
    if (value != original) {
      return 'Passwords do not match.';
    }
    return null;
  }

  static String? phone(String? value, {bool isRequired = true}) {
    if (value == null || value.trim().isEmpty) {
      return isRequired ? 'Phone number is required.' : null;
    }
    if (!_phone.hasMatch(value.trim())) {
      return 'Enter a valid phone number.';
    }
    return null;
  }

  static String? leaveReason(String? value) {
    final String trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return 'A reason is required.';
    }
    if (trimmed.length < ValidationRules.minLeaveReasonLength) {
      return 'Give at least ${ValidationRules.minLeaveReasonLength} '
          'characters so your manager has context.';
    }
    if (trimmed.length > ValidationRules.maxLeaveReasonLength) {
      return 'Keep the reason under '
          '${ValidationRules.maxLeaveReasonLength} characters.';
    }
    return null;
  }

  /// Enforces a maximum length on an optional free-text field.
  static String? maxLength(String? value, int max, {String field = 'This'}) {
    if (value != null && value.trim().length > max) {
      return '$field must be $max characters or fewer.';
    }
    return null;
  }

  /// Validates a leave date range.
  ///
  /// Returns a message describing the first problem found, or null when the
  /// range is usable.
  static String? leaveDateRange({
    required DateTime? startDate,
    required DateTime? endDate,
  }) {
    if (startDate == null) {
      return 'Choose a start date.';
    }
    if (endDate == null) {
      return 'Choose an end date.';
    }
    if (endDate.isBefore(startDate)) {
      return 'The end date cannot be before the start date.';
    }
    return null;
  }
}
