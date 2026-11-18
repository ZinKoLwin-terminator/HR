import {
  GRIEVANCE_OPEN_STATUSES,
  GRIEVANCE_STATUSES,
  GRIEVANCE_STATUS_LABELS,
  GrievanceHistoryAction,
  GrievanceStatus,
} from '../models/grievance.models';

/**
 * The grievance state machine of the approved plan: who may do what from which status. Screens use it
 * to decide which actions to OFFER (UX only); the API (`GrievanceCaseWorkflow`) is the authority and
 * answers 403/409 whatever is shown. The demo mock runs the same table; the step itself (history,
 * messages, input rules) is in `mock-services/grievance/grievance-case-domain.ts`, and both are
 * checked against the shared golden file `docs/test-data/grievance-transition-cases.json`.
 *
 * Order of the checks (the API's): an actor with none of the rights the action ever accepts is
 * Forbidden; an archived case, or a status the action does not start from, is a Conflict; an actor
 * without the right for this particular start status (for example a manager proposing a resolution
 * while the Lead investigates, or the proposer deciding their own proposal) is Forbidden; a guard
 * (already escalated, legal hold) is a Conflict. The API checks the `rowVersion` between the first
 * check and the rest (`grievanceActorRefusal`, then stale, then `grievanceTransition`).
 */

export type GrievanceWorkflowAction =
  | 'submit'
  | 'acknowledge'
  | 'startReview'
  | 'assignLead'
  | 'assignMember'
  | 'unassign'
  | 'startInvestigation'
  | 'requestInfo'
  | 'respond'
  | 'proposeResolution'
  | 'approveResolution'
  | 'rejectResolution'
  | 'reject'
  | 'close'
  | 'reopen'
  | 'escalate'
  | 'archive'
  | 'postSubmitterMessage'
  | 'postTeamMessage';

/** What the transition depends on, besides the actor. */
export interface GrievanceWorkflowState {
  status: GrievanceStatus;
  /** The status a response returns to (AwaitingEmployeeResponse only). */
  returnStatus: GrievanceStatus | null;
  hasActiveLead: boolean;
  legalHold: boolean;
  archived: boolean;
  /** A case is escalated once; escalations are never cleared. */
  isEscalated: boolean;
}

/**
 * The caller's rights on one case. Each is the permission AND the scope: `managementEdit` is
 * `grievance-management:Edit` on a case of the management scope, `investigatorEdit` is
 * `grievance-investigation:Edit` with an active assignment, `leadInvestigator` that assignment being
 * the Lead, `reviewApprove` is `grievance-review:Approve`, `owner` is `grievance:Create` on one's
 * own case. No case-team right exists on a case the caller submitted or is the subject of.
 */
export interface GrievanceActorRights {
  owner: boolean;
  managementEdit: boolean;
  managementDelete: boolean;
  investigatorEdit: boolean;
  leadInvestigator: boolean;
  /** `grievance-review:Approve` (with management Edit, reopening a Closed case). */
  reviewApprove: boolean;
  /**
   * May decide a proposed resolution: the reviewer tier on the case (review View and Approve) and not
   * an active investigator of it.
   */
  reviewDecide: boolean;
  /** The caller made the pending resolution proposal. */
  isProposer: boolean;
}

export const NO_GRIEVANCE_RIGHTS: Readonly<GrievanceActorRights> = {
  owner: false,
  managementEdit: false,
  managementDelete: false,
  investigatorEdit: false,
  leadInvestigator: false,
  reviewApprove: false,
  reviewDecide: false,
  isProposer: false,
};

type Right = Exclude<keyof GrievanceActorRights, 'isProposer'>;

interface Rule {
  from: readonly GrievanceStatus[];
  /** Any one of these. */
  anyOf?: readonly Right[];
  /** All of these. */
  allOf?: readonly Right[];
  /** The proposer may not decide. */
  notProposer?: boolean;
  /** Null: the status does not change. */
  to: (state: GrievanceWorkflowState) => GrievanceStatus | null;
}

