import {
  GRIEVANCE_PROPOSER_MESSAGE,
  GrievanceActorRights,
  GrievanceWorkflowState,
  grievanceActorRefusal,
  grievanceAvailableActions,
  grievanceStateOf,
  grievanceSubmitterOptions,
  grievanceViewerRights,
  grievanceTransition,
} from '../../core/utilities/grievance-workflow';

// The full transition table is covered by the shared golden file (grievance-case-domain.spec.ts);
// these are the ordering rules the file does not spell out.

const NO_RIGHTS: GrievanceActorRights = {
  owner: false,
  managementEdit: false,
  managementDelete: false,
  investigatorEdit: false,
  leadInvestigator: false,
  reviewApprove: false,
  reviewDecide: false,
  isProposer: false,
};

const state = (over: Partial<GrievanceWorkflowState>): GrievanceWorkflowState => ({
  status: 'Submitted',
  returnStatus: null,
  hasActiveLead: false,
  legalHold: false,
  archived: false,
  isEscalated: false,
  ...over,
});

describe('grievance workflow rules', () => {
  it('refuses an actor without any right of the action before looking at the status', () => {
    expect(grievanceActorRefusal('acknowledge', NO_RIGHTS)).not.toBeNull();
    // A wrong status for an actor with no right is still Forbidden (403 before 409).
    const result = grievanceTransition('close', state({ status: 'Draft' }), NO_RIGHTS);
    expect(result).toMatchObject({ ok: false, code: 'Forbidden' });
  });

  it('names the proposer rule in the refusal', () => {
    const result = grievanceTransition(
      'approveResolution',
      state({ status: 'ResolutionProposed' }),
      { ...NO_RIGHTS, reviewDecide: true, isProposer: true },
    );
    expect(result).toEqual({ ok: false, code: 'Forbidden', message: GRIEVANCE_PROPOSER_MESSAGE });
  });

  it('explains a status refusal with the status label', () => {
    const result = grievanceTransition('close', state({ status: 'UnderReview' }), {
      ...NO_RIGHTS,
      managementEdit: true,
    });
    expect(result).toMatchObject({ ok: false, code: 'Conflict' });
    expect(result.ok ? '' : result.message).toContain('Under Review');
  });

  it('needs management and review to reopen a closed case', () => {
    const closed = state({ status: 'Closed' });
    expect(
      grievanceTransition('reopen', closed, { ...NO_RIGHTS, managementEdit: true }),
    ).toMatchObject({ ok: false, code: 'Forbidden' });
    expect(
      grievanceTransition('reopen', closed, {
        ...NO_RIGHTS,
        managementEdit: true,
        reviewApprove: true,
      }),
    ).toEqual({ ok: true, to: 'Reopened' });
  });

  it('lets the case team write on a rejected case but nobody on a closed one', () => {
    const team = { ...NO_RIGHTS, managementEdit: true };
    expect(grievanceTransition('postTeamMessage', state({ status: 'Rejected' }), team).ok).toBe(
      true,
    );
    expect(grievanceTransition('postTeamMessage', state({ status: 'Closed' }), team).ok).toBe(
      false,
    );
  });

  it('tells the submitter screens what to offer', () => {
    expect(grievanceSubmitterOptions('Draft')).toEqual({
      canEditDraft: true,
      canMessage: false,
      isRespondMode: false,
      canUpload: true,
    });
    expect(grievanceSubmitterOptions('AwaitingEmployeeResponse')).toMatchObject({
      canMessage: true,
      isRespondMode: true,
    });
    expect(grievanceSubmitterOptions('Rejected')).toMatchObject({
      canMessage: true,
      canUpload: false,
    });
    expect(grievanceSubmitterOptions('Closed').canMessage).toBe(false);
    expect(grievanceSubmitterOptions('Resolved', true).canMessage).toBe(false);
  });

  it('lists the actions a screen may offer', () => {
    const actions = grievanceAvailableActions(state({ status: 'Submitted' }), {
      ...NO_RIGHTS,
      managementEdit: true,
    });
    expect(actions).toEqual(['acknowledge', 'reject', 'escalate', 'postTeamMessage']);
  });
});

describe('grievance viewer rights', () => {
  const facts = {
    status: 'ResolutionProposed' as const,
    returnStatus: null,
    leadInvestigatorEmployeeId: 9,
    legalHold: false,
    archivedAtUtc: null,
    isEscalated: false,
    isLeadInvestigator: false,
    proposal: { decision: 'Pending', proposedByEmployeeId: 1 },
  };
  const all = () => true;

  it('lets a reviewer decide, but never one who also investigates the case', () => {
    const reviewer = grievanceViewerRights({ ...facts, viewerRoles: ['Reviewer'] }, all, 5);
    expect(reviewer.reviewDecide).toBe(true);
    const both = grievanceViewerRights(
      { ...facts, viewerRoles: ['Investigator', 'Reviewer'] },
      all,
      5,
    );
    expect(both.reviewDecide).toBe(false);
    expect(
      grievanceAvailableActions(
        grievanceStateOf({ ...facts, viewerRoles: ['Investigator', 'Reviewer'] }),
        both,
      ),
    ).not.toContain('approveResolution');
    // Management without the reviewer tier does not decide either.
    expect(
      grievanceViewerRights({ ...facts, viewerRoles: ['Management'] }, all, 5).reviewDecide,
    ).toBe(false);
  });
});
