-- Reference data only (idempotent). Never put sample HR data here: no employees, no users.
-- Nothing below deletes rows; removing a module, action or role is a deliberate, reviewed migration.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Modules this deployment adds. Their starter grants (below) go to the existing roles too, once: a
-- module that already exists is never re-granted, so an administrator's later changes are kept.
-- (OUTPUT also lists updated rows, so the MERGE action is kept and only inserts count.)
DECLARE @ModuleChange TABLE ([MergeAction] nvarchar(10) NOT NULL, [ModuleKey] varchar(30) NOT NULL);

-- ---- Permission modules (PERMISSION_MODULES in settings.models.ts) -------------------------------
MERGE [sec].[PermissionModule] WITH (HOLDLOCK) AS t
USING (VALUES
    ('dashboard',    N'Dashboard',    1),
    ('organization', N'Organization', 2),
    ('employees',    N'Employees',    3),
    ('attendance',   N'Attendance',   4),
    ('leave',        N'Leave',        5),
    ('payroll',      N'Payroll',      6),
    ('recruitment',  N'Recruitment',  7),
    ('settings',     N'Settings',     8),
    ('activity',     N'Activity',     9),
    ('excel',        N'Excel',        10),
    -- KPI master data (categories, templates, periods, rating bands) and assignments: HR.
    ('kpi-setup',    N'KPI Setup',    11),
    -- Scorecards: Create enters and submits your own results; Approve reviews them (the API scopes
    -- managers to their direct reports and HR staff to everyone, like leave).
    ('kpi',          N'Performance',  12),
    -- Grievances. View/Create: your own grievances (drafts, submit, messages). The case-team modules are scoped by
    -- the API: investigation to the cases the caller is assigned to, review to escalated cases and pending
    -- resolutions, management to every submitted case (Delete archives). Nobody on the case team sees a case
    -- about themselves.
    ('grievance',               N'Grievances',              13),
    ('grievance-investigation', N'Grievance Investigation', 14),
    ('grievance-management',    N'Grievance Management',    15),
    ('grievance-review',        N'Grievance Review',        16)
) AS s ([ModuleKey], [Label], [SortOrder])
    ON t.[ModuleKey] = s.[ModuleKey]
WHEN MATCHED AND (t.[Label] <> s.[Label] OR t.[SortOrder] <> s.[SortOrder]) THEN
    UPDATE SET t.[Label] = s.[Label], t.[SortOrder] = s.[SortOrder]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ModuleKey], [Label], [SortOrder]) VALUES (s.[ModuleKey], s.[Label], s.[SortOrder])
OUTPUT $action, inserted.[ModuleKey] INTO @ModuleChange ([MergeAction], [ModuleKey]);

-- ---- Actions per module (SortOrder follows PERMISSION_ACTIONS) ------------------------------------
-- organization and employees must match PermissionModule.All in the API (Authorization\PermissionModule.cs);
-- the personal-data actions follow the API's Permission enum order (6 to 9).
MERGE [sec].[PermissionModuleAction] WITH (HOLDLOCK) AS t
USING (VALUES
    ('dashboard',    'View',    1),
    ('organization', 'View',    1), ('organization', 'Create', 2), ('organization', 'Edit', 3), ('organization', 'Delete', 4),
    ('employees',    'View',    1), ('employees',    'Create', 2), ('employees',    'Edit', 3), ('employees',    'Delete', 4),
    ('employees',    'ViewSensitive', 6), ('employees', 'EditSensitive', 7), ('employees', 'ViewBank', 8), ('employees', 'EditBank', 9),
    ('employees',    'ViewBlacklist', 12), ('employees', 'EditBlacklist', 13),
    ('attendance',   'View',    1), ('attendance',   'Create', 2), ('attendance',   'Edit', 3), ('attendance',   'Delete', 4), ('attendance', 'Approve', 5),
    ('leave',        'View',    1), ('leave',        'Create', 2), ('leave',        'Edit', 3), ('leave',        'Delete', 4), ('leave', 'Approve', 5),
    ('payroll',      'View',    1), ('payroll',      'Create', 2), ('payroll',      'Edit', 3), ('payroll',      'Approve', 5),
    ('recruitment',  'View',    1), ('recruitment',  'Create', 2), ('recruitment',  'Edit', 3), ('recruitment',  'Delete', 4),
    ('settings',     'View',    1), ('settings',     'Create', 2), ('settings',     'Edit', 3), ('settings',     'Delete', 4),
    ('activity',     'View',    1),
    ('excel',        'View',    1), ('excel',        'Import', 10), ('excel',        'Export', 11),
    ('kpi-setup',    'View',    1), ('kpi-setup',    'Create', 2), ('kpi-setup',    'Edit', 3), ('kpi-setup',    'Delete', 4),
    ('kpi',          'View',    1), ('kpi',          'Create', 2), ('kpi',          'Approve', 5),
    ('grievance',               'View', 1), ('grievance',               'Create', 2),
    ('grievance-investigation', 'View', 1), ('grievance-investigation', 'Edit',   3),
    ('grievance-management',    'View', 1), ('grievance-management',    'Create', 2), ('grievance-management', 'Edit', 3), ('grievance-management', 'Delete', 4),
    ('grievance-review',        'View', 1), ('grievance-review',        'Approve', 5)
) AS s ([ModuleKey], [Action], [SortOrder])
    ON t.[ModuleKey] = s.[ModuleKey] AND t.[Action] = s.[Action]
