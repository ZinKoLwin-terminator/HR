import { fullName } from '../../core/models/employee.models';
import {
  GRIEVANCE_ANONYMOUS_ACTOR_NAME,
  GRIEVANCE_SUBMITTER_ACTOR_NAME,
  GrievanceActorRole,
  GrievanceHistoryAction,
  GrievancePriority,
  GrievanceStatus,
} from '../../core/models/grievance.models';
import { addDays, addMonths, todayIso } from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import {
  GrievanceAssignmentRecord,
  GrievanceAttachmentRecord,
  GrievanceCaseRecord,
  GrievanceCommunicationRecord,
  GrievanceCorrectiveActionRecord,
  GrievanceEscalationRecord,
  GrievanceNoteRecord,
  GrievanceProposalRecord,
  GrievanceSubmitterLink,
  grievanceCaseNo,
  grievanceDemoPseudonym,
  grievanceStepVisibleToSubmitter,
} from './grievance-records';

/**
 * SAMPLE DATA - fictional grievance cases for the seeded employees, across most statuses: drafts,
 * named and anonymous cases, three overdue ones (#8, #9, #16), an escalated Critical case (#9), a
 * pending resolution proposal (#11) and an archived Closed case (#14). Every name, text and file is
 * invented. Dates are relative to today, so "overdue" stays true over time.
 *
 * Who acts in the samples (the seeded users): Su Myat Noe (EMP002, user 1, HR Administrator:
 * management and review) and Ye Yint Naing (EMP009, user 6, HR Officer: lead investigator). Daniel
 * Carter (EMP005, user 11, Employee) owns cases #1, #2, #4 (anonymous) and #10. Case #5 names Ye Yint
 * Naing as its subject employee, so he never sees it.
 */

interface Actor {
  userId: number | null;
  employeeId: number | null;
  name: string;
  role: GrievanceActorRole;
}

const nameOf = (employeeId: number): string => {
  const employee = MOCK_EMPLOYEES.find((e) => e.id === employeeId);
  return employee ? fullName(employee) : `Employee #${employeeId}`;
};

/** HR Administrator: management and reviewer. */
const HR_MANAGEMENT = (): Actor => ({
  userId: 1,
  employeeId: 2,
  name: nameOf(2),
  role: 'Management',
});
const HR_REVIEWER = (): Actor => ({ ...HR_MANAGEMENT(), role: 'Reviewer' });
/** HR Officer: the investigator of the samples. */
const INVESTIGATOR_ID = 9;
const INVESTIGATOR = (): Actor => ({
  userId: 6,
  employeeId: INVESTIGATOR_ID,
  name: nameOf(INVESTIGATOR_ID),
  role: 'Investigator',
});

/** The API's text of the escalation written when a Critical case is acknowledged. */
const CRITICAL_ACK_REASON = 'Critical priority: escalated when acknowledged.';
/** The reason of the Unassign steps a reopening writes (the API's text). */
const REOPEN_UNASSIGN_REASON = 'Case reopened';

/** Closed or rejected cases are kept 7 years from that date. */
const retentionFrom = (daysAgo: number): string => addMonths(addDays(todayIso(), -daysAgo), 84);

/** ISO date-time of a day relative to today (UTC; the demo ignores the time zone here). */
const at = (daysAgo: number, hour = 9): string =>
  `${addDays(todayIso(), -daysAgo)}T${String(hour).padStart(2, '0')}:15:00.000Z`;

type Step = [action: GrievanceHistoryAction, daysAgo: number, reason?: string];

interface CaseSeed {
  id: number;
  submitter: number;
  anonymous?: boolean;
  categoryId: number;
  subject: string;
  description: string;
  incidentDaysAgo?: number;
  location?: string;
  involvedPersonText?: string;
  involvedDepartmentId?: number;
  subjectEmployeeId?: number;
  createdDaysAgo: number;
  priority?: GrievancePriority;
  /** Target resolution date relative to today (negative: in the past). */
  targetInDays?: number;
  steps: Step[];
  /** Immediate escalation at acknowledgement (Critical). */
  criticalEscalation?: boolean;
  resolutionVisible?: boolean;
  archivedDaysAgo?: number;
  legalHold?: boolean;
  /** Days ago the submitter last marked the case seen; unset: seen right after the last update. */
  seenDaysAgo?: number;
  notes?: Omit<GrievanceNoteRecord, 'id' | 'authorUserId' | 'authorEmployeeId' | 'authorName'>[];
  messages?: {
    direction: 'ToSubmitter' | 'FromSubmitter';
    kind: 'Message' | 'InfoRequest' | 'InfoResponse';
    body: string;
    daysAgo: number;
  }[];
  attachments?: {
    fileName: string;
    visibility: 'Shared' | 'CaseTeam' | 'Restricted';
    fromSubmitter?: boolean;
    sizeBytes: number;
    daysAgo: number;
  }[];
  correctiveActions?: Omit<
    GrievanceCorrectiveActionRecord,
    'id' | 'createdByUserId' | 'rowVersion'
  >[];
}

