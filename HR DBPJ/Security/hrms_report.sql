-- Read-only database role for reporting: SELECT only, no bank details (security H1).
-- Permissions: Security\hrms_report.Permissions.sql. Membership is environment-specific (see Security\hrms_api.sql).
CREATE ROLE [hrms_report] AUTHORIZATION [dbo];
