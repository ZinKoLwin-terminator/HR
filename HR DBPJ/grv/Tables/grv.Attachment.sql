-- A file on a case, optionally on one message or one investigation note. Metadata only: the file lives in the
-- API's grievance file storage (Grievance:FileFolder) under StorageKey, a server-generated key (32 hex characters,
-- never a user-supplied path or file name). Removing a file keeps the row (RemovedAtUtc).
-- Visibility: Shared (the submitter sees it), CaseTeam (management and the assigned investigators), Restricted
-- (management only). A submitter's upload is always Shared; a note's file is never Shared.
-- Written by the submitter too, so the row has no user audit columns; UploadedByUserId is NULL for every upload by
-- the submitter, named or anonymous.
CREATE TABLE [grv].[Attachment]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [CaseId]           int            NOT NULL,
    [CommunicationId]  int            NULL,
    [NoteId]           int            NULL,
    [Visibility]       varchar(20)    NOT NULL,
    [UploadedByRole]   varchar(20)    NOT NULL,
    [UploadedByUserId] int            NULL,
    [FileName]         nvarchar(260)  NOT NULL,
    [ContentType]      varchar(100)   NOT NULL,
    [SizeBytes]        bigint         NOT NULL,
    [StorageKey]       char(32)       NOT NULL,
    [Sha256]           binary(32)     NOT NULL,
    [UploadedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Attachment_UploadedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [RemovedAtUtc]     datetime2(3)   NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Attachment_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Attachment_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Attachment] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Attachment_StorageKey] UNIQUE ([StorageKey]),
    -- Removed with a deleted draft (a submitted case cannot be deleted). The API deletes the files.
    CONSTRAINT [FK_Attachment_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]) ON DELETE CASCADE,
    -- A message's or a note's file belongs to the same case as the message or note.
    CONSTRAINT [FK_Attachment_Communication] FOREIGN KEY ([CommunicationId], [CaseId]) REFERENCES [grv].[Communication] ([Id], [CaseId]),
    CONSTRAINT [FK_Attachment_InvestigationNote] FOREIGN KEY ([NoteId], [CaseId]) REFERENCES [grv].[InvestigationNote] ([Id], [CaseId]),
    CONSTRAINT [CK_Attachment_Visibility] CHECK ([Visibility] IN ('Shared', 'CaseTeam', 'Restricted')),
    CONSTRAINT [CK_Attachment_UploadedByRole] CHECK ([UploadedByRole] IN ('Submitter', 'Management', 'Investigator', 'Reviewer', 'System')),
    CONSTRAINT [CK_Attachment_SubmitterShared] CHECK ([UploadedByRole] <> 'Submitter' OR [Visibility] = 'Shared'),
    -- The submitter is never identified here, named or anonymous.
    CONSTRAINT [CK_Attachment_SubmitterUnnamed] CHECK ([UploadedByRole] <> 'Submitter' OR [UploadedByUserId] IS NULL),
    CONSTRAINT [CK_Attachment_NoteNotShared] CHECK ([NoteId] IS NULL OR [Visibility] <> 'Shared'),
    CONSTRAINT [CK_Attachment_OneParent] CHECK ([CommunicationId] IS NULL OR [NoteId] IS NULL),
    -- At most 10 MB per file.
    CONSTRAINT [CK_Attachment_SizeBytes] CHECK ([SizeBytes] > 0 AND [SizeBytes] <= 10485760),
    CONSTRAINT [CK_Attachment_StorageKey] CHECK ([StorageKey] NOT LIKE '%[^0-9a-f]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Attachment_RemovedAtUtc] CHECK ([RemovedAtUtc] IS NULL OR [RemovedAtUtc] >= [UploadedAtUtc])
);
GO

CREATE NONCLUSTERED INDEX [IX_Attachment_CaseId] ON [grv].[Attachment] ([CaseId]) INCLUDE ([Visibility], [RemovedAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_Attachment_CommunicationId_CaseId] ON [grv].[Attachment] ([CommunicationId], [CaseId]);
GO

CREATE NONCLUSTERED INDEX [IX_Attachment_NoteId_CaseId] ON [grv].[Attachment] ([NoteId], [CaseId]);
