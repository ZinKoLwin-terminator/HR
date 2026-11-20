import { dayOfWeek } from '../../shared/utilities/date.util';
import { AttendanceStatus, Holiday, Shift } from '../models/attendance.models';

export interface DerivedAttendance {
  status: AttendanceStatus;
  workedMinutes: number;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  overtimeMinutes: number;
}

type ShiftRules = Pick<
  Shift,
  'startTime' | 'endTime' | 'breakMinutes' | 'graceMinutes' | 'isNightShift'
>;

const toMinutes = (time: string): number => {
  const [h, m] = time.split(':').map(Number);
  return (h ?? 0) * 60 + (m ?? 0);
};

/** Minutes after the scheduled end before overtime starts counting. */
export const OVERTIME_THRESHOLD_MINUTES = 30;
/** Worked time below this (with a check-out) is treated as a half day. */
export const HALF_DAY_MAX_MINUTES = 240;

/**
 * Sample attendance rules, shared by check-in/out, adjustments and the data generator:
 *  - Late: checked in after start + grace; late minutes are counted from the shift start.
 *  - Worked = (check-out − check-in) − break; half-day when worked < 4 h.
 *  - Early leave: minutes before the scheduled end. Overtime: minutes after end beyond 30.
 * Night shifts roll the end (and a check-out earlier than check-in) into the next day.
 */
export function deriveAttendance(
  shift: ShiftRules,
  checkIn: string | null,
  checkOut: string | null,
  forcedStatus?: AttendanceStatus | null,
): DerivedAttendance {
  if (!checkIn) {
    return {
      status: forcedStatus ?? 'Absent',
      workedMinutes: 0,
      lateMinutes: 0,
      earlyLeaveMinutes: 0,
      overtimeMinutes: 0,
    };
  }
  const start = toMinutes(shift.startTime);
  let end = toMinutes(shift.endTime);
  if (shift.isNightShift && end <= start) {
    end += 1440;
  }
  const inAt = toMinutes(checkIn);
  const late = inAt > start + shift.graceMinutes ? inAt - start : 0;

  if (!checkOut) {
    return {
      status: forcedStatus ?? (late > 0 ? 'Late' : 'Present'),
      workedMinutes: 0,
      lateMinutes: late,
      earlyLeaveMinutes: 0,
      overtimeMinutes: 0,
    };
  }

  let outAt = toMinutes(checkOut);
  if (outAt < inAt) {
    outAt += 1440; // crossed midnight
  }
  const grossMinutes = outAt - inAt;
  const halfDay =
    forcedStatus === 'Half Day' ||
    (forcedStatus == null && grossMinutes - shift.breakMinutes < HALF_DAY_MAX_MINUTES);
  const worked = Math.max(0, grossMinutes - (halfDay ? 0 : shift.breakMinutes));
  const after = outAt - end;
  return {
    status: forcedStatus ?? (halfDay ? 'Half Day' : late > 0 ? 'Late' : 'Present'),
    workedMinutes: worked,
    lateMinutes: late,
    earlyLeaveMinutes: halfDay ? 0 : Math.max(0, end - outAt),
    overtimeMinutes: after > OVERTIME_THRESHOLD_MINUTES ? after : 0,
  };
}

/** Why a date is not a working day for an employee: a holiday, or not one of the shift's days. */
export interface DayOff {
  status: 'Holiday' | 'Weekend';
  /** The holiday on that date, when there is one. */
  holiday: Pick<Holiday, 'name' | 'type'> | null;
}

/**
 * Same rule as the API's punch handler: a holiday on the date wins, then the shift's working days
 * decide (no shift: every day is a working day). Working on a day off is recorded as Present,
 * with no late or early-leave minutes. Returns null on a working day.
 */
export function dayOffOn(
  date: string,
  shift: Pick<Shift, 'workDays'> | null | undefined,
  holidays: readonly Pick<Holiday, 'date' | 'name' | 'type'>[],
): DayOff | null {
  const holiday = holidays.find((h) => h.date === date);
  if (holiday) {
    return { status: 'Holiday', holiday: { name: holiday.name, type: holiday.type } };
  }
  return shift && !shift.workDays.includes(dayOfWeek(date))
    ? { status: 'Weekend', holiday: null }
    : null;
}

/** Minutes between two HH:mm times (rolls over midnight when end < start). */
export function minutesBetween(start: string, end: string): number {
  const diff = toMinutes(end) - toMinutes(start);
  return diff >= 0 ? diff : diff + 1440;
}
