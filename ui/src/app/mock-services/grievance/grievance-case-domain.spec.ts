// Imported from its repository location (not copied), like the KPI golden files.
import goldenFile from '../../../../../docs/test-data/grievance-transition-cases.json';
import { AppError } from '../../core/models/common.models';
import {
  GrievanceActorRole,
  GrievanceAssignmentRole,
  GrievancePriority,
  GrievanceStatus,
} from '../../core/models/grievance.models';
import { GrievanceCaseRecord } from '../../mock-data/grievance/grievance-records';
import {
  GrievanceDomainActor,
  GrievanceDomainCommand,
  applyGrievanceCommand,
} from './grievance-case-domain';
import { GrievanceActorRights } from '../../core/utilities/grievance-workflow';

/**
 * Runs the shared golden file `docs/test-data/grievance-transition-cases.json` (written with the
 * API's `GrievanceCaseWorkflow` tests) against the mock's domain step. The shape is described in the
 * file's `_comment`; this runner builds each starting state directly, as the comment allows.
 */

type ActorName = 'submitter' | 'management' | 'investigator' | 'reviewer';

interface GoldenCase {
  name: string;
  case: {
    status: GrievanceStatus;
    isAnonymous?: boolean;
    returnStatus?: GrievanceStatus | null;
    hasLead?: boolean;
    members?: number[];
    proposedBy?: ActorName;
    reopenedFrom?: GrievanceStatus;
    isEscalated?: boolean;
    legalHold?: boolean;
    archived?: boolean;
    subjectEmployeeId?: number | null;
    /** Assigned again after a reopening ended the lead's first assignment (same lead). */
    reassignedAfterReopen?: boolean;
  };
  action: string;
  actor: ActorName;
  input?: Record<string, unknown>;
  expected?: Record<string, unknown>;
  expectedError?: { type: AppError['code']; fields?: string[] };
}

const CASES = (goldenFile as unknown as { cases: GoldenCase[] }).cases;

const NOW = '2026-11-18T08:00:00Z';
const TODAY = '2026-11-18';
const LEAD_EMPLOYEE = 300;
const NAMED_SUBMITTER_EMPLOYEE = 100;
/** New rows of a step get ids from here, so they are told apart from the starting rows. */
const FIRST_NEW_ID = 1000;

const ACTORS: Record<ActorName, GrievanceDomainActor> = {
  submitter: { userId: 10, employeeId: 100, name: 'TST Submitter', role: 'Submitter' },
  management: { userId: 20, employeeId: 200, name: 'TST HR Manager', role: 'Management' },
  investigator: { userId: 30, employeeId: 300, name: 'TST Investigator', role: 'Investigator' },
  reviewer: { userId: 40, employeeId: 400, name: 'TST Reviewer', role: 'Reviewer' },
};
const ANONYMOUS: GrievanceDomainActor = {
  userId: null,
  employeeId: null,
  name: 'Anonymous submitter',
  role: 'Submitter' as GrievanceActorRole,
};

const THROUGH_ASSIGNED: readonly GrievanceStatus[] = ['Assigned', 'UnderInvestigation'];