WHEN MATCHED AND t.[SortOrder] <> s.[SortOrder] THEN
    UPDATE SET t.[SortOrder] = s.[SortOrder]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ModuleKey], [Action], [SortOrder]) VALUES (s.[ModuleKey], s.[Action], s.[SortOrder]);

-- ---- Roles (MOCK_ROLES) -------------------------------------------------------------------------
-- Built-in roles are always ensured. The other starter roles are only seeded into an empty table,
-- so a role an administrator deleted or edited is not brought back by the next deployment.
DECLARE @IsInitialRoleSeed bit = CASE WHEN EXISTS (SELECT 1 FROM [sec].[Role]) THEN 0 ELSE 1 END;

MERGE [sec].[Role] WITH (HOLDLOCK) AS t
USING (VALUES
    (N'HR Administrator',   N'Full access to every module, including settings.',               CAST(1 AS bit), 'Active'),
    (N'HR Officer',         N'Day-to-day HR operations and payroll approval, without settings.', CAST(0 AS bit), 'Active'),
    (N'Department Manager', N'Views the team and approves attendance and leave.',             CAST(0 AS bit), 'Active'),
    (N'Payroll Officer',    N'Prepares and approves payroll.',                                 CAST(0 AS bit), 'Active'),
    (N'Recruiter',          N'Manages job openings, candidates and interviews.',               CAST(0 AS bit), 'Active'),
    (N'Employee',           N'Self-service basics for every employee.',                        CAST(1 AS bit), 'Active'),
    (N'Auditor',            N'Read-only access (not in use yet).',                             CAST(0 AS bit), 'Inactive')
) AS s ([Name], [Description], [IsSystem], [Status])
    ON t.[Name] = s.[Name]
WHEN MATCHED AND s.[IsSystem] = 1 AND (t.[IsSystem] = 0 OR t.[Status] <> 'Active') THEN
    UPDATE SET t.[IsSystem] = 1, t.[Status] = 'Active', t.[ModifiedAtUtc] = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET AND (s.[IsSystem] = 1 OR @IsInitialRoleSeed = 1) THEN
    INSERT ([Name], [Description], [IsSystem], [Status]) VALUES (s.[Name], s.[Description], s.[IsSystem], s.[Status]);

-- ---- Role permissions (MOCK_ROLE_PERMISSIONS) ---------------------------------------------------
DECLARE @Grant TABLE ([RoleName] nvarchar(60) NOT NULL, [ModuleKey] varchar(30) NOT NULL, [Action] varchar(30) NOT NULL);

-- HR Administrator always has every action of every module (ADMIN_ROLE_NAME rule).
INSERT INTO @Grant ([RoleName], [ModuleKey], [Action])
SELECT N'HR Administrator', a.[ModuleKey], a.[Action]
FROM [sec].[PermissionModuleAction] AS a;

