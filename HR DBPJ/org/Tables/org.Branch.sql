CREATE TABLE [org].[Branch]
(
    [Id]                int            IDENTITY (1, 1) NOT NULL,
    [CompanyId]         int            NOT NULL,
    [Code]              nvarchar(20)   NOT NULL,
    [Name]              nvarchar(100)  NOT NULL,
    [Type]              varchar(30)    NOT NULL,
    [Email]             nvarchar(120)  NULL,
    [Phone]             nvarchar(25)   NULL,
    [Address]           nvarchar(200)  NULL,
    [City]              nvarchar(60)   NOT NULL,
    [Country]           nvarchar(60)   NOT NULL,
    [ManagerEmployeeId] int            NULL,
    [OpenedDate]        date           NOT NULL,
    [Status]            varchar(30)    NOT NULL CONSTRAINT [DF_Branch_Status] DEFAULT ('Active'),
    [CreatedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_Branch_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int            NULL,
    [ModifiedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Branch_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int            NULL,
    [RowVersion]        rowversion     NOT NULL,
    CONSTRAINT [PK_Branch] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Branch_Code] UNIQUE ([Code]),
    -- Target of composite FKs that keep Department and Employee inside the branch's company.
    CONSTRAINT [UQ_Branch_Id_CompanyId] UNIQUE ([Id], [CompanyId]),
    CONSTRAINT [FK_Branch_Company] FOREIGN KEY ([CompanyId]) REFERENCES [org].[Company] ([Id]),
    CONSTRAINT [FK_Branch_Employee_ManagerEmployeeId] FOREIGN KEY ([ManagerEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_Branch_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Branch_Type] CHECK ([Type] IN ('Head Office', 'Branch', 'Regional Office', 'Warehouse')),
    CONSTRAINT [CK_Branch_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Branch_CompanyId] ON [org].[Branch] ([CompanyId]);
GO

CREATE NONCLUSTERED INDEX [IX_Branch_ManagerEmployeeId] ON [org].[Branch] ([ManagerEmployeeId]);