function startingRecord(spec: GoldenCase['case']): GrievanceCaseRecord {
  const status = spec.status;
  const anonymous = spec.isAnonymous ?? false;
  const returnStatus =
    status === 'AwaitingEmployeeResponse' ? (spec.returnStatus ?? 'UnderReview') : null;
  const hasLead =
    spec.hasLead ??
    (THROUGH_ASSIGNED.includes(status) ||
      (returnStatus !== null && THROUGH_ASSIGNED.includes(returnStatus)));
  const submitted = status !== 'Draft';
  const assignment = (
    id: number,
    employeeId: number,
    role: GrievanceAssignmentRole,
    ended = false,
  ) => ({
    id,
    investigatorEmployeeId: employeeId,
    role,
    assignedAtUtc: '2026-11-01T08:00:00Z',
    assignedByUserId: 20,
    assignedByName: 'TST HR Manager',
    unassignedAtUtc: ended ? '2026-11-12T08:00:00Z' : null,
    unassignedByUserId: ended ? 20 : null,
    unassignedByName: ended ? 'TST HR Manager' : null,
    reason: ended ? 'Case reopened' : null,
  });
  // On a Reopened case "hasLead" is the lead before the reopening, which the reopening ended.
  const leadEnded = status === 'Reopened' || (spec.reassignedAfterReopen ?? false);
  const leadActive = hasLead && status !== 'Reopened';
  const proposer = ACTORS[spec.proposedBy ?? (hasLead ? 'investigator' : 'management')];
  const rejectedOnTheWay = status === 'Rejected' || spec.reopenedFrom === 'Rejected';
  const resolvedOnTheWay =
    status === 'Resolved' || status === 'Closed' || spec.reopenedFrom === 'Closed';
  return {
    id: 1,
    caseNo: submitted ? 'GRV-2026-00001' : null,
    categoryId: 0,
    subject: 'Repeated unfair shift assignments',
    description: 'My shifts are changed without notice every week.',
    incidentDate: null,
    location: null,
    involvedPersonText: null,
    involvedDepartmentId: null,
    subjectEmployeeId: spec.subjectEmployeeId ?? null,
    isAnonymous: anonymous,
    status,
    priority: submitted && status !== 'Submitted' ? 'Medium' : null,
    targetResolutionDate: submitted && status !== 'Submitted' ? '2026-12-02' : null,
    returnStatus,
    isEscalated: spec.isEscalated ?? false,
    companyId: submitted && !anonymous ? 1 : null,
    branchId: submitted && !anonymous ? 2 : null,
    departmentId: submitted && !anonymous ? 3 : null,
    positionId: submitted && !anonymous ? 4 : null,
    createdAtUtc: '2026-11-01T08:00:00Z',
    declarationAcceptedAtUtc: submitted ? '2026-11-01T08:00:00Z' : null,
    submittedAtUtc: submitted ? '2026-11-01T08:00:00Z' : null,
    acknowledgedAtUtc: null,
    resolvedAtUtc: resolvedOnTheWay ? '2026-11-10T08:00:00Z' : null,
    closedAtUtc: status === 'Closed' ? '2026-11-11T08:00:00Z' : null,
    resolutionSummary: resolvedOnTheWay ? 'TST resolution on the way.' : null,
    resolutionVisibleToSubmitter: false,
    rejectionReason: rejectedOnTheWay ? 'TST rejected on the way.' : null,
    archivedAtUtc: spec.archived ? '2026-11-12T08:00:00Z' : null,
    retentionUntil: null,
    legalHold: spec.legalHold ?? false,
    lastSubmitterUpdateAtUtc: null,
    rowVersion: '1',
    assignments: [
      ...(hasLead && leadEnded ? [assignment(1, LEAD_EMPLOYEE, 'Lead', true)] : []),
      ...(leadActive ? [assignment(leadEnded ? 2 : 1, LEAD_EMPLOYEE, 'Lead')] : []),
      ...(spec.members ?? []).map((employeeId, i) => assignment(3 + i, employeeId, 'Member')),
    ],
    notes: [],
    communications:
      status === 'AwaitingEmployeeResponse'
        ? [
            {
              id: 1,
              direction: 'ToSubmitter',
              kind: 'InfoRequest',
              body: 'TST question on the way?',
              authorRole: 'Management',
              authorUserId: 20,
              authorEmployeeId: 200,
              authorName: 'TST HR Manager',
              inReplyToId: null,
              createdAtUtc: '2026-11-05T08:00:00Z',
            },
          ]
        : [],
    attachments: [],
    correctiveActions: [],
    proposals:
      status === 'ResolutionProposed'
        ? [
            {
              id: 1,
              summary: 'TST resolution on the way.',
              proposedByUserId: proposer.userId,
              proposedByEmployeeId: proposer.employeeId,
              proposedByName: proposer.name,
              proposedAtUtc: '2026-11-09T08:00:00Z',
              decision: 'Pending',
              decidedByUserId: null,
              decidedByName: null,
              decidedAtUtc: null,
              decisionComment: null,
            },
          ]
        : [],
    escalations: [],
    history: [],
  };
}

