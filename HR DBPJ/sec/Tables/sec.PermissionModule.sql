-- Reference data maintained by the post-deployment script (PERMISSION_MODULES in settings.models.ts).
CREATE TABLE [sec].[PermissionModule]
(
    [ModuleKey] varchar(30)  NOT NULL,
    [Label]     nvarchar(60) NOT NULL,
    [SortOrder] smallint     NOT NULL,
    CONSTRAINT [PK_PermissionModule] PRIMARY KEY CLUSTERED ([ModuleKey]),
    CONSTRAINT [UQ_PermissionModule_SortOrder] UNIQUE ([SortOrder])
);
