-- Messages between the case team and the submitter, including requests for more information and the answers.
-- Append-only (hrms_api has no UPDATE or DELETE on it), so it has no modification columns. Written by the
-- submitter too: a submitter message keeps no ids, the author is N'Submitter' on a named case (the API shows the
-- submitter's name from grv.CaseSubmitter) and N'Anonymous submitter' on an anonymous one.
CREATE TABLE [grv].[Communication]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [CaseId]           int            NOT NULL,
    [Direction]        varchar(20)    NOT NULL,
    [Kind]             varchar(20)    NOT NULL,
    [Body]             nvarchar(4000) NOT NULL,
    [AuthorRole]       varchar(20)    NOT NULL,
    [AuthorUserId]     int            NULL,
    [AuthorEmployeeId] int            NULL,
    [AuthorName]       nvarchar(130)  NOT NULL,
    -- The information request an answer replies to (on the same case).
    [InReplyToId]      int            NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Communication_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Communication] PRIMARY KEY CLUSTERED ([Id]),
    -- Target of the composite keys that keep a reply and a message's files on the message's own case.
    CONSTRAINT [UQ_Communication_Id_CaseId] UNIQUE ([Id], [CaseId]),
    CONSTRAINT [FK_Communication_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [FK_Communication_Employee_AuthorEmployeeId] FOREIGN KEY ([AuthorEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [FK_Communication_Communication_InReplyToId] FOREIGN KEY ([InReplyToId], [CaseId]) REFERENCES [grv].[Communication] ([Id], [CaseId]),
    CONSTRAINT [CK_Communication_Direction] CHECK ([Direction] IN ('ToSubmitter', 'FromSubmitter')),
    CONSTRAINT [CK_Communication_Kind] CHECK ([Kind] IN ('Message', 'InfoRequest', 'InfoResponse')),
    CONSTRAINT [CK_Communication_AuthorRole] CHECK ([AuthorRole] IN ('Submitter', 'Management', 'Investigator', 'Reviewer', 'System')),
    CONSTRAINT [CK_Communication_Body] CHECK (LEN(LTRIM([Body])) > 0),
    -- Only the submitter writes FromSubmitter; requests go to the submitter, answers come from them.
    CONSTRAINT [CK_Communication_Author] CHECK (([Direction] = 'FromSubmitter' AND [AuthorRole] = 'Submitter' AND [Kind] IN ('Message', 'InfoResponse'))
                                             OR ([Direction] = 'ToSubmitter' AND [AuthorRole] <> 'Submitter' AND [Kind] IN ('Message', 'InfoRequest'))),
    CONSTRAINT [CK_Communication_InReplyTo] CHECK ([InReplyToId] IS NULL OR ([Kind] = 'InfoResponse' AND [InReplyToId] <> [Id])),
    -- The submitter is never identified here, named or anonymous, and only the submitter carries those two names.
    CONSTRAINT [CK_Communication_SubmitterAuthor] CHECK (([AuthorRole] = 'Submitter' AND [AuthorUserId] IS NULL AND [AuthorEmployeeId] IS NULL
                                                          AND [AuthorName] IN (N'Submitter', N'Anonymous submitter'))
                                                      OR ([AuthorRole] <> 'Submitter' AND [AuthorName] NOT IN (N'Submitter', N'Anonymous submitter')))
);
GO

CREATE NONCLUSTERED INDEX [IX_Communication_CaseId_CreatedAtUtc] ON [grv].[Communication] ([CaseId], [CreatedAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_Communication_InReplyToId_CaseId] ON [grv].[Communication] ([InReplyToId], [CaseId]);
GO

CREATE NONCLUSTERED INDEX [IX_Communication_AuthorEmployeeId] ON [grv].[Communication] ([AuthorEmployeeId]);
