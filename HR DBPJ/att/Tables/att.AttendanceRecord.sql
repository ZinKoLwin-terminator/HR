-- One row per employee per day. Minutes and status are derived by the attendance rules (attendance-calc.ts).
CREATE TABLE [att].[AttendanceRecord]
(
    [Id]                bigint         IDENTITY (1, 1) NOT NULL,
    [EmployeeId]        int            NOT NULL,
    [Date]              date           NOT NULL,
    [ShiftId]           int            NULL,
    [CheckIn]           time(0)        NULL,
    [CheckOut]          time(0)        NULL,
    [WorkedMinutes]     smallint       NOT NULL CONSTRAINT [DF_AttendanceRecord_WorkedMinutes] DEFAULT (0),
    [LateMinutes]       smallint       NOT NULL CONSTRAINT [DF_AttendanceRecord_LateMinutes] DEFAULT (0),
    [EarlyLeaveMinutes] smallint       NOT NULL CONSTRAINT [DF_AttendanceRecord_EarlyLeaveMinutes] DEFAULT (0),
    [OvertimeMinutes]   smallint       NOT NULL CONSTRAINT [DF_AttendanceRecord_OvertimeMinutes] DEFAULT (0),
    [Status]            varchar(30)    NOT NULL,
    [Source]            varchar(30)    NOT NULL,
    [Remarks]           nvarchar(250)  NULL,
    [CreatedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_AttendanceRecord_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int            NULL,
    [ModifiedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_AttendanceRecord_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int            NULL,
    [RowVersion]        rowversion     NOT NULL,
    CONSTRAINT [PK_AttendanceRecord] PRIMARY KEY CLUSTERED ([Id]),
    -- One record per employee and day (punch finds the record by employee + date); also the FK index.
    CONSTRAINT [UQ_AttendanceRecord_EmployeeId_Date] UNIQUE ([EmployeeId], [Date]),
    CONSTRAINT [FK_AttendanceRecord_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [FK_AttendanceRecord_Shift] FOREIGN KEY ([ShiftId]) REFERENCES [att].[Shift] ([Id]),
    CONSTRAINT [CK_AttendanceRecord_Minutes] CHECK ([WorkedMinutes] >= 0 AND [LateMinutes] >= 0 AND [EarlyLeaveMinutes] >= 0 AND [OvertimeMinutes] >= 0),
    CONSTRAINT [CK_AttendanceRecord_Status] CHECK ([Status] IN ('Present', 'Absent', 'Late', 'Half Day', 'On Leave', 'Holiday', 'Weekend')),
    CONSTRAINT [CK_AttendanceRecord_Source] CHECK ([Source] IN ('Biometric', 'Web', 'Mobile', 'Manual'))
);
GO

CREATE NONCLUSTERED INDEX [IX_AttendanceRecord_Date] ON [att].[AttendanceRecord] ([Date]) INCLUDE ([EmployeeId], [Status]);
GO

CREATE NONCLUSTERED INDEX [IX_AttendanceRecord_ShiftId] ON [att].[AttendanceRecord] ([ShiftId]);
