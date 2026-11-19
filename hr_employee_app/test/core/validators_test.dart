import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/validators/validators.dart';

void main() {
  group('Validators.loginIdentifier', () {
    test('accepts an employee ID', () {
      expect(Validators.loginIdentifier('EMP-00421'), isNull);
    });

    test('accepts a work email', () {
      expect(Validators.loginIdentifier('aye.chan@example.com'), isNull);
    });

    test('rejects an empty value', () {
      expect(Validators.loginIdentifier(''), isNotNull);
      expect(Validators.loginIdentifier('   '), isNotNull);
      expect(Validators.loginIdentifier(null), isNotNull);
    });

    test('rejects a malformed email', () {
      // Contains an @, so it is judged as an email rather than an ID.
      expect(Validators.loginIdentifier('aye.chan@'), isNotNull);
      expect(Validators.loginIdentifier('@example.com'), isNotNull);
      expect(Validators.loginIdentifier('aye@example'), isNotNull);
    });

    test('rejects an identifier that is too short', () {
      expect(Validators.loginIdentifier('ab'), isNotNull);
    });
  });

  group('Validators.newPassword', () {
    test('accepts a password meeting the policy', () {
      expect(Validators.newPassword('Password1'), isNull);
    });

    test('rejects one that is too short', () {
      expect(Validators.newPassword('Pass1'), isNotNull);
    });

    test('rejects letters with no digit', () {
      expect(Validators.newPassword('PasswordOnly'), isNotNull);
    });

    test('rejects digits with no letter', () {
      expect(Validators.newPassword('12345678'), isNotNull);
    });

    test('rejects one that exceeds the maximum', () {
      expect(Validators.newPassword('a1${'x' * 100}'), isNotNull);
    });
  });

  group('Validators.confirmPassword', () {
    test('accepts a match', () {
      expect(Validators.confirmPassword('Password1', 'Password1'), isNull);
    });

    test('rejects a mismatch', () {
      expect(Validators.confirmPassword('Password1', 'Password2'), isNotNull);
    });

    test('rejects an empty confirmation', () {
      expect(Validators.confirmPassword('', 'Password1'), isNotNull);
    });
  });

  group('Validators.leaveReason', () {
    test('accepts a reason with enough context', () {
      expect(
        Validators.leaveReason('Family matters out of town this weekend.'),
        isNull,
      );
    });

    test('rejects one that is too short', () {
      expect(Validators.leaveReason('Sick'), isNotNull);
    });

    test('counts length after trimming', () {
      // Nine visible characters padded with spaces must still be rejected.
      expect(Validators.leaveReason('   holiday   '), isNotNull);
    });
  });

  group('Validators.phone', () {
    test('accepts international and local formats', () {
      expect(Validators.phone('+95 9 123 456 789'), isNull);
      expect(Validators.phone('09-123-456-789'), isNull);
      expect(Validators.phone('(01) 234 5678'), isNull);
    });

    test('rejects letters', () {
      expect(Validators.phone('call me'), isNotNull);
    });

    test('allows an empty value when optional', () {
      expect(Validators.phone('', isRequired: false), isNull);
      expect(Validators.phone(null, isRequired: false), isNull);
    });

    test('rejects an empty value when required', () {
      expect(Validators.phone(''), isNotNull);
    });
  });

  group('Validators.leaveDateRange', () {
    test('accepts a forward range', () {
      expect(
        Validators.leaveDateRange(
          startDate: DateTime(2026, 4, 2),
          endDate: DateTime(2026, 4, 3),
        ),
        isNull,
      );
    });

    test('accepts a single day', () {
      expect(
        Validators.leaveDateRange(
          startDate: DateTime(2026, 4, 2),
          endDate: DateTime(2026, 4, 2),
        ),
        isNull,
      );
    });

    test('rejects an inverted range', () {
      expect(
        Validators.leaveDateRange(
          startDate: DateTime(2026, 4, 5),
          endDate: DateTime(2026, 4, 2),
        ),
        isNotNull,
      );
    });

    test('rejects a missing date', () {
      expect(
        Validators.leaveDateRange(
          startDate: null,
          endDate: DateTime(2026, 4, 2),
        ),
        isNotNull,
      );
      expect(
        Validators.leaveDateRange(
          startDate: DateTime(2026, 4, 2),
          endDate: null,
        ),
        isNotNull,
      );
    });
  });
}
