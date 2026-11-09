import {
  AttendanceRecord,
  AttendanceSource,
  AttendanceStatus,
} from '../../core/models/attendance.models';
import { Employee } from '../../core/models/employee.models';
import { LeaveApplication } from '../../core/models/leave.models';
import {
  addDays,
  dayOfWeek,
  eachDay,
  minutesToTime,
  timeToMinutes,
  todayIso,
} from '../../shared/utilities/date.util';
import { seededInt, seededPick, seededRandom } from '../mock-random';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { MOCK_LEAVE_APPLICATIONS } from '../leave/leave-applications.mock';
import { MOCK_HOLIDAYS } from './holidays.mock';
import { MOCK_SHIFTS } from './shifts.mock';

/** Number of past days (plus today) covered by generated attendance. */
export const ATTENDANCE_HISTORY_DAYS = 60;

const SOURCES: readonly AttendanceSource[] = [
  'Biometric',
  'Biometric',
  'Biometric',
  'Mobile',
  'Web',
];

function findApprovedLeave(
  employeeId: number,
  date: string,
  leaves: readonly LeaveApplication[],
): LeaveApplication | undefined {
  return leaves.find(
    (l) =>
      l.employeeId === employeeId &&
      l.status === 'Approved' &&
      date >= l.startDate &&
      date <= l.endDate,
  );
}

function buildRecord(
  employee: Employee,
  date: string,
  id: number,
  isToday: boolean,
): AttendanceRecord {
  const shift = MOCK_SHIFTS.find((s) => s.id === employee.shiftId) ?? MOCK_SHIFTS[0];
  const key = `att-${employee.id}-${date}`;
  const base: AttendanceRecord = {
    id,
    employeeId: employee.id,
    date,
    shiftId: shift.id,
    checkIn: null,
    checkOut: null,
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    overtimeMinutes: 0,
    status: 'Present',
    source: seededPick(`${key}:src`, SOURCES),
    remarks: '',
  };

  const holiday = MOCK_HOLIDAYS.find((h) => h.date === date);
  if (holiday) {
    return { ...base, status: 'Holiday', remarks: holiday.name };
  }
  if (!shift.workDays.includes(dayOfWeek(date))) {
    return { ...base, status: 'Weekend' };
  }

  const leave = findApprovedLeave(employee.id, date, MOCK_LEAVE_APPLICATIONS);
  if (leave && !leave.halfDay) {
    return { ...base, status: 'On Leave', remarks: leave.applicationNo };
  }

  const roll = seededRandom(`${key}:roll`);
  const start = timeToMinutes(shift.startTime);
  const end = timeToMinutes(shift.endTime);
  let status: AttendanceStatus = 'Present';
  let checkIn = start - seededInt(`${key}:early`, 0, 20);
  let checkOut = end + seededInt(`${key}:out`, -5, 25);

  if (leave?.halfDay || roll < 0.02) {
    status = 'Half Day';
    checkOut = start + 4 * 60 + seededInt(`${key}:half`, 0, 20);
  } else if (roll < 0.05) {
    return { ...base, status: 'Absent', remarks: 'No check-in recorded' };
  } else if (roll < 0.15) {
    status = 'Late';
    checkIn = start + shift.graceMinutes + seededInt(`${key}:late`, 1, 55);
  }

  if (status !== 'Half Day') {
    const exitRoll = seededRandom(`${key}:exit`);
    if (exitRoll < 0.05) {
      checkOut = end - seededInt(`${key}:el`, 20, 90);
    } else if (exitRoll > 0.85) {
      checkOut = end + seededInt(`${key}:ot`, 45, 150);
    }
  }

  const lateMinutes = Math.max(0, checkIn - start - shift.graceMinutes) > 0 ? checkIn - start : 0;

  if (isToday) {
    // Today's shift is still in progress: only the check-in is known.
    return {
      ...base,
      status,
      checkIn: minutesToTime(checkIn),
      lateMinutes,
      remarks: status === 'Late' ? 'Late arrival' : '',
    };
  }

  const worked = Math.max(0, checkOut - checkIn - (status === 'Half Day' ? 0 : shift.breakMinutes));
  return {
    ...base,
    status,
    checkIn: minutesToTime(checkIn),
    checkOut: minutesToTime(checkOut),
    workedMinutes: worked,
    lateMinutes,
    earlyLeaveMinutes: status !== 'Half Day' ? Math.max(0, end - checkOut) : 0,
    overtimeMinutes: Math.max(0, checkOut - end - 30) > 0 ? checkOut - end : 0,
    remarks: leave?.halfDay ? leave.applicationNo : status === 'Late' ? 'Late arrival' : '',
  };
}

function isEmployedOn(employee: Employee, date: string): boolean {
  return (
    date >= employee.joiningDate &&
    (!employee.exitDate || date <= employee.exitDate) &&
    !(employee.employmentStatus === 'Suspended' && date >= addDays(todayIso(), -14))
  );
}

function generateAttendance(): AttendanceRecord[] {
  const today = todayIso();
  const clock = new Date();
  const nowMinutes = clock.getHours() * 60 + clock.getMinutes();
  const records: AttendanceRecord[] = [];
  let id = 1;
  for (const date of eachDay(addDays(today, -ATTENDANCE_HISTORY_DAYS), today)) {
    for (const employee of MOCK_EMPLOYEES) {
      if (isEmployedOn(employee, date)) {
        const record = buildRecord(employee, date, id, date === today);
        // Today: nobody can have checked in at a time that has not happened yet.
        const arrivedYet =
          !record.checkIn || date !== today || timeToMinutes(record.checkIn) <= nowMinutes;
        if (arrivedYet) {
          records.push(record);
          id++;
        }
      }
    }
  }
  return records;
}

/** SAMPLE DATA — generated deterministically for the last 60 days. */
export const MOCK_ATTENDANCE_RECORDS: AttendanceRecord[] = generateAttendance();
