import { Holiday } from '../../core/models/attendance.models';

type HolidaySeed = [date: string, name: string, type: Holiday['type']];

/**
 * SAMPLE holiday calendar for demonstration only — not an official
 * public-holiday schedule for any country.
 */
const SEED: HolidaySeed[] = [
  ['2026-01-01', "New Year's Day", 'Public'],
  ['2026-01-04', 'Independence Day', 'Public'],
  ['2026-04-13', 'Water Festival', 'Public'],
  ['2026-04-14', 'Water Festival', 'Public'],
  ['2026-04-15', 'Water Festival', 'Public'],
  ['2026-04-16', 'Water Festival', 'Public'],
  ['2026-04-17', 'New Year Holiday', 'Public'],
  ['2026-05-01', 'Labour Day', 'Public'],
  ['2026-10-23', 'Festival of Lights', 'Public'],
  ['2026-11-20', 'Company Foundation Day', 'Company'],
  ['2026-11-23', 'Full Moon Festival', 'Public'],
  ['2026-12-25', 'Christmas Day', 'Public'],
  ['2026-12-31', 'Year-End Holiday', 'Company'],
  ['2027-01-01', "New Year's Day", 'Public'],
  ['2027-01-04', 'Independence Day', 'Public'],
];

export const MOCK_HOLIDAYS: Holiday[] = SEED.map(([date, name, type], index) => ({
  id: index + 1,
  date,
  name,
  type,
}));
