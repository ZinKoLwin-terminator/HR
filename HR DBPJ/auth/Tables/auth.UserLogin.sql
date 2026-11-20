-- ASP.NET Core Identity external logins (IdentityUserLogin). Owned by the identity user.
CREATE TABLE [auth].[UserLogin]
(
    [LoginProvider]       nvarchar(128) NOT NULL,
    [ProviderKey]         nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId]              nvarchar(450) NOT NULL,
    CONSTRAINT [PK_UserLogin] PRIMARY KEY CLUSTERED ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_UserLogin_User] FOREIGN KEY ([UserId]) REFERENCES [auth].[User] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_UserLogin_UserId] ON [auth].[UserLogin] ([UserId]);
