-- Overall-score bands: a score of at least MinScore gets the label, up to the next band's MinScore.
-- The post-deployment script seeds a default set into an empty table only.
CREATE TABLE [kpi].[RatingBand]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Label]            nvarchar(60)   NOT NULL,
    [MinScore]         decimal(8, 2)  NOT NULL,
    [Description]      nvarchar(300)  NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_RatingBand_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_RatingBand_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_RatingBand_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_RatingBand] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_RatingBand_Label] UNIQUE ([Label]),
    CONSTRAINT [UQ_RatingBand_MinScore] UNIQUE ([MinScore]),
    CONSTRAINT [CK_RatingBand_MinScore] CHECK ([MinScore] >= 0 AND [MinScore] <= 500),
    CONSTRAINT [CK_RatingBand_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
