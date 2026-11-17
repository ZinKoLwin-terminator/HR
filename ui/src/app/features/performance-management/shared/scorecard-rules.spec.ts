import {
  EVIDENCE_MAX_BYTES,
  EvaluationPeriodStatus,
  ReviewAction,
  ScorecardDetail,
  ScorecardStatus,
} from '../../../core/models/kpi.models';
import { pdfFile } from '../../../mock-services/kpi/kpi-test-support';
import {
  EVIDENCE_LIMIT_MESSAGE,
  NO_REVIEW_ACTIONS,
  ReviewActions,
  ScorecardViewer,
  actualInputFor,
  actualText,
  canWorkOnScorecard,
  employeeNote,
  evidenceProblem,
  fileSize,
  isLateSubmission,
  latestDecision,
  managerApproverName,
  noActionReason,
  reviewActions,
  rowErrors,
  stageLabel,
} from './scorecard-rules';
import { loadDueDates, rowIsLate } from './period-due-dates';
import { of, firstValueFrom, throwError } from 'rxjs';
import { Scorecard } from '../../../core/models/kpi.models';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';

// The scorecard: employee 46, reviewer 5. The viewers of the button matrix.
const viewer = (over: Partial<ScorecardViewer>): ScorecardViewer => ({
  employeeId: null,
  displayName: '',
  canCreate: true,
  canApprove: false,
  hrTier: false,
  ...over,
});
const OWNER = viewer({ employeeId: 46, displayName: 'Hein Thant Zin' });
const REVIEWER = viewer({ employeeId: 5, displayName: 'Daniel Carter', canApprove: true });
const OTHER_MANAGER = viewer({ employeeId: 9, displayName: 'Ye Yint Naing', canApprove: true });
const HR = viewer({ employeeId: 2, displayName: 'Su Myat Noe', canApprove: true, hrTier: true });
const HR_WHO_APPROVED = viewer({
  employeeId: 1,
  displayName: 'Thura Aung',
  canApprove: true,
  hrTier: true,
});
/** The HR person reviewing their own scorecard (the owner has every grant). */
const HR_OWNER = viewer({ ...HR, employeeId: 46 });

const approvedBy = (name: string): ReviewAction => ({
  step: 'Manager',
  action: 'Approve',
  fromStatus: 'Under Review',
  toStatus: 'Approved',
  actorName: name,
  comment: null,
  overallScore: 80,
  occurredAtUtc: '2026-11-10T09:00:00.000Z',
});

function card(
  status: ScorecardStatus,
  periodStatus: EvaluationPeriodStatus = 'Active',
  history: ReviewAction[] = [],
): ScorecardDetail {
  return {
    employeeId: 46,
    employeeName: 'Hein Thant Zin',
    reviewerEmployeeId: 5,
    status,
    periodStatus,
    periodName: '2026 Q4',
    history,
  } as ScorecardDetail;
}

const on = (a: ReviewActions): string[] =>
  Object.entries(a)
    .filter(([, v]) => v)
    .map(([k]) => k)
    .sort();

describe('scorecard rules: Late chip', () => {
  it('flags a submission after the due date only', () => {
    expect(isLateSubmission('2027-01-15', '2027-01-14')).toBe(true);
    expect(isLateSubmission('2027-01-14', '2027-01-14')).toBe(false);
    expect(isLateSubmission('2026-12-01', '2027-01-14')).toBe(false);
    expect(isLateSubmission(null, '2027-01-14')).toBe(false);
    expect(isLateSubmission('2027-01-15', null)).toBe(false);
  });

  it('reads one detail per period of the submitted rows for the list chip', async () => {
    const rows = [
      { id: 1, periodId: 2, submittedOn: '2027-01-20' },
      { id: 4, periodId: 2, submittedOn: '2026-12-01' },
      { id: 6, periodId: 2, submittedOn: null },
      { id: 8, periodId: 1, submittedOn: '2026-10-03' },
      { id: 9, periodId: 3, submittedOn: null },
    ] as Scorecard[];
    const getById = vi.fn((id: number) =>
      id === 8
        ? throwError(() => new Error('gone'))
        : of({ submissionDueDate: '2027-01-14' } as ScorecardDetail),
    );
    const dates = await firstValueFrom(
      loadDueDates({ getById } as unknown as ScorecardDataService, rows),
    );
    expect(getById.mock.calls.map(([id]) => id)).toEqual([1, 8]); // one per period, submitted only
    expect(rowIsLate(rows[0], dates)).toBe(true);
    expect(rowIsLate(rows[1], dates)).toBe(false);
    expect(rowIsLate(rows[3], dates)).toBe(false); // a failed read: no chip
    expect(await firstValueFrom(loadDueDates({ getById } as never, [rows[2]]))).toEqual(new Map());
  });
});

