-- A proposed resolution waiting for a reviewer. Approving it copies the summary to the case (Resolved);
-- rejecting it sends the case back. Nobody decides their own proposal.
CREATE TABLE [grv].[ResolutionProposal]
(
    [Id]                   int            IDENTITY (1, 1) NOT NULL,
    [CaseId]               int            NOT NULL,
    [Summary]              nvarchar(4000) NOT NULL,
    [ProposedByUserId]     int            NOT NULL,
    -- NULL for the built-in administrator (linked to no employee).
    [ProposedByEmployeeId] int            NULL,
    [ProposedAtUtc]        datetime2(3)   NOT NULL CONSTRAINT [DF_ResolutionProposal_ProposedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [Decision]             varchar(20)    NOT NULL CONSTRAINT [DF_ResolutionProposal_Decision] DEFAULT ('Pending'),
    [DecidedByUserId]      int            NULL,
    [DecidedAtUtc]         datetime2(3)   NULL,
    [DecisionComment]      nvarchar(1000) NULL,
    CONSTRAINT [PK_ResolutionProposal] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ResolutionProposal_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [FK_ResolutionProposal_Employee_ProposedByEmployeeId] FOREIGN KEY ([ProposedByEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_ResolutionProposal_Decision] CHECK ([Decision] IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT [CK_ResolutionProposal_Summary] CHECK (LEN(LTRIM([Summary])) > 0),
    -- A decision has its reviewer and time; a pending proposal has neither.
    CONSTRAINT [CK_ResolutionProposal_Decided] CHECK (([Decision] = 'Pending' AND [DecidedByUserId] IS NULL AND [DecidedAtUtc] IS NULL)
                                                   OR ([Decision] <> 'Pending' AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL)),
    -- "Nobody approves their own proposal." The API checks it too.
    CONSTRAINT [CK_ResolutionProposal_NotOwnDecision] CHECK ([DecidedByUserId] <> [ProposedByUserId]),
    CONSTRAINT [CK_ResolutionProposal_RejectionReason] CHECK ([Decision] <> 'Rejected' OR ([DecisionComment] IS NOT NULL AND LEN(LTRIM([DecisionComment])) > 0))
);
GO

-- One pending proposal per case.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_ResolutionProposal_CaseId_Pending]
    ON [grv].[ResolutionProposal] ([CaseId])
    WHERE [Decision] = 'Pending';
GO

CREATE NONCLUSTERED INDEX [IX_ResolutionProposal_CaseId] ON [grv].[ResolutionProposal] ([CaseId]) INCLUDE ([Decision], [DecidedByUserId]);
GO

-- "Cases the reviewer decided" (review scope).
CREATE NONCLUSTERED INDEX [IX_ResolutionProposal_DecidedByUserId] ON [grv].[ResolutionProposal] ([DecidedByUserId]) INCLUDE ([CaseId]);
GO

CREATE NONCLUSTERED INDEX [IX_ResolutionProposal_ProposedByEmployeeId] ON [grv].[ResolutionProposal] ([ProposedByEmployeeId]);
