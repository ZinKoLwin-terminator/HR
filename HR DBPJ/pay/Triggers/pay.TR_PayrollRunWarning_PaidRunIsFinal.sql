-- Processing warnings of an approved or paid run cannot be added, changed or removed (name kept from when only Paid was locked).
-- Allowed: changes only while the run is Draft or Pending Approval; to return an approved run to draft, set the run to Draft first, then delete its warnings (same transaction).
CREATE TRIGGER [pay].[TR_PayrollRunWarning_PaidRunIsFinal]
ON [pay].[PayrollRunWarning]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM (SELECT [PayrollRunId] FROM inserted
              UNION
              SELECT [PayrollRunId] FROM deleted) AS c
        JOIN [pay].[PayrollRun] AS r ON r.[Id] = c.[PayrollRunId]
        WHERE r.[Status] IN ('Approved', 'Paid'))
        THROW 51004, N'Warnings of an approved or paid payroll run are locked.', 1;
END;