describe('scorecard rules: the employee works on their own scorecard only', () => {
  const statuses: ScorecardStatus[] = [
    'Planning',
    'Draft',
    'Submitted',
    'Under Review',
    'Returned',
    'Resubmitted',
    'Approved',
    'Rejected',
    'Finalized',
  ];

  it('owner, Draft or Returned, Active period, with kpi:Create', () => {
    const editable = statuses.filter((s) => canWorkOnScorecard(card(s), OWNER));
    expect(editable).toEqual(['Draft', 'Returned']);
    for (const other of [REVIEWER, HR, OTHER_MANAGER]) {
      expect(statuses.filter((s) => canWorkOnScorecard(card(s), other))).toEqual([]);
    }
    expect(canWorkOnScorecard(card('Draft', 'Completed'), OWNER)).toBe(false);
    expect(canWorkOnScorecard(card('Draft', 'Closed'), OWNER)).toBe(false);
    expect(canWorkOnScorecard(card('Draft'), { ...OWNER, canCreate: false })).toBe(false);
    expect(canWorkOnScorecard(card('Draft'), { ...OWNER, employeeId: null })).toBe(false);
  });

  it('says why the page is read-only', () => {
    expect(employeeNote(card('Draft'), OWNER)).toBeNull();
    expect(employeeNote(card('Draft'), HR)).toContain('Only they can enter results');
    expect(employeeNote(card('Draft', 'Completed'), OWNER)).toContain('is Completed');
    expect(employeeNote(card('Submitted'), OWNER)).toContain('can no longer be changed');
    expect(employeeNote(card('Approved'), OWNER)).toContain('HR final review');
    expect(employeeNote(card('Finalized'), OWNER)).toContain('Finalized');
    expect(employeeNote(card('Rejected'), OWNER)).toContain('Rejected');
  });
});

describe('scorecard rules: reviewer buttons for every status and viewer', () => {
  const MANAGER_BUTTONS = ['approve', 'reject', 'returnForRevision', 'saveReview'];
  const HR_BUTTONS = ['finalize', 'reject', 'returnForRevision'];
  const approvedHistory = [approvedBy('Thura Aung')];

  // status → [owner, reviewer, other manager, HR, HR who approved]
  const matrix: [ScorecardStatus, string[][]][] = [
    ['Planning', [[], [], [], [], []]],
    ['Draft', [[], [], [], [], []]],
    [
      'Submitted',
      [
        [],
        [...MANAGER_BUTTONS, 'startReview'].sort(),
        [],
        [...MANAGER_BUTTONS, 'startReview'].sort(),
        [...MANAGER_BUTTONS, 'startReview'].sort(),
      ],
    ],
    [
      'Resubmitted',
      [
        [],
        [...MANAGER_BUTTONS, 'startReview'].sort(),
        [],
        [...MANAGER_BUTTONS, 'startReview'].sort(),
        [...MANAGER_BUTTONS, 'startReview'].sort(),
      ],
    ],
    ['Under Review', [[], MANAGER_BUTTONS, [], MANAGER_BUTTONS, MANAGER_BUTTONS]],
    ['Returned', [[], [], [], [], []]],
    // HR stage: HR only, never the reviewer, and not the HR person who approved it.
    ['Approved', [[], [], [], HR_BUTTONS, []]],
    ['Rejected', [[], [], [], [], []]],
    ['Finalized', [[], [], [], [], []]],
  ];

  it.each(matrix)('%s', (status, expected) => {
    const viewers = [OWNER, REVIEWER, OTHER_MANAGER, HR, HR_WHO_APPROVED];
    const got = viewers.map((v) => on(reviewActions(card(status, 'Active', approvedHistory), v)));
    expect(got).toEqual(expected);
  });

  it('never offers review actions on the viewer’s own scorecard, even to HR', () => {
    for (const status of ['Submitted', 'Under Review', 'Approved'] as ScorecardStatus[]) {
      expect(reviewActions(card(status), HR_OWNER)).toEqual(NO_REVIEW_ACTIONS);
    }
    expect(noActionReason(card('Submitted'), HR_OWNER)).toContain('your own scorecard');
  });

  it('offers HR the final review when nobody HR approved it (the reviewer did)', () => {
    const byReviewer = card('Approved', 'Active', [approvedBy('Daniel Carter')]);
    expect(on(reviewActions(byReviewer, HR_WHO_APPROVED))).toEqual(HR_BUTTONS);
    expect(on(reviewActions(byReviewer, REVIEWER))).toEqual([]);
    expect(noActionReason(byReviewer, REVIEWER)).toContain('waits for the HR final review');
    const byHr = card('Approved', 'Active', [approvedBy('Thura Aung')]);
    expect(noActionReason(byHr, HR_WHO_APPROVED)).toContain('another HR person');
  });

  it('closes the review outside an Active or Completed period', () => {
    expect(on(reviewActions(card('Submitted', 'Completed'), REVIEWER))).toContain('approve');
    expect(reviewActions(card('Submitted', 'Closed'), REVIEWER)).toEqual(NO_REVIEW_ACTIONS);
    expect(noActionReason(card('Submitted', 'Closed'), REVIEWER)).toContain('reviews are closed');
  });

  it('needs kpi:Approve, and a scorecard without a reviewer is for HR only', () => {
    expect(reviewActions(card('Submitted'), { ...REVIEWER, canApprove: false })).toEqual(
      NO_REVIEW_ACTIONS,
    );
    const noReviewer = { ...card('Submitted'), reviewerEmployeeId: null };
    expect(reviewActions(noReviewer, REVIEWER)).toEqual(NO_REVIEW_ACTIONS);
    expect(reviewActions(noReviewer, HR).approve).toBe(true);
  });

  it('names the stage and finds the approver and the latest return or reject', () => {
    expect(stageLabel('Under Review')).toBe('Manager review');
    expect(stageLabel('Approved')).toBe('HR final review');
    expect(stageLabel('Finalized')).toBe('—');
    const returned: ReviewAction = {
      ...approvedBy('Daniel Carter'),
      action: 'Return',
      toStatus: 'Returned',
      comment: 'First reason',
    };
    const history = [
      approvedBy('A'),
      returned,
      approvedBy('B'),
      { ...returned, comment: 'Second' },
    ];
    expect(managerApproverName(history)).toBe('B');
    expect(latestDecision(history, 'Return')?.comment).toBe('Second');
    expect(latestDecision(history, 'Reject')).toBeNull();
  });
});

