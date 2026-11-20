CREATE TABLE [rec].[JobOpening]
(
    [Id]                      int            IDENTITY (1, 1) NOT NULL,
    -- JOB-yyyy-nnn, numbered from rec.JobOpeningCodeSeq by the API (year of PostedOn).
    [Code]                    nvarchar(20)   NOT NULL,
    [Title]                   nvarchar(80)   NOT NULL,
    [DepartmentId]            int            NOT NULL,
    [HiringManagerEmployeeId] int            NOT NULL,
    [EmploymentType]          varchar(30)    NOT NULL,
    [Vacancies]               smallint       NOT NULL,
    [PostedOn]                date           NOT NULL,
    [ClosingDate]             date           NOT NULL,
    [Description]             nvarchar(500)  NULL,
    [Status]                  varchar(30)    NOT NULL CONSTRAINT [DF_JobOpening_Status] DEFAULT ('Draft'),
    [CreatedAtUtc]            datetime2(3)   NOT NULL CONSTRAINT [DF_JobOpening_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]         int            NULL,
    [ModifiedAtUtc]           datetime2(3)   NOT NULL CONSTRAINT [DF_JobOpening_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]        int            NULL,
    [RowVersion]              rowversion     NOT NULL,
    CONSTRAINT [PK_JobOpening] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_JobOpening_Code] UNIQUE ([Code]),
    CONSTRAINT [FK_JobOpening_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [org].[Department] ([Id]),
    CONSTRAINT [FK_JobOpening_Employee_HiringManagerEmployeeId] FOREIGN KEY ([HiringManagerEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_JobOpening_Code] CHECK ([Code] LIKE N'JOB-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'JOB-____-%[^0-9]%' COLLATE Latin1_General_BIN2),
    -- Recruitment uses 'Internship' where employees use 'Intern' (RecruitmentEmploymentType).
    CONSTRAINT [CK_JobOpening_EmploymentType] CHECK ([EmploymentType] IN ('Full-time', 'Part-time', 'Contract', 'Internship')),
    CONSTRAINT [CK_JobOpening_Vacancies] CHECK ([Vacancies] BETWEEN 1 AND 50),
    CONSTRAINT [CK_JobOpening_ClosingDate] CHECK ([ClosingDate] >= [PostedOn]),
    CONSTRAINT [CK_JobOpening_Status] CHECK ([Status] IN ('Draft', 'Open', 'On Hold', 'Closed', 'Filled'))
);
GO

CREATE NONCLUSTERED INDEX [IX_JobOpening_DepartmentId] ON [rec].[JobOpening] ([DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_JobOpening_HiringManagerEmployeeId] ON [rec].[JobOpening] ([HiringManagerEmployeeId]);
GO

CREATE NONCLUSTERED INDEX [IX_JobOpening_Status] ON [rec].[JobOpening] ([Status]);
