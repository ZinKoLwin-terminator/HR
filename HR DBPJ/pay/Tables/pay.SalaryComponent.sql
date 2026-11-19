-- Reusable pay element (allowance or deduction). SAMPLE rules only, not statutory calculations.
CREATE TABLE [pay].[SalaryComponent]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Code]             nvarchar(20)   NOT NULL,
    [Name]             nvarchar(60)   NOT NULL,
    [Type]             varchar(30)    NOT NULL,
    [Calculation]      varchar(30)    NOT NULL,
    -- Amount for Fixed, otherwise a percentage (0-100).
    [Value]            decimal(18, 2) NOT NULL,
    [Description]      nvarchar(250)  NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_SalaryComponent_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_SalaryComponent_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_SalaryComponent_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_SalaryComponent] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_SalaryComponent_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_SalaryComponent_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_SalaryComponent_Type] CHECK ([Type] IN ('Earning', 'Deduction')),
    CONSTRAINT [CK_SalaryComponent_Calculation] CHECK ([Calculation] IN ('Fixed', 'PercentOfBasic', 'PercentOfGross')),
    CONSTRAINT [CK_SalaryComponent_Value] CHECK ([Value] > 0 AND ([Calculation] = 'Fixed' OR [Value] <= 100)),
    CONSTRAINT [CK_SalaryComponent_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
