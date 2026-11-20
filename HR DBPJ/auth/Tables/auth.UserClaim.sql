-- ASP.NET Core Identity user claims (IdentityUserClaim). Owned by the identity user.
CREATE TABLE [auth].[UserClaim]
(
    [Id]         int           IDENTITY (1, 1) NOT NULL,
    [UserId]     nvarchar(450) NOT NULL,
    [ClaimType]  nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_UserClaim] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_UserClaim_User] FOREIGN KEY ([UserId]) REFERENCES [auth].[User] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_UserClaim_UserId] ON [auth].[UserClaim] ([UserId]);
