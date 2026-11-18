-- A case raised to the reviewers (grievance-review), by HR or an assigned investigator, or immediately when HR
-- acknowledges a Critical case. grv.Case.IsEscalated stays set for reporting.
CREATE TABLE [grv].[Escalation]
(
    [Id]                int            IDENTITY (1, 1) NOT NULL,
    [CaseId]            int            NOT NULL,
    [Reason]            nvarchar(1000) NOT NULL,
    [IsImmediate]       bit            NOT NULL CONSTRAINT [DF_Escalation_IsImmediate] DEFAULT (0),
    [EscalatedByUserId] int            NULL,
    [EscalatedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Escalation_EscalatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ClearedAtUtc]      datetime2(3)   NULL,
    [Outcome]           nvarchar(1000) NULL,
    CONSTRAINT [PK_Escalation] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Escalation_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [CK_Escalation_Reason] CHECK (LEN(LTRIM([Reason])) > 0),
    CONSTRAINT [CK_Escalation_ClearedAtUtc] CHECK ([ClearedAtUtc] IS NULL OR [ClearedAtUtc] >= [EscalatedAtUtc])
);
GO

-- One open escalation per case.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Escalation_CaseId_Open]
    ON [grv].[Escalation] ([CaseId])
    WHERE [ClearedAtUtc] IS NULL;
GO

CREATE NONCLUSTERED INDEX [IX_Escalation_CaseId] ON [grv].[Escalation] ([CaseId]);