const SEEDS: CaseSeed[] = [
  {
    id: 1,
    submitter: 5,
    categoryId: 9,
    subject: 'Air conditioning in the engineering room',
    description:
      'Sample draft: the air conditioning on the third floor has been off most afternoons for two weeks.',
    location: 'Head office, third floor',
    createdDaysAgo: 2,
    steps: [],
  },
  {
    id: 2,
    submitter: 5,
    anonymous: true,
    categoryId: 1,
    subject: 'Comments made during team meetings',
    description:
      'Sample draft: repeated remarks during stand-up meetings that make some colleagues uncomfortable.',
    createdDaysAgo: 1,
    steps: [],
  },
  {
    id: 3,
    submitter: 23,
    categoryId: 7,
    subject: 'On-call rota assigned unevenly',
    description:
      'Sample case: the weekend on-call rota has listed the same three people for six weeks in a row.',
    incidentDaysAgo: 4,
    createdDaysAgo: 2,
    steps: [['Submit', 1]],
  },
  {
    id: 4,
    submitter: 5,
    anonymous: true,
    categoryId: 6,
    subject: 'Overtime approvals withheld',
    description:
      'Sample case: overtime requests in one team are left unapproved until after the pay cut-off.',
    createdDaysAgo: 4,
    steps: [['Submit', 3]],
  },
  {
    id: 5,
    submitter: 17,
    categoryId: 5,
    subject: 'Disagreement over test environment access',
    description:
      'Sample case: access to the shared test environment is withdrawn without notice, which blocks QA work.',
    involvedPersonText: 'A manager in IT Infrastructure',
    involvedDepartmentId: 7,
    subjectEmployeeId: INVESTIGATOR_ID,
    createdDaysAgo: 10,
    priority: 'Medium',
    targetInDays: 23,
    steps: [
      ['Submit', 9],
      ['Acknowledge', 7],
    ],
  },
  {
    id: 6,
    submitter: 46,
    anonymous: true,
    categoryId: 2,
    subject: 'Training places offered unequally',
    description:
      'Sample case: external training places appear to be offered only to part of the team, without criteria.',
    createdDaysAgo: 16,
    priority: 'High',
    targetInDays: 6,
    steps: [
      ['Submit', 15],
      ['Acknowledge', 13],
      ['StartReview', 12],
    ],
  },
  {
    id: 7,
    submitter: 28,
    categoryId: 4,
    subject: 'Blocked fire exit near the support desk',
    description:
      'Sample case: boxes of equipment are stored in front of the fire exit beside the customer support desk.',
    location: 'Support centre, ground floor',
    incidentDaysAgo: 7,
    createdDaysAgo: 6,
    priority: 'High',
    targetInDays: 2,
    steps: [
      ['Submit', 6],
      ['Acknowledge', 5],
      ['Assign', 4],
    ],
  },
  {
    id: 8,
    submitter: 14,
    categoryId: 7,
    subject: 'Promotion review skipped',
    description:
      'Sample case: the yearly promotion review was not held for me although my manager confirmed I was eligible.',
    createdDaysAgo: 41,
    priority: 'Medium',
    targetInDays: -8,
    steps: [
      ['Submit', 40],
      ['Acknowledge', 38],
      ['Assign', 36],
      ['StartInvestigation', 35],
      ['Escalate', 7, 'The target date has passed; the review panel should follow it.'],
    ],
    notes: [
      {
        kind: 'Interview',
        body: 'Sample note: the employee described the review timeline and the messages received.',
        interviewDate: addDays(todayIso(), -30),
        intervieweeText: 'The employee',
        createdAtUtc: at(30),
      },
      {
        kind: 'Note',
        body: 'Sample note: asked the department for the list of reviews held this year.',
        interviewDate: null,
        intervieweeText: null,
        createdAtUtc: at(28),
      },
    ],
    messages: [
      {
        direction: 'ToSubmitter',
        kind: 'Message',
        body: 'Sample message: we have started looking into your grievance and will update you here.',
        daysAgo: 35,
      },
    ],
  },
  {
    id: 9,
    submitter: 36,
    anonymous: true,
    categoryId: 1,
    subject: 'Intimidating behaviour after hours',
    description:
      'Sample case: repeated intimidating messages outside working hours from a senior colleague.',
    createdDaysAgo: 20,
    priority: 'Critical',
    targetInDays: -5,
    criticalEscalation: true,
    steps: [
      ['Submit', 20],
      ['Acknowledge', 19],
      ['Assign', 18],
      ['StartInvestigation', 17],
    ],
    notes: [
      {
        kind: 'Interview',
        body: 'Sample note: interviewed a colleague named in the report as a witness.',
        interviewDate: addDays(todayIso(), -12),
        intervieweeText: 'Witness A',
        createdAtUtc: at(12),
      },
      {
        kind: 'Finding',
        body: 'Sample finding: the messages described were sent; their tone is under assessment.',
        interviewDate: null,
        intervieweeText: null,
        createdAtUtc: at(8),
      },
    ],
    messages: [
      {
        direction: 'FromSubmitter',
        kind: 'Message',
        body: 'Sample message: the messages continued this week.',
        daysAgo: 10,
      },
    ],
    attachments: [
      { fileName: 'interview-summary.pdf', visibility: 'CaseTeam', sizeBytes: 48_213, daysAgo: 12 },
      { fileName: 'witness-list.pdf', visibility: 'Restricted', sizeBytes: 12_870, daysAgo: 12 },
    ],
  },
  {
    id: 10,
    submitter: 5,
    categoryId: 3,
    subject: 'Allowance missing from two payslips',
    description:
      'Sample case: the transport allowance was not paid in two recent months although nothing changed.',
    createdDaysAgo: 26,
    priority: 'Medium',
    targetInDays: 5,
    seenDaysAgo: 10,
    steps: [
      ['Submit', 25],
      ['Acknowledge', 24],
      ['Assign', 22],
      ['StartInvestigation', 21],
      ['RequestInfo', 2, 'Please tell us which two months are concerned.'],
    ],
    messages: [
      {
        direction: 'ToSubmitter',
        kind: 'InfoRequest',
        body: 'Please tell us which two months are concerned.',
        daysAgo: 2,
      },
    ],
    attachments: [
      {
        fileName: 'allowance-note.pdf',
        visibility: 'Shared',
        fromSubmitter: true,
        sizeBytes: 22_040,
        daysAgo: 25,
      },
    ],
  },
  {
    id: 11,
    submitter: 33,
    categoryId: 9,
    subject: 'Headsets not replaced',
    description:
      'Sample case: broken support headsets have not been replaced for a month, so calls are taken on mobiles.',
    createdDaysAgo: 30,
    priority: 'Low',
    targetInDays: 1,
    steps: [
      ['Submit', 30],
      ['Acknowledge', 29],
      ['Assign', 27],
      ['StartInvestigation', 26],
      ['ProposeResolution', 3],
    ],
    notes: [
      {
        kind: 'Finding',
        body: 'Sample finding: six headsets are out of order; the purchase request was not raised.',
        interviewDate: null,
        intervieweeText: null,
        createdAtUtc: at(5),
      },
      {
        kind: 'Recommendation',
        body: 'Sample recommendation: replace the headsets and keep two spares at the desk.',
        interviewDate: null,
        intervieweeText: null,
        createdAtUtc: at(4),
      },
    ],
    correctiveActions: [
      {
        actionType: 'Equipment',
        description: 'Sample action: order eight replacement headsets.',
        responsibleText: 'Customer Support Lead',
        dueDate: addDays(todayIso(), 10),
        completedOn: null,
        status: 'Proposed',
      },
    ],
  },
  {
    id: 12,
    submitter: 37,
    categoryId: 4,
    subject: 'Loose cabling under desks',
    description: 'Sample case: loose power cables under the support desks are a tripping hazard.',
    createdDaysAgo: 50,
    priority: 'High',
    targetInDays: -36,
    resolutionVisible: true,
    steps: [
      ['Submit', 50],
      ['Acknowledge', 49],
      ['Assign', 48],
      ['StartInvestigation', 47],
      ['ProposeResolution', 40],
      ['ApproveResolution', 38],
    ],
    notes: [
      {
        kind: 'Finding',
        body: 'Sample finding: cable trays were never fitted on the new desks.',
        interviewDate: null,
        intervieweeText: null,
        createdAtUtc: at(42),
      },
    ],
    attachments: [
      {
        fileName: 'safety-inspection-report.pdf',
        visibility: 'Shared',
        sizeBytes: 96_512,
        daysAgo: 39,
      },
    ],
    correctiveActions: [
      {
        actionType: 'Facilities',
        description: 'Sample action: fit cable trays to every support desk.',
        responsibleText: 'Facilities team',
        dueDate: addDays(todayIso(), -40),
        completedOn: addDays(todayIso(), -41),
        status: 'Implemented',
      },
    ],
  },
  {
    id: 13,
    submitter: 20,
    anonymous: true,
    categoryId: 8,
    subject: 'Expense rules applied inconsistently',
    description:
      'Sample case: travel expense rules are applied strictly for some staff and loosely for others.',
    createdDaysAgo: 91,
    priority: 'Medium',
    targetInDays: -70,
    resolutionVisible: true,
    steps: [
      ['Submit', 90],
      ['Acknowledge', 88],
      ['Assign', 86],
      ['StartInvestigation', 85],
      ['ProposeResolution', 70],
      ['ApproveResolution', 68],
      ['Close', 60],
    ],
  },
  {
    id: 14,
    submitter: 21,
    categoryId: 3,
    subject: 'Commission calculated on the wrong target',
    description:
      'Sample case: last quarter commission used the old sales target instead of the revised one.',
    createdDaysAgo: 121,
    priority: 'Low',
    targetInDays: -90,
    resolutionVisible: true,
    archivedDaysAgo: 20,
    steps: [
      ['Submit', 120],
      ['Acknowledge', 118],
      ['Assign', 117],
      ['StartInvestigation', 116],
      ['ProposeResolution', 104],
      ['ApproveResolution', 100],
      ['Close', 95],
    ],
  },
  {
    id: 15,
    submitter: 29,
    categoryId: 10,
    subject: 'Request to move to the night shift',
    description:
      'Sample case: I would like to move to the night shift for family reasons and have had no answer.',
    createdDaysAgo: 46,
    steps: [
      ['Submit', 45],
      [
        'Reject',
        43,
        'A shift change request is not a grievance; please send it through your supervisor.',
      ],
    ],
  },
  {
    id: 16,
    submitter: 19,
    categoryId: 5,
    subject: 'Code review comments turned personal',
    description:
      'Sample case: code review comments from one colleague have become personal and dismissive.',
    createdDaysAgo: 71,
    priority: 'Medium',
    targetInDays: -40,
    resolutionVisible: true,
    steps: [
      ['Submit', 70],
      ['Acknowledge', 69],
      ['Assign', 67],
      ['StartInvestigation', 66],
      ['ProposeResolution', 55],
      ['ApproveResolution', 54],
      ['Reopen', 4, 'The employee reports that the behaviour has started again.'],
    ],
  },
  {
    id: 17,
    submitter: 48,
    anonymous: true,
    categoryId: 4,
    subject: 'Forklift used without a trained driver',
    description: 'Sample case: the warehouse forklift is sometimes driven by untrained staff.',
    createdDaysAgo: 76,
    priority: 'Low',
    targetInDays: -60,
    resolutionVisible: false,
    steps: [
      ['Submit', 75],
      ['Acknowledge', 74],
      ['Assign', 73],
      ['StartInvestigation', 72],
      ['ProposeResolution', 63],
      ['ApproveResolution', 61],
    ],
  },
  {
    id: 18,
    submitter: 26,
    categoryId: 7,
    subject: 'Leave requests refused at short notice',
    description:
      'Sample case: approved leave was cancelled two days before the date without explanation.',
    createdDaysAgo: 100,
    priority: 'Medium',
    targetInDays: -75,
    resolutionVisible: true,
    steps: [
      ['Submit', 100],
      ['Acknowledge', 99],
      ['Assign', 98],
      ['StartInvestigation', 97],
      ['ProposeResolution', 85],
      ['ApproveResolution', 84],
      ['Close', 80],
    ],
  },
];

