-- Actions that make sense for each module. Reference data maintained by the post-deployment script.
CREATE TABLE [sec].[PermissionModuleAction]
(
    [ModuleKey] varchar(30) NOT NULL,
    [Action]    varchar(30) NOT NULL,
    [SortOrder] smallint    NOT NULL,
    CONSTRAINT [PK_PermissionModuleAction] PRIMARY KEY CLUSTERED ([ModuleKey], [Action]),
    CONSTRAINT [FK_PermissionModuleAction_PermissionModule] FOREIGN KEY ([ModuleKey]) REFERENCES [sec].[PermissionModule] ([ModuleKey]),
    -- Permission enum in the API (Authorization\PermissionModule.cs) plus Approve for the workflow modules.
    CONSTRAINT [CK_PermissionModuleAction_Action] CHECK ([Action] IN ('View', 'Create', 'Edit', 'Delete', 'Approve',
                                                                     'ViewSensitive', 'EditSensitive', 'ViewBank', 'EditBank', 'ViewBlacklist', 'EditBlacklist',
                                                                     'Import', 'Export'))
);
