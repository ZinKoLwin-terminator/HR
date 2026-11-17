-- Recent-activity feed. Append-only: rows are inserted, never updated.
CREATE TABLE [app].[ActivityLog]
(
    [Id]            bigint        IDENTITY (1, 1) NOT NULL,
    [Category]      varchar(30)   NOT NULL,
    [Message]       nvarchar(500) NOT NULL,
    [ActorUserId]   int           NULL,
    -- Display name of the actor at the time (kept even if the user is later removed).
    [ActorName]     nvarchar(130) NOT NULL,
    [OccurredAtUtc] datetime2(3)  NOT NULL CONSTRAINT [DF_ActivityLog_OccurredAtUtc] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ActivityLog] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_ActivityLog_Category] CHECK ([Category] IN ('employee', 'organization', 'attendance', 'leave', 'payroll', 'recruitment', 'performance', 'system'))
);
GO

-- Dashboard reads the most recent entries.
CREATE NONCLUSTERED INDEX [IX_ActivityLog_OccurredAtUtc] ON [app].[ActivityLog] ([OccurredAtUtc] DESC);
