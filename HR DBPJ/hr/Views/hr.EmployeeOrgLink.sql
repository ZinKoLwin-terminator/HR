-- Organization placement of each employee, without personal data (security L5). The organization
-- module reads this for its delete and move checks instead of hr.Employee. hrms_api still has table
-- access to hr.Employee because the Employee module needs it, so this view narrows what the
-- organization code reads; it does not by itself restrict the API's database permissions.
CREATE VIEW [hr].[EmployeeOrgLink]
AS
SELECT [Id],
       [CompanyId],
       [BranchId],
       [DepartmentId],
       [SectionId],
       [PositionId],
       [JobGradeId],
       [EmploymentStatus],
       [ExitDate]
FROM [hr].[Employee];
