CREATE TABLE [att].[OvertimeRecord]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]       int            NOT NULL,
    [Date]             date           NOT NULL,
    [StartTime]        time(0)        NOT NULL,
    [EndTime]          time(0)        NOT NULL,
    [Minutes]          smallint       NOT NULL,
    [Reason]           nvarchar(120)  NOT NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_OvertimeRecord_Status] DEFAULT ('Pending'),
    [RequestedOn]      date           NOT NULL,
    [DecidedByUserId]  int            NULL,
    [DecidedByName]    nvarchar(130)  NULL,
    [DecidedOn]        date           NULL,
    [Remarks]          nvarchar(250)  NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_OvertimeRecord_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_OvertimeRecord_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_OvertimeRecord] PRIMARY KEY CLUSTERED ([Id]),
    -- One overtime entry per employee and day; also the FK index.
    CONSTRAINT [UQ_OvertimeRecord_EmployeeId_Date] UNIQUE ([EmployeeId], [Date]),
    CONSTRAINT [FK_OvertimeRecord_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    -- Minutes = end - start, rolling over midnight (minutesBetween), between 15 minutes and 12 hours.
    CONSTRAINT [CK_OvertimeRecord_Minutes] CHECK ([Minutes] = (DATEDIFF(MINUTE, [StartTime], [EndTime]) + 1440) % 1440 AND [Minutes] BETWEEN 15 AND 720),
    CONSTRAINT [CK_OvertimeRecord_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT [CK_OvertimeRecord_Decision] CHECK (([Status] = 'Pending' AND [DecidedOn] IS NULL AND [DecidedByName] IS NULL) OR ([Status] <> 'Pending' AND [DecidedOn] IS NOT NULL AND [DecidedByName] IS NOT NULL))
);
GO

-- Payroll sums approved overtime per period.
CREATE NONCLUSTERED INDEX [IX_OvertimeRecord_Status_Date] ON [att].[OvertimeRecord] ([Status], [Date]) INCLUDE ([EmployeeId], [Minutes]);
