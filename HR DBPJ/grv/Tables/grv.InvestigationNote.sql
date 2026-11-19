-- The case team's working record: notes, interviews, follow-ups, findings and recommendations. Append-only
-- (hrms_api has no UPDATE or DELETE on it): a correction is a new note. Never shown to the submitter; reviewers
-- see only Findings and Recommendations.
CREATE TABLE [grv].[InvestigationNote]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [CaseId]           int            NOT NULL,
    [Kind]             varchar(20)    NOT NULL,
    [Body]             nvarchar(4000) NOT NULL,
    -- Interviews only.
    [InterviewDate]    date           NULL,
    [IntervieweeText]  nvarchar(200)  NULL,
    [AuthorUserId]     int            NOT NULL,
    -- NULL for the built-in administrator (linked to no employee).
    [AuthorEmployeeId] int            NULL,
    [AuthorName]       nvarchar(130)  NOT NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_InvestigationNote_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_InvestigationNote] PRIMARY KEY CLUSTERED ([Id]),
    -- Target of the composite key that keeps a note's files on the note's own case.
    CONSTRAINT [UQ_InvestigationNote_Id_CaseId] UNIQUE ([Id], [CaseId]),
    CONSTRAINT [FK_InvestigationNote_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [FK_InvestigationNote_Employee_AuthorEmployeeId] FOREIGN KEY ([AuthorEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_InvestigationNote_Kind] CHECK ([Kind] IN ('Note', 'Interview', 'FollowUp', 'Finding', 'Recommendation')),
    CONSTRAINT [CK_InvestigationNote_Body] CHECK (LEN(LTRIM([Body])) > 0),
    -- An interview has its date; other notes have neither interview field.
    CONSTRAINT [CK_InvestigationNote_Interview] CHECK (([Kind] = 'Interview' AND [InterviewDate] IS NOT NULL)
                                                    OR ([Kind] <> 'Interview' AND [InterviewDate] IS NULL AND [IntervieweeText] IS NULL))
);
GO

CREATE NONCLUSTERED INDEX [IX_InvestigationNote_CaseId_CreatedAtUtc] ON [grv].[InvestigationNote] ([CaseId], [CreatedAtUtc]) INCLUDE ([Kind]);
GO

CREATE NONCLUSTERED INDEX [IX_InvestigationNote_AuthorEmployeeId] ON [grv].[InvestigationNote] ([AuthorEmployeeId]);
