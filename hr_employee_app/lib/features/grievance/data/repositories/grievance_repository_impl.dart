import 'package:hr_employee_app/core/errors/repository_guard.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';
import 'package:hr_employee_app/features/grievance/data/datasources/grievance_remote_datasource.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_models.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/domain/repositories/grievance_repository.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// Maps grievance payloads to entities.
///
/// Log lines carry the operation only: never a subject, description or
/// message body, which may identify an anonymous employee.
class GrievanceRepositoryImpl
    with RepositoryGuard
    implements GrievanceRepository {
  GrievanceRepositoryImpl({required this._remote, required this.logger});

  final GrievanceRemoteDataSource _remote;

  @override
  final AppLogger logger;

  @override
  Future<Result<GrievanceOptions>> getOptions() {
    return guard(operation: 'getGrievanceOptions', () async {
      final GrievanceOptionsModel model = await _remote.getOptions();
      return model.toEntity();
    });
  }

  @override
  Future<Result<PaginatedResponse<GrievanceSummary>>> getMyGrievances({
    required PageRequest page,
    GrievanceStatus? status,
    String? search,
  }) {
    return guard(operation: 'getMyGrievances', () async {
      final PaginatedResponse<MyGrievanceModel> response = await _remote
          .getMyGrievances(page: page, status: status, search: search);
      return response.mapItems((MyGrievanceModel model) => model.toEntity());
    });
  }

  @override
  Future<Result<GrievanceDetail>> getById(int id) {
    return guard(operation: 'getMyGrievance', () async {
      final MyGrievanceDetailModel model = await _remote.getById(id);
      return model.toEntity();
    });
  }

  @override
  Future<Result<GrievanceDetail>> createDraft(GrievanceDraftInput input) {
    return guard(operation: 'createGrievanceDraft', () async {
      final MyGrievanceDetailModel model = await _remote.createDraft(input);
      return model.toEntity();
    });
  }

  @override
  Future<Result<GrievanceDetail>> updateDraft(
    int id, {
    required String rowVersion,
    required GrievanceDraftInput input,
  }) {
    return guard(operation: 'updateGrievanceDraft', () async {
      final MyGrievanceDetailModel model = await _remote.updateDraft(
        id,
        rowVersion: rowVersion,
        input: input,
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<void>> deleteDraft(int id, {required String rowVersion}) {
    return guard(
      operation: 'deleteGrievanceDraft',
      () => _remote.deleteDraft(id, rowVersion: rowVersion),
    );
  }

  @override
  Future<Result<GrievanceDetail>> submit(
    int id, {
    required String rowVersion,
    required bool declarationAccepted,
  }) {
    return guard(operation: 'submitGrievance', () async {
      final MyGrievanceDetailModel model = await _remote.submit(
        id,
        rowVersion: rowVersion,
        declarationAccepted: declarationAccepted,
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<GrievanceDetail>> sendMessage(
    int id, {
    required String rowVersion,
    required String body,
    int? inReplyToId,
  }) {
    return guard(operation: 'sendGrievanceMessage', () async {
      final MyGrievanceDetailModel model = await _remote.sendMessage(
        id,
        rowVersion: rowVersion,
        body: body,
        inReplyToId: inReplyToId,
      );
      return model.toEntity();
    });
  }

  @override
  Future<Result<void>> markSeen(int id) {
    return guard(operation: 'markGrievanceSeen', () => _remote.markSeen(id));
  }
}
