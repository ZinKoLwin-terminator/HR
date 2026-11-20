-- Kind of grievance (Workplace Harassment, Discrimination ...). Deactivated, not deleted, once used.
-- AllowsAnonymous decides whether a case of this category may be submitted anonymously; DefaultTargetDays
-- proposes the target resolution date when a case is acknowledged.
CREATE TABLE [grv].[Category]
(
    [Id]                int            IDENTITY (1, 1) NOT NULL,
    [Code]              nvarchar(20)   NOT NULL,
    [Name]              nvarchar(100)  NOT NULL,
    [Description]       nvarchar(300)  NULL,
    [AllowsAnonymous]   bit            NOT NULL CONSTRAINT [DF_Category_AllowsAnonymous] DEFAULT (1),
    [DefaultTargetDays] smallint       NOT NULL CONSTRAINT [DF_Category_DefaultTargetDays] DEFAULT (30),
    [SortOrder]         smallint       NOT NULL CONSTRAINT [DF_Category_SortOrder] DEFAULT (0),
    [Status]            varchar(30)    NOT NULL CONSTRAINT [DF_Category_Status] DEFAULT ('Active'),
    [CreatedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_Category_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int            NULL,
    [ModifiedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Category_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int            NULL,
    [RowVersion]        rowversion     NOT NULL,
    CONSTRAINT [PK_Category] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Category_Code] UNIQUE ([Code]),
    CONSTRAINT [UQ_Category_Name] UNIQUE ([Name]),
    CONSTRAINT [CK_Category_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Category_DefaultTargetDays] CHECK ([DefaultTargetDays] >= 1 AND [DefaultTargetDays] <= 365),
    CONSTRAINT [CK_Category_SortOrder] CHECK ([SortOrder] >= 0),
    CONSTRAINT [CK_Category_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
