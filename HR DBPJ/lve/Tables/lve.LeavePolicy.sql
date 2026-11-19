-- Entitlement rule. NULL JobGradeId / EmploymentType means "applies to all".
CREATE TABLE [lve].[LeavePolicy]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Name]             nvarchar(100)  NOT NULL,
    [LeaveTypeId]      int            NOT NULL,
    [JobGradeId]       int            NULL,
    [EmploymentType]   varchar(30)    NULL,
    [DaysPerYear]      decimal(5, 1)  NOT NULL,
    [Accrual]          varchar(30)    NOT NULL,
    [MinServiceMonths] smallint       NOT NULL CONSTRAINT [DF_LeavePolicy_MinServiceMonths] DEFAULT (0),
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_LeavePolicy_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_LeavePolicy_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_LeavePolicy_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_LeavePolicy] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_LeavePolicy_Name] UNIQUE ([Name]),
    CONSTRAINT [FK_LeavePolicy_LeaveType] FOREIGN KEY ([LeaveTypeId]) REFERENCES [lve].[LeaveType] ([Id]),
    CONSTRAINT [FK_LeavePolicy_JobGrade] FOREIGN KEY ([JobGradeId]) REFERENCES [org].[JobGrade] ([Id]),
    CONSTRAINT [CK_LeavePolicy_EmploymentType] CHECK ([EmploymentType] IN ('Full-time', 'Part-time', 'Contract', 'Intern')),
    CONSTRAINT [CK_LeavePolicy_DaysPerYear] CHECK ([DaysPerYear] >= 0),
    CONSTRAINT [CK_LeavePolicy_Accrual] CHECK ([Accrual] IN ('Upfront', 'Monthly')),
    CONSTRAINT [CK_LeavePolicy_MinServiceMonths] CHECK ([MinServiceMonths] >= 0),
    CONSTRAINT [CK_LeavePolicy_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

-- One active policy per leave type, grade and employment type. A unique index treats NULLs as equal,
-- matching the mock's null === null comparison.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_LeavePolicy_ActiveGroup]
    ON [lve].[LeavePolicy] ([LeaveTypeId], [JobGradeId], [EmploymentType])
    WHERE [Status] = 'Active';
GO

CREATE NONCLUSTERED INDEX [IX_LeavePolicy_LeaveTypeId] ON [lve].[LeavePolicy] ([LeaveTypeId]);
GO

CREATE NONCLUSTERED INDEX [IX_LeavePolicy_JobGradeId] ON [lve].[LeavePolicy] ([JobGradeId]);