const OPEN = GRIEVANCE_OPEN_STATUSES;
/** Every status but Draft and Closed: where messages may still be written. */
const TALKABLE: readonly GrievanceStatus[] = GRIEVANCE_STATUSES.filter(
  (s) => s !== 'Draft' && s !== 'Closed',
);
/** Where investigators can be added or removed: open and acknowledged. */
const STAFFABLE: readonly GrievanceStatus[] = OPEN.filter((s) => s !== 'Submitted');
const fixed = (status: GrievanceStatus) => (): GrievanceStatus => status;
const same = (): null => null;

const RULES: Readonly<Record<GrievanceWorkflowAction, readonly Rule[]>> = {
  submit: [{ from: ['Draft'], anyOf: ['owner'], to: fixed('Submitted') }],
  acknowledge: [{ from: ['Submitted'], anyOf: ['managementEdit'], to: fixed('Acknowledged') }],
  startReview: [
    { from: ['Acknowledged', 'Reopened'], anyOf: ['managementEdit'], to: fixed('UnderReview') },
  ],
  assignLead: [
    {
      from: ['Acknowledged', 'UnderReview', 'Reopened'],
      anyOf: ['managementEdit'],
      to: fixed('Assigned'),
    },
    // Replaces the active Lead (a reason is required).
    {
      from: ['Assigned', 'UnderInvestigation', 'AwaitingEmployeeResponse'],
      anyOf: ['managementEdit'],
      to: same,
    },
  ],
  assignMember: [{ from: STAFFABLE, anyOf: ['managementEdit'], to: same }],
  unassign: [{ from: STAFFABLE, anyOf: ['managementEdit'], to: same }],
  startInvestigation: [
    {
      from: ['Assigned'],
      anyOf: ['investigatorEdit', 'managementEdit'],
      to: fixed('UnderInvestigation'),
    },
  ],
  requestInfo: [
    {
      from: ['UnderReview', 'Assigned', 'UnderInvestigation'],
      anyOf: ['managementEdit', 'investigatorEdit'],
      to: fixed('AwaitingEmployeeResponse'),
    },
  ],
  respond: [
    {
      from: ['AwaitingEmployeeResponse'],
      anyOf: ['owner'],
      to: (state) => state.returnStatus ?? 'UnderReview',
    },
  ],
  proposeResolution: [
    { from: ['UnderInvestigation'], anyOf: ['leadInvestigator'], to: fixed('ResolutionProposed') },
    { from: ['UnderReview'], anyOf: ['managementEdit'], to: fixed('ResolutionProposed') },
  ],
  approveResolution: [
    {
      from: ['ResolutionProposed'],
      anyOf: ['reviewDecide'],
      notProposer: true,
      to: fixed('Resolved'),
    },
  ],
  rejectResolution: [
    {
      from: ['ResolutionProposed'],
      anyOf: ['reviewDecide'],
      notProposer: true,
      to: (state) => (state.hasActiveLead ? 'UnderInvestigation' : 'UnderReview'),
    },
  ],
  reject: [
    {
      from: ['Submitted', 'Acknowledged', 'UnderReview'],
      anyOf: ['managementEdit'],
      to: fixed('Rejected'),
    },
  ],
  close: [{ from: ['Resolved'], anyOf: ['managementEdit'], to: fixed('Closed') }],
  reopen: [
    { from: ['Resolved', 'Rejected'], anyOf: ['managementEdit'], to: fixed('Reopened') },
    { from: ['Closed'], allOf: ['managementEdit', 'reviewApprove'], to: fixed('Reopened') },
  ],
  escalate: [{ from: OPEN, anyOf: ['managementEdit', 'investigatorEdit'], to: same }],
  archive: [{ from: ['Closed', 'Rejected'], anyOf: ['managementDelete'], to: same }],
  // Messages write no history. While the case awaits the submitter, their message is the response.
  postSubmitterMessage: [
    {
      from: TALKABLE.filter((s) => s !== 'AwaitingEmployeeResponse'),
      anyOf: ['owner'],
      to: same,
    },
  ],
  postTeamMessage: [{ from: TALKABLE, anyOf: ['managementEdit', 'investigatorEdit'], to: same }],
};

export const GRIEVANCE_WORKFLOW_ACTIONS = Object.keys(RULES) as GrievanceWorkflowAction[];

