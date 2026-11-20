-- Request to correct an attendance record; approval applies it to the record.
CREATE TABLE [att].[AttendanceAdjustment]
(
    [Id]                int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]        int            NOT NULL,
    [Date]              date           NOT NULL,
    -- Times on the record when the request was saved (snapshot).
    [CurrentCheckIn]    time(0)        NULL,
    [CurrentCheckOut]   time(0)        NULL,
    [RequestedCheckIn]  time(0)        NULL,
    [RequestedCheckOut] time(0)        NULL,
    [RequestedStatus]   varchar(30)    NULL,
    [Reason]            nvarchar(250)  NOT NULL,
    [Status]            varchar(30)    NOT NULL CONSTRAINT [DF_AttendanceAdjustment_Status] DEFAULT ('Pending'),
    [RequestedOn]       date           NOT NULL,
    [DecidedByUserId]   int            NULL,
    [DecidedByName]     nvarchar(130)  NULL,
    [DecidedOn]         date           NULL,
    [Remarks]           nvarchar(250)  NULL,
    [CreatedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_AttendanceAdjustment_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int            NULL,
    [ModifiedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_AttendanceAdjustment_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int            NULL,
    [RowVersion]        rowversion     NOT NULL,
    CONSTRAINT [PK_AttendanceAdjustment] PRIMARY KEY CLUSTERED ([Id]),
    -- The record being corrected must exist; the API resolves recordId through this key.
    CONSTRAINT [FK_AttendanceAdjustment_AttendanceRecord] FOREIGN KEY ([EmployeeId], [Date]) REFERENCES [att].[AttendanceRecord] ([EmployeeId], [Date]),
    CONSTRAINT [CK_AttendanceAdjustment_RequestedStatus] CHECK ([RequestedStatus] IN ('Present', 'Absent', 'Late', 'Half Day', 'On Leave', 'Holiday', 'Weekend')),
    CONSTRAINT [CK_AttendanceAdjustment_SomethingRequested] CHECK ([RequestedCheckIn] IS NOT NULL OR [RequestedCheckOut] IS NOT NULL OR [RequestedStatus] IS NOT NULL),
    CONSTRAINT [CK_AttendanceAdjustment_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
    -- Pending requests have no decision; decided ones record who and when.
    CONSTRAINT [CK_AttendanceAdjustment_Decision] CHECK (([Status] = 'Pending' AND [DecidedOn] IS NULL AND [DecidedByName] IS NULL) OR ([Status] <> 'Pending' AND [DecidedOn] IS NOT NULL AND [DecidedByName] IS NOT NULL))
);
GO

-- At most one pending request per employee and date.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_AttendanceAdjustment_EmployeeId_Date_Pending]
    ON [att].[AttendanceAdjustment] ([EmployeeId], [Date])
    WHERE [Status] = 'Pending';
GO

-- Unfiltered index for the FK and for history lookups.
CREATE NONCLUSTERED INDEX [IX_AttendanceAdjustment_EmployeeId_Date] ON [att].[AttendanceAdjustment] ([EmployeeId], [Date]);
GO

CREATE NONCLUSTERED INDEX [IX_AttendanceAdjustment_Status] ON [att].[AttendanceAdjustment] ([Status]) INCLUDE ([EmployeeId], [Date]);
