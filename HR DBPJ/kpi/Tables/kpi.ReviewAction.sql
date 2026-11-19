-- Approval history of a scorecard: one row per workflow action. Append-only (hrms_api has no UPDATE
-- or DELETE on it), so the review trail cannot be rewritten. A scorecard with history cannot be deleted.
CREATE TABLE [kpi].[ReviewAction]
(
    [Id]              int            IDENTITY (1, 1) NOT NULL,
    [ScorecardId]     int            NOT NULL,
    [Step]            varchar(30)    NOT NULL,
    [Action]          varchar(30)    NOT NULL,
    [FromStatus]      varchar(30)    NOT NULL,
    [ToStatus]        varchar(30)    NOT NULL,
    [ActorUserId]     int            NULL,
    -- NULL when the built-in administrator (linked to no employee) acted.
    [ActorEmployeeId] int            NULL,
    -- Display name of the actor at the time (kept even if the user is later removed).
    [ActorName]       nvarchar(130)  NOT NULL,
    [Comment]         nvarchar(1000) NULL,
    -- Overall score at the time of the action, when one was calculated.
    [OverallScore]    decimal(8, 2)  NULL,
    [OccurredAtUtc]   datetime2(3)   NOT NULL CONSTRAINT [DF_ReviewAction_OccurredAtUtc] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ReviewAction] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ReviewAction_Scorecard] FOREIGN KEY ([ScorecardId]) REFERENCES [kpi].[Scorecard] ([Id]),
    CONSTRAINT [FK_ReviewAction_Employee_ActorEmployeeId] FOREIGN KEY ([ActorEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_ReviewAction_Step] CHECK ([Step] IN ('Employee', 'Manager', 'HR')),
    CONSTRAINT [CK_ReviewAction_Action] CHECK ([Action] IN ('Finalize Plan', 'Submit', 'Resubmit', 'Start Review', 'Approve', 'Reject', 'Return', 'Finalize')),
    CONSTRAINT [CK_ReviewAction_FromStatus] CHECK ([FromStatus] IN ('Planning', 'Draft', 'Submitted', 'Under Review', 'Returned', 'Resubmitted', 'Approved', 'Rejected', 'Finalized')),
    CONSTRAINT [CK_ReviewAction_ToStatus] CHECK ([ToStatus] IN ('Planning', 'Draft', 'Submitted', 'Under Review', 'Returned', 'Resubmitted', 'Approved', 'Rejected', 'Finalized')),
    -- Rejecting or returning needs a reason (a NULL comment fails too).
    CONSTRAINT [CK_ReviewAction_Reason] CHECK ([Action] NOT IN ('Reject', 'Return') OR ([Comment] IS NOT NULL AND LEN(LTRIM([Comment])) > 0))
);
GO

CREATE NONCLUSTERED INDEX [IX_ReviewAction_ScorecardId_OccurredAtUtc] ON [kpi].[ReviewAction] ([ScorecardId], [OccurredAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_ReviewAction_ActorEmployeeId] ON [kpi].[ReviewAction] ([ActorEmployeeId]);