function rightsOf(name: ActorName, record: GrievanceCaseRecord): GrievanceActorRights {
  const actor = ACTORS[name];
  const active = record.assignments.filter((a) => a.unassignedAtUtc === null);
  const pending = record.proposals.find((p) => p.decision === 'Pending');
  return {
    owner: name === 'submitter',
    managementEdit: name === 'management',
    managementDelete: name === 'management',
    investigatorEdit:
      name === 'investigator' && active.some((a) => a.investigatorEmployeeId === actor.employeeId),
    leadInvestigator:
      name === 'investigator' &&
      active.some((a) => a.role === 'Lead' && a.investigatorEmployeeId === actor.employeeId),
    // The golden HR manager also holds review approval (needed to reopen a Closed case; the
    // proposer rule is what refuses their own proposal).
    reviewApprove: name === 'reviewer' || name === 'management',
    // Deciding a proposal is the reviewer tier's (the golden reviewer is never an investigator).
    reviewDecide: name === 'reviewer',
    isProposer: pending !== undefined && pending.proposedByUserId === actor.userId,
  };
}

/** The input with every `<field>Length: n` expanded into a text of n letters "x". */
function expandInput(input: Record<string, unknown> = {}): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(input)) {
    if (key.endsWith('Length') && typeof value === 'number') {
      result[key.slice(0, -'Length'.length)] = 'x'.repeat(value);
    } else {
      result[key] = value;
    }
  }
  return result;
}

function commandOf(test: GoldenCase): GrievanceDomainCommand {
  const input = expandInput(test.input);
  const str = (key: string): string | null => (input[key] as string | null | undefined) ?? null;
  switch (test.action) {
    case 'submit':
      return {
        action: 'submit',
        declarationAccepted: (input['declarationAccepted'] as boolean | undefined) ?? true,
        categoryActive: (input['categoryActive'] as boolean | undefined) ?? true,
        categoryAllowsAnonymous: (input['categoryAllowsAnonymous'] as boolean | undefined) ?? true,
        caseNo: 'GRV-2026-00001',
        placement: { companyId: 1, branchId: 2, departmentId: 3, positionId: 4 },
      };
    case 'acknowledge':
      return {
        action: 'acknowledge',
        priority: input['priority'] as GrievancePriority,
        targetResolutionDate: str('targetResolutionDate'),
        defaultTargetDays: (input['defaultTargetDays'] as number | undefined) ?? 14,
      };
    case 'assign':
      return {
        action: 'assign',
        investigatorEmployeeId: input['investigatorEmployeeId'] as number,
        role: input['role'] as GrievanceAssignmentRole,
        reason: str('reason'),
        namedSubmitterEmployeeId: test.case.isAnonymous ? null : NAMED_SUBMITTER_EMPLOYEE,
      };
    case 'unassign':
      return {
        action: 'unassign',
        investigatorEmployeeId: input['investigatorEmployeeId'] as number,
      };
    case 'reject':
    case 'rejectResolution':
    case 'reopen':
    case 'escalate':
      return { action: test.action, reason: str('reason') };
    case 'requestInfo':
    case 'respond':
    case 'postSubmitterMessage':
    case 'postTeamMessage':
      return { action: test.action, message: str('message') };
    case 'proposeResolution':
      return { action: 'proposeResolution', summary: str('summary') };
    case 'approveResolution':
      return {
        action: 'approveResolution',
        visibleToSubmitter: input['visibleToSubmitter'] as boolean,
        comment: str('comment'),
      };
    case 'startReview':
    case 'startInvestigation':
    case 'close':
    case 'archive':
      return { action: test.action };
    default:
      throw new Error(`Unknown golden action "${test.action}".`);
  }
}

