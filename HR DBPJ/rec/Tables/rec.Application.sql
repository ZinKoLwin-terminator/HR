-- A candidate's application for one opening: Applied -> Screening -> Interview -> Selected / Rejected.
CREATE TABLE [rec].[Application]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [JobOpeningId]     int            NOT NULL,
    [CandidateId]      int            NOT NULL,
    [Stage]            varchar(30)    NOT NULL CONSTRAINT [DF_Application_Stage] DEFAULT ('Applied'),
    [AppliedOn]        date           NOT NULL,
    [RejectionReason]  nvarchar(200)  NULL,
    [Notes]            nvarchar(300)  NULL,
    -- Set when an HR Administrator approved the hire despite a do-not-rehire match (both columns or neither).
    [BlacklistOverrideReason]   nvarchar(300) NULL,
    [BlacklistOverrideByUserId] int           NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Application_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Application_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Application] PRIMARY KEY CLUSTERED ([Id]),
    -- One application per candidate per opening; also the FK index for the opening.
    CONSTRAINT [UQ_Application_JobOpeningId_CandidateId] UNIQUE ([JobOpeningId], [CandidateId]),
    CONSTRAINT [FK_Application_JobOpening] FOREIGN KEY ([JobOpeningId]) REFERENCES [rec].[JobOpening] ([Id]),
    CONSTRAINT [FK_Application_Candidate] FOREIGN KEY ([CandidateId]) REFERENCES [rec].[Candidate] ([Id]),
    CONSTRAINT [CK_Application_Stage] CHECK ([Stage] IN ('Applied', 'Screening', 'Interview', 'Selected', 'Rejected')),
    CONSTRAINT [CK_Application_BlacklistOverride] CHECK (([BlacklistOverrideReason] IS NULL AND [BlacklistOverrideByUserId] IS NULL) OR ([BlacklistOverrideReason] IS NOT NULL AND [BlacklistOverrideByUserId] IS NOT NULL AND LEN(LTRIM(RTRIM([BlacklistOverrideReason]))) > 0)),
    -- A rejection needs a reason; other stages carry none.
    CONSTRAINT [CK_Application_RejectionReason] CHECK (([Stage] = 'Rejected' AND LEN(LTRIM([RejectionReason])) > 0) OR ([Stage] <> 'Rejected' AND [RejectionReason] IS NULL))
);
GO

CREATE NONCLUSTERED INDEX [IX_Application_CandidateId] ON [rec].[Application] ([CandidateId]);
GO

-- Counting selected candidates per opening (vacancies, Filled status).
CREATE NONCLUSTERED INDEX [IX_Application_JobOpeningId_Stage] ON [rec].[Application] ([JobOpeningId], [Stage]);