/** The status an action leads to in the samples (the full rules are in the mock workflow). */
function nextStatus(
  action: GrievanceHistoryAction,
  from: GrievanceStatus,
  returnStatus: GrievanceStatus | null,
): GrievanceStatus {
  switch (action) {
    case 'Submit':
      return 'Submitted';
    case 'Acknowledge':
      return 'Acknowledged';
    case 'StartReview':
      return 'UnderReview';
    case 'Assign':
      return 'Assigned';
    case 'StartInvestigation':
      return 'UnderInvestigation';
    case 'RequestInfo':
      return 'AwaitingEmployeeResponse';
    case 'Respond':
      return returnStatus ?? from;
    case 'ProposeResolution':
      return 'ResolutionProposed';
    case 'ApproveResolution':
      return 'Resolved';
    case 'RejectResolution':
      return 'UnderInvestigation';
    case 'Reject':
      return 'Rejected';
    case 'Close':
      return 'Closed';
    case 'Reopen':
      return 'Reopened';
    default:
      return from;
  }
}

function actorOf(action: GrievanceHistoryAction, submitter: Actor): Actor {
  switch (action) {
    case 'Submit':
    case 'Respond':
      return submitter;
    case 'StartInvestigation':
    case 'RequestInfo':
    case 'ProposeResolution':
      return INVESTIGATOR();
    case 'ApproveResolution':
    case 'RejectResolution':
      return HR_REVIEWER();
    default:
      return HR_MANAGEMENT();
  }
}

