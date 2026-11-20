-- Snapshot of one employee's pay for one run. Name, organization and bank columns are copied at
-- processing time so later changes do not alter the payslip. SAMPLE figures only.
CREATE TABLE [pay].[Payslip]
(
    [Id]              int            IDENTITY (1, 1) NOT NULL,
    [PayrollRunId]    int            NOT NULL,
    [EmployeeId]      int            NOT NULL,
    [PeriodName]      nvarchar(30)   NOT NULL,
    [EmployeeCode]    nvarchar(12)   NOT NULL,
    [EmployeeName]    nvarchar(130)  NOT NULL,
    -- Snapshot of the department at processing time (no FK, so the payslip survives reorganisations).
    [DepartmentId]    int            NOT NULL,
    [DepartmentName]  nvarchar(100)  NOT NULL,
    [PositionTitle]   nvarchar(100)  NOT NULL,
    [CompanyName]     nvarchar(100)  NOT NULL,
    [BankName]        nvarchar(60)   NOT NULL,
    -- Masked account only: four bullets, a space and the last 4 digits. The full number stays in hr.EmployeeBankAccount.
    [AccountMasked]   nvarchar(9)    NOT NULL,
    [BasicSalary]     decimal(18, 2) NOT NULL,
    [GrossSalary]     decimal(18, 2) NOT NULL,
    [TotalDeductions] decimal(18, 2) NOT NULL,
    [NetSalary]       decimal(18, 2) NOT NULL,
    [PeriodDays]      tinyint        NOT NULL,
    [DaysEmployed]    tinyint        NOT NULL,
    [WorkingDays]     tinyint        NOT NULL,
    [UnpaidLeaveDays] decimal(4, 1)  NOT NULL,
    [OvertimeMinutes] int            NOT NULL,
    [GeneratedOn]     date           NOT NULL,
    [CreatedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Payslip_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId] int            NULL,
    CONSTRAINT [PK_Payslip] PRIMARY KEY CLUSTERED ([Id]),
    -- One payslip per employee per run; also the FK index for the run.
    CONSTRAINT [UQ_Payslip_PayrollRunId_EmployeeId] UNIQUE ([PayrollRunId], [EmployeeId]),
    CONSTRAINT [FK_Payslip_PayrollRun] FOREIGN KEY ([PayrollRunId]) REFERENCES [pay].[PayrollRun] ([Id]),
    CONSTRAINT [FK_Payslip_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    -- Exactly four bullets (U+2022), a space and the last four digits.
    CONSTRAINT [CK_Payslip_AccountMasked] CHECK (LEN([AccountMasked]) = 9 AND LEFT([AccountMasked], 5) = REPLICATE(NCHAR(8226), 4) + N' ' AND RIGHT([AccountMasked], 4) NOT LIKE N'%[^0-9]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Payslip_Amounts] CHECK ([BasicSalary] >= 0 AND [GrossSalary] >= [BasicSalary] AND [TotalDeductions] >= 0 AND [NetSalary] = [GrossSalary] - [TotalDeductions]),
    CONSTRAINT [CK_Payslip_Days] CHECK ([PeriodDays] BETWEEN 28 AND 31 AND [DaysEmployed] BETWEEN 1 AND [PeriodDays] AND [WorkingDays] <= [PeriodDays] AND [UnpaidLeaveDays] >= 0 AND [OvertimeMinutes] >= 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_Payslip_EmployeeId] ON [pay].[Payslip] ([EmployeeId]) INCLUDE ([PayrollRunId]);
GO

-- Payslip list filter by department.
CREATE NONCLUSTERED INDEX [IX_Payslip_DepartmentId] ON [pay].[Payslip] ([DepartmentId]);