function run(test: GoldenCase): { before: GrievanceCaseRecord; after: GrievanceCaseRecord } {
  const before = startingRecord(test.case);
  let next = FIRST_NEW_ID;
  const actor = test.actor === 'submitter' && before.isAnonymous ? ANONYMOUS : ACTORS[test.actor];
  const after = applyGrievanceCommand(before, commandOf(test), {
    now: NOW,
    today: TODAY,
    actor,
    rights: rightsOf(test.actor, before),
    nextId: () => next++,
  });
  return { before, after };
}

/** The fields of `expected` the file compares, read from the record after the step. */
function observed(
  expected: Record<string, unknown>,
  after: GrievanceCaseRecord,
): Record<string, unknown> {
  const newRows = <T extends { id: number }>(rows: T[]): T[] =>
    rows.filter((r) => r.id >= FIRST_NEW_ID);
  const read: Record<string, () => unknown> = {
    status: () => after.status,
    returnStatus: () => after.returnStatus,
    isEscalated: () => after.isEscalated,
    archived: () => after.archivedAtUtc !== null,
    hasPlacement: () => after.departmentId !== null,
    priority: () => after.priority,
    targetResolutionDate: () => after.targetResolutionDate,
    rejectionReason: () => after.rejectionReason,
    resolutionVisibleToSubmitter: () => after.resolutionVisibleToSubmitter,
    proposalDecision: () => after.proposals.at(-1)?.decision ?? null,
    assignment: () => {
      const added = newRows(after.assignments).at(-1);
      return added
        ? { investigatorEmployeeId: added.investigatorEmployeeId, role: added.role }
        : null;
    },
    endedAssignment: () =>
      after.assignments.find((a) => a.unassignedAtUtc === NOW)?.investigatorEmployeeId ?? null,
    endedAssignments: () =>
      after.assignments
        .filter((a) => a.unassignedAtUtc === NOW)
        .map((a) => a.investigatorEmployeeId),
    message: () => {
      const added = newRows(after.communications).at(-1);
      return added ? { direction: added.direction, kind: added.kind } : null;
    },
    history: () => {
      const wanted = (expected['history'] as Record<string, unknown>[]) ?? [];
      return newRows(after.history).map((h, i) => {
        const keys = Object.keys(wanted[i] ?? {});
        return Object.fromEntries(
          keys.map((k) => [k, (h as unknown as Record<string, unknown>)[k]]),
        );
      });
    },
  };
  return Object.fromEntries(
    Object.keys(expected).map((key) => {
      if (!(key in read)) {
        throw new Error(`The runner does not know the expected field "${key}".`);
      }
      return [key, read[key]()];
    }),
  );
}

describe('grievance golden transition cases (docs/test-data/grievance-transition-cases.json)', () => {
  it('has unique case names', () => {
    const names = CASES.map((c) => c.name);
    expect(names.length).toBeGreaterThan(0);
    expect(new Set(names).size).toBe(names.length);
  });

  it.each(CASES.map((c) => [c.name, c] as const))('%s', (_, test) => {
    if (test.expectedError) {
      let error: AppError | undefined;
      try {
        run(test);
      } catch (thrown) {
        error = thrown as AppError;
      }
      expect(error, 'expected the step to fail').toBeDefined();
      expect(error!.code).toBe(test.expectedError.type);
      if (test.expectedError.fields) {
        expect(Object.keys(error!.fieldErrors ?? {}).sort()).toEqual(
          [...test.expectedError.fields].sort(),
        );
      }
      return;
    }
    const { before, after } = run(test);
    expect(observed(test.expected!, after)).toEqual(test.expected);
    // The input record is never changed.
    expect(before.status).toBe(test.case.status);
  });
});
