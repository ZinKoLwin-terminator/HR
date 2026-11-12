import { BlacklistEntry } from '../../core/models/blacklist.models';
import { fullName } from '../../core/models/employee.models';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from './employees.mock';

/**
 * SAMPLE DATA: one entry in force, for a fictional former employee who was terminated. The reason
 * is invented text; no real person or case is described.
 */
const formerEmployee = MOCK_EMPLOYEES.find(
  (e) => e.employmentStatus === 'Terminated' && e.exitDate,
);

export const MOCK_BLACKLIST: BlacklistEntry[] = formerEmployee
  ? [
      {
        id: 1,
        employeeId: formerEmployee.id,
        employeeCode: formerEmployee.employeeCode,
        employeeName: fullName(formerEmployee),
        reason: 'Sample reason: contract ended early after a conduct review. Fictional demo text.',
        blacklistedOn: addDays(todayIso(), -20),
        blacklistedBy: 'Sample HR Administrator',
        liftedOn: null,
        liftedBy: null,
        liftReason: null,
      },
    ]
  : [];
