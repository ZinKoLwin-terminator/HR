-- Payslips of an approved or paid run cannot be added, changed or removed (name kept from when only Paid was locked).
-- Allowed: changes only while the run is Draft or Pending Approval; to return an approved run to draft, set the run to Draft first, then delete its payslips (same transaction).
CREATE TRIGGER [pay].[TR_Payslip_PaidRunIsFinal]
ON [pay].[Payslip]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Reads the run's status after any earlier statement in the transaction, so a run already set to
    -- Draft allows its payslips to be deleted.
    IF EXISTS (
        SELECT 1
        FROM (SELECT [PayrollRunId] FROM inserted
              UNION
              SELECT [PayrollRunId] FROM deleted) AS c
        JOIN [pay].[PayrollRun] AS r ON r.[Id] = c.[PayrollRunId]
        WHERE r.[Status] IN ('Approved', 'Paid'))
        THROW 51002, N'Payslips of an approved or paid payroll run are locked.', 1;
END;
