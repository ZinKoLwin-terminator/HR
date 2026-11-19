-- An investigator on a case: one Lead at most and any number of Members at a time. Unassigning keeps the row
-- (UnassignedAtUtc), so the case keeps the record of everyone who had access to it.
CREATE TABLE [grv].[Assignment]
(
    [Id]                     int            IDENTITY (1, 1) NOT NULL,
    [CaseId]                 int            NOT NULL,
    [InvestigatorEmployeeId] int            NOT NULL,
    [Role]                   varchar(10)    NOT NULL,
    [AssignedAtUtc]          datetime2(3)   NOT NULL CONSTRAINT [DF_Assignment_AssignedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [AssignedByUserId]       int            NOT NULL,
    [UnassignedAtUtc]        datetime2(3)   NULL,
    [UnassignedByUserId]     int            NULL,
    [Reason]                 nvarchar(1000) NULL,
    CONSTRAINT [PK_Assignment] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Assignment_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]),
    CONSTRAINT [FK_Assignment_Employee_InvestigatorEmployeeId] FOREIGN KEY ([InvestigatorEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_Assignment_Role] CHECK ([Role] IN ('Lead', 'Member')),
    CONSTRAINT [CK_Assignment_Unassigned] CHECK (([UnassignedAtUtc] IS NULL AND [UnassignedByUserId] IS NULL)
                                              OR ([UnassignedAtUtc] IS NOT NULL AND [UnassignedByUserId] IS NOT NULL AND [UnassignedAtUtc] >= [AssignedAtUtc]))
);
GO

-- One active Lead per case.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Assignment_CaseId_ActiveLead]
    ON [grv].[Assignment] ([CaseId])
    WHERE [Role] = 'Lead' AND [UnassignedAtUtc] IS NULL;
GO

-- An investigator is assigned to a case once at a time.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Assignment_CaseId_InvestigatorEmployeeId_Active]
    ON [grv].[Assignment] ([CaseId], [InvestigatorEmployeeId])
    WHERE [UnassignedAtUtc] IS NULL;
GO

-- The investigator's queue and the investigation scope.
CREATE NONCLUSTERED INDEX [IX_Assignment_InvestigatorEmployeeId_CaseId]
    ON [grv].[Assignment] ([InvestigatorEmployeeId], [CaseId])
    INCLUDE ([Role], [UnassignedAtUtc]);

GO

-- The assignments of one case (case detail, scope and lead checks).
CREATE NONCLUSTERED INDEX [IX_Assignment_CaseId]
    ON [grv].[Assignment] ([CaseId])
    INCLUDE ([InvestigatorEmployeeId], [Role], [UnassignedAtUtc]);
