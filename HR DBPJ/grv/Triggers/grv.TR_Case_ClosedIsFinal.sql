-- A Closed grievance case is final. Allowed: reopening it (Closed -> Reopened; the case content stays as it was),
-- and, while it stays Closed, only the archive and retention columns (ArchivedAtUtc, RetentionUntil, LegalHold) and
-- the modification stamp. (The submitter's read receipt lives in grv.CaseSubmitter.) A Closed case is never deleted
-- (archive it instead), never inserted directly, and is reached only from Resolved.
CREATE TRIGGER [grv].[TR_Case_ClosedIsFinal]
ON [grv].[Case]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- No delete of a Closed case.
    IF EXISTS (
        SELECT 1
        FROM deleted AS d
        LEFT JOIN inserted AS i ON i.[Id] = d.[Id]
        WHERE d.[Status] = 'Closed' AND i.[Id] IS NULL)
        THROW 51101, N'A closed grievance case is final and cannot be deleted. Archive it instead.', 1;

    -- Closed is reached only from Resolved (never inserted directly).
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        LEFT JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE i.[Status] = 'Closed'
          AND (d.[Id] IS NULL OR d.[Status] NOT IN ('Resolved', 'Closed')))
        THROW 51101, N'A grievance case is closed only from Resolved.', 1;

    -- A Closed case only leaves Closed by being reopened.
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[Status] = 'Closed'
          AND i.[Status] NOT IN ('Closed', 'Reopened'))
        THROW 51101, N'A closed grievance case is final. It can only be reopened.', 1;

    -- Reopening or archiving never changes what the case says or how it was handled.
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[Status] = 'Closed'
          AND NOT EXISTS (
              SELECT i.[CaseNo], i.[CategoryId], i.[Subject], i.[Description], i.[IncidentDate], i.[Location],
                     i.[InvolvedPersonText], i.[InvolvedDepartmentId], i.[SubjectEmployeeId], i.[IsAnonymous],
                     i.[Priority], i.[TargetResolutionDate], i.[ReturnStatus], i.[IsEscalated],
                     i.[CompanyId], i.[BranchId], i.[DepartmentId], i.[PositionId],
                     i.[DeclarationAcceptedAtUtc], i.[SubmittedAtUtc], i.[AcknowledgedAtUtc],
                     i.[ResolutionSummary], i.[ResolutionVisibleToSubmitter], i.[RejectionReason], i.[CreatedAtUtc]
              INTERSECT
              SELECT d.[CaseNo], d.[CategoryId], d.[Subject], d.[Description], d.[IncidentDate], d.[Location],
                     d.[InvolvedPersonText], d.[InvolvedDepartmentId], d.[SubjectEmployeeId], d.[IsAnonymous],
                     d.[Priority], d.[TargetResolutionDate], d.[ReturnStatus], d.[IsEscalated],
                     d.[CompanyId], d.[BranchId], d.[DepartmentId], d.[PositionId],
                     d.[DeclarationAcceptedAtUtc], d.[SubmittedAtUtc], d.[AcknowledgedAtUtc],
                     d.[ResolutionSummary], d.[ResolutionVisibleToSubmitter], d.[RejectionReason], d.[CreatedAtUtc]))
        THROW 51101, N'A closed grievance case is final. Reopen it to change it.', 1;

    -- While it stays Closed, its resolution and closing times and the submitter update stamp stay too.
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[Status] = 'Closed'
          AND i.[Status] = 'Closed'
          AND NOT EXISTS (
              SELECT i.[ResolvedAtUtc], i.[ClosedAtUtc], i.[LastSubmitterUpdateAtUtc]
              INTERSECT
              SELECT d.[ResolvedAtUtc], d.[ClosedAtUtc], d.[LastSubmitterUpdateAtUtc]))
        THROW 51101, N'A closed grievance case is final. Reopen it to change it.', 1;
END;
