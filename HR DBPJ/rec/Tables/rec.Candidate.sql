-- Job applicant (personal data of a non-employee; see the retention note in the design).
CREATE TABLE [rec].[Candidate]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [FirstName]        nvarchar(40)   NOT NULL,
    [LastName]         nvarchar(40)   NOT NULL,
    -- Stored lower-cased by the API (CandidateMockService.prepare).
    [Email]            nvarchar(100)  NOT NULL,
    [Phone]            nvarchar(25)   NOT NULL,
    [Source]           varchar(30)    NOT NULL,
    [CurrentEmployer]  nvarchar(80)   NULL,
    [ExperienceYears]  decimal(4, 1)  NOT NULL CONSTRAINT [DF_Candidate_ExperienceYears] DEFAULT (0),
    -- Demo keeps only the file name; real files belong in file storage.
    [ResumeName]       nvarchar(260)  NULL,
    [Notes]            nvarchar(300)  NULL,
    -- Optional; used only to match the do-not-rehire list (never returned by the API).
    [NationalId]       nvarchar(40)   NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Candidate_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Candidate_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Candidate] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Candidate_Email] UNIQUE ([Email]),
    CONSTRAINT [CK_Candidate_Source] CHECK ([Source] IN ('Career site', 'Referral', 'Job board', 'LinkedIn', 'Agency')),
    CONSTRAINT [CK_Candidate_ExperienceYears] CHECK ([ExperienceYears] BETWEEN 0 AND 50)
);
