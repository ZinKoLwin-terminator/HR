/*
  Step 2: the API's own SQL login (least privilege). Run in SSMS as the RDS master login, AFTER 1-publish-schema.ps1.

  Before running:
    1. Replace <STRONG-PASSWORD> below with a new random password (20+ characters).
       Avoid ; ' " and spaces so it can go into a connection string unquoted.
    2. Store that password in your password manager. You type it again in step 3 on the API server.
  Never commit this file with the real password in it.
*/

-- ---- 1. Server login (master) ----------------------------------------------------------------------
USE [master];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'hrms_api_uat')
    CREATE LOGIN [hrms_api_uat] WITH PASSWORD = N'<STRONG-PASSWORD>', CHECK_POLICY = ON, DEFAULT_DATABASE = [HR];
GO

-- ---- 2. Database user in role hrms_api (data access only, no DDL) -----------------------------------
USE [HR];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'hrms_api_uat')
    CREATE USER [hrms_api_uat] FOR LOGIN [hrms_api_uat];
GO
ALTER ROLE [hrms_api] ADD MEMBER [hrms_api_uat];
GO

-- ---- 3. Check: expect one row, role hrms_api ----------------------------------------------------------
SELECT u.name AS [user], r.name AS [role]
FROM sys.database_role_members AS m
JOIN sys.database_principals AS r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals AS u ON u.principal_id = m.member_principal_id
WHERE u.name = N'hrms_api_uat';
GO
