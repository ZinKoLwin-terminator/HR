import { EmploymentHistoryEntry, HistoryEventType } from '../../core/models/employee-extras.models';
import { addDays, addMonths, todayIso } from '../../shared/utilities/date.util';
import { seededInt, seededRandom } from '../mock-random';
import { MOCK_POSITIONS } from '../organization/positions.mock';
import { MOCK_EMPLOYEES } from './employees.mock';

const RECORDERS = ['Su Myat Noe', 'Hnin Ei Phyu', 'Khin Sandar'] as const;

function positionTitle(id: number): string {
  return MOCK_POSITIONS.find((p) => p.id === id)?.title ?? `Position #${id}`;
}

function generate(): EmploymentHistoryEntry[] {
  const today = todayIso();
  const entries: EmploymentHistoryEntry[] = [];
  const add = (
    employeeId: number,
    date: string,
    type: HistoryEventType,
    title: string,
    details: string,
  ) => {
    entries.push({
      id: entries.length + 1,
      employeeId,
      date,
      type,
      title,
      details,
      recordedBy:
        RECORDERS[seededInt(`hist-${employeeId}-${entries.length}`, 0, RECORDERS.length - 1)],
    });
  };

  for (const e of MOCK_EMPLOYEES) {
    add(
      e.id,
      e.joiningDate,
      'Joined',
      `Joined as ${positionTitle(e.positionId)}`,
      `${e.employmentType} employee. Onboarding completed.`,
    );

    if (e.probationEndDate && e.probationEndDate <= today && e.employmentStatus !== 'Probation') {
      add(
        e.id,
        e.probationEndDate,
        'Confirmation',
        'Probation completed',
        'Employment confirmed after a successful probation review.',
      );
    }

    // Older employees: a deterministic mid-career salary revision and, sometimes, a promotion.
    const tenureMonths = Math.round(
      (new Date(today).getTime() - new Date(e.joiningDate).getTime()) / 2_592_000_000,
    );
    if (tenureMonths >= 18) {
      const revisionDate = addMonths(
        e.joiningDate,
        12 + seededInt(`rev-${e.id}`, 0, Math.min(tenureMonths - 13, 10)),
      );
      add(
        e.id,
        revisionDate,
        'Salary Revision',
        'Annual salary revision',
        'Salary adjusted following the annual performance review.',
      );
    }
    if (tenureMonths >= 30 && seededRandom(`promo-${e.id}`) < 0.4) {
      add(
        e.id,
        addMonths(e.joiningDate, 24),
        'Promotion',
        'Promoted within the department',
        `Recognised for strong performance; now ${positionTitle(e.positionId)}.`,
      );
    }

    if (e.employmentStatus === 'Suspended') {
      add(
        e.id,
        addDays(today, -14),
        'Status Change',
        'Suspended pending review',
        'Employment status changed to Suspended.',
      );
    }
    if (e.exitDate) {
      add(
        e.id,
        e.exitDate,
        'Exit',
        e.employmentStatus === 'Terminated' ? 'Employment terminated' : 'Resignation effective',
        `Last working day: ${e.exitDate}.`,
      );
    }
  }
  return entries;
}

/** SAMPLE DATA */
export const MOCK_EMPLOYMENT_HISTORY: EmploymentHistoryEntry[] = generate();
