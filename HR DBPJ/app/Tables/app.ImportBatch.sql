-- One Excel import (validation, then execution). Counts and status mirror ImportBatchSummary.
CREATE TABLE [app].[ImportBatch]
(
    [Id]               uniqueidentifier NOT NULL CONSTRAINT [DF_ImportBatch_Id] DEFAULT (NEWSEQUENTIALID()),
    [ModuleCode]       varchar(30)      NOT NULL,
    [ModuleLabel]      nvarchar(60)     NOT NULL,
    [FileName]         nvarchar(260)    NOT NULL,
    [Strategy]         varchar(30)      NOT NULL,
    -- Chosen when the import is executed; NULL while only validated.
    [Mode]             varchar(30)      NULL,
    [Status]           varchar(30)      NOT NULL CONSTRAINT [DF_ImportBatch_Status] DEFAULT ('Validated'),
    [UploadedByUserId] int              NULL,
    [UploadedByName]   nvarchar(130)    NOT NULL,
    [UploadedAtUtc]    datetime2(3)     NOT NULL CONSTRAINT [DF_ImportBatch_UploadedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [StartedAtUtc]     datetime2(3)     NULL,
    [CompletedAtUtc]   datetime2(3)     NULL,
    [TotalRows]        int              NOT NULL,
    [ValidRows]        int              NOT NULL,
    [InvalidRows]      int              NOT NULL,
    [SuccessRows]      int              NOT NULL CONSTRAINT [DF_ImportBatch_SuccessRows] DEFAULT (0),
    [FailedRows]       int              NOT NULL CONSTRAINT [DF_ImportBatch_FailedRows] DEFAULT (0),
    [SkippedRows]      int              NOT NULL CONSTRAINT [DF_ImportBatch_SkippedRows] DEFAULT (0),
    [ErrorSummary]     nvarchar(500)    NULL,
    CONSTRAINT [PK_ImportBatch] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_ImportBatch_Strategy] CHECK ([Strategy] IN ('InsertOnly', 'UpdateOnly', 'InsertOrUpdate')),
    CONSTRAINT [CK_ImportBatch_Mode] CHECK ([Mode] IN ('AllOrNothing', 'Partial')),
    CONSTRAINT [CK_ImportBatch_Status] CHECK ([Status] IN ('Validated', 'Completed', 'CompletedWithErrors', 'Failed')),
    -- A validated batch has not run yet; any other status means it was executed with a mode.
    CONSTRAINT [CK_ImportBatch_Execution] CHECK (([Status] = 'Validated' AND [Mode] IS NULL AND [CompletedAtUtc] IS NULL) OR ([Status] <> 'Validated' AND [Mode] IS NOT NULL AND [StartedAtUtc] IS NOT NULL)),
    CONSTRAINT [CK_ImportBatch_CompletedAfterStart] CHECK ([CompletedAtUtc] >= [StartedAtUtc]),
    CONSTRAINT [CK_ImportBatch_Counts] CHECK ([TotalRows] >= 0 AND [ValidRows] >= 0 AND [InvalidRows] >= 0 AND [SuccessRows] >= 0 AND [FailedRows] >= 0 AND [SkippedRows] >= 0)
);
GO

-- Import history, newest first.
CREATE NONCLUSTERED INDEX [IX_ImportBatch_UploadedAtUtc] ON [app].[ImportBatch] ([UploadedAtUtc] DESC) INCLUDE ([ModuleCode], [Status]);
