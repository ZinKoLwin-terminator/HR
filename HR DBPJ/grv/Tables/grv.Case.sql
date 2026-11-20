-- One employee grievance. Draft (the submitter is still writing it; no case number) -> Submitted -> Acknowledged
-- -> UnderReview / Assigned -> UnderInvestigation -> ResolutionProposed -> Resolved -> Closed. Rejected ends a case
-- early, AwaitingEmployeeResponse waits for the submitter and goes back to ReturnStatus, Reopened brings a Resolved,
-- Rejected or Closed case back. Every transition is a row of grv.StatusHistory.
-- Confidential: the submitter is NOT stored here (grv.CaseSubmitter holds the employee, or only a keyed pseudonym
-- for an anonymous case), and the row has no CreatedByUserId/ModifiedByUserId. An anonymous case keeps no organization
-- placement. Deliberately NOT system-versioned: draft revisions and deleted drafts must leave no trace. The audit trail
-- is grv.StatusHistory (every transition) and grv.AuditLog (views, files, and triage changes as field codes and ids).
-- Triggers: TR_Case_ClosedIsFinal (Closed and archived rows) and TR_Case_SubmittedIsFixed (what submission fixes).
CREATE TABLE [grv].[Case]
(
    [Id]                           int             IDENTITY (1, 1) NOT NULL,
    -- GRV-yyyy-nnnnn from grv.CaseNoSeq, given when the draft is submitted (NULL while Draft).
    [CaseNo]                       varchar(20)     NULL,
    [CategoryId]                   int             NOT NULL,
    [Subject]                      nvarchar(200)   NOT NULL,
    [Description]                  nvarchar(4000)  NOT NULL,
    [IncidentDate]                 date            NULL,
    [Location]                     nvarchar(200)   NULL,
    -- Who or what the grievance is about, in the submitter's words (free text, never matched to an employee).
    [InvolvedPersonText]           nvarchar(200)   NULL,
    [InvolvedDepartmentId]         int             NULL,
    -- The employee the grievance is about, set by HR at triage. That employee is excluded from every case-team view.
    [SubjectEmployeeId]            int             NULL,
    [IsAnonymous]                  bit             NOT NULL CONSTRAINT [DF_Case_IsAnonymous] DEFAULT (0),
    [Status]                       varchar(30)     NOT NULL CONSTRAINT [DF_Case_Status] DEFAULT ('Draft'),
    [Priority]                     varchar(10)     NULL,
    [TargetResolutionDate]         date            NULL,
    -- While AwaitingEmployeeResponse: the status the case returns to when the submitter responds.
    [ReturnStatus]                 varchar(30)     NULL,
    -- Set once the case is escalated to the reviewers; kept for reporting.
    [IsEscalated]                  bit             NOT NULL CONSTRAINT [DF_Case_IsEscalated] DEFAULT (0),
    -- A named submitter's placement when the case was submitted (NULL for drafts and anonymous cases).
    [CompanyId]                    int             NULL,
    [BranchId]                     int             NULL,
    [DepartmentId]                 int             NULL,
    [PositionId]                   int             NULL,
    [DeclarationAcceptedAtUtc]     datetime2(3)    NULL,
    [SubmittedAtUtc]               datetime2(3)    NULL,
    [AcknowledgedAtUtc]            datetime2(3)    NULL,
    [ResolvedAtUtc]                datetime2(3)    NULL,
    [ClosedAtUtc]                  datetime2(3)    NULL,
    [ResolutionSummary]            nvarchar(4000)  NULL,
    [ResolutionVisibleToSubmitter] bit             NOT NULL CONSTRAINT [DF_Case_ResolutionVisibleToSubmitter] DEFAULT (0),
    [RejectionReason]              nvarchar(1000)  NULL,
    -- Archive (soft delete) of a Closed or Rejected case; the row stays.
    [ArchivedAtUtc]                datetime2(3)    NULL,
    [RetentionUntil]               date            NULL,
    [LegalHold]                    bit             NOT NULL CONSTRAINT [DF_Case_LegalHold] DEFAULT (0),
    -- The submitter's unread badge: the last case-team change the submitter may see (compared with
    -- grv.CaseSubmitter.SubmitterSeenAtUtc, which lives there so that reading never changes this row's version).
    [LastSubmitterUpdateAtUtc]     datetime2(3)    NULL,
    [CreatedAtUtc]                 datetime2(3)    NOT NULL CONSTRAINT [DF_Case_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedAtUtc]                datetime2(3)    NOT NULL CONSTRAINT [DF_Case_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [RowVersion]                   rowversion      NOT NULL,
    CONSTRAINT [PK_Case] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Case_Category] FOREIGN KEY ([CategoryId]) REFERENCES [grv].[Category] ([Id]),
    CONSTRAINT [FK_Case_Department_InvolvedDepartmentId] FOREIGN KEY ([InvolvedDepartmentId]) REFERENCES [org].[Department] ([Id]),
    CONSTRAINT [FK_Case_Employee_SubjectEmployeeId] FOREIGN KEY ([SubjectEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    -- Composite FKs keep the copied placement consistent: company > branch > department > position (FK_Case_Branch
    -- also checks the company, so there is no separate company key).
    CONSTRAINT [FK_Case_Branch] FOREIGN KEY ([BranchId], [CompanyId]) REFERENCES [org].[Branch] ([Id], [CompanyId]),
    CONSTRAINT [FK_Case_Department] FOREIGN KEY ([DepartmentId], [BranchId]) REFERENCES [org].[Department] ([Id], [BranchId]),
    CONSTRAINT [FK_Case_Position] FOREIGN KEY ([PositionId], [DepartmentId]) REFERENCES [org].[Position] ([Id], [DepartmentId]),
    CONSTRAINT [CK_Case_Status] CHECK ([Status] IN ('Draft', 'Submitted', 'Acknowledged', 'UnderReview', 'Assigned', 'UnderInvestigation',
                                                    'AwaitingEmployeeResponse', 'ResolutionProposed', 'Resolved', 'Closed', 'Rejected', 'Reopened')),
    CONSTRAINT [CK_Case_Priority] CHECK ([Priority] IS NULL OR [Priority] IN ('Low', 'Medium', 'High', 'Critical')),
    CONSTRAINT [CK_Case_CaseNo] CHECK ([CaseNo] LIKE 'GRV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9]%' COLLATE Latin1_General_BIN2 AND [CaseNo] NOT LIKE 'GRV-____-%[^0-9]%' COLLATE Latin1_General_BIN2),
    -- A draft has no number and no submission; every other case has both and the accepted declaration.
    CONSTRAINT [CK_Case_Submitted] CHECK (([Status] = 'Draft' AND [CaseNo] IS NULL AND [SubmittedAtUtc] IS NULL)
                                       OR ([Status] <> 'Draft' AND [CaseNo] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL AND [DeclarationAcceptedAtUtc] IS NOT NULL)),
    CONSTRAINT [CK_Case_ReturnStatus] CHECK (([Status] = 'AwaitingEmployeeResponse' AND [ReturnStatus] IN ('UnderReview', 'Assigned', 'UnderInvestigation'))
                                          OR ([Status] <> 'AwaitingEmployeeResponse' AND [ReturnStatus] IS NULL)),
    CONSTRAINT [CK_Case_RejectionReason] CHECK ([Status] <> 'Rejected' OR ([RejectionReason] IS NOT NULL AND LEN(LTRIM([RejectionReason])) > 0)),
    CONSTRAINT [CK_Case_ResolutionSummary] CHECK ([Status] NOT IN ('Resolved', 'Closed') OR ([ResolutionSummary] IS NOT NULL AND LEN(LTRIM([ResolutionSummary])) > 0)),
    CONSTRAINT [CK_Case_Resolved] CHECK ([Status] NOT IN ('Resolved', 'Closed') OR [ResolvedAtUtc] IS NOT NULL),
    CONSTRAINT [CK_Case_Closed] CHECK ([Status] <> 'Closed' OR [ClosedAtUtc] IS NOT NULL),
    -- Only a Closed or Rejected case is archived.
    CONSTRAINT [CK_Case_Archived] CHECK ([ArchivedAtUtc] IS NULL OR [Status] IN ('Closed', 'Rejected')),
    -- The placement is copied whole or not at all (a composite FK with a NULL column is not checked).
    CONSTRAINT [CK_Case_Placement] CHECK (([CompanyId] IS NULL AND [BranchId] IS NULL AND [DepartmentId] IS NULL AND [PositionId] IS NULL)
                                       OR ([CompanyId] IS NOT NULL AND [BranchId] IS NOT NULL AND [DepartmentId] IS NOT NULL AND [PositionId] IS NOT NULL)),
    -- An anonymous case keeps no organization placement of its submitter.
    CONSTRAINT [CK_Case_AnonymousPlacement] CHECK ([IsAnonymous] = 0 OR [CompanyId] IS NULL),
    -- The placement is copied only when a named case is submitted.
    CONSTRAINT [CK_Case_DraftPlacement] CHECK ([Status] <> 'Draft' OR [CompanyId] IS NULL),
    -- Nobody triages or escalates a draft.
    CONSTRAINT [CK_Case_DraftUntriaged] CHECK ([Status] <> 'Draft' OR ([Priority] IS NULL AND [IsEscalated] = 0)),
    -- An acknowledged case has its acknowledgement time; once acknowledged, a case keeps a priority and a target date.
    CONSTRAINT [CK_Case_Acknowledged] CHECK ([Status] <> 'Acknowledged' OR [AcknowledgedAtUtc] IS NOT NULL),
    CONSTRAINT [CK_Case_AcknowledgedTriage] CHECK ([AcknowledgedAtUtc] IS NULL OR ([Priority] IS NOT NULL AND [TargetResolutionDate] IS NOT NULL)),
    -- A draft needs a subject; the minimum lengths (subject 5, description 20 characters) apply from submission on.
    -- The maximum lengths are the column sizes.
    CONSTRAINT [CK_Case_Subject] CHECK (LEN(LTRIM([Subject])) > 0 AND ([Status] = 'Draft' OR LEN(LTRIM([Subject])) >= 5)),
    CONSTRAINT [CK_Case_Description] CHECK ([Status] = 'Draft' OR LEN(LTRIM([Description])) >= 20)
);
GO

-- Case numbers are unique once given (drafts have none).
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Case_CaseNo]
    ON [grv].[Case] ([CaseNo])
    WHERE [CaseNo] IS NOT NULL;
GO

-- Case lists and the dashboard counts.
CREATE NONCLUSTERED INDEX [IX_Case_Status_Priority]
    ON [grv].[Case] ([Status], [Priority])
    INCLUDE ([CategoryId], [DepartmentId], [SubmittedAtUtc], [TargetResolutionDate]);
GO

-- Category report and trend.
CREATE NONCLUSTERED INDEX [IX_Case_CategoryId_SubmittedAtUtc] ON [grv].[Case] ([CategoryId], [SubmittedAtUtc]);
GO

-- Department report (anonymous cases have no department) and the FK_Case_Department checks.
CREATE NONCLUSTERED INDEX [IX_Case_DepartmentId_SubmittedAtUtc] ON [grv].[Case] ([DepartmentId], [SubmittedAtUtc]);
GO

-- Overdue: an open case past its target date (worked out when queried). Filtered indexes cannot use NOT IN,
-- so the open statuses are listed.
CREATE NONCLUSTERED INDEX [IX_Case_TargetResolutionDate_Open]
    ON [grv].[Case] ([TargetResolutionDate])
    INCLUDE ([Status], [Priority])
    WHERE [Status] IN ('Submitted', 'Acknowledged', 'UnderReview', 'Assigned', 'UnderInvestigation',
                       'AwaitingEmployeeResponse', 'ResolutionProposed', 'Reopened');
GO

-- Conflict-of-interest exclusion.
CREATE NONCLUSTERED INDEX [IX_Case_SubjectEmployeeId] ON [grv].[Case] ([SubjectEmployeeId]);
GO

CREATE NONCLUSTERED INDEX [IX_Case_InvolvedDepartmentId] ON [grv].[Case] ([InvolvedDepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Case_BranchId_CompanyId] ON [grv].[Case] ([BranchId], [CompanyId]);
GO

CREATE NONCLUSTERED INDEX [IX_Case_PositionId_DepartmentId] ON [grv].[Case] ([PositionId], [DepartmentId]);
