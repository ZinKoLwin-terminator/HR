CREATE TABLE [lve].[LeaveApplication]
(
    [Id]                 int            IDENTITY (1, 1) NOT NULL,
    -- LV-yyyy-nnnn, numbered from lve.LeaveApplicationNoSeq by the API.
    [ApplicationNo]      nvarchar(20)   NOT NULL,
    [EmployeeId]         int            NOT NULL,
    [LeaveTypeId]        int            NOT NULL,
    [StartDate]          date           NOT NULL,
    [EndDate]            date           NOT NULL,
    -- Working days requested (weekends and holidays excluded); 0.5 for a half day.
    [Days]               decimal(4, 1)  NOT NULL,
    [HalfDay]            bit            NOT NULL CONSTRAINT [DF_LeaveApplication_HalfDay] DEFAULT (0),
    [Reason]             nvarchar(500)  NOT NULL,
    [DocumentName]       nvarchar(260)  NULL,
    [Status]             varchar(30)    NOT NULL CONSTRAINT [DF_LeaveApplication_Status] DEFAULT ('Draft'),
    [AppliedOn]          date           NOT NULL,
    -- Employee who approved or rejected (NULL when decided by the built-in administrator).
    [ApproverEmployeeId] int            NULL,
    [ActionedByUserId]   int            NULL,
    [ActionedByName]     nvarchar(130)  NULL,
    [ActionedOn]         date           NULL,
    [ApproverRemarks]    nvarchar(250)  NULL,
    [CreatedAtUtc]       datetime2(3)   NOT NULL CONSTRAINT [DF_LeaveApplication_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]    int            NULL,
    [ModifiedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_LeaveApplication_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]   int            NULL,
    [RowVersion]         rowversion     NOT NULL,
    -- System-versioned so every decision (approve, reject, cancel) and who made it is kept.
    [ValidFromUtc]       datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]         datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_LeaveApplication] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_LeaveApplication_ApplicationNo] UNIQUE ([ApplicationNo]),
    CONSTRAINT [FK_LeaveApplication_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [FK_LeaveApplication_LeaveType] FOREIGN KEY ([LeaveTypeId]) REFERENCES [lve].[LeaveType] ([Id]),
    CONSTRAINT [FK_LeaveApplication_Employee_ApproverEmployeeId] FOREIGN KEY ([ApproverEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_LeaveApplication_ApplicationNo] CHECK ([ApplicationNo] LIKE N'LV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]%' COLLATE Latin1_General_BIN2 AND [ApplicationNo] NOT LIKE N'LV-____-%[^0-9]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_LeaveApplication_DateOrder] CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [CK_LeaveApplication_Days] CHECK ([Days] > 0),
    CONSTRAINT [CK_LeaveApplication_HalfDay] CHECK ([HalfDay] = 0 OR ([StartDate] = [EndDate] AND [Days] = 0.5)),
    CONSTRAINT [CK_LeaveApplication_Status] CHECK ([Status] IN ('Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled')),
    -- Draft and submitted applications are not actioned yet; decided or cancelled ones are.
    CONSTRAINT [CK_LeaveApplication_Actioned] CHECK (([Status] IN ('Draft', 'Submitted') AND [ActionedOn] IS NULL) OR ([Status] IN ('Approved', 'Rejected', 'Cancelled') AND [ActionedOn] IS NOT NULL)),
    CONSTRAINT [CK_LeaveApplication_RejectionReason] CHECK ([Status] <> 'Rejected' OR LEN(LTRIM([ApproverRemarks])) > 0),
    -- "You cannot decide your own leave request."
    -- A NULL approver (built-in administrator) bypasses this; the API must also block deciding your own leave via the user -> employee link.
    CONSTRAINT [CK_LeaveApplication_NotOwnApprover] CHECK ([ApproverEmployeeId] <> [EmployeeId])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [lve].[LeaveApplicationHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- Balances, overlap checks and payroll read an employee's leave by date.
CREATE NONCLUSTERED INDEX [IX_LeaveApplication_EmployeeId_StartDate]
    ON [lve].[LeaveApplication] ([EmployeeId], [StartDate])
    INCLUDE ([EndDate], [LeaveTypeId], [Status], [Days], [HalfDay]);
GO

-- Approval queue and team calendar.
CREATE NONCLUSTERED INDEX [IX_LeaveApplication_Status_StartDate]
    ON [lve].[LeaveApplication] ([Status], [StartDate])
    INCLUDE ([EndDate], [EmployeeId], [LeaveTypeId], [HalfDay]);
GO

CREATE NONCLUSTERED INDEX [IX_LeaveApplication_LeaveTypeId] ON [lve].[LeaveApplication] ([LeaveTypeId]);
GO

CREATE NONCLUSTERED INDEX [IX_LeaveApplication_ApproverEmployeeId] ON [lve].[LeaveApplication] ([ApproverEmployeeId]);
