import { describeWorkDays } from '../constants/weekdays';
import { deriveAttendance, minutesBetween } from './attendance-calc';

const DAY = {
  startTime: '09:00',
  endTime: '18:00',
  breakMinutes: 60,
  graceMinutes: 15,
  isNightShift: false,
};
const NIGHT = {
  startTime: '22:00',
  endTime: '06:00',
  breakMinutes: 45,
  graceMinutes: 10,
  isNightShift: true,
};

describe('deriveAttendance', () => {
  it('marks an on-time full day as Present with worked time net of the break', () => {
    expect(deriveAttendance(DAY, '08:55', '18:00')).toEqual({
      status: 'Present',
      workedMinutes: 9 * 60 + 5 - 60,
      lateMinutes: 0,
      earlyLeaveMinutes: 0,
      overtimeMinutes: 0,
    });
  });

  it('treats arrivals within the grace period as on time and later ones as late from the shift start', () => {
    expect(deriveAttendance(DAY, '09:15', '18:00').status).toBe('Present');
    const late = deriveAttendance(DAY, '09:16', '18:00');
    expect(late.status).toBe('Late');
    expect(late.lateMinutes).toBe(16);
    expect(deriveAttendance(DAY, '09:40', null)).toMatchObject({
      status: 'Late',
      lateMinutes: 40,
      workedMinutes: 0,
    });
  });

  it('computes early leave and overtime (only beyond the 30 minute threshold)', () => {
    expect(deriveAttendance(DAY, '09:00', '17:00')).toMatchObject({
      earlyLeaveMinutes: 60,
      overtimeMinutes: 0,
    });
    expect(deriveAttendance(DAY, '09:00', '18:30').overtimeMinutes).toBe(0);
    expect(deriveAttendance(DAY, '09:00', '19:00').overtimeMinutes).toBe(60);
  });

  it('derives Half Day for short days and honours a forced status', () => {
    const short = deriveAttendance(DAY, '09:00', '12:30');
    expect(short.status).toBe('Half Day');
    expect(short.workedMinutes).toBe(210); // no break deducted
    expect(short.earlyLeaveMinutes).toBe(0);

    expect(deriveAttendance(DAY, '09:00', '18:00', 'Half Day').status).toBe('Half Day');
    expect(deriveAttendance(DAY, '09:00', '18:00', 'Late').status).toBe('Late');
  });

  it('is Absent without a check-in unless a status is forced', () => {
    expect(deriveAttendance(DAY, null, null).status).toBe('Absent');
    expect(deriveAttendance(DAY, null, null, 'On Leave').status).toBe('On Leave');
  });

  it('handles night shifts that end the next morning', () => {
    const result = deriveAttendance(NIGHT, '22:05', '06:00');
    expect(result.workedMinutes).toBe(8 * 60 - 5 - 45);
    expect(result.earlyLeaveMinutes).toBe(0);
    expect(result.status).toBe('Present');
  });

  it('measures time between HH:mm values across midnight', () => {
    expect(minutesBetween('18:00', '20:30')).toBe(150);
    expect(minutesBetween('22:00', '01:00')).toBe(180);
  });
});

describe('describeWorkDays', () => {
  it('summarises contiguous and scattered days', () => {
    expect(describeWorkDays([1, 2, 3, 4, 5])).toBe('Mon–Fri');
    expect(describeWorkDays([1, 2, 3, 4, 5, 6])).toBe('Mon–Sat');
    expect(describeWorkDays([1, 3, 5])).toBe('Mon, Wed, Fri');
    expect(describeWorkDays([6, 0])).toBe('Sat, Sun');
    expect(describeWorkDays([])).toBe('—');
  });
});
