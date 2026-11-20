-- Salary bank details, kept apart from hr.Employee so access can be granted separately (PII).
CREATE TABLE [hr].[EmployeeBankAccount]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]       int            NOT NULL,
    [BankName]         nvarchar(60)   NOT NULL,
    [BranchName]       nvarchar(60)   NULL,
    [AccountName]      nvarchar(80)   NOT NULL,
    [AccountNumber]    varchar(20)    NOT NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_EmployeeBankAccount_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_EmployeeBankAccount_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    [ValidFromUtc]     datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]       datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_EmployeeBankAccount] PRIMARY KEY CLUSTERED ([Id]),
    -- One bank account per employee (1:1); also serves as the FK index.
    CONSTRAINT [UQ_EmployeeBankAccount_EmployeeId] UNIQUE ([EmployeeId]),
    -- Owned by the employee: removed with it.
    CONSTRAINT [FK_EmployeeBankAccount_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]) ON DELETE CASCADE,
    -- Digits only, 8 to 20 characters (employee Excel module and form).
    CONSTRAINT [CK_EmployeeBankAccount_AccountNumber] CHECK (LEN([AccountNumber]) >= 8 AND [AccountNumber] NOT LIKE '%[^0-9]%' COLLATE Latin1_General_BIN2)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hr].[EmployeeBankAccountHistory], DATA_CONSISTENCY_CHECK = ON));
