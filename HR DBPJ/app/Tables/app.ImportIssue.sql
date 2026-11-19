-- Validation and execution problems of an import. Value holds the original cell text, truncated,
-- and can contain personal data from the uploaded file.
CREATE TABLE [app].[ImportIssue]
(
    [Id]            bigint           IDENTITY (1, 1) NOT NULL,
    [ImportBatchId] uniqueidentifier NOT NULL,
    -- Row number in the worksheet (1 = header).
    [RowNumber]     int              NOT NULL,
    [Field]         nvarchar(100)    NOT NULL,
    [Value]         nvarchar(100)    NOT NULL,
    [Message]       nvarchar(500)    NOT NULL,
    [Suggestion]    nvarchar(300)    NULL,
    [Severity]      varchar(30)      NOT NULL,
    CONSTRAINT [PK_ImportIssue] PRIMARY KEY CLUSTERED ([Id]),
    -- Owned by the batch: removed with it.
    CONSTRAINT [FK_ImportIssue_ImportBatch] FOREIGN KEY ([ImportBatchId]) REFERENCES [app].[ImportBatch] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_ImportIssue_RowNumber] CHECK ([RowNumber] >= 1),
    CONSTRAINT [CK_ImportIssue_Severity] CHECK ([Severity] IN ('Error', 'Warning'))
);
GO

CREATE NONCLUSTERED INDEX [IX_ImportIssue_ImportBatchId_RowNumber] ON [app].[ImportIssue] ([ImportBatchId], [RowNumber]);