-- Starter grants of the other roles: on the first deployment, and for a module this deployment adds
-- (only to roles that still exist). Otherwise grants are an administrator's decision.
INSERT INTO @Grant ([RoleName], [ModuleKey], [Action])
SELECT g.[RoleName], g.[ModuleKey], TRIM(x.[value])
FROM (VALUES
        (N'HR Officer',         'dashboard',    'View'),
        (N'HR Officer',         'organization', 'View,Create,Edit'),
        (N'HR Officer',         'employees',    'View,Create,Edit'),
        (N'HR Officer',         'attendance',   'View,Create,Edit,Approve'),
        (N'HR Officer',         'leave',        'View,Create,Edit,Approve'),
        -- Approve lets HR Officer see every payslip and salary (the API shows others only their own);
        -- it also approves runs, never one the same person processed.
        (N'HR Officer',         'payroll',      'View,Approve'),
        (N'HR Officer',         'recruitment',  'View,Create,Edit'),
        -- The API scopes a manager to their own record and direct reports (hr.Employee.ManagerEmployeeId).
        (N'Department Manager', 'dashboard',    'View'),
        (N'Department Manager', 'employees',    'View'),
        (N'Department Manager', 'organization', 'View'),
        (N'Department Manager', 'recruitment',  'View'),
        (N'Department Manager', 'attendance',   'View,Approve'),
        (N'Department Manager', 'leave',        'View,Approve'),
        (N'Payroll Officer',    'dashboard',    'View'),
        (N'Payroll Officer',    'employees',    'View'),
        (N'Payroll Officer',    'payroll',      'View,Create,Edit,Approve'),
        (N'Recruiter',          'dashboard',    'View'),
        (N'Recruiter',          'recruitment',  'View,Create,Edit,Delete'),
        (N'Employee',           'dashboard',    'View'),
        -- Create lets an employee punch and request adjustments or overtime for their own record only
        -- (the API scopes it); creating shifts and holidays needs attendance:Edit.
        (N'Employee',           'attendance',   'View,Create'),
        (N'Employee',           'leave',        'View,Create'),
        (N'Auditor',            'dashboard',    'View'),
        (N'Auditor',            'organization', 'View'),
        (N'Auditor',            'employees',    'View'),
        (N'Auditor',            'attendance',   'View'),
        (N'Auditor',            'leave',        'View'),
        (N'Auditor',            'payroll',      'View'),
        (N'Auditor',            'recruitment',  'View'),
        (N'Auditor',            'settings',     'View'),
        -- KPI: HR Officer runs the KPI setup and, with employees:Edit, reviews every scorecard; a
        -- Department Manager reviews direct reports; everyone enters and submits their own results.
        (N'HR Officer',         'kpi-setup',    'View,Create,Edit,Delete'),
        (N'HR Officer',         'kpi',          'View,Create,Approve'),
        (N'Department Manager', 'kpi',          'View,Create,Approve'),
        (N'Payroll Officer',    'kpi',          'View,Create'),
        (N'Recruiter',          'kpi',          'View,Create'),
        (N'Employee',           'kpi',          'View,Create'),
        (N'Auditor',            'kpi-setup',    'View'),
        (N'Auditor',            'kpi',          'View'),
        -- Grievances: everyone can raise their own; HR Officer investigates the cases they are assigned to. Management
        -- and review stay with HR Administrator until an administrator grants them (no starter role gets them).
        (N'HR Officer',         'grievance',               'View,Create'),
        (N'HR Officer',         'grievance-investigation', 'View,Edit'),
        (N'Department Manager', 'grievance',               'View,Create'),
        (N'Payroll Officer',    'grievance',               'View,Create'),
        (N'Recruiter',          'grievance',               'View,Create'),
        (N'Employee',           'grievance',               'View,Create')
) AS g ([RoleName], [ModuleKey], [Actions])
CROSS APPLY STRING_SPLIT(g.[Actions], ',') AS x
WHERE @IsInitialRoleSeed = 1
   OR g.[ModuleKey] IN (SELECT [ModuleKey] FROM @ModuleChange WHERE [MergeAction] = N'INSERT');

-- View grants first: FK_RolePermission_ViewGrant requires them before any other action.
INSERT INTO [sec].[RolePermission] ([RoleId], [ModuleKey], [Action])
SELECT r.[Id], g.[ModuleKey], g.[Action]
FROM @Grant AS g
JOIN [sec].[Role] AS r ON r.[Name] = g.[RoleName]
WHERE g.[Action] = 'View'
  AND NOT EXISTS (SELECT 1 FROM [sec].[RolePermission] AS p
                  WHERE p.[RoleId] = r.[Id] AND p.[ModuleKey] = g.[ModuleKey] AND p.[Action] = g.[Action]);

