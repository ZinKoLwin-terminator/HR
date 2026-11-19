-- Who looked at or did what with a grievance case: case-team views, downloads, exports, assignments, status changes
-- and refused access. Append-only (hrms_api has no UPDATE or DELETE on it). Not app.ActivityLog: that log is readable
-- company-wide and stores names. Detail holds codes and ids only, never case content or personal data.
-- CaseId is deliberately not a foreign key: the entries outlive a deleted draft and never block its deletion.
-- A submitter's own reads are not logged at all; what a submitter does (a message, a file) is logged without a user
-- id (ActorRole Submitter), named or anonymous, so the log never links a case to its submitter.
CREATE TABLE [grv].[AuditLog]
(
    [Id]            bigint         IDENTITY (1, 1) NOT NULL,
    [CaseId]        int            NULL,
    [Action]        varchar(40)    NOT NULL,
    [ActorUserId]   int            NULL,
    [ActorRole]     varchar(20)    NOT NULL,
    [OccurredAtUtc] datetime2(3)   NOT NULL CONSTRAINT [DF_AuditLog_OccurredAtUtc] DEFAULT (SYSUTCDATETIME()),
    [Detail]        nvarchar(500)  NULL,
    CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_AuditLog_Action] CHECK ([Action] IN ('ViewCase', 'ViewHistory', 'DownloadAttachment', 'UploadAttachment', 'Export', 'Assign', 'Unassign',
                                                        'StatusChange', 'Triage', 'AddNote', 'SendMessage', 'CorrectiveAction', 'AccessRefused')),
    CONSTRAINT [CK_AuditLog_ActorRole] CHECK ([ActorRole] IN ('Submitter', 'Management', 'Investigator', 'Reviewer', 'System')),
    -- A submitter is never identified in the log.
    CONSTRAINT [CK_AuditLog_SubmitterUnnamed] CHECK ([ActorRole] <> 'Submitter' OR [ActorUserId] IS NULL)
);
GO

CREATE NONCLUSTERED INDEX [IX_AuditLog_CaseId_OccurredAtUtc] ON [grv].[AuditLog] ([CaseId], [OccurredAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_AuditLog_ActorUserId_OccurredAtUtc] ON [grv].[AuditLog] ([ActorUserId], [OccurredAtUtc]);
