CREATE TABLE [org].[Department]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [CompanyId]        int            NOT NULL,
    [BranchId]         int            NOT NULL,
    [Code]             nvarchar(20)   NOT NULL,
    [Name]             nvarchar(100)  NOT NULL,
    [Description]      nvarchar(300)  NULL,
    [HeadEmployeeId]   int            NULL,
    [CostCenter]       nvarchar(20)   NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_Department_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Department_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Department_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Department] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Department_Code] UNIQUE ([Code]),
    -- Target of composite FKs that keep Employee inside the department's branch.
    CONSTRAINT [UQ_Department_Id_BranchId] UNIQUE ([Id], [BranchId]),
    -- The branch must belong to the same company (DepartmentMockService.validate).
    CONSTRAINT [FK_Department_Branch] FOREIGN KEY ([BranchId], [CompanyId]) REFERENCES [org].[Branch] ([Id], [CompanyId]),
    CONSTRAINT [FK_Department_Employee_HeadEmployeeId] FOREIGN KEY ([HeadEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_Department_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Department_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Department_BranchId_CompanyId] ON [org].[Department] ([BranchId], [CompanyId]);
GO

CREATE NONCLUSTERED INDEX [IX_Department_HeadEmployeeId] ON [org].[Department] ([HeadEmployeeId]);
