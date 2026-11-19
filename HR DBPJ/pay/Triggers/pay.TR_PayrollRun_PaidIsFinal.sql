-- Approved runs are locked and paid runs are final (name kept from when only Paid was locked).
-- Allowed transitions: Pending Approval -> Approved; Approved -> Paid (only Status, PaidOn, Modified* change); Approved -> Draft; nothing from Paid; no INSERT/DELETE as Approved or Paid.
CREATE TRIGGER [pay].[TR_PayrollRun_PaidIsFinal]
ON [pay].[PayrollRun]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Paid is final: no change and no delete.
    IF EXISTS (SELECT 1 FROM deleted WHERE [Status] = 'Paid')
        THROW 51001, N'A paid payroll run is final and cannot be changed or deleted.', 1;

    -- Approved: never deleted, and only left by marking it paid or returning it to draft.
    IF EXISTS (
        SELECT 1
        FROM deleted AS d
        LEFT JOIN inserted AS i ON i.[Id] = d.[Id]
        WHERE d.[Status] = 'Approved'
          AND (i.[Id] IS NULL OR i.[Status] NOT IN ('Paid', 'Draft')))
        THROW 51001, N'An approved payroll run is locked. Mark it as paid or return it to draft.', 1;

    -- Marking paid changes nothing but the status, the paid date and the modification stamp.
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE d.[Status] = 'Approved'
          AND i.[Status] = 'Paid'
          AND NOT EXISTS (
              SELECT i.[PeriodName], i.[Year], i.[Month], i.[StartDate], i.[EndDate], i.[PayDate],
                     i.[EmployeeCount], i.[TotalBasic], i.[TotalAllowances], i.[TotalGross], i.[TotalDeductions], i.[TotalNet],
                     i.[ProcessedOn], i.[ProcessedByUserId], i.[ProcessedByName],
                     i.[ApprovedOn], i.[ApprovedByUserId], i.[ApprovedByName],
                     i.[CreatedAtUtc], i.[CreatedByUserId]
              INTERSECT
              SELECT d.[PeriodName], d.[Year], d.[Month], d.[StartDate], d.[EndDate], d.[PayDate],
                     d.[EmployeeCount], d.[TotalBasic], d.[TotalAllowances], d.[TotalGross], d.[TotalDeductions], d.[TotalNet],
                     d.[ProcessedOn], d.[ProcessedByUserId], d.[ProcessedByName],
                     d.[ApprovedOn], d.[ApprovedByUserId], d.[ApprovedByName],
                     d.[CreatedAtUtc], d.[CreatedByUserId]))
        THROW 51001, N'Marking a payroll run as paid cannot change its figures or approval details.', 1;

    -- Approved and Paid are reached only through the workflow, never inserted directly.
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        LEFT JOIN deleted AS d ON d.[Id] = i.[Id]
        WHERE (i.[Status] = 'Approved' AND (d.[Id] IS NULL OR d.[Status] <> 'Pending Approval'))
           OR (i.[Status] = 'Paid'     AND (d.[Id] IS NULL OR d.[Status] <> 'Approved')))
        THROW 51001, N'A payroll run is approved only from pending approval and paid only from approved.', 1;
END;
