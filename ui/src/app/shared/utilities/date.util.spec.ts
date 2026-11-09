import {
  addDays,
  addMonths,
  countWorkingDays,
  diffInDays,
  endOfMonth,
  formatDuration,
  minutesToTime,
  timeToMinutes,
  yearsBetween,
} from './date.util';

describe('date.util', () => {
  it('adds days and months across boundaries', () => {
    expect(addDays('2026-02-27', 3)).toBe('2026-03-02');
    expect(addMonths('2026-11-15', 2)).toBe('2027-01-15');
    expect(endOfMonth('2028-02-10')).toBe('2028-02-29');
    expect(diffInDays('2026-11-01', '2026-11-03')).toBe(2);
  });

  it('counts working days excluding weekends and holidays', () => {
    // Mon 2 Nov 2026 – Sun 8 Nov 2026
    expect(countWorkingDays('2026-11-02', '2026-11-08', [0, 6])).toBe(5);
    expect(countWorkingDays('2026-11-02', '2026-11-08', [0, 6], new Set(['2026-11-04']))).toBe(4);
    expect(countWorkingDays('2026-11-05', '2026-11-02', [0, 6])).toBe(0);
  });

  it('computes full years', () => {
    expect(yearsBetween('2000-11-04', '2026-11-03')).toBe(25);
    expect(yearsBetween('2000-11-03', '2026-11-03')).toBe(26);
  });

  it('converts times', () => {
    expect(timeToMinutes('09:15')).toBe(555);
    expect(minutesToTime(555)).toBe('09:15');
    expect(minutesToTime(1500)).toBe('01:00');
    expect(formatDuration(125)).toBe('2h 05m');
  });
});
