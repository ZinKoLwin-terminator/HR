CREATE TABLE [att].[Holiday]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Date]             date           NOT NULL,
    [Name]             nvarchar(100)  NOT NULL,
    [Type]             varchar(30)    NOT NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Holiday_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Holiday_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Holiday] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_Holiday_Type] CHECK ([Type] IN ('Public', 'Company'))
);
GO

-- Working-day calculations look holidays up by date range.
CREATE NONCLUSTERED INDEX [IX_Holiday_Date] ON [att].[Holiday] ([Date]) INCLUDE ([Name], [Type]);
