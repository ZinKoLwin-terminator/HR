-- Database role for the APEX HRMS API's runtime account: data access only, no DDL (security H1).
-- Permissions: Security\hrms_api.Permissions.sql. Schema deployment uses the publishing login, not this role.
-- Membership is environment-specific and is not part of this project. A human maps the account, e.g.:
--   CREATE LOGIN [DOMAIN\svc-hrms-api$] FROM WINDOWS;            -- gMSA (note the trailing $) or Windows account
--   CREATE USER [DOMAIN\svc-hrms-api$] FOR LOGIN [DOMAIN\svc-hrms-api$];
--   ALTER ROLE [hrms_api] ADD MEMBER [DOMAIN\svc-hrms-api$];
CREATE ROLE [hrms_api] AUTHORIZATION [dbo];
