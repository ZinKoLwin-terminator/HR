-- ASP.NET Core Identity tokens (IdentityUserToken): password reset, authenticator keys, recovery codes.
-- Values are secrets; owned by the identity user. The primary key also serves as the FK index.
CREATE TABLE [auth].[UserToken]
(
    [UserId]        nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name]          nvarchar(128) NOT NULL,
    [Value]         nvarchar(max) NULL,
    CONSTRAINT [PK_UserToken] PRIMARY KEY CLUSTERED ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_UserToken_User] FOREIGN KEY ([UserId]) REFERENCES [auth].[User] ([Id]) ON DELETE CASCADE
);
