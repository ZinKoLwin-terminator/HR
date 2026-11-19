import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';

/// The grievance rules the client can check, with the API's own wording.
///
/// Mirrors `GrievanceCase.ValidateDraft` / `CheckSubmission` and the request
/// validators in `Apex.Hrms.Application/Grievances`. The client checks them
/// to save a round trip and to put the message on the right field; the
/// **server stays the authority** and its refusal is shown as it comes.
abstract final class GrievanceRules {
  static const int subjectMinLength = 5;
  static const int subjectMaxLength = 200;
  static const int descriptionMinLength = 20;
  static const int descriptionMaxLength = 4000;
  static const int shortTextMaxLength = 200;
  static const int messageMaxLength = 4000;

  /// Unfinished drafts one employee may keep.
  static const int maxOpenDrafts = 5;

  static const String tooManyDrafts =
      'You already have 5 unfinished drafts. Submit or delete one before you '
      'start another.';

  static const String anonymityFixed =
      'Start a new draft to change how you report.';

  static const String anonymousNotAllowed =
      'This category cannot be raised anonymously.';

  static const String inactiveCategory = 'Choose an active category.';

  static const String declarationRequired =
      'Confirm the declaration to submit the grievance.';

  static const String staleRecord =
      'The record was changed or deleted by someone else. Reload it and try '
      'again.';

  /// Field name (as the API spells it) to the first problem found.
  ///
  /// [category] is the chosen category when it is known; without it the
  /// category checks are left to the server. [forSubmission] adds the
  /// minimum lengths and the declaration.
  static Map<String, String> violations(
    GrievanceDraftInput input, {
    required DateTime today,
    GrievanceCategory? category,
    bool isCategoryActive = true,
    bool forSubmission = false,
    bool declarationAccepted = false,
  }) {
    final GrievanceDraftInput d = input.normalized();
    final Map<String, String> errors = <String, String>{};
    void add(String field, String message) =>
        errors.putIfAbsent(field, () => message);

    if (d.categoryId <= 0) {
      add('categoryId', 'Choose a category.');
    } else if (category != null && !isCategoryActive) {
      add('categoryId', inactiveCategory);
    }

    if (category != null && d.isAnonymous && !category.allowsAnonymous) {
      add('isAnonymous', anonymousNotAllowed);
    }

    final String? subject = subjectError(
      d.subject,
      forSubmission: forSubmission,
    );
    if (subject != null) {
      add('subject', subject);
    }

    final String? description = descriptionError(
      d.description,
      forSubmission: forSubmission,
    );
    if (description != null) {
      add('description', description);
    }

    final String? incident = incidentDateError(d.incidentDate, today: today);
    if (incident != null) {
      add('incidentDate', incident);
    }

    if ((d.location?.length ?? 0) > shortTextMaxLength) {
      add(
        'location',
        'Location must be at most $shortTextMaxLength characters.',
      );
    }
    if ((d.involvedPersonText?.length ?? 0) > shortTextMaxLength) {
      add(
        'involvedPersonText',
        'Person involved must be at most $shortTextMaxLength characters.',
      );
    }

    if (forSubmission && !declarationAccepted) {
      add('declarationAccepted', declarationRequired);
    }
    return errors;
  }

  /// A draft needs a subject of at most 200 characters; a submission needs
  /// 5 to 200.
  static String? subjectError(String? value, {required bool forSubmission}) {
    final String text = (value ?? '').trim();
    if (text.isEmpty) {
      return 'Subject is required.';
    }
    if (text.length > subjectMaxLength) {
      return 'Subject must be at most $subjectMaxLength characters.';
    }
    if (forSubmission && text.length < subjectMinLength) {
      return 'The subject must be $subjectMinLength to $subjectMaxLength '
          'characters.';
    }
    return null;
  }

  /// A draft's description may be empty; a submission needs 20 to 4000
  /// characters.
  static String? descriptionError(
    String? value, {
    required bool forSubmission,
  }) {
    final int length = (value ?? '').trim().length;
    if (length > descriptionMaxLength) {
      return 'Description must be at most $descriptionMaxLength characters.';
    }
    if (forSubmission && length < descriptionMinLength) {
      return 'The description must be $descriptionMinLength to '
          '$descriptionMaxLength characters.';
    }
    return null;
  }

  /// The incident date is optional and cannot be after [today].
  static String? incidentDateError(DateTime? date, {required DateTime today}) {
    if (date == null) {
      return null;
    }
    final DateTime day = DateTime(date.year, date.month, date.day);
    final DateTime limit = DateTime(today.year, today.month, today.day);
    return day.isAfter(limit)
        ? 'The incident date cannot be in the future.'
        : null;
  }

  /// A message (or a response) is 1 to 4000 characters.
  static String? messageError(String? value) {
    final int length = (value ?? '').trim().length;
    if (length == 0) {
      return 'Enter a message.';
    }
    if (length > messageMaxLength) {
      return 'The message must be at most $messageMaxLength characters.';
    }
    return null;
  }
}
