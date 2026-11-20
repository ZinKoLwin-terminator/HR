-- Payslip lines of an approved or paid run cannot be added, changed or removed (name kept from when only Paid was locked).
-- Allowed: changes only while the run is Draft or Pending Approval; lines removed by the payslip delete cascade are covered by pay.TR_Payslip_PaidRunIsFinal.
CREATE TRIGGER [pay].[TR_PayslipLine_PaidRunIsFinal]
ON [pay].[PayslipLine]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM (SELECT [PayslipId] FROM inserted
              UNION
              SELECT [PayslipId] FROM deleted) AS c
        JOIN [pay].[Payslip] AS p ON p.[Id] = c.[PayslipId]
        JOIN [pay].[PayrollRun] AS r ON r.[Id] = p.[PayrollRunId]
        WHERE r.[Status] IN ('Approved', 'Paid'))
        THROW 51003, N'Payslip lines of an approved or paid payroll run are locked.', 1;
END;
