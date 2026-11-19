import 'package:hr_employee_app/core/network/api_client.dart';
import 'package:hr_employee_app/core/network/api_endpoints.dart';
import 'package:hr_employee_app/features/dashboard/data/models/dashboard_summary_model.dart';

/// Reads the dashboard aggregate.
abstract interface class DashboardRemoteDataSource {
  Future<DashboardSummaryModel> getSummary();
}

/// Calls the real API.
class DashboardRemoteDataSourceImpl implements DashboardRemoteDataSource {
  const DashboardRemoteDataSourceImpl(this._client);

  final ApiClient _client;

  @override
  Future<DashboardSummaryModel> getSummary() {
    return _client.get<DashboardSummaryModel>(
      ApiEndpoints.dashboardSummary,
      decode: (Object? data) =>
          DashboardSummaryModel.fromJson(Decode.object(data)),
    );
  }
}
