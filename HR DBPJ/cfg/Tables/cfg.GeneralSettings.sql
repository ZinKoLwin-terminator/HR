-- Single-row table: the organization-wide general settings (row Id = 1, created by post-deployment).
CREATE TABLE [cfg].[GeneralSettings]
(
    [Id]                   int            NOT NULL CONSTRAINT [DF_GeneralSettings_Id] DEFAULT (1),
    [OrganizationName]     nvarchar(80)   NOT NULL,
    [ContactEmail]         nvarchar(120)  NOT NULL,
    [Timezone]             varchar(40)    NOT NULL,
    [DateFormat]           varchar(20)    NOT NULL,
    [CurrencyCode]         char(3)        NOT NULL,
    [FiscalYearStartMonth] tinyint        NOT NULL,
    [EmailNotifications]   bit            NOT NULL,
    [WeeklyDigest]         bit            NOT NULL,
    [CreatedAtUtc]         datetime2(3)   NOT NULL CONSTRAINT [DF_GeneralSettings_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]      int            NULL,
    [ModifiedAtUtc]        datetime2(3)   NOT NULL CONSTRAINT [DF_GeneralSettings_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]     int            NULL,
    [RowVersion]           rowversion     NOT NULL,
    CONSTRAINT [PK_GeneralSettings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_GeneralSettings_Singleton] CHECK ([Id] = 1),
    CONSTRAINT [CK_GeneralSettings_OrganizationName] CHECK (LEN(LTRIM([OrganizationName])) > 0),
    -- Approximates /^[^\s@]+@[^\s@]+\.[^\s@]+$/ from GeneralSettingsMockService.save.
    CONSTRAINT [CK_GeneralSettings_ContactEmail] CHECK ([ContactEmail] LIKE N'_%@_%._%' AND [ContactEmail] NOT LIKE N'%@%@%' AND [ContactEmail] NOT LIKE N'% %'),
    CONSTRAINT [CK_GeneralSettings_Timezone] CHECK ([Timezone] IN ('Asia/Yangon', 'Asia/Bangkok', 'Asia/Singapore', 'Asia/Kolkata', 'Europe/London', 'America/New_York', 'UTC')),
    CONSTRAINT [CK_GeneralSettings_DateFormat] CHECK ([DateFormat] IN ('yyyy-MM-dd', 'dd/MM/yyyy', 'MM/dd/yyyy', 'dd MMM yyyy')),
    CONSTRAINT [CK_GeneralSettings_CurrencyCode] CHECK ([CurrencyCode] LIKE '[A-Z][A-Z][A-Z]' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_GeneralSettings_FiscalYearStartMonth] CHECK ([FiscalYearStartMonth] BETWEEN 1 AND 12)
);
