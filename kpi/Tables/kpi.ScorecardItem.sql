-- One assigned KPI on a scorecard, with the employee's result. The template's code, name, target,
-- weight and scoring rules are copied when the KPI is assigned (historical snapshot): later template
-- edits never change an existing scorecard. The total weight must be 100 before the plan is
-- finalized (checked by the API, which owns the cross-row rule).
CREATE TABLE [kpi].[ScorecardItem]
(
    [Id]                int             IDENTITY (1, 1) NOT NULL,
    [ScorecardId]       int             NOT NULL,
    [TemplateId]        int             NOT NULL,
    [CategoryId]        int             NOT NULL,
    [KpiCode]           nvarchar(20)    NOT NULL,
    [KpiName]           nvarchar(150)   NOT NULL,
    [MeasurementType]   varchar(30)     NOT NULL,
    [Unit]              nvarchar(20)    NULL,
    [Direction]         varchar(30)     NOT NULL,
    [ScoringMethod]     varchar(30)     NOT NULL,
    [Target]            decimal(18, 4)  NOT NULL,
    [RangeMin]          decimal(18, 4)  NULL,
    [RangeMax]          decimal(18, 4)  NULL,
    [MaxRating]         decimal(18, 4)  NULL,
    [Weight]            decimal(5, 2)   NOT NULL,
    [MaxAchievementPct] decimal(8, 2)   NULL,
    [StartDate]         date            NOT NULL,
    [EndDate]           date            NOT NULL,
    [SortOrder]         smallint        NOT NULL CONSTRAINT [DF_ScorecardItem_SortOrder] DEFAULT (0),
    [AssignedByUserId]  int             NULL,
    [AssignedOn]        date            NOT NULL,
    -- Employee's result.
    [ActualValue]       decimal(18, 4)  NULL,
    [EmployeeComment]   nvarchar(1000)  NULL,
    -- Calculated by the API when results are saved.
    [AchievementPct]    decimal(8, 2)   NULL,
    [WeightedScore]     decimal(8, 2)   NULL,
    -- Manager's review of this KPI.
    [ManagerRating]     decimal(4, 2)   NULL,
    [ManagerComment]    nvarchar(1000)  NULL,
    [CreatedAtUtc]      datetime2(3)    NOT NULL CONSTRAINT [DF_ScorecardItem_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]   int             NULL,
    [ModifiedAtUtc]     datetime2(3)    NOT NULL CONSTRAINT [DF_ScorecardItem_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]  int             NULL,
    [RowVersion]        rowversion      NOT NULL,
    -- System-versioned: earlier results (before a return and resubmission) are never lost.
    [ValidFromUtc]      datetime2(7)    GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]        datetime2(7)    GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_ScorecardItem] PRIMARY KEY CLUSTERED ([Id]),
    -- Duplicate prevention: a template is assigned once per scorecard.
    CONSTRAINT [UQ_ScorecardItem_ScorecardId_TemplateId] UNIQUE ([ScorecardId], [TemplateId]),
    -- Items are removed with a scorecard that is still being planned (the API refuses later deletes).
    CONSTRAINT [FK_ScorecardItem_Scorecard] FOREIGN KEY ([ScorecardId]) REFERENCES [kpi].[Scorecard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ScorecardItem_Template] FOREIGN KEY ([TemplateId]) REFERENCES [kpi].[Template] ([Id]),
    CONSTRAINT [FK_ScorecardItem_Category] FOREIGN KEY ([CategoryId]) REFERENCES [kpi].[Category] ([Id]),
    CONSTRAINT [CK_ScorecardItem_MeasurementType] CHECK ([MeasurementType] IN ('Number', 'Percentage', 'Currency', 'Hours', 'Rating', 'Boolean')),
    CONSTRAINT [CK_ScorecardItem_Direction] CHECK ([Direction] IN ('Higher Is Better', 'Lower Is Better', 'Exact Target', 'Range Based')),
    CONSTRAINT [CK_ScorecardItem_ScoringMethod] CHECK ([ScoringMethod] IN ('Direct Percentage', 'Capped Percentage', 'Rating Based', 'Boolean', 'Range Based')),
    CONSTRAINT [CK_ScorecardItem_Target] CHECK ([Target] >= 0 AND ([Direction] NOT IN ('Higher Is Better', 'Exact Target') OR [Target] > 0)),
    CONSTRAINT [CK_ScorecardItem_Weight] CHECK ([Weight] > 0 AND [Weight] <= 100),
    CONSTRAINT [CK_ScorecardItem_MaxAchievementPct] CHECK ([MaxAchievementPct] IS NULL OR ([MaxAchievementPct] >= 1 AND [MaxAchievementPct] <= 500)),
    CONSTRAINT [CK_ScorecardItem_Capped] CHECK ([ScoringMethod] <> 'Capped Percentage' OR [MaxAchievementPct] IS NOT NULL),
    CONSTRAINT [CK_ScorecardItem_Uncapped] CHECK ([ScoringMethod] <> 'Direct Percentage' OR [MaxAchievementPct] IS NULL),
    CONSTRAINT [CK_ScorecardItem_LowerCapped] CHECK ([Direction] <> 'Lower Is Better' OR [MaxAchievementPct] IS NOT NULL),
    CONSTRAINT [CK_ScorecardItem_Rating] CHECK (([ScoringMethod] = 'Rating Based' AND [MeasurementType] = 'Rating' AND [MaxRating] IS NOT NULL AND [MaxRating] > 0)
                                             OR ([ScoringMethod] <> 'Rating Based' AND [MaxRating] IS NULL)),
    CONSTRAINT [CK_ScorecardItem_Boolean] CHECK (([ScoringMethod] = 'Boolean' AND [MeasurementType] = 'Boolean')
                                              OR ([ScoringMethod] <> 'Boolean' AND [MeasurementType] <> 'Boolean')),
    CONSTRAINT [CK_ScorecardItem_Range] CHECK (([ScoringMethod] = 'Range Based' AND [Direction] = 'Range Based' AND [RangeMin] IS NOT NULL AND [RangeMax] IS NOT NULL AND [RangeMin] > 0 AND [RangeMax] > [RangeMin])
                                            OR ([ScoringMethod] <> 'Range Based' AND [Direction] <> 'Range Based' AND [RangeMin] IS NULL AND [RangeMax] IS NULL)),
    CONSTRAINT [CK_ScorecardItem_DateOrder] CHECK ([EndDate] >= [StartDate]),
    -- Measurement validation of the result.
    CONSTRAINT [CK_ScorecardItem_BooleanActual] CHECK ([MeasurementType] <> 'Boolean' OR [ActualValue] IS NULL OR [ActualValue] IN (0, 1)),
    CONSTRAINT [CK_ScorecardItem_RatingActual] CHECK ([ScoringMethod] <> 'Rating Based' OR [ActualValue] IS NULL OR ([ActualValue] >= 0 AND [ActualValue] <= [MaxRating])),
    -- A result is scored, or neither figure is set.
    CONSTRAINT [CK_ScorecardItem_Score] CHECK (([AchievementPct] IS NULL AND [WeightedScore] IS NULL)
                                            OR ([ActualValue] IS NOT NULL AND [AchievementPct] >= 0 AND [WeightedScore] >= 0)),
    CONSTRAINT [CK_ScorecardItem_ManagerRating] CHECK ([ManagerRating] IS NULL OR ([ManagerRating] >= 1 AND [ManagerRating] <= 5))
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [kpi].[ScorecardItemHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- Template usage (delete protection) and KPI achievement reports.
CREATE NONCLUSTERED INDEX [IX_ScorecardItem_TemplateId]
    ON [kpi].[ScorecardItem] ([TemplateId])
    INCLUDE ([ScorecardId], [AchievementPct]);
GO

CREATE NONCLUSTERED INDEX [IX_ScorecardItem_CategoryId] ON [kpi].[ScorecardItem] ([CategoryId]);
