-- Reusable KPI definition. Assigning a template copies its target, weight and scoring rules into
-- kpi.ScorecardItem, so editing a template never changes scorecards that already use it.
-- Scoring (the API's KPI calculation engine, versioned on kpi.Scorecard.CalculationVersion):
--   Higher Is Better  achievement = actual / target x 100
--   Lower Is Better   achievement = target / actual x 100; needs a cap, which an actual of 0 scores
--   Exact Target      100 at the target, falling by the relative distance from it, never below 0
--   Range Based       100 inside [RangeMin, RangeMax], proportional outside it
--   Rating Based      achievement = actual rating / MaxRating x 100
--   Boolean           achieved (1) = 100, not achieved (0) = 0
-- Direct Percentage is uncapped; every other method is capped at MaxAchievementPct when it is set.
CREATE TABLE [kpi].[Template]
(
    [Id]                int             IDENTITY (1, 1) NOT NULL,
    [Code]              nvarchar(20)    NOT NULL,
    [Name]              nvarchar(150)   NOT NULL,
    [Description]       nvarchar(500)   NULL,
    [CategoryId]        int             NOT NULL,
    -- NULL: available to every department; otherwise a department-specific KPI.
    [DepartmentId]      int             NULL,
    [MeasurementType]   varchar(30)     NOT NULL,
    [Unit]              nvarchar(20)    NULL,
    [Direction]         varchar(30)     NOT NULL,
    [ScoringMethod]     varchar(30)     NOT NULL,
    [DefaultTarget]     decimal(18, 4)  NOT NULL,
    [RangeMin]          decimal(18, 4)  NULL,
    [RangeMax]          decimal(18, 4)  NULL,
    [MaxRating]         decimal(18, 4)  NULL,
    [DefaultWeight]     decimal(5, 2)   NOT NULL,
    [MaxAchievementPct] decimal(8, 2)   NULL,
    [Frequency]         varchar(30)     NOT NULL,
    [Status]            varchar(30)     NOT NULL CONSTRAINT [DF_Template_Status] DEFAULT ('Active'),
    [CreatedAtUtc]      datetime2(3)    NOT NULL CONSTRAINT [DF_Template_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int             NULL,
    [ModifiedAtUtc]     datetime2(3)    NOT NULL CONSTRAINT [DF_Template_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int             NULL,
    [RowVersion]        rowversion      NOT NULL,
    [ValidFromUtc]      datetime2(7)    GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]        datetime2(7)    GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_Template] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Template_Code] UNIQUE ([Code]),
    CONSTRAINT [FK_Template_Category] FOREIGN KEY ([CategoryId]) REFERENCES [kpi].[Category] ([Id]),
    CONSTRAINT [FK_Template_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [org].[Department] ([Id]),
    CONSTRAINT [CK_Template_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Template_MeasurementType] CHECK ([MeasurementType] IN ('Number', 'Percentage', 'Currency', 'Hours', 'Rating', 'Boolean')),
    CONSTRAINT [CK_Template_Direction] CHECK ([Direction] IN ('Higher Is Better', 'Lower Is Better', 'Exact Target', 'Range Based')),
    CONSTRAINT [CK_Template_ScoringMethod] CHECK ([ScoringMethod] IN ('Direct Percentage', 'Capped Percentage', 'Rating Based', 'Boolean', 'Range Based')),
    CONSTRAINT [CK_Template_Frequency] CHECK ([Frequency] IN ('Monthly', 'Quarterly', 'Half-Yearly', 'Yearly', 'Custom')),
    CONSTRAINT [CK_Template_Status] CHECK ([Status] IN ('Active', 'Inactive')),
    -- Division by the target: Higher Is Better and Exact Target need a positive one.
    CONSTRAINT [CK_Template_DefaultTarget] CHECK ([DefaultTarget] >= 0 AND ([Direction] NOT IN ('Higher Is Better', 'Exact Target') OR [DefaultTarget] > 0)),
    CONSTRAINT [CK_Template_DefaultWeight] CHECK ([DefaultWeight] > 0 AND [DefaultWeight] <= 100),
    CONSTRAINT [CK_Template_MaxAchievementPct] CHECK ([MaxAchievementPct] IS NULL OR ([MaxAchievementPct] >= 1 AND [MaxAchievementPct] <= 500)),
    CONSTRAINT [CK_Template_Capped] CHECK ([ScoringMethod] <> 'Capped Percentage' OR [MaxAchievementPct] IS NOT NULL),
    CONSTRAINT [CK_Template_Uncapped] CHECK ([ScoringMethod] <> 'Direct Percentage' OR [MaxAchievementPct] IS NULL),
    -- Target / actual is unbounded as the actual nears 0: the cap is what a perfect (zero) result scores.
    CONSTRAINT [CK_Template_LowerCapped] CHECK ([Direction] <> 'Lower Is Better' OR [MaxAchievementPct] IS NOT NULL),
    CONSTRAINT [CK_Template_Rating] CHECK (([ScoringMethod] = 'Rating Based' AND [MeasurementType] = 'Rating' AND [MaxRating] IS NOT NULL AND [MaxRating] > 0)
                                        OR ([ScoringMethod] <> 'Rating Based' AND [MaxRating] IS NULL)),
    CONSTRAINT [CK_Template_Boolean] CHECK (([ScoringMethod] = 'Boolean' AND [MeasurementType] = 'Boolean')
                                         OR ([ScoringMethod] <> 'Boolean' AND [MeasurementType] <> 'Boolean')),
    CONSTRAINT [CK_Template_Range] CHECK (([ScoringMethod] = 'Range Based' AND [Direction] = 'Range Based' AND [RangeMin] IS NOT NULL AND [RangeMax] IS NOT NULL AND [RangeMin] > 0 AND [RangeMax] > [RangeMin])
                                       OR ([ScoringMethod] <> 'Range Based' AND [Direction] <> 'Range Based' AND [RangeMin] IS NULL AND [RangeMax] IS NULL))
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [kpi].[TemplateHistory], DATA_CONSISTENCY_CHECK = ON));
GO

CREATE NONCLUSTERED INDEX [IX_Template_CategoryId] ON [kpi].[Template] ([CategoryId]);
GO

CREATE NONCLUSTERED INDEX [IX_Template_DepartmentId] ON [kpi].[Template] ([DepartmentId]);
