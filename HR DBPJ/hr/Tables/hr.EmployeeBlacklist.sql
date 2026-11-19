-- Do-not-rehire list: a former employee HR does not want hired again. The reason is sensitive; it is never written to the
-- activity feed or shown outside the blacklist permissions. History of added and lifted entries is kept: a lifted entry stays.
CREATE TABLE [hr].[EmployeeBlacklist]
(
    [Id]                 int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]         int            NOT NULL,
    [Reason]             nvarchar(500)  NOT NULL,
    [BlacklistedOn]      date           NOT NULL,
    [BlacklistedByUserId] int           NOT NULL,
    -- Display-name snapshot of the user at the time (kept even if the user is later removed).
    [BlacklistedByName]  nvarchar(130)  NOT NULL,
    -- The four lift columns are all null (active entry) or all set (lifted entry).
    [LiftedOn]           date           NULL,
    [LiftedByUserId]     int            NULL,
    [LiftedByName]       nvarchar(130)  NULL,
    [LiftReason]         nvarchar(500)  NULL,
    [CreatedAtUtc]       datetime2(3)   NOT NULL CONSTRAINT [DF_EmployeeBlacklist_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]    int            NULL,
    [ModifiedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_EmployeeBlacklist_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]   int            NULL,
    [RowVersion]         rowversion     NOT NULL,
    [ValidFromUtc]       datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]         datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_EmployeeBlacklist] PRIMARY KEY CLUSTERED ([Id]),
    -- No cascade: an employee with blacklist history cannot be deleted (the API checks it first, the key is the backstop).
    CONSTRAINT [FK_EmployeeBlacklist_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_EmployeeBlacklist_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [CK_EmployeeBlacklist_Lift] CHECK (
        ([LiftedOn] IS NULL     AND [LiftedByUserId] IS NULL     AND [LiftedByName] IS NULL     AND [LiftReason] IS NULL)
     OR ([LiftedOn] IS NOT NULL AND [LiftedByUserId] IS NOT NULL AND [LiftedByName] IS NOT NULL AND [LiftReason] IS NOT NULL AND LEN(LTRIM(RTRIM([LiftReason]))) > 0)),
    CONSTRAINT [CK_EmployeeBlacklist_LiftedOn] CHECK ([LiftedOn] IS NULL OR [LiftedOn] >= [BlacklistedOn])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hr].[EmployeeBlacklistHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- At most one active (not lifted) entry per employee; also the FK index for the employee.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_EmployeeBlacklist_ActiveEmployee]
    ON [hr].[EmployeeBlacklist] ([EmployeeId])
    WHERE [LiftedOn] IS NULL;
GO

CREATE NONCLUSTERED INDEX [IX_EmployeeBlacklist_EmployeeId] ON [hr].[EmployeeBlacklist] ([EmployeeId]);
