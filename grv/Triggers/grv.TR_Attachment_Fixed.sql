-- A grievance file's row is fixed once written: who uploaded it, its visibility, its name, type, size, hash, storage key and case never
-- change (the API never moves a file to another case or another audience; RemovedAtUtc is not used by the API either, so it is fixed
-- too). Only the modification stamp may move. A file is deleted only with its draft: either explicitly while the case is still a Draft,
-- or by the ON DELETE CASCADE of a draft that is being deleted (the case row is then already gone when this trigger runs, and
-- TR_Case_SubmittedIsFixed has made sure only a draft is deleted). THROW 51102.
CREATE TRIGGER [grv].[TR_Attachment_Fixed]
ON [grv].[Attachment]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted AS d
        LEFT JOIN inserted AS i ON i.[Id] = d.[Id]
        JOIN [grv].[Case] AS c ON c.[Id] = d.[CaseId]
        WHERE i.[Id] IS NULL AND c.[Status] <> 'Draft')
        THROW 51102, N'A file of a submitted grievance case cannot be deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE NOT EXISTS (
            SELECT i.[CaseId], i.[CommunicationId], i.[NoteId], i.[Visibility], i.[UploadedByRole], i.[UploadedByUserId],
                   i.[FileName], i.[ContentType], i.[SizeBytes], i.[StorageKey], i.[Sha256], i.[UploadedAtUtc], i.[RemovedAtUtc],
                   i.[CreatedAtUtc]
            INTERSECT
            SELECT d.[CaseId], d.[CommunicationId], d.[NoteId], d.[Visibility], d.[UploadedByRole], d.[UploadedByUserId],
                   d.[FileName], d.[ContentType], d.[SizeBytes], d.[StorageKey], d.[Sha256], d.[UploadedAtUtc], d.[RemovedAtUtc],
                   d.[CreatedAtUtc]))
        THROW 51102, N'A grievance file cannot be changed once it is stored.', 1;
END;
