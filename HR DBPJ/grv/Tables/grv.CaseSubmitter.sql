-- Who submitted a case, kept apart from grv.Case (which the whole case team reads).
-- A named case stores the employee; an anonymous case stores only HMACSHA256(secret, employee id), so the
-- submitter can find their own case again while nobody can read who it is from the database. The secret is
-- the API's Grievance:PseudonymKey and never lives in the database. Named or anonymous is fixed when the draft is created (the
-- API refuses changing it), so a row never switches between the employee id and the pseudonym. Not system-versioned.
-- SubmitterSeenAtUtc is the submitter's read receipt (unread badge against grv.Case.LastSubmitterUpdateAtUtc). It is
-- kept here so that reading a case never changes the case row: no row version change (no 409 for the case team)
-- and no timing signal on the case row.
CREATE TABLE [grv].[CaseSubmitter]
(
    [CaseId]             int           NOT NULL,
    [EmployeeId]         int           NULL,
    [SubmitterPseudonym] binary(32)    NULL,
    [SubmitterSeenAtUtc] datetime2(3)  NULL,
    CONSTRAINT [PK_CaseSubmitter] PRIMARY KEY CLUSTERED ([CaseId]),
    -- Removed with a deleted draft (a submitted case cannot be deleted: its status history refers to it).
    CONSTRAINT [FK_CaseSubmitter_Case] FOREIGN KEY ([CaseId]) REFERENCES [grv].[Case] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CaseSubmitter_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    -- Exactly one of the two.
    CONSTRAINT [CK_CaseSubmitter_OneIdentity] CHECK (([EmployeeId] IS NOT NULL AND [SubmitterPseudonym] IS NULL)
                                                  OR ([EmployeeId] IS NULL AND [SubmitterPseudonym] IS NOT NULL))
);
GO

-- "My grievances" is two seeks combined with UNION ALL: EmployeeId = me, and SubmitterPseudonym = HMAC(me).
CREATE NONCLUSTERED INDEX [IX_CaseSubmitter_EmployeeId] ON [grv].[CaseSubmitter] ([EmployeeId]) WHERE [EmployeeId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_CaseSubmitter_SubmitterPseudonym] ON [grv].[CaseSubmitter] ([SubmitterPseudonym]) WHERE [SubmitterPseudonym] IS NOT NULL;
