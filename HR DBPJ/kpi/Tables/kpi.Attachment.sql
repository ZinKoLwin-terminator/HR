-- Evidence for a KPI result. Metadata only: the file lives in the API's file storage under
-- StorageKey, a server-generated key (32 hex characters, never a user-supplied path or file name).
CREATE TABLE [kpi].[Attachment]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [ScorecardItemId]  int            NOT NULL,
    [FileName]         nvarchar(260)  NOT NULL,
    [ContentType]      varchar(100)   NOT NULL,
    [SizeBytes]        bigint         NOT NULL,
    [StorageKey]       char(32)       NOT NULL,
    [UploadedOn]       date           NOT NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Attachment_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Attachment_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Attachment] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Attachment_StorageKey] UNIQUE ([StorageKey]),
    CONSTRAINT [FK_Attachment_ScorecardItem] FOREIGN KEY ([ScorecardItemId]) REFERENCES [kpi].[ScorecardItem] ([Id]) ON DELETE CASCADE,
    -- At most 10 MB per file.
    CONSTRAINT [CK_Attachment_SizeBytes] CHECK ([SizeBytes] > 0 AND [SizeBytes] <= 10485760),
    CONSTRAINT [CK_Attachment_StorageKey] CHECK ([StorageKey] NOT LIKE '%[^0-9a-f]%' COLLATE Latin1_General_BIN2)
);
GO

CREATE NONCLUSTERED INDEX [IX_Attachment_ScorecardItemId] ON [kpi].[Attachment] ([ScorecardItemId]);
