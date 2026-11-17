-- Least privilege for the API (security H1). Nothing here grants ALTER, CONTROL, EXECUTE, REFERENCES,
-- VIEW DEFINITION or any database-level permission. Object-level DENY overrides the schema-level GRANT.

-- ---- Application data: read and write -----------------------------------------------------------
-- Schema grants also cover the system-versioned history tables (SELECT only in practice: SQL Server
-- rejects DML on a history table while versioning is on) and the sequences in these schemas.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[org] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[hr]  TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[att] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[lve] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[pay] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[rec] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[cfg] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[sec] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[kpi] TO [hrms_api];
GO
-- ASP.NET Core Identity store (credentials). Only the API reads or writes it.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[auth] TO [hrms_api];
GO

-- ---- Append-only logs ---------------------------------------------------------------------------
GRANT SELECT, INSERT ON OBJECT::[app].[ActivityLog] TO [hrms_api];
GO
DENY UPDATE, DELETE ON OBJECT::[app].[ActivityLog] TO [hrms_api];
GO

-- Rows still go with their employee through FK_EmploymentHistory_Employee (ON DELETE CASCADE); the
-- API must not delete them itself.
DENY UPDATE, DELETE ON OBJECT::[hr].[EmploymentHistory] TO [hrms_api];
GO

-- The KPI approval history cannot be rewritten or removed.
DENY UPDATE, DELETE ON OBJECT::[kpi].[ReviewAction] TO [hrms_api];
GO

-- ---- Other app tables ---------------------------------------------------------------------------
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[app].[Notification] TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[app].[ImportBatch]  TO [hrms_api];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[app].[ImportIssue]  TO [hrms_api];
GO

-- ---- Sequences: NEXT VALUE FOR needs UPDATE --------------------------------------------------------
-- Already covered by the schema grants; stated explicitly so numbering keeps working if those narrow.
GRANT UPDATE ON OBJECT::[hr].[EmployeeCodeSeq]        TO [hrms_api];
GO
GRANT UPDATE ON OBJECT::[lve].[LeaveApplicationNoSeq] TO [hrms_api];
GO
GRANT UPDATE ON OBJECT::[rec].[JobOpeningCodeSeq]     TO [hrms_api];
GO

-- ---- Organization module's delete/move checks (security L5) ----------------------------------------
GRANT SELECT ON OBJECT::[hr].[EmployeeOrgLink] TO [hrms_api];
GO

-- ---- No DDL -------------------------------------------------------------------------------------
-- DENY ALTER also blocks TRUNCATE, SET IDENTITY_INSERT and bulk loads that skip constraints or
-- triggers, even if the account is later added to a role such as db_ddladmin. DENY CONTROL is not
-- used: it would also deny SELECT, INSERT, UPDATE and DELETE.
DENY ALTER ON SCHEMA::[org] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[hr]  TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[att] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[lve] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[pay] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[rec] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[cfg] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[sec] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[kpi] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[app] TO [hrms_api];
GO
DENY ALTER ON SCHEMA::[auth] TO [hrms_api];
GO
