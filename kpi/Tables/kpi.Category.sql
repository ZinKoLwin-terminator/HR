-- Grouping of KPI templates (Productivity, Quality, Attendance ...). Deactivated, not deleted, once used.
CREATE TABLE [kpi].[Category]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Code]             nvarchar(20)   NOT NULL,
    [Name]             nvarchar(100)  NOT NULL,
    [Description]      nvarchar(300)  NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_Category_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Category_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Category_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Category] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Category_Code] UNIQUE ([Code]),
    CONSTRAINT [UQ_Category_Name] UNIQUE ([Name]),
    CONSTRAINT [CK_Category_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Category_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