const VERBS: Readonly<Record<GrievanceWorkflowAction, string>> = {
  submit: 'submitted',
  acknowledge: 'acknowledged',
  startReview: 'put under review',
  assignLead: 'given a lead investigator',
  assignMember: 'given an investigator',
  unassign: 'have an investigator removed',
  startInvestigation: 'put under investigation',
  requestInfo: 'sent an information request',
  respond: 'answered',
  proposeResolution: 'given a proposed resolution',
  approveResolution: 'resolved',
  rejectResolution: 'sent back',
  reject: 'rejected',
  close: 'closed',
  reopen: 'reopened',
  escalate: 'escalated',
  archive: 'archived',
  postSubmitterMessage: 'sent a message',
  postTeamMessage: 'sent a message',
};

export type GrievanceTransitionResult =
  | { ok: true; to: GrievanceStatus }
  | { ok: false; code: 'Forbidden' | 'Conflict'; message: string };

export const GRIEVANCE_FORBIDDEN_MESSAGE = 'You are not allowed to do this on this case.';
export const GRIEVANCE_PROPOSER_MESSAGE =
  'You proposed this resolution; another reviewer has to decide on it.';

const has = (rights: GrievanceActorRights, rule: Rule): boolean =>
  (rule.anyOf === undefined || rule.anyOf.some((r) => rights[r])) &&
  (rule.allOf === undefined || rule.allOf.every((r) => rights[r]));

/** The first check: Forbidden when the actor holds none of the rights the action ever accepts. */
export function grievanceActorRefusal(
  action: GrievanceWorkflowAction,
  rights: GrievanceActorRights,
): string | null {
  const any = RULES[action].some((rule) =>
    [...(rule.anyOf ?? []), ...(rule.allOf ?? [])].some((r) => rights[r]),
  );
  return any ? null : GRIEVANCE_FORBIDDEN_MESSAGE;
}

/** Every check of the action, in the API's order. */
export function grievanceTransition(
  action: GrievanceWorkflowAction,
  state: GrievanceWorkflowState,
  rights: GrievanceActorRights,
): GrievanceTransitionResult {
  const refusal = grievanceActorRefusal(action, rights);
  if (refusal !== null) {
    return { ok: false, code: 'Forbidden', message: refusal };
  }
  if (state.archived) {
    return { ok: false, code: 'Conflict', message: 'The case is archived and cannot change.' };
  }
  const rule = RULES[action].find((r) => r.from.includes(state.status));
  if (!rule) {
    return {
      ok: false,
      code: 'Conflict',
      message: `The case is ${GRIEVANCE_STATUS_LABELS[state.status]}; it cannot be ${VERBS[action]} now.`,
    };
  }
  if (!has(rights, rule)) {
    return { ok: false, code: 'Forbidden', message: GRIEVANCE_FORBIDDEN_MESSAGE };
  }
  if (rule.notProposer && rights.isProposer) {
    return { ok: false, code: 'Forbidden', message: GRIEVANCE_PROPOSER_MESSAGE };
  }
  if (action === 'escalate' && state.isEscalated) {
    return { ok: false, code: 'Conflict', message: 'The case is already escalated.' };
  }
  if (action === 'archive' && state.legalHold) {
    return {
      ok: false,
      code: 'Conflict',
      message: 'The case is under legal hold and cannot be archived.',
    };
  }
  return { ok: true, to: rule.to(state) ?? state.status };
}

/** The actions the table allows now (inputs aside): what a screen may offer. */
export function grievanceAvailableActions(
  state: GrievanceWorkflowState,
  rights: GrievanceActorRights,
): GrievanceWorkflowAction[] {
  return GRIEVANCE_WORKFLOW_ACTIONS.filter((a) => grievanceTransition(a, state, rights).ok);
}

export function isGrievanceOpen(status: GrievanceStatus): boolean {
  return OPEN.includes(status);
}

/**
 * Whether a step is on the submitter's timeline: every status change except the internal resolution
 * review loop (proposal and its rejection). Assign, Unassign, Escalate and Archive keep the status
 * and stay internal.
 */
export function grievanceStepVisibleToSubmitter(
  action: GrievanceHistoryAction,
  from: GrievanceStatus | null,
  to: GrievanceStatus,
): boolean {
  return from !== to && action !== 'ProposeResolution' && action !== 'RejectResolution';
}

