CREATE TABLE [org].[Company]
(
    [Id]                 int            IDENTITY (1, 1) NOT NULL,
    [Code]               nvarchar(20)   NOT NULL,
    [Name]               nvarchar(100)  NOT NULL,
    [LegalName]          nvarchar(150)  NOT NULL,
    [RegistrationNumber] nvarchar(40)   NULL,
    [TaxId]              nvarchar(40)   NULL,
    [Industry]           nvarchar(80)   NOT NULL,
    [Email]              nvarchar(120)  NOT NULL,
    [Phone]              nvarchar(25)   NOT NULL,
    [Website]            nvarchar(150)  NULL,
    [Address]            nvarchar(200)  NULL,
    [City]               nvarchar(60)   NOT NULL,
    [Country]            nvarchar(60)   NOT NULL,
    [FoundedDate]        date           NOT NULL,
    [Status]             varchar(30)    NOT NULL CONSTRAINT [DF_Company_Status] DEFAULT ('Active'),
    [CreatedAtUtc]       datetime2(3)   NOT NULL CONSTRAINT [DF_Company_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]    int            NULL,
    [ModifiedAtUtc]      datetime2(3)   NOT NULL CONSTRAINT [DF_Company_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]   int            NULL,
    [RowVersion]         rowversion     NOT NULL,
    CONSTRAINT [PK_Company] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Company_Code] UNIQUE ([Code]),
    CONSTRAINT [UQ_Company_Name] UNIQUE ([Name]),
    -- Upper-case letters, digits and dashes, starting with a letter or digit (HrmsValidators.code).
    CONSTRAINT [CK_Company_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Company_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
