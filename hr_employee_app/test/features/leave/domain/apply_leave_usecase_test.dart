import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_type.dart';
import 'package:hr_employee_app/features/leave/domain/usecases/apply_leave_usecase.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/test_doubles.dart';

void main() {
  late MockLeaveRepository repository;
  late ApplyLeaveUseCase useCase;

  setUpAll(registerCommonFallbacks);

  setUp(() {
    repository = MockLeaveRepository();
    useCase = ApplyLeaveUseCase(repository);

    when(() => repository.apply(any(), onProgress: any(named: 'onProgress')))
        .thenAnswer(
          (_) async => Result<LeaveRequest>.success(TestData.leaveRequest()),
        );
  });

  group('client-side guards', () {
    test('rejects a missing leave type', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(leaveTypeId: ''),
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
      verifyNever(
        () => repository.apply(any(), onProgress: any(named: 'onProgress')),
      );
    });

    test('rejects an inverted date range', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(
          startDate: DateTime(2026, 4, 5),
          endDate: DateTime(2026, 4, 2),
        ),
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
      verifyNever(
        () => repository.apply(any(), onProgress: any(named: 'onProgress')),
      );
    });

    test('rejects a reason that is too short', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(reason: 'Sick'),
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
    });

    test('rejects more than the allowed number of attachments', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(
          attachmentPaths: List<String>.generate(
            6,
            (int index) => '/tmp/file-$index.pdf',
          ),
        ),
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
    });
  });

  group('leave-type rules', () {
    test('requires an attachment when the type demands one', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(leaveTypeId: 'lt-2'),
        leaveType: TestData.medicalLeave,
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
      expect(
        result.failureOrNull!.message,
        contains('requires a supporting document'),
      );
    });

    test('accepts the same request once a document is attached', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(
          leaveTypeId: 'lt-2',
          attachmentPaths: <String>['/tmp/medical.pdf'],
        ),
        leaveType: TestData.medicalLeave,
      );

      expect(result.isSuccess, isTrue);
    });

    test('enforces the maximum consecutive days', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(
          startDate: DateTime(2026, 4),
          endDate: DateTime(2026, 4, 30),
        ),
        leaveType: TestData.annualLeave,
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
      expect(result.failureOrNull!.message, contains('14 consecutive days'));
    });
  });

  group('balance pre-check', () {
    test('rejects a request larger than the remaining balance', () async {
      const LeaveBalance nearlyExhausted = LeaveBalance(
        leaveTypeId: 'lt-1',
        leaveTypeName: 'Annual Leave',
        year: 2026,
        entitledDays: 14,
        usedDays: 13,
        remainingDays: 1,
      );

      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(),
        balance: nearlyExhausted,
      );

      expect(result.failureOrNull, isA<ValidationFailure>());
      expect(result.failureOrNull!.message, contains('1 day'));
    });

    test('accepts a request that fits', () async {
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(),
        balance: TestData.annualBalance,
      );

      expect(result.isSuccess, isTrue);
    });

    test('submits when no balance was supplied', () async {
      // The server re-validates, so an unknown balance must not block a
      // request the employee may well be entitled to make.
      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(),
      );

      expect(result.isSuccess, isTrue);
    });
  });

  group('server authority', () {
    test('surfaces a server rejection verbatim', () async {
      when(() => repository.apply(any(), onProgress: any(named: 'onProgress')))
          .thenAnswer(
            (_) async => const Result<LeaveRequest>.failure(
              ConflictFailure(
                'These dates overlap a request you have already submitted.',
              ),
            ),
          );

      final Result<LeaveRequest> result = await useCase(
        TestData.leaveApplication(),
        balance: TestData.annualBalance,
      );

      expect(result.failureOrNull, isA<ConflictFailure>());
      expect(
        result.failureOrNull!.message,
        'These dates overlap a request you have already submitted.',
      );
    });
  });

  group('estimated days', () {
    test('counts calendar days inclusively for a full-day request', () {
      expect(
        TestData.leaveApplication(
          startDate: DateTime(2026, 4, 2),
          endDate: DateTime(2026, 4, 3),
        ).estimatedDays,
        2,
      );
    });

    test('a half-day request is always half a day', () {
      expect(
        TestData.leaveApplication(
          startDate: DateTime(2026, 4, 2),
          endDate: DateTime(2026, 4, 2),
          dayPortion: LeaveDayPortion.firstHalf,
        ).estimatedDays,
        0.5,
      );
    });
  });
}