interface BuiltSeed {
  record: GrievanceCaseRecord;
  link: GrievanceSubmitterLink;
}

const ids = { history: 0, assignment: 0, note: 0, message: 0, attachment: 0, action: 0, other: 0 };
const next = (key: keyof typeof ids): number => ++ids[key];

function build(seed: CaseSeed, caseNo: string | null): BuiltSeed {
  const employee = MOCK_EMPLOYEES.find((e) => e.id === seed.submitter)!;
  const anonymous = seed.anonymous ?? false;
  const submitter: Actor = anonymous
    ? { userId: null, employeeId: null, name: GRIEVANCE_ANONYMOUS_ACTOR_NAME, role: 'Submitter' }
    : // As the API stores it: never the submitter's ids, and a fixed name in history rows.
      { userId: null, employeeId: null, name: GRIEVANCE_SUBMITTER_ACTOR_NAME, role: 'Submitter' };

  const record: GrievanceCaseRecord = {
    id: seed.id,
    caseNo,
    categoryId: seed.categoryId,
    subject: seed.subject,
    description: seed.description,
    incidentDate:
      seed.incidentDaysAgo === undefined ? null : addDays(todayIso(), -seed.incidentDaysAgo),
    location: seed.location ?? null,
    involvedPersonText: seed.involvedPersonText ?? null,
    involvedDepartmentId: seed.involvedDepartmentId ?? null,
    subjectEmployeeId: seed.subjectEmployeeId ?? null,
    isAnonymous: anonymous,
    status: 'Draft',
    priority: seed.priority ?? null,
    targetResolutionDate:
      seed.targetInDays === undefined ? null : addDays(todayIso(), seed.targetInDays),
    returnStatus: null,
    isEscalated: false,
    companyId: null,
    branchId: null,
    departmentId: null,
    positionId: null,
    createdAtUtc: at(seed.createdDaysAgo, 8),
    declarationAcceptedAtUtc: null,
    submittedAtUtc: null,
    acknowledgedAtUtc: null,
    resolvedAtUtc: null,
    closedAtUtc: null,
    resolutionSummary: null,
    resolutionVisibleToSubmitter: seed.resolutionVisible ?? false,
    rejectionReason: null,
    archivedAtUtc: seed.archivedDaysAgo === undefined ? null : at(seed.archivedDaysAgo),
    retentionUntil: null,
    legalHold: seed.legalHold ?? false,
    lastSubmitterUpdateAtUtc: null,
    rowVersion: String(seed.steps.length + 1),
    assignments: [],
    notes: [],
    communications: [],
    attachments: [],
    correctiveActions: [],
    proposals: [],
    escalations: [],
    history: [],
  };

  const addHistory = (
    action: GrievanceHistoryAction,
    from: GrievanceStatus,
    to: GrievanceStatus,
    actor: Actor,
    reason: string | null,
    occurredAtUtc: string,
  ): void => {
    const visible = grievanceStepVisibleToSubmitter(action, from, to);
    record.history.push({
      id: next('history'),
      fromStatus: from,
      toStatus: to,
      action,
      actorRole: actor.role,
      actorUserId: actor.userId,
      actorEmployeeId: actor.employeeId,
      actorName: actor.name,
      reason,
      visibleToSubmitter: visible,
      occurredAtUtc,
    });
    if (visible) {
      record.lastSubmitterUpdateAtUtc = occurredAtUtc;
    }
  };

  for (const [action, daysAgo, reason] of seed.steps) {
    const from = record.status;
    const to = nextStatus(action, from, record.returnStatus);
    const actor = actorOf(action, submitter);
    const when = at(daysAgo);
    switch (action) {
      case 'Submit':
        record.submittedAtUtc = when;
        record.declarationAcceptedAtUtc = when;
        if (!anonymous) {
          record.companyId = employee.companyId;
          record.branchId = employee.branchId;
          record.departmentId = employee.departmentId;
          record.positionId = employee.positionId;
        }
        break;
      case 'Acknowledge':
        record.acknowledgedAtUtc = when;
        break;
      case 'Assign':
        record.assignments.push(assignment(INVESTIGATOR_ID, 'Lead', when));
        break;
      case 'RequestInfo':
        record.returnStatus = from;
        break;
      case 'ProposeResolution':
        record.proposals.push(proposal(seed, when));
        break;
      case 'ApproveResolution': {
        const pending = record.proposals.find((p) => p.decision === 'Pending')!;
        const reviewer = HR_REVIEWER();
        Object.assign(pending, {
          decision: 'Approved',
          decidedByUserId: reviewer.userId,
          decidedByName: reviewer.name,
          decidedAtUtc: when,
        });
        record.resolutionSummary = pending.summary;
        record.resolvedAtUtc = when;
        break;
      }
      case 'Reject':
        record.rejectionReason = reason ?? null;
        record.retentionUntil = retentionFrom(daysAgo);
        break;
      case 'Close':
        record.closedAtUtc = when;
        record.retentionUntil = retentionFrom(daysAgo);
        break;
      case 'Escalate':
        record.isEscalated = true;
        record.escalations.unshift(escalation(reason ?? '', false, when));
        break;
      default:
        break;
    }
    record.status = to;
    if (to !== 'AwaitingEmployeeResponse') {
      record.returnStatus = null;
    }
    addHistory(action, from, to, actor, reason ?? null, when);
    if (action === 'Acknowledge' && seed.criticalEscalation) {
      record.isEscalated = true;
      record.escalations.unshift(escalation(CRITICAL_ACK_REASON, true, when));
      addHistory('Escalate', to, to, HR_MANAGEMENT(), CRITICAL_ACK_REASON, when);
    }
    if (action === 'Reopen') {
      // Reopening ends every active assignment, each with a hidden Unassign step (the API's rule).
      const by = HR_MANAGEMENT();
      for (const a of record.assignments.filter((x) => x.unassignedAtUtc === null)) {
        Object.assign(a, {
          unassignedAtUtc: when,
          unassignedByUserId: by.userId,
          unassignedByName: by.name,
          reason: REOPEN_UNASSIGN_REASON,
        });
        addHistory('Unassign', to, to, by, REOPEN_UNASSIGN_REASON, when);
      }
    }
  }

  for (const note of seed.notes ?? []) {
    const author = INVESTIGATOR();
    record.notes.push({
      ...note,
      id: next('note'),
      authorUserId: author.userId,
      authorEmployeeId: author.employeeId,
      authorName: author.name,
    });
  }
  for (const message of seed.messages ?? []) {
    const fromSubmitter = message.direction === 'FromSubmitter';
    const author = fromSubmitter ? submitter : INVESTIGATOR();
    const when = at(message.daysAgo, 10);
    const communication: GrievanceCommunicationRecord = {
      id: next('message'),
      direction: message.direction,
      kind: message.kind,
      body: message.body,
      authorRole: author.role,
      authorUserId: author.userId,
      authorEmployeeId: author.employeeId,
      authorName: author.name,
      inReplyToId: null,
      createdAtUtc: when,
    };
    record.communications.push(communication);
    if (!fromSubmitter && (record.lastSubmitterUpdateAtUtc ?? '') < when) {
      record.lastSubmitterUpdateAtUtc = when;
    }
  }
  for (const file of seed.attachments ?? []) {
    const when = at(file.daysAgo, 11);
    const attachment: GrievanceAttachmentRecord = {
      id: next('attachment'),
      communicationId: null,
      noteId: null,
      visibility: file.fromSubmitter ? 'Shared' : file.visibility,
      uploadedByRole: file.fromSubmitter ? 'Submitter' : 'Investigator',
      uploadedByUserId: file.fromSubmitter ? null : INVESTIGATOR().userId,
      fileName: file.fileName,
      contentType: 'application/pdf',
      sizeBytes: file.sizeBytes,
      uploadedAtUtc: when,
      removedAtUtc: null,
    };
    record.attachments.push(attachment);
    if (
      attachment.visibility === 'Shared' &&
      !file.fromSubmitter &&
      (record.lastSubmitterUpdateAtUtc ?? '') < when
    ) {
      record.lastSubmitterUpdateAtUtc = when;
    }
  }
  for (const action of seed.correctiveActions ?? []) {
    record.correctiveActions.push({
      ...action,
      id: next('action'),
      createdByUserId: HR_MANAGEMENT().userId,
      rowVersion: '1',
    });
  }

  const link: GrievanceSubmitterLink = {
    caseId: seed.id,
    employeeId: anonymous ? null : seed.submitter,
    pseudonym: anonymous ? grievanceDemoPseudonym(seed.submitter) : null,
    seenAtUtc:
      seed.seenDaysAgo !== undefined ? at(seed.seenDaysAgo, 18) : record.lastSubmitterUpdateAtUtc,
  };
  return { record, link };
}

