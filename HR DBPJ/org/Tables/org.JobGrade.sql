CREATE TABLE [org].[JobGrade]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Code]             nvarchar(20)   NOT NULL,
    [Name]             nvarchar(60)   NOT NULL,
    [Level]            smallint       NOT NULL,
    [MinSalary]        decimal(18, 2) NOT NULL,
    [MaxSalary]        decimal(18, 2) NOT NULL,
    [Description]      nvarchar(300)  NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_JobGrade_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_JobGrade_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_JobGrade_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_JobGrade] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_JobGrade_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_JobGrade_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_JobGrade_Level] CHECK ([Level] >= 1),
    CONSTRAINT [CK_JobGrade_MinSalary] CHECK ([MinSalary] >= 0),
    CONSTRAINT [CK_JobGrade_SalaryBand] CHECK ([MaxSalary] >= [MinSalary]),
    CONSTRAINT [CK_JobGrade_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
