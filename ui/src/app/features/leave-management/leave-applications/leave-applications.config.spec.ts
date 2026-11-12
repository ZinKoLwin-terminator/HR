import { LeaveApplication } from '../../../core/models/leave.models';
import { LeaveApplicationDataService } from '../../../core/services/data/leave-data.services';
import { buildLeaveApplicationsConfig } from './leave-applications.config';

const submitted = (employeeId: number) =>
  ({ id: employeeId, employeeId, status: 'Submitted' }) as unknown as LeaveApplication;

function decisions(ownEmployeeId: number | null) {
  const config = buildLeaveApplicationsConfig({
    mode: 'pending',
    service: {} as LeaveApplicationDataService,
    employees: [],
    types: [],
    holidays: [],
    weekendDays: [0, 6],
    ownEmployeeId,
  });
  const actions = config.extraActions ?? [];
  const approve = actions.find((a) => a.id === 'approve')!;
  const reject = actions.find((a) => a.id === 'reject')!;
  return { approve, reject };
}

describe('leave approvals: own requests', () => {
  it('hides Approve and Reject on the signed-in person’s own request', () => {
    const { approve, reject } = decisions(5);
    expect(approve.visible?.(submitted(5))).toBe(false);
    expect(reject.visible?.(submitted(5))).toBe(false);
  });

  it('keeps Approve and Reject on someone else’s submitted request', () => {
    const { approve, reject } = decisions(5);
    expect(approve.visible?.(submitted(9))).toBe(true);
    expect(reject.visible?.(submitted(9))).toBe(true);
  });

  it('keeps them for a signed-in user without an employee (built-in administrator)', () => {
    const { approve } = decisions(null);
    expect(approve.visible?.(submitted(5))).toBe(true);
  });
});
