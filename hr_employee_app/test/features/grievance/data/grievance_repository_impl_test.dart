import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_remote_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:hr_employee_app/features/grievance/data/repositories/grievance_repository_impl.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

import '../../../helpers/test_doubles.dart';
import '../grievance_test_helpers.dart';

void main() {
  late GrievanceRepositoryImpl repository;

  setUp(() {
    repository = GrievanceRepositoryImpl(
      remote: freshMockDataSource(),
      logger: silentLogger(),
    );
  });

  group('GrievanceRepositoryImpl', () {
    test('maps the list to entities with typed statuses', () async {
      final Result<PaginatedResponse<GrievanceSummary>> result =
          await repository.getMyGrievances(page: const PageRequest());

      final List<GrievanceSummary> items = result.requireValue.items;
      expect(
        items.map((GrievanceSummary g) => g.status),
        containsAll(<GrievanceStatus>[
          GrievanceStatus.draft,
          GrievanceStatus.submitted,
          GrievanceStatus.awaitingEmployeeResponse,
          GrievanceStatus.resolved,
          GrievanceStatus.rejected,
        ]),
      );
      expect(
        items.firstWhere((GrievanceSummary g) => g.id == 101).reference,
        'Draft',
      );
    });

    test('maps the detail, history reasons included', () async {
      final GrievanceDetail detail = (await repository.getById(102))
          .requireValue;

      expect(detail.status, GrievanceStatus.submitted);
      expect(
        detail.timeline.first.reason,
        'Recorded by HR on behalf of the employee (received by phone).',
      );
    });

    test(
      'a rule refusal becomes a ValidationFailure with field errors',
      () async {
        final Result<GrievanceDetail> result = await repository.createDraft(
          GrievanceTestData.input(categoryId: 4, isAnonymous: true),
        );

        final Failure? failure = result.failureOrNull;
        expect(failure, isA<ValidationFailure>());
        expect(
          (failure! as ValidationFailure).errorFor('isAnonymous'),
          'This category cannot be raised anonymously.',
        );
      },
    );

    test(
      'a state refusal becomes a ConflictFailure, a missing case NotFound',
      () async {
        final GrievanceDetail submitted = (await repository.getById(102))
            .requireValue;

        final Result<void> delete = await repository.deleteDraft(
          102,
          rowVersion: submitted.rowVersion,
        );
        final Result<GrievanceDetail> missing = await repository.getById(4242);

        expect(delete.failureOrNull, isA<ConflictFailure>());
        expect(missing.failureOrNull, isA<NotFoundFailure>());
      },
    );
  });

  group('wire format of the real API', () {
    test('parses a MyGrievanceDetailDto body', () {
      final MyGrievanceDetailModel model = MyGrievanceDetailModel.fromJson(
        <String, dynamic>{
          'id': 12,
          'caseNo': 'GRV-2026-00012',
          'categoryId': 3,
          'categoryName': 'Working conditions',
          'subject': 'No air conditioning',
          'status': 'AwaitingEmployeeResponse',
          'isAnonymous': false,
          'createdAtUtc': '2026-06-01T08:00:00Z',
          'submittedAtUtc': '2026-06-01T08:05:00Z',
          'targetResolutionDate': '2026-06-22',
          'lastUpdateAtUtc': '2026-06-03T10:00:00Z',
          'hasUnreadUpdate': true,
          'rowVersion': 'AAAAAAAAB9E=',
          'description': 'The room reaches 34 degrees in the afternoon.',
          'incidentDate': '2026-05-28',
          'location': null,
          'involvedPersonText': null,
          'involvedDepartmentId': null,
          'involvedDepartmentName': null,
          'declarationAcceptedAtUtc': '2026-06-01T08:05:00Z',
          'acknowledgedAtUtc': null,
          'resolvedAtUtc': null,
          'closedAtUtc': null,
          'rejectionReason': null,
          'resolutionSummary': null,
          'timeline': <Map<String, dynamic>>[
            <String, dynamic>{
              'fromStatus': 'Draft',
              'toStatus': 'Submitted',
              'action': 'Submit',
              'actor': 'HR team',
              'reason':
                  'Recorded by HR on behalf of the employee (received by '
                  'phone).',
              'occurredAtUtc': '2026-06-01T08:05:00Z',
            },
          ],
          'messages': <Map<String, dynamic>>[
            <String, dynamic>{
              'id': 5,
              'direction': 'ToSubmitter',
              'kind': 'InfoRequest',
              'body': 'Which room?',
              'actor': 'HR team',
              'inReplyToId': null,
              'createdAtUtc': '2026-06-03T10:00:00Z',
            },
          ],
          'attachments': <Map<String, dynamic>>[],
        },
      );

      final GrievanceDetail detail = model.toEntity();
      expect(detail.status, GrievanceStatus.awaitingEmployeeResponse);
      expect(detail.isRespondMode, isTrue);
      expect(detail.openInfoRequest?.id, 5);
      expect(detail.incidentDate, DateTime(2026, 5, 28));
      expect(detail.targetResolutionDate, DateTime(2026, 6, 22));
      expect(detail.timeline.single.reason, contains('on behalf of'));
    });

    test('reads the API page shape with its 0-based page index', () {
      final PaginatedResponse<MyGrievanceModel> page =
          GrievanceRemoteDataSourceImpl.pageFromJson(<String, dynamic>{
            'items': <Map<String, dynamic>>[
              <String, dynamic>{
                'id': 1,
                'caseNo': null,
                'categoryId': 1,
                'categoryName': 'Harassment or bullying',
                'subject': 'Draft subject',
                'status': 'Draft',
                'isAnonymous': true,
                'createdAtUtc': '2026-06-01T08:00:00Z',
                'hasUnreadUpdate': false,
                'rowVersion': 'AAAAAAAAB9E=',
              },
            ],
            'total': 21,
            'pageIndex': 1,
            'pageSize': 10,
          }, MyGrievanceModel.fromJson);

      expect(page.pageNumber, 2);
      expect(page.totalPages, 3);
      expect(page.hasNextPage, isTrue);
      expect(page.items.single.toEntity().status, GrievanceStatus.draft);
    });

    test('unwraps an envelope but leaves a bare body alone', () {
      final Map<String, dynamic> bare = <String, dynamic>{'id': 1};
      expect(GrievanceRemoteDataSourceImpl.unwrap(bare), same(bare));
      expect(
        GrievanceRemoteDataSourceImpl.unwrap(<String, dynamic>{
          'success': true,
          'data': bare,
        }),
        same(bare),
      );
    });

    test('sends a draft with the API field names, blanks as null', () {
      final Map<String, dynamic> body = GrievanceRequestBodies.draft(
        GrievanceDraftInput(
          categoryId: 3,
          subject: '  No air conditioning  ',
          description: 'Too hot.',
          incidentDate: DateTime(2026, 5, 28),
          location: '   ',
          involvedDepartmentId: 14,
        ),
        rowVersion: 'AAAAAAAAB9E=',
      );

      expect(body, <String, dynamic>{
        'rowVersion': 'AAAAAAAAB9E=',
        'categoryId': 3,
        'subject': 'No air conditioning',
        'description': 'Too hot.',
        'incidentDate': '2026-05-28',
        'location': null,
        'involvedPersonText': null,
        'involvedDepartmentId': 14,
        'isAnonymous': false,
      });
      expect(
        GrievanceRequestBodies.draft(GrievanceTestData.input()),
        isNot(contains('rowVersion')),
      );
    });
  });
}
