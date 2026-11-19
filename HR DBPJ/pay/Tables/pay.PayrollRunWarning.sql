-- Notes from processing a run (PayrollRun.warnings), e.g. employees skipped for a missing salary.
CREATE TABLE [pay].[PayrollRunWarning]
(
    [Id]           int           IDENTITY (1, 1) NOT NULL,
    [PayrollRunId] int           NOT NULL,
    [Message]      nvarchar(400) NOT NULL,
    [CreatedAtUtc] datetime2(3)  NOT NULL CONSTRAINT [DF_PayrollRunWarning_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PayrollRunWarning] PRIMARY KEY CLUSTERED ([Id]),
    -- Owned by the run: removed with it.
    CONSTRAINT [FK_PayrollRunWarning_PayrollRun] FOREIGN KEY ([PayrollRunId]) REFERENCES [pay].[PayrollRun] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_PayrollRunWarning_PayrollRunId] ON [pay].[PayrollRunWarning] ([PayrollRunId]);
