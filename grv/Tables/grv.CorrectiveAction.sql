-- An action HR plans or takes because of a case (training, mediation, a policy change ...). HR only; never shown
-- to the submitter.
CREATE TABLE [grv].[CorrectiveAction]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [CaseId]           int            NOT NULL,
    -- Free text, for example "Training" or "Mediation".
    [ActionType]       nvarchar(100)  NOT NULL,
    [Description]      nvarchar(1000) NOT NULL,
    -- Who carries it out, in words (a role or a team; not linked to an employee).
    [ResponsibleText]  nvarchar(200)  NULL,
    [DueDate]          date           NULL,
    [CompletedOn]      date           NULL,
    [Status]           varchar(20)    NOT NULL CONSTRAINT [DF_CorrectiveAction_Status] DEFAULT ('Proposed'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_CorrectiveAction_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_CorrectiveAction_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_CorrectiveAction] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_CorrectiveAction_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [CK_CorrectiveAction_ActionType] CHECK (LEN(LTRIM([ActionType])) > 0),
    CONSTRAINT [CK_CorrectiveAction_Status] CHECK ([Status] IN ('Proposed', 'Approved', 'Rejected', 'Implemented')),
    CONSTRAINT [CK_CorrectiveAction_Description] CHECK (LEN(LTRIM([Description])) > 0),
    -- Implemented exactly when it has a completion date.
    CONSTRAINT [CK_CorrectiveAction_CompletedOn] CHECK (([Status] = 'Implemented' AND [CompletedOn] IS NOT NULL)
                                                     OR ([Status] <> 'Implemented' AND [CompletedOn] IS NULL))
);
GO

CREATE NONCLUSTERED INDEX [IX_CorrectiveAction_CaseId] ON [grv].[CorrectiveAction] ([CaseId]);
