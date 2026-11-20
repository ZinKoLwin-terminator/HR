CREATE TABLE [pay].[SalaryStructure]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Code]             nvarchar(20)   NOT NULL,
    [Name]             nvarchar(60)   NOT NULL,
    [Description]      nvarchar(250)  NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_SalaryStructure_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_SalaryStructure_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_SalaryStructure_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_SalaryStructure] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_SalaryStructure_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_SalaryStructure_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_SalaryStructure_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
