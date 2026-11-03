import { PayrollRun } from '../../models/payroll.models';
import { CrudDataService } from './crud-data.service';

export abstract class PayrollRunDataService extends CrudDataService<PayrollRun> {}
