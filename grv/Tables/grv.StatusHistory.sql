-- Every transition of a case: one row per workflow action. Append-only (hrms_api has no UPDATE or DELETE on it).
-- A submitter's action keeps no ids: the actor is N'Submitter' on a named case (the API shows the submitter's name
-- from grv.CaseSubmitter) and N'Anonymous submitter' on an anonymous one.
-- Escalate, Unassign and Archive do not change the status (FromStatus = ToStatus); Submit is always Draft -> Submitted.
-- Assign may or may not change it (a first Lead moves the case to Assigned).
CREATE TABLE [grv].[StatusHistory]
(
    [Id]                 int            IDENTITY (1, 1) NOT NULL,
    [CaseId]             int            NOT NULL,
    [FromStatus]         varchar(30)    NOT NULL,
    [ToStatus]           varchar(30)    NOT NULL,
    [Action]             varchar(30)    NOT NULL,
    -- NULL for the submitter.
    [ActorUserId]        int            NULL,
    -- NULL for the built-in administrator (linked to no employee) and for the submitter.
    [ActorEmployeeId]    int            NULL,
    -- Display name at the time (kept even if the user is later removed); see above for the submitter.
    [ActorName]          nvarchar(130)  NOT NULL,
    [ActorRole]          varchar(20)    NOT NULL,
    [Reason]             nvarchar(1000) NULL,
    -- Shown on the submitter's timeline.
    [VisibleToSubmitter] bit            NOT NULL CONSTRAINT [DF_StatusHistory_VisibleToSubmitter] DEFAULT (0),
    [OccurredAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_StatusHistory_OccurredAtUtc] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_StatusHistory] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StatusHistory_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [FK_StatusHistory_Employee_ActorEmployeeId] FOREIGN KEY ([ActorEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_StatusHistory_FromStatus] CHECK ([FromStatus] IN ('Draft', 'Submitted', 'Acknowledged', 'UnderReview', 'Assigned', 'UnderInvestigation',
                                                                     'AwaitingEmployeeResponse', 'ResolutionProposed', 'Resolved', 'Closed', 'Rejected', 'Reopened')),
    -- Nothing moves a case back to Draft.
    CONSTRAINT [CK_StatusHistory_ToStatus] CHECK ([ToStatus] IN ('Submitted', 'Acknowledged', 'UnderReview', 'Assigned', 'UnderInvestigation',
                                                                 'AwaitingEmployeeResponse', 'ResolutionProposed', 'Resolved', 'Closed', 'Rejected', 'Reopened')),
    CONSTRAINT [CK_StatusHistory_Action] CHECK ([Action] IN ('Submit', 'Acknowledge', 'Reject', 'StartReview', 'Assign', 'Unassign', 'StartInvestigation',
                                                             'RequestInfo', 'Respond', 'ProposeResolution', 'ApproveResolution', 'RejectResolution',
                                                             'Close', 'Reopen', 'Escalate', 'Archive')),
    CONSTRAINT [CK_StatusHistory_ActorRole] CHECK ([ActorRole] IN ('Submitter', 'Management', 'Investigator', 'Reviewer', 'System')),
    -- These actions need a reason (a NULL reason fails too).
    CONSTRAINT [CK_StatusHistory_Reason] CHECK ([Action] NOT IN ('Reject', 'Reopen', 'Escalate', 'RejectResolution', 'RequestInfo')
                                             OR ([Reason] IS NOT NULL AND LEN(LTRIM([Reason])) > 0)),
    CONSTRAINT [CK_StatusHistory_StatusKept] CHECK ([Action] NOT IN ('Escalate', 'Unassign', 'Archive') OR [FromStatus] = [ToStatus]),
    CONSTRAINT [CK_StatusHistory_Submit] CHECK ([Action] <> 'Submit' OR ([FromStatus] = 'Draft' AND [ToStatus] = 'Submitted')),
    -- The submitter is never identified here, named or anonymous, and only the submitter carries those two names.
    CONSTRAINT [CK_StatusHistory_SubmitterActor] CHECK (([ActorRole] = 'Submitter' AND [ActorUserId] IS NULL AND [ActorEmployeeId] IS NULL
                                                         AND [ActorName] IN (N'Submitter', N'Anonymous submitter'))
                                                     OR ([ActorRole] <> 'Submitter' AND [ActorName] NOT IN (N'Submitter', N'Anonymous submitter')))
);
GO

CREATE NONCLUSTERED INDEX [IX_StatusHistory_CaseId_OccurredAtUtc] ON [grv].[StatusHistory] ([CaseId], [OccurredAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_StatusHistory_ActorEmployeeId] ON [grv].[StatusHistory] ([ActorEmployeeId]);
