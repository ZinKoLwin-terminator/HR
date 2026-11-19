import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:hr_employee_app/core/errors/failure.dart';

part 'submission_state.freezed.dart';

/// The state of a form submission.
///
/// Used instead of `AsyncValue` for forms because a form has four states, not
/// three: it starts *idle*, which is not the same as "loaded with no data".
/// Keeping them distinct is what stops a freshly opened form from rendering a
/// success message or an empty error.
@freezed
sealed class SubmissionState with _$SubmissionState {
  /// Nothing submitted yet, or the form was reset.
  const factory SubmissionState.idle() = SubmissionIdle;

  /// A request is in flight. Buttons disable themselves on this.
  const factory SubmissionState.submitting() = SubmissionInProgress;

  /// The server accepted it. [message] is shown in a snack bar when present.
  const factory SubmissionState.success({String? message}) = SubmissionSuccess;

  /// The server, or a local rule, rejected it.
  const factory SubmissionState.failure(Failure failure) = SubmissionFailed;

  const SubmissionState._();

  bool get isSubmitting => this is SubmissionInProgress;

  bool get isSuccess => this is SubmissionSuccess;

  Failure? get failureOrNull =>
      this is SubmissionFailed ? (this as SubmissionFailed).failure : null;

  /// The per-field errors from a server-side validation failure, so a form
  /// can mark the offending inputs rather than only showing a banner.
  Map<String, List<String>> get fieldErrors {
    final Failure? failure = failureOrNull;
    return failure is ValidationFailure
        ? failure.fieldErrors
        : const <String, List<String>>{};
  }

  /// The first server error recorded against [field], if any.
  String? errorFor(String field) {
    final List<String>? errors = fieldErrors[field];
    return (errors == null || errors.isEmpty) ? null : errors.first;
  }
}
