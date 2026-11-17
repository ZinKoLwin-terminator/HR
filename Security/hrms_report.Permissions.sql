-- Read-only reporting (security H1). SELECT only; no write, DDL or EXECUTE permission is granted.
-- Not granted: sec (accounts and permissions), app (activity, notifications, imports) and auth (credentials).

GRANT SELECT ON SCHEMA::[org] TO [hrms_report];
GO
GRANT SELECT ON SCHEMA::[att] TO [hrms_report];
GO
GRANT SELECT ON SCHEMA::[lve] TO [hrms_report];
GO
GRANT SELECT ON SCHEMA::[pay] TO [hrms_report];
GO
GRANT SELECT ON SCHEMA::[rec] TO [hrms_report];
GO
GRANT SELECT ON SCHEMA::[cfg] TO [hrms_report];
GO
-- KPI results for performance reports (includes the history tables, which hold earlier results).
GRANT SELECT ON SCHEMA::[kpi] TO [hrms_report];
GO

-- hr is granted per object, not per schema: a schema grant would also expose the bank history table
-- [hr].[EmployeeBankAccountHistory], which SQL Server creates and this project cannot reference.
-- Side effect: FOR SYSTEM_TIME queries on hr.Employee need SELECT on hr.EmployeeHistory, which is not granted.
GRANT SELECT ON OBJECT::[hr].[Employee]          TO [hrms_report];
GO
GRANT SELECT ON OBJECT::[hr].[EmployeeDocument]  TO [hrms_report];
GO
GRANT SELECT ON OBJECT::[hr].[EmploymentHistory] TO [hrms_report];
GO
GRANT SELECT ON OBJECT::[hr].[EmployeeOrgLink]   TO [hrms_report];
GO

-- Bank details stay out of reporting even if a broader grant is added later.
DENY SELECT ON OBJECT::[hr].[EmployeeBankAccount] TO [hrms_report];
GO

-- Credentials (password hashes, security stamps, reset and authenticator tokens) are never reported on.
DENY SELECT ON SCHEMA::[auth] TO [hrms_report];
GO