describe('scorecard rules: result inputs', () => {
  it('adapts the actual input to the measurement type', () => {
    const item = (measurementType: string, extra = {}) =>
      ({ measurementType, maxRating: null, direction: 'Higher Is Better', ...extra }) as never;
    expect(actualInputFor(item('Boolean'))).toEqual({ kind: 'boolean' });
    expect(actualInputFor(item('Rating', { maxRating: 5 }))).toEqual({
      kind: 'rating',
      min: 0,
      max: 5,
    });
    expect(actualInputFor(item('Rating', { maxRating: 10 }))).toMatchObject({ max: 10 });
    for (const type of ['Number', 'Percentage', 'Currency', 'Hours']) {
      expect(actualInputFor(item(type))).toEqual({ kind: 'decimal', min: null });
    }
    expect(actualInputFor(item('Hours', { direction: 'Lower Is Better' }))).toEqual({
      kind: 'decimal',
      min: 0,
    });
  });

  it('shows a Boolean result as Yes or No and a value with its unit', () => {
    expect(actualText({ measurementType: 'Boolean', actualValue: 1, unit: null })).toBe('Yes');
    expect(actualText({ measurementType: 'Boolean', actualValue: 0, unit: null })).toBe('No');
    expect(actualText({ measurementType: 'Number', actualValue: 30, unit: 'tasks' })).toBe(
      '30 tasks',
    );
    expect(actualText({ measurementType: 'Number', actualValue: null, unit: 'tasks' })).toBe('—');
  });
});

describe('scorecard rules: server errors on their row', () => {
  it('maps items[i] to the i-th KPI in scorecard order (the submit check’s index)', () => {
    // Ids that do not follow the indexes, as after a re-ordered plan.
    const ids = [31, 7, 12, 40];
    const mapped = rowErrors(
      {
        'items[1].actualValue': 'The actual result is missing.',
        'items[3].actualValue': 'The actual result is missing.',
        'Items[2].EmployeeComment': 'Too long.',
        employeeComment: 'Overall too long.',
        'items[9].actualValue': 'Out of range index.',
      },
      ids,
    );
    expect([...mapped.items.keys()]).toEqual([7, 40, 12]);
    expect(mapped.items.get(7)).toEqual({ actualValue: 'The actual result is missing.' });
    expect(mapped.items.get(12)).toEqual({ employeeComment: 'Too long.' });
    expect(mapped.items.has(31)).toBe(false);
    expect(mapped.other).toEqual({
      employeeComment: 'Overall too long.',
      'items[9].actualValue': 'Out of range index.',
    });
    expect(rowErrors(undefined, ids)).toEqual({ items: new Map(), other: {} });
  });
});

describe('scorecard rules: evidence pre-check', () => {
  const file = (bytes: number[], name: string) => new File([new Uint8Array(bytes)], name);

  it('allows a real PDF and refuses the 6th file, an empty, a large or a wrong file', async () => {
    expect(await evidenceProblem(pdfFile('report.pdf'), 4)).toBeNull();
    expect(await evidenceProblem(pdfFile('report.pdf'), 5)).toBe(EVIDENCE_LIMIT_MESSAGE);
    expect(await evidenceProblem(file([], 'empty.pdf'), 0)).toContain('The file is empty.');
    const big = pdfFile('big.pdf');
    Object.defineProperty(big, 'size', { value: EVIDENCE_MAX_BYTES + 1 });
    expect(await evidenceProblem(big, 0)).toContain('larger than 10 MB');
    // Text pretending to be a PDF, and a real PDF with a PNG name: refused by content.
    expect(await evidenceProblem(file([0x68, 0x69], 'notes.pdf'), 0)).toContain(
      'Allowed evidence files',
    );
    expect(await evidenceProblem(pdfFile('picture.png'), 0)).toContain('Allowed evidence files');
  });

  it('prints sizes in B, KB and MB', () => {
    expect(fileSize(900)).toBe('900 B');
    expect(fileSize(200 * 1024)).toBe('200 KB');
    expect(fileSize(1.5 * 1024 * 1024)).toBe('1.5 MB');
  });
});
