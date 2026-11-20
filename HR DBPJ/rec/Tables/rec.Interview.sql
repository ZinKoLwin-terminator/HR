CREATE TABLE [rec].[Interview]
(
    [Id]                    int            IDENTITY (1, 1) NOT NULL,
    [ApplicationId]         int            NOT NULL,
    [Round]                 varchar(30)    NOT NULL,
    [Date]                  date           NOT NULL,
    [StartTime]             time(0)        NOT NULL,
    [DurationMinutes]       smallint       NOT NULL,
    [Mode]                  varchar(30)    NOT NULL,
    [InterviewerEmployeeId] int            NOT NULL,
    [Location]              nvarchar(120)  NULL,
    [Status]                varchar(30)    NOT NULL CONSTRAINT [DF_Interview_Status] DEFAULT ('Scheduled'),
    [Score]                 tinyint        NULL,
    [Recommendation]        varchar(30)    NULL,
    [Feedback]              nvarchar(500)  NULL,
    [CreatedAtUtc]          datetime2(3)   NOT NULL CONSTRAINT [DF_Interview_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]       int            NULL,
    [ModifiedAtUtc]         datetime2(3)   NOT NULL CONSTRAINT [DF_Interview_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]      int            NULL,
    [RowVersion]            rowversion     NOT NULL,
    CONSTRAINT [PK_Interview] PRIMARY KEY CLUSTERED ([Id]),
    -- An application with interviews cannot be deleted (ApplicationMockService.beforeDelete).
    CONSTRAINT [FK_Interview_Application] FOREIGN KEY ([ApplicationId]) REFERENCES [rec].[Application] ([Id]),
    CONSTRAINT [FK_Interview_Employee_InterviewerEmployeeId] FOREIGN KEY ([InterviewerEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [CK_Interview_Round] CHECK ([Round] IN ('Phone screen', 'Technical', 'HR', 'Final')),
    CONSTRAINT [CK_Interview_DurationMinutes] CHECK ([DurationMinutes] BETWEEN 15 AND 240),
    CONSTRAINT [CK_Interview_Mode] CHECK ([Mode] IN ('Onsite', 'Video', 'Phone')),
    CONSTRAINT [CK_Interview_Status] CHECK ([Status] IN ('Scheduled', 'Completed', 'Cancelled')),
    CONSTRAINT [CK_Interview_Score] CHECK ([Score] BETWEEN 1 AND 5),
    CONSTRAINT [CK_Interview_Recommendation] CHECK ([Recommendation] IN ('Strong hire', 'Hire', 'No hire')),
    -- Completed interviews need a score and a recommendation; others have neither.
    CONSTRAINT [CK_Interview_CompletedOutcome] CHECK (([Status] = 'Completed' AND [Score] IS NOT NULL AND [Recommendation] IS NOT NULL) OR ([Status] <> 'Completed' AND [Score] IS NULL AND [Recommendation] IS NULL))
);
GO

CREATE NONCLUSTERED INDEX [IX_Interview_ApplicationId] ON [rec].[Interview] ([ApplicationId]) INCLUDE ([Status], [Recommendation]);
GO

-- Interviewer double-booking check and the interviewer's schedule.
CREATE NONCLUSTERED INDEX [IX_Interview_InterviewerEmployeeId_Date]
    ON [rec].[Interview] ([InterviewerEmployeeId], [Date])
    INCLUDE ([StartTime], [DurationMinutes], [Status]);