function assignment(
  employeeId: number,
  role: 'Lead' | 'Member',
  when: string,
): GrievanceAssignmentRecord {
  const by = HR_MANAGEMENT();
  return {
    id: next('assignment'),
    investigatorEmployeeId: employeeId,
    role,
    assignedAtUtc: when,
    assignedByUserId: by.userId,
    assignedByName: by.name,
    unassignedAtUtc: null,
    unassignedByUserId: null,
    unassignedByName: null,
    reason: null,
  };
}

function proposal(seed: CaseSeed, when: string): GrievanceProposalRecord {
  const by = INVESTIGATOR();
  return {
    id: next('other'),
    summary: `Sample resolution for "${seed.subject}": the issue was confirmed and the agreed corrective steps were taken.`,
    proposedByUserId: by.userId,
    proposedByEmployeeId: by.employeeId,
    proposedByName: by.name,
    proposedAtUtc: when,
    decision: 'Pending',
    decidedByUserId: null,
    decidedByName: null,
    decidedAtUtc: null,
    decisionComment: null,
  };
}

function escalation(reason: string, isImmediate: boolean, when: string): GrievanceEscalationRecord {
  const by = HR_MANAGEMENT();
  return {
    id: next('other'),
    reason,
    isImmediate,
    escalatedByUserId: by.userId,
    escalatedByName: by.name,
    escalatedAtUtc: when,
    clearedAtUtc: null,
    outcome: null,
  };
}

