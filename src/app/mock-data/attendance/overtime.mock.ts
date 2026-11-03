import { ApprovalStatus, OvertimeRecord } from '../../core/models/attendance.models';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { seededPick, seededRandom } from '../mock-random';
import { MOCK_ATTENDANCE_RECORDS } from './attendance-records.mock';
import { MOCK_SHIFTS } from './shifts.mock';

const REASONS = [
  'Month-end processing',
  'Customer deadline',
  'System maintenance window',
  'Inventory count',
  'Urgent shipment',
  'Release preparation',
] as const;
const APPROVERS = ['Su Myat Noe', 'Hnin Ei Phyu', 'Kyaw Zin Htet'] as const;

function generate(): OvertimeRecord[] {
  const today = todayIso();
  const from = addDays(today, -45);
  const result: OvertimeRecord[] = [];
  for (const r of MOCK_ATTENDANCE_RECORDS) {
    if (!r.checkOut || r.overtimeMinutes < 60 || r.date < from || r.date >= today) {
      continue;
    }
    const shift = MOCK_SHIFTS.find((s) => s.id === r.shiftId);
    if (!shift) {
      continue;
    }
    const key = `ot-${r.id}`;
    const old = r.date < addDays(today, -14);
    const roll = seededRandom(key);
    const status: ApprovalStatus = old
      ? roll < 0.12
        ? 'Rejected'
        : 'Approved'
      : roll < 0.8
        ? 'Pending'
        : 'Approved';
    const decided = status !== 'Pending';
    result.push({
      id: result.length + 1,
      employeeId: r.employeeId,
      date: r.date,
      startTime: shift.endTime,
      endTime: r.checkOut,
      minutes: r.overtimeMinutes,
      reason: seededPick(`${key}:why`, REASONS),
      status,
      requestedOn: addDays(r.date, 1) > today ? today : addDays(r.date, 1),
      decidedBy: decided ? seededPick(`${key}:by`, APPROVERS) : null,
      decidedOn: decided ? (addDays(r.date, 2) > today ? today : addDays(r.date, 2)) : null,
      remarks:
        status === 'Rejected' ? 'Not pre-approved by the manager.' : decided ? 'Approved.' : '',
    });
  }
  return result;
}

/** SAMPLE DATA — derived from attendance records with at least one hour of overtime. */
export const MOCK_OVERTIME_RECORDS: OvertimeRecord[] = generate();
