import { EvaluationPeriod } from '../../core/models/kpi.models';
import { addDays, addMonths, endOfMonth, todayIso } from '../../shared/utilities/date.util';

const pad = (value: number): string => String(value).padStart(2, '0');

/** First and last day, and the label, of the calendar quarter `offset` quarters from the current one. */
function quarter(offset: number): { start: string; end: string; name: string } {
  const today = todayIso();
  const index =
    Number(today.slice(0, 4)) * 4 + Math.floor((Number(today.slice(5, 7)) - 1) / 3) + offset;
  const year = Math.floor(index / 4);
  const q = index % 4;
  const start = `${year}-${pad(q * 3 + 1)}-01`;
  return { start, end: endOfMonth(addMonths(start, 2)), name: `${year} Q${q + 1}` };
}

/** The half-year after the current one. */
function nextHalfYear(): { start: string; end: string; name: string } {
  const today = todayIso();
  const year = Number(today.slice(0, 4));
  const firstHalf = Number(today.slice(5, 7)) <= 6;
  return firstHalf
    ? { start: `${year}-07-01`, end: `${year}-12-31`, name: `${year} H2` }
    : { start: `${year + 1}-01-01`, end: `${year + 1}-06-30`, name: `${year + 1} H1` };
}

const previous = quarter(-1);
const current = quarter(0);
const upcoming = nextHalfYear();

/**
 * SAMPLE DATA - company 1 (AHT). Dates are relative to today: last quarter is Closed (with an HR
 * review), this quarter is Active, and the coming half-year is still a Draft. The Draft one is
 * Half-Yearly so it never overlaps a Quarterly period that an example creates for the next quarter.
 */
export const MOCK_EVALUATION_PERIODS: EvaluationPeriod[] = [
  {
    id: 1,
    companyId: 1,
    name: previous.name,
    periodType: 'Quarterly',
    startDate: previous.start,
    endDate: previous.end,
    submissionDueDate: addDays(previous.end, 14),
    requiresHrReview: true,
    status: 'Closed',
    rowVersion: '1',
  },
  {
    id: 2,
    companyId: 1,
    name: current.name,
    periodType: 'Quarterly',
    startDate: current.start,
    endDate: current.end,
    submissionDueDate: addDays(current.end, 14),
    requiresHrReview: false,
    status: 'Active',
    rowVersion: '1',
  },
  {
    id: 3,
    companyId: 1,
    name: upcoming.name,
    periodType: 'Half-Yearly',
    startDate: upcoming.start,
    endDate: upcoming.end,
    submissionDueDate: addDays(upcoming.end, 14),
    requiresHrReview: true,
    status: 'Draft',
    rowVersion: '1',
  },
];
