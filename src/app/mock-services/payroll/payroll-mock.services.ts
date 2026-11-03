import { Injectable } from '@angular/core';
import { PayrollRun } from '../../core/models/payroll.models';
import { PayrollRunDataService } from '../../core/services/data/payroll-data.services';
import { MOCK_PAYROLL_RUNS } from '../../mock-data/payroll/payroll-runs.mock';
import { InMemoryCrudService } from '../in-memory-crud.service';

@Injectable({ providedIn: 'root' })
export class PayrollRunMockService
  extends InMemoryCrudService<PayrollRun>
  implements PayrollRunDataService
{
  protected readonly entityLabel = 'Payroll run';
  protected readonly activityCategory = 'payroll';
  protected override readonly searchFields = ['periodName', 'status'] as const;

  constructor() {
    super(MOCK_PAYROLL_RUNS);
  }

  protected override describe(entity: PayrollRun): string {
    return entity.periodName;
  }
}
