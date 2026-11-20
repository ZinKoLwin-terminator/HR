-- In-app notification. RecipientUserId NULL means it is shown to every user.
CREATE TABLE [app].[Notification]
(
    [Id]              bigint        IDENTITY (1, 1) NOT NULL,
    [RecipientUserId] int           NULL,
    [Title]           nvarchar(120) NOT NULL,
    [Message]         nvarchar(500) NOT NULL,
    -- Material icon name, e.g. "event_available".
    [Icon]            varchar(40)   NOT NULL,
    -- In-app route, e.g. "/leave/approvals".
    [Link]            nvarchar(300) NULL,
    [CreatedAtUtc]    datetime2(3)  NOT NULL CONSTRAINT [DF_Notification_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [IsRead]          bit           NOT NULL CONSTRAINT [DF_Notification_IsRead] DEFAULT (0),
    [ReadAtUtc]       datetime2(3)  NULL,
    CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id]),
    -- A user's notifications are removed with the user.
    CONSTRAINT [FK_Notification_AppUser_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [sec].[AppUser] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_Notification_ReadAt] CHECK (([IsRead] = 0 AND [ReadAtUtc] IS NULL) OR ([IsRead] = 1 AND [ReadAtUtc] IS NOT NULL))
);
GO

CREATE NONCLUSTERED INDEX [IX_Notification_RecipientUserId_CreatedAtUtc]
    ON [app].[Notification] ([RecipientUserId], [CreatedAtUtc] DESC)
    INCLUDE ([IsRead]);
