CREATE TABLE [org].[Position]
(
    [Id]                  int            IDENTITY (1, 1) NOT NULL,
    [DepartmentId]        int            NOT NULL,
    [SectionId]           int            NULL,
    [JobGradeId]          int            NOT NULL,
    [Code]                nvarchar(20)   NOT NULL,
    [Title]               nvarchar(100)  NOT NULL,
    [Description]         nvarchar(300)  NULL,
    [Headcount]           smallint       NOT NULL,
    [ReportsToPositionId] int            NULL,
    [Status]              varchar(30)    NOT NULL CONSTRAINT [DF_Position_Status] DEFAULT ('Active'),
    [CreatedAtUtc]        datetime2(3)   NOT NULL CONSTRAINT [DF_Position_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]     int            NULL,
    [ModifiedAtUtc]       datetime2(3)   NOT NULL CONSTRAINT [DF_Position_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]    int            NULL,
    [RowVersion]          rowversion     NOT NULL,
    CONSTRAINT [PK_Position] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Position_Code] UNIQUE ([Code]),
    -- Target of the composite FK that keeps Employee.PositionId inside the employee's department.
    CONSTRAINT [UQ_Position_Id_DepartmentId] UNIQUE ([Id], [DepartmentId]),
    CONSTRAINT [FK_Position_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [org].[Department] ([Id]),
    -- The section must belong to the same department; skipped when SectionId is NULL.
    CONSTRAINT [FK_Position_Section] FOREIGN KEY ([SectionId], [DepartmentId]) REFERENCES [org].[Section] ([Id], [DepartmentId]),
    CONSTRAINT [FK_Position_JobGrade] FOREIGN KEY ([JobGradeId]) REFERENCES [org].[JobGrade] ([Id]),
    CONSTRAINT [FK_Position_Position_ReportsToPositionId] FOREIGN KEY ([ReportsToPositionId]) REFERENCES [org].[Position] ([Id]),
    CONSTRAINT [CK_Position_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Position_Headcount] CHECK ([Headcount] >= 1),
    CONSTRAINT [CK_Position_ReportsToNotSelf] CHECK ([ReportsToPositionId] <> [Id]),
    CONSTRAINT [CK_Position_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Position_DepartmentId] ON [org].[Position] ([DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Position_SectionId_DepartmentId] ON [org].[Position] ([SectionId], [DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Position_JobGradeId] ON [org].[Position] ([JobGradeId]);
GO

CREATE NONCLUSTERED INDEX [IX_Position_ReportsToPositionId] ON [org].[Position] ([ReportsToPositionId]);
