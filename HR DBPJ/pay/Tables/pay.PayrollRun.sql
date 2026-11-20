-- One payroll period (a calendar month): Draft -> Pending Approval -> Approved -> Paid. Approved is
-- locked except for mark paid / return to draft, and Paid is final (pay.TR_PayrollRun_PaidIsFinal).
-- SAMPLE figures only.
CREATE TABLE [pay].[PayrollRun]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [PeriodName]       nvarchar(30)   NOT NULL,
    [Year]             smallint       NOT NULL,
    [Month]            tinyint        NOT NULL,
    [StartDate]        date           NOT NULL,
    [EndDate]          date           NOT NULL,
    [PayDate]          date           NOT NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_PayrollRun_Status] DEFAULT ('Draft'),
    [EmployeeCount]    int            NOT NULL CONSTRAINT [DF_PayrollRun_EmployeeCount] DEFAULT (0),
    [TotalBasic]       decimal(18, 2) NOT NULL CONSTRAINT [DF_PayrollRun_TotalBasic] DEFAULT (0),
    [TotalAllowances]  decimal(18, 2) NOT NULL CONSTRAINT [DF_PayrollRun_TotalAllowances] DEFAULT (0),
    [TotalGross]       decimal(18, 2) NOT NULL CONSTRAINT [DF_PayrollRun_TotalGross] DEFAULT (0),
    [TotalDeductions]  decimal(18, 2) NOT NULL CONSTRAINT [DF_PayrollRun_TotalDeductions] DEFAULT (0),
    [TotalNet]         decimal(18, 2) NOT NULL CONSTRAINT [DF_PayrollRun_TotalNet] DEFAULT (0),
    [ProcessedOn]      date           NULL,
    [ApprovedByUserId] int            NULL,
    [ApprovedByName]   nvarchar(130)  NULL,
    [ApprovedOn]       date           NULL,
    [PaidOn]           date           NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_PayrollRun_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_PayrollRun_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    [ValidFromUtc]     datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]       datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    -- User who processed the run, with a display-name snapshot (like ApprovedByName). Appended after
    -- the existing columns so publishing adds them with ALTER TABLE instead of rebuilding the table.
    [ProcessedByUserId] int            NULL,
    [ProcessedByName]   nvarchar(130)  NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_PayrollRun] PRIMARY KEY CLUSTERED ([Id]),
    -- One period per month.
    CONSTRAINT [UQ_PayrollRun_Year_Month] UNIQUE ([Year], [Month]),
    CONSTRAINT [CK_PayrollRun_Month] CHECK ([Month] BETWEEN 1 AND 12),
    -- Dates are derived from year and month (PayrollRunMockService.prepare).
    CONSTRAINT [CK_PayrollRun_PeriodDates] CHECK ([StartDate] = DATEFROMPARTS([Year], [Month], 1) AND [EndDate] = EOMONTH([StartDate])),
    CONSTRAINT [CK_PayrollRun_PayDate] CHECK ([PayDate] >= [StartDate]),
    CONSTRAINT [CK_PayrollRun_Status] CHECK ([Status] IN ('Draft', 'Pending Approval', 'Approved', 'Paid')),
    CONSTRAINT [CK_PayrollRun_Totals] CHECK ([EmployeeCount] >= 0 AND [TotalBasic] >= 0 AND [TotalDeductions] >= 0 AND [TotalGross] = [TotalBasic] + [TotalAllowances] AND [TotalNet] = [TotalGross] - [TotalDeductions]),
    -- Each status carries exactly the milestones reached so far: date, user and name of who did it.
    CONSTRAINT [CK_PayrollRun_Lifecycle] CHECK (
        ([Status] = 'Draft'            AND [ProcessedOn] IS NULL     AND [ProcessedByUserId] IS NULL     AND [ProcessedByName] IS NULL
                                       AND [ApprovedOn] IS NULL      AND [ApprovedByUserId] IS NULL      AND [ApprovedByName] IS NULL
                                       AND [PaidOn] IS NULL          AND [EmployeeCount] = 0)
     OR ([Status] = 'Pending Approval' AND [ProcessedOn] IS NOT NULL AND [ProcessedByUserId] IS NOT NULL AND [ProcessedByName] IS NOT NULL
                                       AND [ApprovedOn] IS NULL      AND [ApprovedByUserId] IS NULL      AND [ApprovedByName] IS NULL
                                       AND [PaidOn] IS NULL)
     OR ([Status] = 'Approved'         AND [ProcessedOn] IS NOT NULL AND [ProcessedByUserId] IS NOT NULL AND [ProcessedByName] IS NOT NULL
                                       AND [ApprovedOn] IS NOT NULL  AND [ApprovedByUserId] IS NOT NULL  AND [ApprovedByName] IS NOT NULL
                                       AND [PaidOn] IS NULL)
     OR ([Status] = 'Paid'             AND [ProcessedOn] IS NOT NULL AND [ProcessedByUserId] IS NOT NULL AND [ProcessedByName] IS NOT NULL
                                       AND [ApprovedOn] IS NOT NULL  AND [ApprovedByUserId] IS NOT NULL  AND [ApprovedByName] IS NOT NULL
                                       AND [PaidOn] IS NOT NULL)),
    -- Separation of duties: the user who processed a run cannot approve it.
    CONSTRAINT [CK_PayrollRun_ApproverNotProcessor] CHECK ([ApprovedByUserId] <> [ProcessedByUserId])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [pay].[PayrollRunHistory], DATA_CONSISTENCY_CHECK = ON));
