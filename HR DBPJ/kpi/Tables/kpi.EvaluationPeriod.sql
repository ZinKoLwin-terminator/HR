-- One evaluation window of a company: Draft -> Active -> Completed -> Closed. Scorecards are planned
-- in Draft and Active, submitted and reviewed while Active, and Closed is final (no more changes).
-- The API refuses overlapping periods of the same type in the same company.
CREATE TABLE [kpi].[EvaluationPeriod]
(
    [Id]                int            IDENTITY (1, 1) NOT NULL,
    [CompanyId]         int            NOT NULL,
    [Name]              nvarchar(100)  NOT NULL,
    [PeriodType]        varchar(30)    NOT NULL,
    [StartDate]         date           NOT NULL,
    [EndDate]           date           NOT NULL,
    [SubmissionDueDate] date           NULL,
    -- Adds the optional HR final review after the manager's approval.
    [RequiresHrReview]  bit            NOT NULL CONSTRAINT [DF_EvaluationPeriod_RequiresHrReview] DEFAULT (0),
    [Status]            varchar(30)    NOT NULL CONSTRAINT [DF_EvaluationPeriod_Status] DEFAULT ('Draft'),
    [CreatedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_EvaluationPeriod_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int            NULL,
    [ModifiedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_EvaluationPeriod_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int            NULL,
    [RowVersion]        rowversion     NOT NULL,
    [ValidFromUtc]      datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]        datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_EvaluationPeriod] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_EvaluationPeriod_CompanyId_Name] UNIQUE ([CompanyId], [Name]),
    -- Target of FK_Scorecard_EvaluationPeriod: a scorecard's employee belongs to the period's company.
    CONSTRAINT [UQ_EvaluationPeriod_Id_CompanyId] UNIQUE ([Id], [CompanyId]),
    CONSTRAINT [FK_EvaluationPeriod_Company] FOREIGN KEY ([CompanyId]) REFERENCES [org].[Company] ([Id]),
    CONSTRAINT [CK_EvaluationPeriod_PeriodType] CHECK ([PeriodType] IN ('Monthly', 'Quarterly', 'Half-Yearly', 'Yearly', 'Custom')),
    CONSTRAINT [CK_EvaluationPeriod_Status] CHECK ([Status] IN ('Draft', 'Active', 'Completed', 'Closed')),
    CONSTRAINT [CK_EvaluationPeriod_DateOrder] CHECK ([EndDate] > [StartDate]),
    CONSTRAINT [CK_EvaluationPeriod_SubmissionDueDate] CHECK ([SubmissionDueDate] >= [StartDate])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [kpi].[EvaluationPeriodHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- Overlap check and the period pickers.
CREATE NONCLUSTERED INDEX [IX_EvaluationPeriod_CompanyId_PeriodType_StartDate]
    ON [kpi].[EvaluationPeriod] ([CompanyId], [PeriodType], [StartDate])
    INCLUDE ([EndDate], [Status]);
