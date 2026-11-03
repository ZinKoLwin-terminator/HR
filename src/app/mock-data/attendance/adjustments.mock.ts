import {
  ApprovalStatus,
  AttendanceAdjustment,
  AttendanceStatus,
} from '../../core/models/attendance.models';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { MOCK_ATTENDANCE_RECORDS } from './attendance-records.mock';

type Seed = [
  employeeId: number,
  daysAgo: number,
  requestedIn: string | null,
  requestedOut: string | null,
  forcedStatus: AttendanceStatus | null,
  reason: string,
  status: ApprovalStatus,
  decidedBy?: string,
  remarks?: string,
];

const SEED: Seed[] = [
  [17, 3, '08:58', null, null, 'Forgot to check in; arrived on time.', 'Pending'],
  [21, 5, null, '18:05', null, 'Biometric device did not register my exit.', 'Pending'],
  [
    24,
    2,
    '09:00',
    '18:00',
    null,
    'Worked on site with a client; no biometric available.',
    'Pending',
  ],
  [33, 8, '09:05', '13:30', 'Half Day', 'Medical appointment in the afternoon.', 'Pending'],
  [40, 4, '06:05', '14:30', null, 'Device offline at the start of my shift.', 'Pending'],
  [
    16,
    12,
    '08:55',
    '18:10',
    null,
    'Missed punch; supervisor confirmed attendance.',
    'Approved',
    'Hnin Ei Phyu',
    'Confirmed with supervisor.',
  ],
  [
    27,
    15,
    '09:02',
    null,
    null,
    'Gate system error in the morning.',
    'Approved',
    'Su Myat Noe',
    'Verified in the gate log.',
  ],
  [
    9,
    18,
    null,
    '18:30',
    null,
    'Late meeting with a vendor.',
    'Approved',
    'Su Myat Noe',
    'Approved.',
  ],
  [
    12,
    20,
    '05:55',
    '14:35',
    null,
    'Requesting a change without supporting evidence.',
    'Rejected',
    'Hnin Ei Phyu',
    'No evidence provided.',
  ],
  [
    38,
    10,
    '08:30',
    null,
    null,
    'Claimed on-time arrival.',
    'Rejected',
    'Su Myat Noe',
    'Gate camera shows a later arrival.',
  ],
];

function generate(): AttendanceAdjustment[] {
  const today = todayIso();
  const result: AttendanceAdjustment[] = [];
  for (const [
    employeeId,
    daysAgo,
    reqIn,
    reqOut,
    forced,
    reason,
    status,
    decidedBy,
    remarks,
  ] of SEED) {
    // Use the nearest earlier day that has a working-day record for this employee.
    let record;
    for (let back = daysAgo; back < daysAgo + 12 && !record; back++) {
      const date = addDays(today, -back);
      record = MOCK_ATTENDANCE_RECORDS.find(
        (r) =>
          r.employeeId === employeeId &&
          r.date === date &&
          ['Present', 'Late', 'Absent', 'Half Day'].includes(r.status),
      );
    }
    if (!record) {
      continue;
    }
    const requestedOn = addDays(record.date, 1) > today ? today : addDays(record.date, 1);
    result.push({
      id: result.length + 1,
      employeeId,
      date: record.date,
      recordId: record.id,
      currentCheckIn: record.checkIn,
      currentCheckOut: record.checkOut,
      requestedCheckIn: reqIn,
      requestedCheckOut: reqOut,
      requestedStatus: forced,
      reason,
      status,
      requestedOn,
      decidedBy: decidedBy ?? null,
      decidedOn:
        status === 'Pending'
          ? null
          : addDays(requestedOn, 1) > today
            ? today
            : addDays(requestedOn, 1),
      remarks: remarks ?? '',
    });
  }
  return result;
}

/** SAMPLE DATA */
export const MOCK_ATTENDANCE_ADJUSTMENTS: AttendanceAdjustment[] = generate();