INSERT INTO [sec].[RolePermission] ([RoleId], [ModuleKey], [Action])
SELECT r.[Id], g.[ModuleKey], g.[Action]
FROM @Grant AS g
JOIN [sec].[Role] AS r ON r.[Name] = g.[RoleName]
WHERE g.[Action] <> 'View'
  AND NOT EXISTS (SELECT 1 FROM [sec].[RolePermission] AS p
                  WHERE p.[RoleId] = r.[Id] AND p.[ModuleKey] = g.[ModuleKey] AND p.[Action] = g.[Action]);

-- ---- General settings singleton (MOCK_GENERAL_SETTINGS; fictional organization) ----------------
-- Inserted once; values an administrator saved later are never overwritten.
IF NOT EXISTS (SELECT 1 FROM [cfg].[GeneralSettings] WHERE [Id] = 1)
BEGIN
    INSERT INTO [cfg].[GeneralSettings]
        ([Id], [OrganizationName], [ContactEmail], [Timezone], [DateFormat], [CurrencyCode],
         [FiscalYearStartMonth], [EmailNotifications], [WeeklyDigest])
    VALUES
        (1, N'APEX Group', N'hr@apexhorizon.example', 'Asia/Yangon', 'yyyy-MM-dd', 'MMK', 1, 1, 0);
END;

-- ---- KPI rating bands (overall score -> label) ----------------------------------------------------
-- Inserted into an empty table only; bands an administrator changed later are never overwritten.
IF NOT EXISTS (SELECT 1 FROM [kpi].[RatingBand])
BEGIN
    INSERT INTO [kpi].[RatingBand] ([Label], [MinScore], [Description])
    VALUES
        (N'Outstanding',          90.00,N'Consistently exceeds every target.'),
        (N'Exceeds Expectations', 75.00, N'Exceeds most targets.'),
        (N'Meets Expectations',   60.00, N'Meets the agreed targets.'),
        (N'Needs Improvement',    40.00, N'Misses several targets; a development plan is expected.'),
        (N'Unsatisfactory',        0.00, N'Misses most targets.');
END;

-- ---- Grievance categories -------------------------------------------------------------------------
-- Inserted into an empty table only; categories an administrator changed later are never overwritten.
-- Salary and compensation cases need the employee's own pay details, so they cannot be raised anonymously.
IF NOT EXISTS (SELECT 1 FROM [grv].[Category])
BEGIN
    INSERT INTO [grv].[Category] ([Code], [Name], [Description], [AllowsAnonymous], [DefaultTargetDays], [SortOrder])
    VALUES
        (N'HARASSMENT',       N'Workplace Harassment',           N'Bullying, intimidation, sexual or other harassment at work.',              1, 14, 1),
        (N'DISCRIMINATION',   N'Discrimination',                 N'Unequal treatment because of a protected characteristic.',                 1, 14, 2),
        (N'COMPENSATION',     N'Salary and Compensation Issues', N'Pay, allowances, deductions or benefits.',                                 0, 21, 3),
        (N'SAFETY',           N'Workplace Safety',               N'Unsafe conditions, equipment or practices.',                               1,  7, 4),
        (N'RELATIONS',        N'Employee Relations',             N'Conflicts between colleagues or within a team.',                           1, 30, 5),
        (N'MISCONDUCT',       N'Management Misconduct',          N'Abuse of authority or improper conduct by a manager.',                     1, 21, 6),
        (N'UNFAIR-TREATMENT', N'Unfair Treatment',               N'Unfair decisions on assignments, appraisals, promotion or discipline.',    1, 30, 7),
        (N'POLICY',           N'Policy Violations',              N'A company policy or procedure that is not being followed.',                1, 30, 8),
        (N'CONDITIONS',       N'Working Conditions',             N'Working hours, workload, facilities or the work environment.',             1, 30, 9),
        (N'OTHER',            N'Other',                          N'Anything that does not fit another category.',                             1, 30, 10);
END;

COMMIT TRANSACTION;

PRINT 'Post-deployment: reference data is up to date.';
