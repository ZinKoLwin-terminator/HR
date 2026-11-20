-- An employee's basic salary and structure from a date onwards; a later row revises the salary.
CREATE TABLE [pay].[SalaryAssignment]
(
    [Id]                int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]        int            NOT NULL,
    [SalaryStructureId] int            NOT NULL,
    [BasicSalary]       decimal(18, 2) NOT NULL,
    [EffectiveFrom]     date           NOT NULL,
    [Remarks]           nvarchar(120)  NULL,
    [CreatedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_SalaryAssignment_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int            NULL,
    [ModifiedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_SalaryAssignment_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int            NULL,
    [RowVersion]        rowversion     NOT NULL,
    [ValidFromUtc]      datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]        datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_SalaryAssignment] PRIMARY KEY CLUSTERED ([Id]),
    -- One assignment per employee per start date; also serves "assignment in force on a date" and the FK.
    CONSTRAINT [UQ_SalaryAssignment_EmployeeId_EffectiveFrom] UNIQUE ([EmployeeId], [EffectiveFrom]),
    CONSTRAINT [FK_SalaryAssignment_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [FK_SalaryAssignment_SalaryStructure] FOREIGN KEY ([SalaryStructureId]) REFERENCES [pay].[SalaryStructure] ([Id]),
    CONSTRAINT [CK_SalaryAssignment_BasicSalary] CHECK ([BasicSalary] > 0)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [pay].[SalaryAssignmentHistory], DATA_CONSISTENCY_CHECK = ON));
GO

CREATE NONCLUSTERED INDEX [IX_SalaryAssignment_SalaryStructureId] ON [pay].[SalaryAssignment] ([SalaryStructureId]);
