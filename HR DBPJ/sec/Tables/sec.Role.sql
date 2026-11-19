CREATE TABLE [sec].[Role]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Name]             nvarchar(60)   NOT NULL,
    [Description]      nvarchar(300)  NULL,
    -- Built-in role: cannot be renamed, deactivated or deleted.
    [IsSystem]         bit            NOT NULL CONSTRAINT [DF_Role_IsSystem] DEFAULT (0),
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_Role_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Role_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Role_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Role_Name] UNIQUE ([Name]),
    CONSTRAINT [CK_Role_Status] CHECK ([Status] IN ('Active', 'Inactive')),
    CONSTRAINT [CK_Role_SystemRoleActive] CHECK ([IsSystem] = 0 OR [Status] = 'Active')
);
