CREATE TABLE [att].[Shift]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [Code]             nvarchar(20)   NOT NULL,
    [Name]             nvarchar(60)   NOT NULL,
    [StartTime]        time(0)        NOT NULL,
    [EndTime]          time(0)        NOT NULL,
    [BreakMinutes]     smallint       NOT NULL,
    [GraceMinutes]     smallint       NOT NULL,
    [IsNightShift]     bit            NOT NULL CONSTRAINT [DF_Shift_IsNightShift] DEFAULT (0),
    [Color]            varchar(7)     NOT NULL,
    [Status]           varchar(30)    NOT NULL CONSTRAINT [DF_Shift_Status] DEFAULT ('Active'),
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_Shift_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_Shift_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_Shift] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Shift_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_Shift_Code] CHECK ([Code] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [Code] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    -- Only a night shift may end at or before its start time (it ends the next day).
    CONSTRAINT [CK_Shift_TimeOrder] CHECK ([IsNightShift] = 1 OR [EndTime] > [StartTime]),
    CONSTRAINT [CK_Shift_BreakMinutes] CHECK ([BreakMinutes] >= 0),
    CONSTRAINT [CK_Shift_GraceMinutes] CHECK ([GraceMinutes] >= 0),
    -- #rrggbb, as produced by the colour picker.
    CONSTRAINT [CK_Shift_Color] CHECK ([Color] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Shift_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
