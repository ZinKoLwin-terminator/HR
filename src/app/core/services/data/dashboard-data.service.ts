import { Observable } from 'rxjs';
import { DashboardSummary } from '../../models/dashboard.models';

export abstract class DashboardDataService {
  abstract getSummary(): Observable<DashboardSummary>;
}
