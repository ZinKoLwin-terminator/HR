-- ASP.NET Core Identity user (IdentityUser shape, no roles: roles and permissions live in sec).
-- Holds credentials: never seeded, never granted to reporting. Accounts are created by the API.
-- The application account is sec.AppUser, linked through sec.AppUser.ExternalSubject.
CREATE TABLE [auth].[User]
(
    -- Identity user id (a GUID string).
    [Id]                   nvarchar(450)     NOT NULL,
    [UserName]             nvarchar(256)     NULL,
    [NormalizedUserName]   nvarchar(256)     NULL,
    [Email]                nvarchar(256)     NULL,
    [NormalizedEmail]      nvarchar(256)     NULL,
    [EmailConfirmed]       bit               NOT NULL CONSTRAINT [DF_User_EmailConfirmed] DEFAULT (0),
    -- Identity password hash (salted, versioned format); never a plain password.
    [PasswordHash]         nvarchar(max)     NULL,
    [SecurityStamp]        nvarchar(max)     NULL,
    [ConcurrencyStamp]     nvarchar(max)     NULL,
    [PhoneNumber]          nvarchar(50)      NULL,
    [PhoneNumberConfirmed] bit               NOT NULL CONSTRAINT [DF_User_PhoneNumberConfirmed] DEFAULT (0),
    [TwoFactorEnabled]     bit               NOT NULL CONSTRAINT [DF_User_TwoFactorEnabled] DEFAULT (0),
    [LockoutEnd]           datetimeoffset(7) NULL,
    [LockoutEnabled]       bit               NOT NULL CONSTRAINT [DF_User_LockoutEnabled] DEFAULT (1),
    [AccessFailedCount]    int               NOT NULL CONSTRAINT [DF_User_AccessFailedCount] DEFAULT (0),
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id])
);
GO

-- Sign-in lookup by user name (Identity's UserNameIndex); users without a name are exempt.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_User_NormalizedUserName]
    ON [auth].[User] ([NormalizedUserName])
    WHERE [NormalizedUserName] IS NOT NULL;
GO

-- Lookup by email (Identity's EmailIndex); not unique at this level, as in Identity.
CREATE NONCLUSTERED INDEX [IX_User_NormalizedEmail] ON [auth].[User] ([NormalizedEmail]);
