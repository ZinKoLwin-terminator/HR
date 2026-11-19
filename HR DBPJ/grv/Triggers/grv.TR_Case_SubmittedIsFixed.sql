-- What submission fixes, and what archiving freezes (TR_Case_ClosedIsFinal guards the Closed status itself).
--  * Only a draft is ever deleted (the API removes a submitter's draft); a submitted case is archived instead.
--  * Once submitted, a case keeps its number, its submission and declaration times, whether it is anonymous and the
--    named submitter's placement copied at submission.
--  * A case on legal hold is not archived; an archived case is never un-archived and, apart from its legal hold and
--    retention date (and the modification stamp), never changes.
-- The comparisons use INTERSECT so that NULLs compare as equal.
CREATE TRIGGER [grv].[TR_Case_SubmittedIsFixed]
ON [grv].[Case]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted AS d
        LEFT JOIN inserted AS i ON i.[Id] = d.[Id]
        WHERE d.[Status] <> 'Draft' AND i.[Id] IS NULL)
        THROW 51101, N'Only a draft grievance can be deleted. Archive a closed or rejected case instead.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[Status] <> 'Draft'
          AND NOT EXISTS (
              SELECT i.[CaseNo], i.[SubmittedAtUtc], i.[DeclarationAcceptedAtUtc], i.[IsAnonymous],
                     i.[CompanyId], i.[BranchId], i.[DepartmentId], i.[PositionId]
              INTERSECT
              SELECT d.[CaseNo], d.[SubmittedAtUtc], d.[DeclarationAcceptedAtUtc], d.[IsAnonymous],
                     d.[CompanyId], d.[BranchId], d.[DepartmentId], d.[PositionId]))
        THROW 51101, N'A submitted grievance keeps its number, submission, anonymity and placement.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[ArchivedAtUtc] IS NULL AND i.[ArchivedAtUtc] IS NOT NULL AND i.[LegalHold] = 1)
        THROW 51101, N'A grievance case on legal hold cannot be archived.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[ArchivedAtUtc] IS NOT NULL
          AND NOT EXISTS (
              SELECT i.[CaseNo], i.[CategoryId], i.[Subject], i.[Description], i.[IncidentDate], i.[Location],
                     i.[InvolvedPersonText], i.[InvolvedDepartmentId], i.[SubjectEmployeeId], i.[IsAnonymous],
                     i.[Status], i.[Priority], i.[TargetResolutionDate], i.[ReturnStatus], i.[IsEscalated],
                     i.[CompanyId], i.[BranchId], i.[DepartmentId], i.[PositionId],
                     i.[DeclarationAcceptedAtUtc], i.[SubmittedAtUtc], i.[AcknowledgedAtUtc], i.[ResolvedAtUtc], i.[ClosedAtUtc],
                     i.[ResolutionSummary], i.[ResolutionVisibleToSubmitter], i.[RejectionReason], i.[ArchivedAtUtc],
                     i.[LastSubmitterUpdateAtUtc], i.[CreatedAtUtc]
              INTERSECT
              SELECT d.[CaseNo], d.[CategoryId], d.[Subject], d.[Description], d.[IncidentDate], d.[Location],
                     d.[InvolvedPersonText], d.[InvolvedDepartmentId], d.[SubjectEmployeeId], d.[IsAnonymous],
                     d.[Status], d.[Priority], d.[TargetResolutionDate], d.[ReturnStatus], d.[IsEscalated],
                     d.[CompanyId], d.[BranchId], d.[DepartmentId], d.[PositionId],
                     d.[DeclarationAcceptedAtUtc], d.[SubmittedAtUtc], d.[AcknowledgedAtUtc], d.[ResolvedAtUtc], d.[ClosedAtUtc],
                     d.[ResolutionSummary], d.[ResolutionVisibleToSubmitter], d.[RejectionReason], d.[ArchivedAtUtc],
                     d.[LastSubmitterUpdateAtUtc], d.[CreatedAtUtc]))
        THROW 51101, N'An archived grievance case cannot be changed; only its legal hold and retention date can.', 1;
END;