/** What the submitter's screens may offer on their own case (UX only; the API decides). */
export interface GrievanceSubmitterOptions {
  /** Draft: edit, delete and submit. */
  canEditDraft: boolean;
  /** A message box is shown (Draft, Closed and archived cases have none). */
  canMessage: boolean;
  /** The message is the answer to HR's information request. */
  isRespondMode: boolean;
  /** Files can be added (a draft or an open case, not archived). */
  canUpload: boolean;
}

export function grievanceSubmitterOptions(
  status: GrievanceStatus,
  archived = false,
): GrievanceSubmitterOptions {
  const state: GrievanceWorkflowState = {
    status,
    returnStatus: null,
    hasActiveLead: false,
    legalHold: false,
    archived,
    isEscalated: false,
  };
  const owner = { ...NO_GRIEVANCE_RIGHTS, owner: true };
  const isRespondMode = grievanceTransition('respond', state, owner).ok;
  return {
    canEditDraft: !archived && status === 'Draft',
    canMessage: isRespondMode || grievanceTransition('postSubmitterMessage', state, owner).ok,
    isRespondMode,
    canUpload: !archived && (status === 'Draft' || isGrievanceOpen(status)),
  };
}

/** The parts of a case-team detail the rights depend on (`GrievanceCaseDetail` fits). */
export interface GrievanceViewerFacts {
  status: GrievanceStatus;
  returnStatus: GrievanceStatus | null;
  leadInvestigatorEmployeeId: number | null;
  legalHold: boolean;
  archivedAtUtc: string | null;
  isEscalated: boolean;
  viewerRoles: readonly ('Management' | 'Investigator' | 'Reviewer')[];
  isLeadInvestigator: boolean;
  proposal: { decision: string; proposedByEmployeeId: number | null } | null;
}

/** The workflow state of a case-team detail. */
export function grievanceStateOf(facts: GrievanceViewerFacts): GrievanceWorkflowState {
  return {
    status: facts.status,
    returnStatus: facts.returnStatus,
    hasActiveLead: facts.leadInvestigatorEmployeeId !== null,
    legalHold: facts.legalHold,
    archived: facts.archivedAtUtc !== null,
    isEscalated: facts.isEscalated,
  };
}

/**
 * The signed-in case-team member's rights on a case, from the tiers the server reports
 * (`viewerRoles`, `isLeadInvestigator`) and the grants (`can('module:Action')`). UX only: the
 * server checks again. Review approval counts only on a case the caller sees as reviewer or
 * manager; the proposer is recognised by employee id.
 */
export function grievanceViewerRights(
  facts: GrievanceViewerFacts,
  can: (key: string) => boolean,
  myEmployeeId: number | null,
): GrievanceActorRights {
  const management = facts.viewerRoles.includes('Management');
  const investigatorEdit =
    facts.viewerRoles.includes('Investigator') && can('grievance-investigation:Edit');
  const pending = facts.proposal?.decision === 'Pending' ? facts.proposal : null;
  return {
    owner: false,
    managementEdit: management && can('grievance-management:Edit'),
    managementDelete: management && can('grievance-management:Delete'),
    investigatorEdit,
    leadInvestigator: investigatorEdit && facts.isLeadInvestigator,
    reviewApprove:
      (facts.viewerRoles.includes('Reviewer') || management) && can('grievance-review:Approve'),
    reviewDecide:
      facts.viewerRoles.includes('Reviewer') &&
      !facts.viewerRoles.includes('Investigator') &&
      can('grievance-review:Approve'),
    isProposer:
      pending !== null && myEmployeeId !== null && pending.proposedByEmployeeId === myEmployeeId,
  };
}

/**
 * Whether the proposer rule is what stops the caller deciding a proposal: shown as an explanation
 * instead of hiding the buttons silently.
 */
export function grievanceProposerBlocked(
  state: GrievanceWorkflowState,
  rights: GrievanceActorRights,
): boolean {
  return (
    state.status === 'ResolutionProposed' &&
    rights.reviewDecide &&
    rights.isProposer &&
    !state.archived
  );
}
