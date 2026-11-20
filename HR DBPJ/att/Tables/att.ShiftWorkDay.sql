-- Working weekdays of a shift (Shift.workDays): 0 = Sunday ... 6 = Saturday.
CREATE TABLE [att].[ShiftWorkDay]
(
    [ShiftId]   int     NOT NULL,
    [DayOfWeek] tinyint NOT NULL,
    CONSTRAINT [PK_ShiftWorkDay] PRIMARY KEY CLUSTERED ([ShiftId], [DayOfWeek]),
    CONSTRAINT [FK_ShiftWorkDay_Shift] FOREIGN KEY ([ShiftId]) REFERENCES [att].[Shift] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_ShiftWorkDay_DayOfWeek] CHECK ([DayOfWeek] BETWEEN 0 AND 6)
);
