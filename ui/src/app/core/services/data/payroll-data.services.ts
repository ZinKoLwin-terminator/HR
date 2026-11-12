import { Observable } from 'rxjs';
import { PagedResult, PageRequest } from '../../models/common.models';
import {
  PayrollRun,
  Payslip,
  PayslipFilter,
  SalaryAssignment,
  SalaryComponent,
  SalaryStructure,
} from '../../models/payroll.models';
import { CrudDataService } from './crud-data.service';

export abstract class SalaryComponentDataService extends CrudDataService<SalaryComponent> {}
export abstract class SalaryStructureDataService extends CrudDataService<SalaryStructure> {}
export abstract class SalaryAssignmentDataService extends CrudDataService<SalaryAssignment> {}

/** Payroll periods and their Draft → Pending Approval → Approved → Paid workflow. */
export abstract class PayrollRunDataService extends CrudDataService<PayrollRun> {
  /** Generates payslips for everyone employed in the period (Draft → Pending Approval). */
  abstract process(id: number): Observable<PayrollRun>;
  abstract approve(id: number): Observable<PayrollRun>;
  /** Sample only: records that payment happened. No money moves and no bank is contacted. */
  abstract markPaid(id: number): Observable<PayrollRun>;
  /** Pending Approval / Approved → Draft; the generated payslips are removed. */
  abstract returnToDraft(id: number, reason: string): Observable<PayrollRun>;
}

/** Payslips are produced by processing a period and are read-only. */
export abstract class PayslipDataService {
  abstract getById(id: number): Observable<Payslip>;
  abstract query(request: PageRequest<PayslipFilter>): Observable<PagedResult<Payslip>>;
}

/**
 * Employee self-service: the signed-in user's own payslips, newest period first. Only payslips of
 * Approved or Paid runs. No payroll permission is needed; a user without a linked employee gets
 * an empty page. Another employee's payslip is NotFound.
 */
export abstract class MyPayslipDataService {
  abstract query(request: {
    pageIndex: number;
    pageSize: number;
  }): Observable<PagedResult<Payslip>>;
  abstract getById(id: number): Observable<Payslip>;
}
