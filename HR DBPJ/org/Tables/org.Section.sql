CREATE TABLE [org].[Section]
(
    [Id]                   int            IDENTITY (1, 1) NOT NULL,
    [DepartmentId]         int            NOT NULL,
    [Code]                 nvarchar(20)   NOT NULL,
    [Name]                 nvarchar(100)  NOT NULL,
    [Description]          nvarchar(300)  NULL,
    [SupervisorEmployeeId] int            NULL,
    [Status]               varchar(30)    NOT NULL CONSTRAINT [DF_Section_Status] DEFAULT ('Active'),
    [CreatedAtUtc]         datetime2(3)   NOT NULL CONSTRAINT [DF_Section_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]      int            NULL,
    [ModifiedAtUtc]        datetime2(3)   NOT NULL CONSTRAINT [DF_Section_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]     int            NULL,
    [RowVersion]           rowversion     NOT NULL,
    CONSTRAINT [PK_Section] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Section_Code] UNIQUE ([Code]),
    -- Target of composite FKs that keep Position and Employee inside the section's department.
    CONSTRAINT [UQ_Section_Id_DepartmentId] UNIQUE ([Id], [DepartmentId]),
    CONSTRAINT [FK_Section_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [org].[Department] ([Id]),
    CONSTRAINT [FK_Section_Employee_SupervisorEmployeeId] FOREIGN KEY ([SupervisorEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_Section_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Section_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Section_DepartmentId] ON [org].[Section] ([DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Section_SupervisorEmployeeId] ON [org].[Section] ([SupervisorEmployeeId]);
