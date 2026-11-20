-- One employee's KPI plan and results for one evaluation period.
-- Planning (HR assigns KPIs) -> Draft (plan finalized at 100% weight; the employee enters results)
-- -> Submitted -> Under Review (manager reviewing) -> Approved -> Finalized.
-- Returned goes back to the employee and comes back as Resubmitted; Rejected is final.
-- Approved is only held while the period requires an HR final review; otherwise approval finalizes.
-- The organization placement is copied when the scorecard is created, so reports keep the department
-- the employee was in during the period even after a transfer.
CREATE TABLE [kpi].[Scorecard]
(
    [Id]                 int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]         int            NOT NULL,
    [PeriodId]           int            NOT NULL,
    [CompanyId]          int            NOT NULL,
    [BranchId]           int            NOT NULL,
    [DepartmentId]       int            NOT NULL,
    [PositionId]         int            NOT NULL,
    -- The employee's manager when the plan was finalized: the only manager who may review it.
    [ReviewerEmployeeId] int            NULL,
    [Status]             varchar(30)    NOT NULL CONSTRAINT [DF_Scorecard_Status] DEFAULT ('Planning'),
    [EmployeeComment]    nvarchar(1000) NULL,
    [ManagerRating]      decimal(4, 2)  NULL,
    [ManagerComment]     nvarchar(1000) NULL,
    [HrComment]          nvarchar(1000) NULL,
    -- Official results, calculated by the API only (never by the browser).
    [OverallScore]       decimal(8, 2)  NULL,
    [RatingLabel]        nvarchar(60)   NULL,
    [CalculationVersion] smallint       NULL,
    [PlanFinalizedOn]    date           NULL,
    [SubmittedOn]        date           NULL,
    [ReviewedOn]         date           NULL,
    [FinalizedOn]        date           NULL,
    [FinalizedByUserId]  int            NULL,
    [CreatedAtUtc]       datetime2(3)   NOT NULL CONSTRAINT [DF_Scorecard_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]    int            NULL,
    [ModifiedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_Scorecard_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]   int            NULL,
    [RowVersion]         rowversion     NOT NULL,
    -- System-versioned: every status change, result and score is kept.
    [ValidFromUtc]       datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]         datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_Scorecard] PRIMARY KEY CLUSTERED ([Id]),
    -- Duplicate prevention: one scorecard per employee and period.
    CONSTRAINT [UQ_Scorecard_EmployeeId_PeriodId] UNIQUE ([EmployeeId], [PeriodId]),
    CONSTRAINT [FK_Scorecard_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [FK_Scorecard_EvaluationPeriod] FOREIGN KEY ([PeriodId], [CompanyId]) REFERENCES [kpi].[EvaluationPeriod] ([Id], [CompanyId]),
    -- Composite FKs keep the copied placement consistent: company > branch > department > position.
    CONSTRAINT [FK_Scorecard_Branch] FOREIGN KEY ([BranchId], [CompanyId]) REFERENCES [org].[Branch] ([Id], [CompanyId]),
    CONSTRAINT [FK_Scorecard_Department] FOREIGN KEY ([DepartmentId], [BranchId]) REFERENCES [org].[Department] ([Id], [BranchId]),
    CONSTRAINT [FK_Scorecard_Position] FOREIGN KEY ([PositionId], [DepartmentId]) REFERENCES [org].[Position] ([Id], [DepartmentId]),
    CONSTRAINT [FK_Scorecard_Employee_ReviewerEmployeeId] FOREIGN KEY ([ReviewerEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_Scorecard_Status] CHECK ([Status] IN ('Planning', 'Draft', 'Submitted', 'Under Review', 'Returned', 'Resubmitted', 'Approved', 'Rejected', 'Finalized')),
    -- "Employees cannot approve their own submissions." The API also blocks it through the user -> employee link.
    CONSTRAINT [CK_Scorecard_NotOwnReviewer] CHECK ([ReviewerEmployeeId] <> [EmployeeId]),
    CONSTRAINT [CK_Scorecard_ManagerRating] CHECK ([ManagerRating] IS NULL OR ([ManagerRating] >= 1 AND [ManagerRating] <= 5)),
    CONSTRAINT [CK_Scorecard_OverallScore] CHECK ([OverallScore] IS NULL OR [OverallScore] >= 0),
    CONSTRAINT [CK_Scorecard_PlanFinalized] CHECK ([Status] = 'Planning' OR [PlanFinalizedOn] IS NOT NULL),
    CONSTRAINT [CK_Scorecard_Submitted] CHECK ([Status] IN ('Planning', 'Draft') OR [SubmittedOn] IS NOT NULL),
    CONSTRAINT [CK_Scorecard_Scored] CHECK ([Status] NOT IN ('Approved', 'Finalized') OR ([OverallScore] IS NOT NULL AND [CalculationVersion] IS NOT NULL)),
    CONSTRAINT [CK_Scorecard_Finalized] CHECK (([Status] = 'Finalized' AND [FinalizedOn] IS NOT NULL)
                                            OR ([Status] <> 'Finalized' AND [FinalizedOn] IS NULL))
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [kpi].[ScorecardHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- Period dashboards, completion rates and rankings.
CREATE NONCLUSTERED INDEX [IX_Scorecard_PeriodId_Status]
    ON [kpi].[Scorecard] ([PeriodId], [Status])
    INCLUDE ([EmployeeId], [DepartmentId], [OverallScore]);
GO

-- Manager review queue.
CREATE NONCLUSTERED INDEX [IX_Scorecard_ReviewerEmployeeId_Status]
    ON [kpi].[Scorecard] ([ReviewerEmployeeId], [Status])
    INCLUDE ([PeriodId], [EmployeeId]);
GO

-- Department reports.
CREATE NONCLUSTERED INDEX [IX_Scorecard_DepartmentId_PeriodId]
    ON [kpi].[Scorecard] ([DepartmentId], [PeriodId])
    INCLUDE ([Status], [OverallScore]);
GO

CREATE NONCLUSTERED INDEX [IX_Scorecard_BranchId_CompanyId] ON [kpi].[Scorecard] ([BranchId], [CompanyId]);
GO

CREATE NONCLUSTERED INDEX [IX_Scorecard_PositionId_DepartmentId] ON [kpi].[Scorecard] ([PositionId], [DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Scorecard_PeriodId_CompanyId] ON [kpi].[Scorecard] ([PeriodId], [CompanyId]);