/** Case numbers follow the submission order, as the sequence does. */
function caseNumbers(): Map<number, string> {
  const submitted = SEEDS.filter((s) => s.steps.length > 0)
    .map((s) => ({ id: s.id, daysAgo: s.steps[0][1] }))
    .sort((a, b) => b.daysAgo - a.daysAgo || a.id - b.id);
  return new Map(
    submitted.map(({ id, daysAgo }, i) => [
      id,
      grievanceCaseNo(Number(addDays(todayIso(), -daysAgo).slice(0, 4)), i + 1),
    ]),
  );
}

function buildAll(): BuiltSeed[] {
  for (const key of Object.keys(ids) as (keyof typeof ids)[]) {
    ids[key] = 0;
  }
  const numbers = caseNumbers();
  return SEEDS.map((seed) => build(seed, numbers.get(seed.id) ?? null));
}

const BUILT = buildAll();

/** SAMPLE DATA - the case records (without their submitters). */
export const MOCK_GRIEVANCE_CASES: GrievanceCaseRecord[] = BUILT.map((b) => b.record);

/** SAMPLE DATA - the submitter link of every sample case (the private `grv.CaseSubmitter`). */
export const MOCK_GRIEVANCE_SUBMITTERS: GrievanceSubmitterLink[] = BUILT.map((b) => b.link);
