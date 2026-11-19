-- One row per granted (role, module, action). A module with no rows is not granted.
CREATE TABLE [sec].[RolePermission]
(
    [RoleId]          int          NOT NULL,
    [ModuleKey]       varchar(30)  NOT NULL,
    [Action]          varchar(30)  NOT NULL,
    -- Constant used by FK_RolePermission_ViewGrant: every grant needs the View grant of its module.
    [ViewAction]      AS CONVERT(varchar(30), 'View') PERSISTED NOT NULL,
    [CreatedAtUtc]    datetime2(3) NOT NULL CONSTRAINT [DF_RolePermission_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId] int          NULL,
    CONSTRAINT [PK_RolePermission] PRIMARY KEY CLUSTERED ([RoleId], [ModuleKey], [Action]),
    CONSTRAINT [FK_RolePermission_Role] FOREIGN KEY ([RoleId]) REFERENCES [sec].[Role] ([Id]) ON DELETE CASCADE,
    -- Only actions defined for the module can be granted.
    CONSTRAINT [FK_RolePermission_PermissionModuleAction] FOREIGN KEY ([ModuleKey], [Action]) REFERENCES [sec].[PermissionModuleAction] ([ModuleKey], [Action]),
    CONSTRAINT [FK_RolePermission_ViewGrant] FOREIGN KEY ([RoleId], [ModuleKey], [ViewAction]) REFERENCES [sec].[RolePermission] ([RoleId], [ModuleKey], [Action])
);
GO

CREATE NONCLUSTERED INDEX [IX_RolePermission_ModuleKey_Action] ON [sec].[RolePermission] ([ModuleKey], [Action]);
