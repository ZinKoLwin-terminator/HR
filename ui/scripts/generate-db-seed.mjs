// Generates two SQL scripts from the sample data in src/app/mock-data, so a local database holds the same records as
// the demo and docs/E2E-TEST-SCENARIOS.md (section 1):
//
//   docs/test-data/sql/seed-organization-employees.sql   1. Organization and Employees (deletes every module's data)
//   docs/test-data/sql/seed-modules.sql                   2. Leave, attendance, payroll, recruitment, KPI, grievances,
//                                                            activity and notifications (needs the data of script 1)
//
//   cd ui
//   node scripts/generate-db-seed.mjs
//
// The mock data moves dates relative to today (and today's attendance to the current time), so the scripts are a
// snapshot of the moment they were generated. Generate them again before you run them on a later day.
//
// The scripts delete data. A human runs them, against a local development database only (see the script headers).
import { createHmac, createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const uiRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = path.resolve(uiRoot, '..');
const outDir = path.resolve(repoRoot, 'docs/test-data/sql');
const orgFile = path.resolve(outDir, 'seed-organization-employees.sql');
const modulesFile = path.resolve(outDir, 'seed-modules.sql');
/** Only the labelled development value of this file is used for the anonymous grievance pseudonyms (never a real key). */
const devSettingsFile = path.resolve(
  repoRoot,
  'api/src/Apex.Hrms.Api/appsettings.Development.json',
);
const DEV_KEY_TEXT = 'DEVELOPMENT-ONLY grievance pseudonym key. Not a secret. LocalDB only.';

const ENTRY = `
export { MOCK_COMPANIES } from './src/app/mock-data/organization/companies.mock';
export { MOCK_BRANCHES } from './src/app/mock-data/organization/branches.mock';
export { MOCK_DEPARTMENTS } from './src/app/mock-data/organization/departments.mock';
export { MOCK_SECTIONS } from './src/app/mock-data/organization/sections.mock';
export { MOCK_JOB_GRADES } from './src/app/mock-data/organization/job-grades.mock';
export { MOCK_POSITIONS } from './src/app/mock-data/organization/positions.mock';
export { MOCK_EMPLOYEES } from './src/app/mock-data/employees/employees.mock';
export { MOCK_EMPLOYMENT_HISTORY } from './src/app/mock-data/employees/history.mock';
export { MOCK_EMPLOYEE_DOCUMENTS } from './src/app/mock-data/employees/documents.mock';
export { MOCK_BLACKLIST } from './src/app/mock-data/employees/blacklist.mock';
export { MOCK_SHIFTS } from './src/app/mock-data/attendance/shifts.mock';
export { MOCK_HOLIDAYS } from './src/app/mock-data/attendance/holidays.mock';
export { MOCK_ATTENDANCE_RECORDS } from './src/app/mock-data/attendance/attendance-records.mock';
export { MOCK_ATTENDANCE_ADJUSTMENTS } from './src/app/mock-data/attendance/adjustments.mock';
export { MOCK_OVERTIME_RECORDS } from './src/app/mock-data/attendance/overtime.mock';
export { MOCK_LEAVE_TYPES } from './src/app/mock-data/leave/leave-types.mock';
export { MOCK_LEAVE_POLICIES } from './src/app/mock-data/leave/leave-policies.mock';
export { MOCK_LEAVE_APPLICATIONS } from './src/app/mock-data/leave/leave-applications.mock';
export {
  MOCK_SALARY_COMPONENTS,
  MOCK_SALARY_STRUCTURES,
  MOCK_SALARY_ASSIGNMENTS,
} from './src/app/mock-data/payroll/salary-setup.mock';
export { MOCK_PAYROLL_RUNS, MOCK_PAYSLIPS } from './src/app/mock-data/payroll/payroll-runs.mock';
export {
  MOCK_JOB_OPENINGS,
  MOCK_CANDIDATES,
  MOCK_APPLICATIONS,
  MOCK_INTERVIEWS,
} from './src/app/mock-data/recruitment/recruitment.mock';
export { MOCK_KPI_CATEGORIES } from './src/app/mock-data/kpi/kpi-categories.mock';
export { MOCK_KPI_TEMPLATES } from './src/app/mock-data/kpi/kpi-templates.mock';
export { DEFAULT_RATING_BANDS } from './src/app/mock-data/kpi/rating-bands.mock';
export { MOCK_EVALUATION_PERIODS } from './src/app/mock-data/kpi/evaluation-periods.mock';
export { MOCK_SCORECARDS } from './src/app/mock-data/kpi/scorecards.mock';
export { MOCK_GRIEVANCE_CATEGORIES } from './src/app/mock-data/grievance/grievance-categories.mock';
export {
  MOCK_GRIEVANCE_CASES,
  MOCK_GRIEVANCE_SUBMITTERS,
} from './src/app/mock-data/grievance/grievance-cases.mock';
export { grievanceDemoPseudonym } from './src/app/mock-data/grievance/grievance-records';
export { MOCK_ACTIVITIES, MOCK_NOTIFICATIONS } from './src/app/mock-data/activity/activities.mock';
export { MOCK_USERS } from './src/app/mock-data/settings/settings.mock';
export { DEFAULT_APP_CONFIG } from './src/app/core/configuration/app-config';
export { addDays, countWorkingDays, todayIso } from './src/app/shared/utilities/date.util';
`;

async function loadMockData() {
  const result = await build({
    stdin: { contents: ENTRY, resolveDir: uiRoot, loader: 'ts', sourcefile: 'seed-entry.ts' },
    bundle: true,
    format: 'esm',
    platform: 'node',
    write: false,
    logLevel: 'error',
  });
  const code = result.outputFiles[0].text;
  return import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);
}

// ---- SQL literals -------------------------------------------------------------------------------
// Text is written as ASCII only: any other character becomes NCHAR(n), so the scripts run the same in sqlcmd, SSMS
// and VS Code whatever code page reads the file.
const str = (v) => {
  if (v === null || v === undefined || v === '') return 'NULL';
  const parts = [];
  let ascii = '';
  const text = String(v);
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    if (code < 128) {
      ascii += text[i] === "'" ? "''" : text[i];
    } else {
      if (ascii) parts.push(`N'${ascii}'`);
      ascii = '';
      parts.push(`NCHAR(${code})`);
    }
  }
  if (ascii) parts.push(`N'${ascii}'`);
  return parts.join(' + ');
};
const num = (v) => (v === null || v === undefined ? 'NULL' : String(Number(v)));
/** decimal(18, 2) money: exact cents. */
const money = (v) => (v === null || v === undefined ? 'NULL' : Number(v).toFixed(2));
const bit = (v) => (v ? '1' : '0');
const date = (v) => (v ? `'${v}'` : 'NULL');
const time = (v) => (v ? `'${v}'` : 'NULL');
/** ISO date-time (UTC, "Z") to a datetime2(3) literal. */
const utc = (v) => (v ? `'${new Date(v).toISOString().slice(0, 23)}'` : 'NULL');
const rows = (list) => list.map((cells) => `    (${cells.join(', ')})`).join(',\n');

/** INSERT ... VALUES in statements of at most 1000 rows (the limit of a table value constructor). */
function insertRows(head, list, indent = '    ') {
  const out = [];
  for (let i = 0; i < list.length; i += 1000) {
    out.push(`${indent}${head}\n${indent}VALUES\n${rows(list.slice(i, i + 1000))};`);
  }
  return out.join('\n');
}

const cents = (v) => Math.round(Number(v) * 100);
const isCents = (v) => Math.abs(Number(v) * 100 - Math.round(Number(v) * 100)) < 1e-6;

function fail(message) {
  throw new Error(`Mock data check failed: ${message}`);
}

const byId = (list) => new Map(list.map((x) => [x.id, x]));

function unique(values, what) {
  const seen = new Set();
  for (const v of values) {
    const key = String(v).toLowerCase();
    if (seen.has(key)) fail(`duplicate ${what} ${v}`);
    seen.add(key);
  }
}

/** Fails when a text is longer than its column. */
function fits(value, max, what) {
  if (value !== null && value !== undefined && String(value).length > max)
    fail(`${what} is longer than ${max} characters: ${value}`);
}

function check(data) {
  const e = data.MOCK_EMPLOYEES;
  e.forEach((employee, index) => {
    if (employee.id !== index + 1)
      fail(`employee ids must be 1..n (found ${employee.id} at ${index})`);
    if (employee.employeeCode !== `EMP${String(employee.id).padStart(3, '0')}`)
      fail(`code of employee ${employee.id}`);
  });
  const shiftCodes = new Map(data.MOCK_SHIFTS.map((s) => [s.id, s.code]));
  const branches = byId(data.MOCK_BRANCHES);
  const departments = byId(data.MOCK_DEPARTMENTS);
  const sections = byId(data.MOCK_SECTIONS);
  const positions = byId(data.MOCK_POSITIONS);

  // The composite foreign keys of the database (company > branch > department > section/position).
  for (const d of data.MOCK_DEPARTMENTS) {
    if (branches.get(d.branchId)?.companyId !== d.companyId)
      fail(`department ${d.code}: branch not in its company`);
  }
  for (const p of data.MOCK_POSITIONS) {
    if (!departments.has(p.departmentId)) fail(`position ${p.code}: unknown department`);
    if (p.sectionId !== null && sections.get(p.sectionId)?.departmentId !== p.departmentId) {
      fail(`position ${p.code}: section not in its department`);
    }
  }
  for (const employee of e) {
    const code = employee.employeeCode;
    if (employee.shiftId !== null && !shiftCodes.has(employee.shiftId))
      fail(`${code}: unknown shift`);
    if (branches.get(employee.branchId)?.companyId !== employee.companyId)
      fail(`${code}: branch not in company`);
    if (departments.get(employee.departmentId)?.branchId !== employee.branchId)
      fail(`${code}: department not in branch`);
    if (positions.get(employee.positionId)?.departmentId !== employee.departmentId) {
      fail(`${code}: position not in department`);
    }
    if (
      employee.sectionId !== null &&
      sections.get(employee.sectionId)?.departmentId !== employee.departmentId
    ) {
      fail(`${code}: section not in department`);
    }
    if (employee.managerId === employee.id) fail(`${code}: manages themselves`);
    if (['Resigned', 'Terminated'].includes(employee.employmentStatus) && !employee.exitDate)
      fail(`${code}: exit date`);
    if (!/^\d{8,20}$/.test(employee.bank.accountNumber)) fail(`${code}: bank account number`);
  }
  unique(
    e.map((x) => x.workEmail),
    'work email',
  );
  for (const [list, what] of [
    [data.MOCK_COMPANIES, 'company'],
    [data.MOCK_BRANCHES, 'branch'],
    [data.MOCK_DEPARTMENTS, 'department'],
    [data.MOCK_SECTIONS, 'section'],
    [data.MOCK_JOB_GRADES, 'job grade'],
    [data.MOCK_POSITIONS, 'position'],
    [data.MOCK_SHIFTS, 'shift'],
  ]) {
    unique(
      list.map((x) => x.code),
      `${what} code`,
    );
  }
  for (const s of data.MOCK_SHIFTS) {
    if (!s.isNightShift && s.endTime <= s.startTime) fail(`shift ${s.code}: end before start`);
    if (!/^#[0-9A-Fa-f]{6}$/.test(s.color)) fail(`shift ${s.code}: colour`);
  }
  return shiftCodes;
}

function sql(data) {
  const shiftCodes = check(data);
  const today = data.todayIso();
  const employees = data.MOCK_EMPLOYEES;
  const fullName = (id) => {
    const employee = employees.find((x) => x.id === id);
    return `${employee.firstName} ${employee.lastName}`;
  };
  const blacklist = data.MOCK_BLACKLIST[0];

  return `-- =================================================================================================
-- Organization and Employees sample data: DELETES the existing data and loads the demo data set.
-- Generated by ui/scripts/generate-db-seed.mjs on ${today} from ui/src/app/mock-data. Do not edit by hand:
-- change the mock data and generate again. Dates relative to "today" were fixed on ${today}.
-- Matches docs/E2E-TEST-SCENARIOS.md section 1 (employee id = EMP number). All data is fictional.
--
-- LOCAL DEVELOPMENT DATABASE ONLY. A human runs this (Claude Code is not allowed to):
--   sqlcmd -S "(localdb)\\MSSQLLocalDB" -d HR -E -b -i docs\\test-data\\sql\\seed-organization-employees.sql
-- (or open it in SSMS / VS Code connected to the local HR database and execute).
-- Then run docs\\test-data\\sql\\seed-modules.sql for the sample data of the other modules (leave, attendance, payroll,
-- recruitment, KPI, grievances, activity). Without it every module except Organization and Employees stays empty.
--
-- What it does, in one transaction (any error rolls everything back):
--   1. Deletes every row that depends on employees or organization units, in all modules:
--      attendance records, adjustments and overtime; leave applications; payroll runs, payslips and salary
--      assignments; recruitment openings, candidates, applications and interviews; KPI scorecards, evaluation periods
--      and attachments; grievance cases and everything under them, including the grievance audit log; activity log,
--      notifications and Excel import history. The history tables of the temporal tables involved are emptied too.
--      The payroll and grievance "final" triggers are disabled for the delete and enabled again afterwards.
--   2. Deletes all Organization and Employees data (companies to positions, employees, bank accounts, documents,
--      employment history, do-not-rehire list).
--   3. Inserts ${data.MOCK_COMPANIES.length} companies, ${data.MOCK_BRANCHES.length} branches, ${data.MOCK_DEPARTMENTS.length} departments, ${data.MOCK_SECTIONS.length} sections, ${data.MOCK_JOB_GRADES.length} job grades, ${data.MOCK_POSITIONS.length} positions,
--      ${employees.length} employees with bank accounts, ${data.MOCK_EMPLOYEE_DOCUMENTS.length} documents, ${data.MOCK_EMPLOYMENT_HISTORY.length} history entries and ${data.MOCK_BLACKLIST.length} do-not-rehire entry.
--
-- Kept: general settings, roles, permissions, user accounts and sign-ins, shifts, holidays, leave types and policies,
-- salary components and structures, KPI categories, templates and rating bands, grievance categories.
--   * User accounts (sec.AppUser) change in one column only: EmployeeId. Every account is unlinked from its employee and
--     linked again to the new employee with the same email (AppUser.Email = Employee.WorkEmail). An account whose email
--     is no sample employee's work email ends with EmployeeId NULL. Names, roles, status and sign-ins are not changed.
--   * Leave policies scoped to a job grade and KPI templates scoped to a department are re-pointed by code
--     (G6, ENG, ...). One whose code is not in the new data is deleted.
--   * The leave, job opening and grievance case number sequences restart at 1 (seed-modules.sql sets them after its
--     sample).
--   * The sample shifts (${data.MOCK_SHIFTS.map((s) => s.code).join(', ')}) are added when no shift has their code (an existing shift is never
--     changed). Employees get their shift by shift code; with no such shift the employee has none.
-- Files in storage (KPI evidence, grievance attachments, CVs) are not deleted; only their database rows are.
-- =================================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ---- Guards: the local HR database only ----------------------------------------------------------
IF DB_NAME() <> N'HR'
    THROW 50001, N'Run this script in the local HR database only.', 1;
${LOCAL_SERVER_GUARD}

BEGIN TRY
    BEGIN TRANSACTION;

    -- ---- Keep: grade-scoped leave policies and department-scoped KPI templates, by code -------------
    DECLARE @PolicyGrade TABLE ([PolicyId] int NOT NULL PRIMARY KEY, [GradeCode] nvarchar(20) NOT NULL);
    INSERT INTO @PolicyGrade ([PolicyId], [GradeCode])
    SELECT p.[Id], g.[Code] FROM [lve].[LeavePolicy] AS p JOIN [org].[JobGrade] AS g ON g.[Id] = p.[JobGradeId];

    DECLARE @TemplateDepartment TABLE ([TemplateId] int NOT NULL PRIMARY KEY, [DepartmentCode] nvarchar(20) NOT NULL);
    INSERT INTO @TemplateDepartment ([TemplateId], [DepartmentCode])
    SELECT t.[Id], d.[Code] FROM [kpi].[Template] AS t JOIN [org].[Department] AS d ON d.[Id] = t.[DepartmentId];

    ALTER TABLE [lve].[LeavePolicy] NOCHECK CONSTRAINT [FK_LeavePolicy_JobGrade];
    ALTER TABLE [kpi].[Template] NOCHECK CONSTRAINT [FK_Template_Department];

${deleteModuleData('1. Data of the other modules')}

    -- ---- 2. Organization and Employees ------------------------------------------------------------
    UPDATE [sec].[AppUser] SET [EmployeeId] = NULL WHERE [EmployeeId] IS NOT NULL;
    UPDATE [org].[Branch] SET [ManagerEmployeeId] = NULL WHERE [ManagerEmployeeId] IS NOT NULL;
    UPDATE [org].[Department] SET [HeadEmployeeId] = NULL WHERE [HeadEmployeeId] IS NOT NULL;
    UPDATE [org].[Section] SET [SupervisorEmployeeId] = NULL WHERE [SupervisorEmployeeId] IS NOT NULL;

    ALTER TABLE [hr].[Employee]              SET (SYSTEM_VERSIONING = OFF);
    ALTER TABLE [hr].[EmployeeBankAccount]   SET (SYSTEM_VERSIONING = OFF);
    ALTER TABLE [hr].[EmployeeBlacklist]     SET (SYSTEM_VERSIONING = OFF);

    DELETE FROM [hr].[EmployeeBlacklist];
    DELETE FROM [hr].[EmployeeDocument];
    DELETE FROM [hr].[EmploymentHistory];
    DELETE FROM [hr].[EmployeeBankAccount];
    DELETE FROM [hr].[Employee];

    DELETE FROM [org].[Position];
    DELETE FROM [org].[Section];
    DELETE FROM [org].[Department];
    DELETE FROM [org].[Branch];
    DELETE FROM [org].[Company];
    DELETE FROM [org].[JobGrade];

    -- History of the temporal tables (dynamic SQL: it runs only once versioning is off).
    EXEC (N'DELETE FROM [hr].[EmployeeHistory];
            DELETE FROM [hr].[EmployeeBankAccountHistory];
            DELETE FROM [hr].[EmployeeBlacklistHistory];');
    -- Versioning of the employee tables stays off until the employees, their links and the do-not-rehire entry are
    -- written, so the load leaves no history rows; it is switched on again below.

${enableTriggers()}

    ALTER SEQUENCE [lve].[LeaveApplicationNoSeq] RESTART WITH 1;
    ALTER SEQUENCE [rec].[JobOpeningCodeSeq] RESTART WITH 1;
    ALTER SEQUENCE [grv].[CaseNoSeq] RESTART WITH 1;

${reseedSql([...ORG_TABLES, ...MODULE_IDENTITY_TABLES])}

    -- ---- Sample shifts that are missing (by code; an existing shift is kept as it is) ----------------
    DECLARE @NewShift TABLE ([Id] int NOT NULL, [Code] nvarchar(20) NOT NULL);
    INSERT INTO [att].[Shift] ([Code], [Name], [StartTime], [EndTime], [BreakMinutes], [GraceMinutes], [IsNightShift], [Color], [Status])
    OUTPUT inserted.[Id], inserted.[Code] INTO @NewShift ([Id], [Code])
    SELECT v.[Code], v.[Name], v.[StartTime], v.[EndTime], v.[BreakMinutes], v.[GraceMinutes], v.[IsNightShift], v.[Color], v.[Status]
    FROM (VALUES
${rows(
  data.MOCK_SHIFTS.map((s) => [
    str(s.code),
    str(s.name),
    `CAST(${time(s.startTime)} AS time(0))`,
    `CAST(${time(s.endTime)} AS time(0))`,
    num(s.breakMinutes),
    num(s.graceMinutes),
    bit(s.isNightShift),
    `'${s.color}'`,
    `'${s.status}'`,
  ]),
)}
    ) AS v ([Code], [Name], [StartTime], [EndTime], [BreakMinutes], [GraceMinutes], [IsNightShift], [Color], [Status])
    WHERE NOT EXISTS (SELECT 1 FROM [att].[Shift] AS s WHERE s.[Code] = v.[Code]);

    INSERT INTO [att].[ShiftWorkDay] ([ShiftId], [DayOfWeek])
    SELECT n.[Id], v.[DayOfWeek]
    FROM (VALUES
${rows(data.MOCK_SHIFTS.flatMap((s) => s.workDays.map((d) => [str(s.code), num(d)])))}
    ) AS v ([Code], [DayOfWeek])
    JOIN @NewShift AS n ON n.[Code] = v.[Code];

    -- ---- 3. Insert: organization (people links are set after the employees exist) -------------------
    SET IDENTITY_INSERT [org].[Company] ON;
    INSERT INTO [org].[Company]
        ([Id], [Code], [Name], [LegalName], [RegistrationNumber], [TaxId], [Industry], [Email], [Phone], [Website],
         [Address], [City], [Country], [FoundedDate], [Status])
    VALUES
${rows(
  data.MOCK_COMPANIES.map((c) => [
    num(c.id),
    str(c.code),
    str(c.name),
    str(c.legalName),
    str(c.registrationNumber),
    str(c.taxId),
    str(c.industry),
    str(c.email),
    str(c.phone),
    str(c.website),
    str(c.address),
    str(c.city),
    str(c.country),
    date(c.foundedDate),
    str(c.status),
  ]),
)};
    SET IDENTITY_INSERT [org].[Company] OFF;

    SET IDENTITY_INSERT [org].[JobGrade] ON;
    INSERT INTO [org].[JobGrade] ([Id], [Code], [Name], [Level], [MinSalary], [MaxSalary], [Description], [Status])
    VALUES
${rows(
  data.MOCK_JOB_GRADES.map((g) => [
    num(g.id),
    str(g.code),
    str(g.name),
    num(g.level),
    num(g.minSalary),
    num(g.maxSalary),
    str(g.description),
    str(g.status),
  ]),
)};
    SET IDENTITY_INSERT [org].[JobGrade] OFF;

    SET IDENTITY_INSERT [org].[Branch] ON;
    INSERT INTO [org].[Branch]
        ([Id], [CompanyId], [Code], [Name], [Type], [Email], [Phone], [Address], [City], [Country], [OpenedDate], [Status])
    VALUES
${rows(
  data.MOCK_BRANCHES.map((b) => [
    num(b.id),
    num(b.companyId),
    str(b.code),
    str(b.name),
    str(b.type),
    str(b.email),
    str(b.phone),
    str(b.address),
    str(b.city),
    str(b.country),
    date(b.openedDate),
    str(b.status),
  ]),
)};
    SET IDENTITY_INSERT [org].[Branch] OFF;

    SET IDENTITY_INSERT [org].[Department] ON;
    INSERT INTO [org].[Department] ([Id], [CompanyId], [BranchId], [Code], [Name], [Description], [CostCenter], [Status])
    VALUES
${rows(
  data.MOCK_DEPARTMENTS.map((d) => [
    num(d.id),
    num(d.companyId),
    num(d.branchId),
    str(d.code),
    str(d.name),
    str(d.description),
    str(d.costCenter),
    str(d.status),
  ]),
)};
    SET IDENTITY_INSERT [org].[Department] OFF;

    SET IDENTITY_INSERT [org].[Section] ON;
    INSERT INTO [org].[Section] ([Id], [DepartmentId], [Code], [Name], [Description], [Status])
    VALUES
${rows(
  data.MOCK_SECTIONS.map((s) => [
    num(s.id),
    num(s.departmentId),
    str(s.code),
    str(s.name),
    str(s.description),
    str(s.status),
  ]),
)};
    SET IDENTITY_INSERT [org].[Section] OFF;

    SET IDENTITY_INSERT [org].[Position] ON;
    INSERT INTO [org].[Position]
        ([Id], [DepartmentId], [SectionId], [JobGradeId], [Code], [Title], [Description], [Headcount], [Status])
    VALUES
${rows(
  data.MOCK_POSITIONS.map((p) => [
    num(p.id),
    num(p.departmentId),
    num(p.sectionId),
    num(p.jobGradeId),
    str(p.code),
    str(p.title),
    str(p.description),
    num(p.headcount),
    str(p.status),
  ]),
)};
    SET IDENTITY_INSERT [org].[Position] OFF;

    UPDATE p SET p.[ReportsToPositionId] = v.[ReportsToPositionId]
    FROM [org].[Position] AS p
    JOIN (VALUES
${rows(
  data.MOCK_POSITIONS.filter((p) => p.reportsToPositionId !== null).map((p) => [
    num(p.id),
    num(p.reportsToPositionId),
  ]),
)}
    ) AS v ([Id], [ReportsToPositionId]) ON v.[Id] = p.[Id];

    -- ---- 3. Insert: employees (managers are set afterwards) -----------------------------------------
    SET IDENTITY_INSERT [hr].[Employee] ON;
    INSERT INTO [hr].[Employee]
        ([Id], [EmployeeCode], [FirstName], [LastName], [Gender], [DateOfBirth], [MaritalStatus], [Nationality],
         [NationalId], [WorkEmail], [PersonalEmail], [Phone], [AlternatePhone], [AddressLine1], [AddressLine2],
         [AddressCity], [AddressState], [AddressPostalCode], [AddressCountry], [EmergencyContactName],
         [EmergencyContactRelationship], [EmergencyContactPhone], [CompanyId], [BranchId], [DepartmentId], [SectionId],
         [PositionId], [JobGradeId], [ShiftId], [EmploymentType], [EmploymentStatus], [JoiningDate], [ProbationEndDate],
         [ExitDate], [PhotoUrl])
    SELECT v.[Id], v.[EmployeeCode], v.[FirstName], v.[LastName], v.[Gender], v.[DateOfBirth], v.[MaritalStatus],
           v.[Nationality], v.[NationalId], v.[WorkEmail], v.[PersonalEmail], v.[Phone], v.[AlternatePhone],
           v.[AddressLine1], v.[AddressLine2], v.[AddressCity], v.[AddressState], v.[AddressPostalCode],
           v.[AddressCountry], v.[EmergencyContactName], v.[EmergencyContactRelationship], v.[EmergencyContactPhone],
           v.[CompanyId], v.[BranchId], v.[DepartmentId], v.[SectionId], v.[PositionId], v.[JobGradeId], s.[Id],
           v.[EmploymentType], v.[EmploymentStatus], v.[JoiningDate], v.[ProbationEndDate], v.[ExitDate], NULL
    FROM (VALUES
${rows(
  employees.map((e) => [
    num(e.id),
    str(e.employeeCode),
    str(e.firstName),
    str(e.lastName),
    str(e.gender),
    date(e.dateOfBirth),
    str(e.maritalStatus),
    str(e.nationality),
    str(e.nationalId),
    str(e.workEmail),
    str(e.personalEmail),
    str(e.phone),
    str(e.alternatePhone),
    str(e.address.line1),
    str(e.address.line2),
    str(e.address.city),
    str(e.address.state),
    str(e.address.postalCode),
    str(e.address.country),
    str(e.emergencyContact.name),
    str(e.emergencyContact.relationship),
    str(e.emergencyContact.phone),
    num(e.companyId),
    num(e.branchId),
    num(e.departmentId),
    num(e.sectionId),
    num(e.positionId),
    num(e.jobGradeId),
    str(e.shiftId === null ? null : shiftCodes.get(e.shiftId)),
    str(e.employmentType),
    str(e.employmentStatus),
    date(e.joiningDate),
    date(e.probationEndDate),
    date(e.exitDate),
  ]),
)}
    ) AS v ([Id], [EmployeeCode], [FirstName], [LastName], [Gender], [DateOfBirth], [MaritalStatus], [Nationality],
            [NationalId], [WorkEmail], [PersonalEmail], [Phone], [AlternatePhone], [AddressLine1], [AddressLine2],
            [AddressCity], [AddressState], [AddressPostalCode], [AddressCountry], [EmergencyContactName],
            [EmergencyContactRelationship], [EmergencyContactPhone], [CompanyId], [BranchId], [DepartmentId],
            [SectionId], [PositionId], [JobGradeId], [ShiftCode], [EmploymentType], [EmploymentStatus], [JoiningDate],
            [ProbationEndDate], [ExitDate])
    LEFT JOIN [att].[Shift] AS s ON s.[Code] = v.[ShiftCode];
    SET IDENTITY_INSERT [hr].[Employee] OFF;

    UPDATE e SET e.[ManagerEmployeeId] = v.[ManagerEmployeeId]
    FROM [hr].[Employee] AS e
    JOIN (VALUES
${rows(employees.filter((e) => e.managerId !== null).map((e) => [num(e.id), num(e.managerId)]))}
    ) AS v ([Id], [ManagerEmployeeId]) ON v.[Id] = e.[Id];

    UPDATE b SET b.[ManagerEmployeeId] = v.[EmployeeId]
    FROM [org].[Branch] AS b
    JOIN (VALUES
${rows(data.MOCK_BRANCHES.filter((b) => b.managerEmployeeId !== null).map((b) => [num(b.id), num(b.managerEmployeeId)]))}
    ) AS v ([Id], [EmployeeId]) ON v.[Id] = b.[Id];

    UPDATE d SET d.[HeadEmployeeId] = v.[EmployeeId]
    FROM [org].[Department] AS d
    JOIN (VALUES
${rows(data.MOCK_DEPARTMENTS.filter((d) => d.headEmployeeId !== null).map((d) => [num(d.id), num(d.headEmployeeId)]))}
    ) AS v ([Id], [EmployeeId]) ON v.[Id] = d.[Id];

    UPDATE s SET s.[SupervisorEmployeeId] = v.[EmployeeId]
    FROM [org].[Section] AS s
    JOIN (VALUES
${rows(
  data.MOCK_SECTIONS.filter((s) => s.supervisorEmployeeId !== null).map((s) => [
    num(s.id),
    num(s.supervisorEmployeeId),
  ]),
)}
    ) AS v ([Id], [EmployeeId]) ON v.[Id] = s.[Id];

    -- ---- 3. Insert: bank accounts, documents, employment history ----------------------------------
    INSERT INTO [hr].[EmployeeBankAccount] ([EmployeeId], [BankName], [BranchName], [AccountName], [AccountNumber])
    VALUES
${rows(
  employees.map((e) => [
    num(e.id),
    str(e.bank.bankName),
    str(e.bank.branchName),
    str(e.bank.accountName),
    `'${e.bank.accountNumber}'`,
  ]),
)};

    INSERT INTO [hr].[EmployeeDocument] ([EmployeeId], [Type], [Title], [FileName], [UploadedOn], [ExpiryDate])
    VALUES
${rows(
  data.MOCK_EMPLOYEE_DOCUMENTS.map((d) => [
    num(d.employeeId),
    str(d.type),
    str(d.title),
    str(d.fileName),
    date(d.uploadedOn),
    date(d.expiryDate),
  ]),
)};

    INSERT INTO [hr].[EmploymentHistory] ([EmployeeId], [Date], [Type], [Title], [Details], [RecordedBy])
    VALUES
${rows(
  data.MOCK_EMPLOYMENT_HISTORY.map((h) => [
    num(h.employeeId),
    date(h.date),
    str(h.type),
    str(h.title),
    str(h.details),
    str(h.recordedBy),
  ]),
)};

    -- Next suggested employee code: EMP${String(employees.length + 1).padStart(3, '0')}.
    ALTER SEQUENCE [hr].[EmployeeCodeSeq] RESTART WITH ${employees.length + 1};

    -- ---- Re-link user accounts by email (only EmployeeId changes; unmatched accounts stay NULL) ------
    UPDATE u SET u.[EmployeeId] = e.[Id]
    FROM [sec].[AppUser] AS u
    JOIN [hr].[Employee] AS e ON e.[WorkEmail] = u.[Email];

${
  blacklist
    ? `    -- ---- Do-not-rehire entry (${fullName(blacklist.employeeId)}): recorded by the first active HR Administrator account.
    DECLARE @AdminUserId int, @AdminName nvarchar(130);
    SELECT TOP (1) @AdminUserId = u.[Id], @AdminName = u.[DisplayName]
    FROM [sec].[AppUser] AS u
    JOIN [sec].[Role] AS r ON r.[Id] = u.[RoleId]
    WHERE r.[Name] = N'HR Administrator' AND u.[Status] = 'Active'
    ORDER BY u.[Id];

    IF @AdminUserId IS NOT NULL
        INSERT INTO [hr].[EmployeeBlacklist] ([EmployeeId], [Reason], [BlacklistedOn], [BlacklistedByUserId], [BlacklistedByName])
        VALUES (${num(blacklist.employeeId)}, ${str(blacklist.reason)}, ${date(blacklist.blacklistedOn)}, @AdminUserId, @AdminName);
    ELSE
        PRINT N'Skipped the do-not-rehire entry: no active HR Administrator account.';
`
    : ''
}
    -- The employee tables are written: versioning on again (their history is empty).
    ALTER TABLE [hr].[Employee]            SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hr].[EmployeeHistory], DATA_CONSISTENCY_CHECK = ON));
    ALTER TABLE [hr].[EmployeeBankAccount] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hr].[EmployeeBankAccountHistory], DATA_CONSISTENCY_CHECK = ON));
    ALTER TABLE [hr].[EmployeeBlacklist]   SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hr].[EmployeeBlacklistHistory], DATA_CONSISTENCY_CHECK = ON));

    -- ---- Re-point kept leave policies and KPI templates -----------------------------------------
    -- First delete, through the code kept before the delete, those whose grade or department code is not in the new
    -- data: the new rows reuse ids from 1, so their old id could now point at another grade or department.
    -- (No scorecard item refers to a template any more: the module data was deleted in step 1.)
    DELETE p FROM [lve].[LeavePolicy] AS p
    JOIN @PolicyGrade AS m ON m.[PolicyId] = p.[Id]
    WHERE NOT EXISTS (SELECT 1 FROM [org].[JobGrade] AS g WHERE g.[Code] = m.[GradeCode]);
    IF @@ROWCOUNT > 0 PRINT N'Deleted leave policies whose job grade code is not in the new data.';
    DELETE t FROM [kpi].[Template] AS t
    JOIN @TemplateDepartment AS m ON m.[TemplateId] = t.[Id]
    WHERE NOT EXISTS (SELECT 1 FROM [org].[Department] AS d WHERE d.[Code] = m.[DepartmentCode]);
    IF @@ROWCOUNT > 0 PRINT N'Deleted KPI templates whose department code is not in the new data.';

    UPDATE p SET p.[JobGradeId] = g.[Id]
    FROM [lve].[LeavePolicy] AS p
    JOIN @PolicyGrade AS m ON m.[PolicyId] = p.[Id]
    JOIN [org].[JobGrade] AS g ON g.[Code] = m.[GradeCode];
    DELETE p FROM [lve].[LeavePolicy] AS p
    WHERE p.[JobGradeId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [org].[JobGrade] AS g WHERE g.[Id] = p.[JobGradeId]);
    IF @@ROWCOUNT > 0 PRINT N'Deleted leave policies whose job grade code is not in the new data.';
    ALTER TABLE [lve].[LeavePolicy] WITH CHECK CHECK CONSTRAINT [FK_LeavePolicy_JobGrade];

    UPDATE t SET t.[DepartmentId] = d.[Id]
    FROM [kpi].[Template] AS t
    JOIN @TemplateDepartment AS m ON m.[TemplateId] = t.[Id]
    JOIN [org].[Department] AS d ON d.[Code] = m.[DepartmentCode];
    DELETE t FROM [kpi].[Template] AS t
    WHERE t.[DepartmentId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [org].[Department] AS d WHERE d.[Id] = t.[DepartmentId]);
    IF @@ROWCOUNT > 0 PRINT N'Deleted KPI templates whose department code is not in the new data.';
    ALTER TABLE [kpi].[Template] WITH CHECK CHECK CONSTRAINT [FK_Template_Department];

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
${identityInsertOff(ORG_IDENTITY_INSERT)}
    THROW;
END CATCH;

-- ---- Summary --------------------------------------------------------------------------------------
SELECT N'org.Company' AS [Table], COUNT(*) AS [Rows] FROM [org].[Company]
UNION ALL SELECT N'org.Branch', COUNT(*) FROM [org].[Branch]
UNION ALL SELECT N'org.Department', COUNT(*) FROM [org].[Department]
UNION ALL SELECT N'org.Section', COUNT(*) FROM [org].[Section]
UNION ALL SELECT N'org.JobGrade', COUNT(*) FROM [org].[JobGrade]
UNION ALL SELECT N'org.Position', COUNT(*) FROM [org].[Position]
UNION ALL SELECT N'hr.Employee', COUNT(*) FROM [hr].[Employee]
UNION ALL SELECT N'hr.EmployeeBankAccount', COUNT(*) FROM [hr].[EmployeeBankAccount]
UNION ALL SELECT N'hr.EmployeeDocument', COUNT(*) FROM [hr].[EmployeeDocument]
UNION ALL SELECT N'hr.EmploymentHistory', COUNT(*) FROM [hr].[EmploymentHistory]
UNION ALL SELECT N'hr.EmployeeBlacklist', COUNT(*) FROM [hr].[EmployeeBlacklist]
UNION ALL SELECT N'hr.Employee without shift', COUNT(*) FROM [hr].[Employee] WHERE [ShiftId] IS NULL;

SELECT u.[Email], u.[DisplayName], e.[EmployeeCode]
FROM [sec].[AppUser] AS u
LEFT JOIN [hr].[Employee] AS e ON e.[Id] = u.[EmployeeId]
ORDER BY u.[Id];
`;
}

// =================================================================================================
// Shared by both scripts: emptying the data of the modules
// =================================================================================================

/** LocalDB, or a server on this machine. A NULL machine or host name never passes. */
const LOCAL_SERVER_GUARD = `IF ISNULL(CAST(SERVERPROPERTY('IsLocalDB') AS int), 0) <> 1
   AND ISNULL(CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)), N'?') <> ISNULL(HOST_NAME(), N'')
    THROW 50002, N'This server is not on this machine. The script runs against a local development server only.', 1;`;

/** The organization and employee tables script 1 empties and loads. */
const ORG_TABLES = [
  'org.Company',
  'org.Branch',
  'org.Department',
  'org.Section',
  'org.JobGrade',
  'org.Position',
  'hr.Employee',
  'hr.EmployeeBankAccount',
  'hr.EmployeeDocument',
  'hr.EmploymentHistory',
  'hr.EmployeeBlacklist',
];

/** The module tables both scripts empty (the ones with an identity column). */
const MODULE_IDENTITY_TABLES = [
  'att.AttendanceRecord',
  'att.AttendanceAdjustment',
  'att.OvertimeRecord',
  'lve.LeaveApplication',
  'pay.PayrollRun',
  'pay.PayrollRunWarning',
  'pay.Payslip',
  'pay.PayslipLine',
  'pay.SalaryAssignment',
  'rec.JobOpening',
  'rec.Candidate',
  'rec.Application',
  'rec.Interview',
  'kpi.EvaluationPeriod',
  'kpi.Scorecard',
  'kpi.ScorecardItem',
  'kpi.ReviewAction',
  'kpi.Attachment',
  'grv.Case',
  'grv.Assignment',
  'grv.Attachment',
  'grv.AuditLog',
  'grv.Communication',
  'grv.CorrectiveAction',
  'grv.Escalation',
  'grv.InvestigationNote',
  'grv.ResolutionProposal',
  'grv.StatusHistory',
  'app.ActivityLog',
  'app.Notification',
  'app.ImportIssue',
];

/** Tables each script inserts into with SET IDENTITY_INSERT ON (switched off again in CATCH). */
const ORG_IDENTITY_INSERT = [
  'org.Company',
  'org.JobGrade',
  'org.Branch',
  'org.Department',
  'org.Section',
  'org.Position',
  'hr.Employee',
];
const MODULES_IDENTITY_INSERT = [
  'lve.LeaveApplication',
  'pay.PayrollRun',
  'pay.Payslip',
  'rec.JobOpening',
  'rec.Candidate',
  'rec.Application',
  'rec.Interview',
  'kpi.EvaluationPeriod',
  'kpi.Scorecard',
  'kpi.ScorecardItem',
  'grv.Case',
];

const bracket = (table) =>
  table
    .split('.')
    .map((part) => `[${part}]`)
    .join('.');

/** After an error: no table may stay in IDENTITY_INSERT mode for the session (a rerun would fail with 8107). */
const identityInsertOff = (tables) =>
  tables.map((t) => `    SET IDENTITY_INSERT ${bracket(t)} OFF;`).join('\n');

/**
 * The listed tables number from 1 again, each only when it is empty. A table that never had a row is left alone: it
 * already starts at 1 (RESEED 0 would make its first id 0).
 */
function reseedSql(tables) {
  return `    -- Emptied tables number from 1 again (only the tables listed here, and only when they are empty).
    DECLARE @Reseed nvarchar(max) = N'';
    SELECT @Reseed += N'IF NOT EXISTS (SELECT 1 FROM ' + QUOTENAME(s.[name]) + N'.' + QUOTENAME(t.[name]) + N') '
                    + N'DBCC CHECKIDENT (N''' + QUOTENAME(s.[name]) + N'.' + QUOTENAME(t.[name]) + N''', RESEED, 0) WITH NO_INFOMSGS;'
    FROM sys.identity_columns AS ic
    JOIN sys.tables AS t ON t.[object_id] = ic.[object_id]
    JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
    JOIN (VALUES
${rows(tables.map((x) => x.split('.').map((part) => `N'${part}'`)))}
    ) AS l ([Schema], [Table]) ON l.[Schema] = s.[name] AND l.[Table] = t.[name]
    WHERE ic.[last_value] IS NOT NULL;
    EXEC sys.sp_executesql @Reseed;`;
}

/** The temporal tables of the module data (current table, history table). */
const MODULE_TEMPORAL = [
  ['[lve].[LeaveApplication]', '[lve].[LeaveApplicationHistory]'],
  ['[pay].[PayrollRun]', '[pay].[PayrollRunHistory]'],
  ['[pay].[SalaryAssignment]', '[pay].[SalaryAssignmentHistory]'],
  ['[kpi].[EvaluationPeriod]', '[kpi].[EvaluationPeriodHistory]'],
  ['[kpi].[Scorecard]', '[kpi].[ScorecardHistory]'],
  ['[kpi].[ScorecardItem]', '[kpi].[ScorecardItemHistory]'],
];

/** Triggers that forbid deleting (or inserting) paid runs and submitted or closed cases. */
const FINAL_TRIGGERS = [
  ['[pay].[TR_PayrollRun_PaidIsFinal]', '[pay].[PayrollRun]'],
  ['[pay].[TR_PayrollRunWarning_PaidRunIsFinal]', '[pay].[PayrollRunWarning]'],
  ['[pay].[TR_Payslip_PaidRunIsFinal]', '[pay].[Payslip]'],
  ['[pay].[TR_PayslipLine_PaidRunIsFinal]', '[pay].[PayslipLine]'],
  ['[grv].[TR_Attachment_Fixed]', '[grv].[Attachment]'],
  ['[grv].[TR_Case_ClosedIsFinal]', '[grv].[Case]'],
  ['[grv].[TR_Case_SubmittedIsFixed]', '[grv].[Case]'],
];

const enableTriggers = () =>
  FINAL_TRIGGERS.map(([t, on]) => `    ENABLE TRIGGER ${t} ON ${on};`).join('\n');

/** Deletes every module row that refers to employees or organization units, with the history of their temporal tables. */
function deleteModuleData(title) {
  return `    -- ---- Triggers that forbid deleting paid runs and submitted or closed cases ----------------------
${FINAL_TRIGGERS.map(([t, on]) => `    DISABLE TRIGGER ${t} ON ${on};`).join('\n')}

    -- ---- Temporal tables: versioning off while their rows and history are deleted -------------------
${MODULE_TEMPORAL.map(([t]) => `    ALTER TABLE ${t.padEnd(26)} SET (SYSTEM_VERSIONING = OFF);`).join('\n')}

    -- ---- ${title} ${'-'.repeat(Math.max(3, 92 - title.length))}
    DELETE FROM [app].[Notification];
    DELETE FROM [app].[ActivityLog];
    DELETE FROM [app].[ImportIssue];
    DELETE FROM [app].[ImportBatch];

    DELETE FROM [grv].[Attachment];
    DELETE FROM [grv].[Assignment];
    DELETE FROM [grv].[CaseSubmitter];
    DELETE FROM [grv].[InvestigationNote];
    DELETE FROM [grv].[ResolutionProposal];
    DELETE FROM [grv].[StatusHistory];
    DELETE FROM [grv].[CorrectiveAction];
    DELETE FROM [grv].[Escalation];
    DELETE FROM [grv].[Communication];
    DELETE FROM [grv].[AuditLog];
    DELETE FROM [grv].[Case];

    DELETE FROM [kpi].[Attachment];
    DELETE FROM [kpi].[ReviewAction];
    DELETE FROM [kpi].[ScorecardItem];
    DELETE FROM [kpi].[Scorecard];
    DELETE FROM [kpi].[EvaluationPeriod];

    DELETE FROM [rec].[Interview];
    DELETE FROM [rec].[Application];
    DELETE FROM [rec].[JobOpening];
    DELETE FROM [rec].[Candidate];

    DELETE FROM [pay].[PayslipLine];
    DELETE FROM [pay].[Payslip];
    DELETE FROM [pay].[PayrollRunWarning];
    DELETE FROM [pay].[PayrollRun];
    DELETE FROM [pay].[SalaryAssignment];

    DELETE FROM [lve].[LeaveApplication];

    DELETE FROM [att].[AttendanceAdjustment];
    DELETE FROM [att].[AttendanceRecord];
    DELETE FROM [att].[OvertimeRecord];

    -- History of the temporal tables (dynamic SQL: it runs only once versioning is off).
    EXEC (N'${MODULE_TEMPORAL.map(([, h]) => `DELETE FROM ${h};`).join('\n            ')}');

${MODULE_TEMPORAL.map(([t, h]) => `    ALTER TABLE ${t.padEnd(26)} SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ${h}, DATA_CONSISTENCY_CHECK = ON));`).join('\n')}`;
}

// =================================================================================================
// Script 2: the module data
// =================================================================================================

/** The labelled development pseudonym key of the repository, decoded. Refuses anything else. */
async function developmentPseudonymKey() {
  const settings = JSON.parse(await readFile(devSettingsFile, 'utf8'));
  const encoded = settings?.Grievance?.PseudonymKey;
  if (typeof encoded !== 'string')
    fail('no Grievance:PseudonymKey in appsettings.Development.json');
  const key = Buffer.from(encoded, 'base64');
  if (key.toString('utf8') !== DEV_KEY_TEXT) {
    fail(
      'Grievance:PseudonymKey of appsettings.Development.json is not the labelled development value',
    );
  }
  return key;
}

/** HMACSHA256(key, "apex-hrms:grievance-submitter:<employee id>"), as HmacGrievancePseudonyms computes it. */
const pseudonym = (key, employeeId) =>
  createHmac('sha256', key).update(`apex-hrms:grievance-submitter:${employeeId}`, 'utf8').digest();

const sha256 = (text) => createHash('sha256').update(text, 'utf8').digest();

function checkModules(data) {
  const employees = byId(data.MOCK_EMPLOYEES);
  const today = data.todayIso();
  const nameToEmployee = new Map();
  for (const e of data.MOCK_EMPLOYEES) {
    const name = `${e.firstName} ${e.lastName}`;
    if (nameToEmployee.has(name)) fail(`two employees are called ${name}`);
    nameToEmployee.set(name, e.id);
  }
  const employee = (id, what) => {
    if (!employees.has(id)) fail(`${what}: unknown employee ${id}`);
    return employees.get(id);
  };

  // ---- Reference data
  for (const [list, what] of [
    [data.MOCK_LEAVE_TYPES, 'leave type'],
    [data.MOCK_SALARY_COMPONENTS, 'salary component'],
    [data.MOCK_SALARY_STRUCTURES, 'salary structure'],
    [data.MOCK_KPI_CATEGORIES, 'KPI category'],
    [data.MOCK_KPI_TEMPLATES, 'KPI template'],
    [data.MOCK_GRIEVANCE_CATEGORIES, 'grievance category'],
  ]) {
    unique(
      list.map((x) => x.code),
      `${what} code`,
    );
    for (const x of list) {
      if (!/^[A-Z0-9][A-Z0-9-]*$/.test(x.code) || x.code.length > 20)
        fail(`${what} code ${x.code}`);
    }
  }
  unique(
    data.MOCK_LEAVE_POLICIES.map((p) => p.name),
    'leave policy name',
  );
  unique(
    data.MOCK_HOLIDAYS.map((h) => h.date),
    'holiday date',
  );
  const leaveTypes = byId(data.MOCK_LEAVE_TYPES);
  const holidays = new Set(data.MOCK_HOLIDAYS.map((h) => h.date));

  // ---- Leave
  const leave = data.MOCK_LEAVE_APPLICATIONS;
  unique(
    leave.map((a) => a.applicationNo),
    'leave application number',
  );
  leave.forEach((a, index) => {
    const no = a.applicationNo;
    if (a.id !== index + 1) fail(`leave ids must be 1..n (${no})`);
    if (!/^LV-\d{4}-\d{4,}$/.test(no)) fail(`${no}: number format`);
    employee(a.employeeId, no);
    if (!leaveTypes.has(a.leaveTypeId)) fail(`${no}: unknown leave type`);
    if (a.endDate < a.startDate) fail(`${no}: end before start`);
    if (!(a.days > 0) || !Number.isInteger(a.days * 10)) fail(`${no}: days`);
    if (a.halfDay && (a.startDate !== a.endDate || a.days !== 0.5)) fail(`${no}: half day`);
    if (!['Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled'].includes(a.status))
      fail(`${no}: status`);
    if (['Approved', 'Rejected'].includes(a.status)) {
      if (!a.actionedOn || a.actionedOn > today) fail(`${no}: decision date`);
      employee(a.approverId, `${no} approver`);
      if (a.approverId === a.employeeId) fail(`${no}: decided by the employee`);
    }
    if (a.status === 'Rejected' && !a.approverRemarks?.trim()) fail(`${no}: rejection reason`);
    if (a.appliedOn > today) fail(`${no}: applied in the future`);
    fits(a.reason, 500, `${no} reason`);
  });

  // ---- Attendance
  const records = data.MOCK_ATTENDANCE_RECORDS;
  const recordKeys = new Map();
  const shifts = byId(data.MOCK_SHIFTS);
  for (const r of records) {
    const key = `${r.employeeId}|${r.date}`;
    if (recordKeys.has(key)) fail(`two attendance records for ${key}`);
    recordKeys.set(key, r);
    employee(r.employeeId, `attendance ${key}`);
    if (!shifts.has(r.shiftId)) fail(`attendance ${key}: unknown shift`);
    for (const m of [r.workedMinutes, r.lateMinutes, r.earlyLeaveMinutes, r.overtimeMinutes]) {
      if (!Number.isInteger(m) || m < 0 || m > 32767) fail(`attendance ${key}: minutes ${m}`);
    }
    if (
      !['Present', 'Absent', 'Late', 'Half Day', 'On Leave', 'Holiday', 'Weekend'].includes(
        r.status,
      )
    )
      fail(`attendance ${key}: status`);
    if (!['Biometric', 'Web', 'Mobile', 'Manual'].includes(r.source))
      fail(`attendance ${key}: source`);
    if (r.date > today) fail(`attendance ${key}: in the future`);
    fits(r.remarks, 250, `attendance ${key} remarks`);
  }
  const pending = new Set();
  for (const a of data.MOCK_ATTENDANCE_ADJUSTMENTS) {
    const key = `${a.employeeId}|${a.date}`;
    if (!recordKeys.has(key)) fail(`adjustment ${key}: no attendance record`);
    if (!a.requestedCheckIn && !a.requestedCheckOut && !a.requestedStatus)
      fail(`adjustment ${key}: nothing requested`);
    if (a.status === 'Pending') {
      if (pending.has(key)) fail(`adjustment ${key}: two pending requests`);
      pending.add(key);
      if (a.decidedOn || a.decidedBy) fail(`adjustment ${key}: pending with a decision`);
    } else if (!a.decidedOn || !a.decidedBy) fail(`adjustment ${key}: decision`);
    if (a.decidedBy && !nameToEmployee.has(a.decidedBy))
      fail(`adjustment ${key}: unknown decider ${a.decidedBy}`);
    fits(a.reason, 250, `adjustment ${key} reason`);
  }
  unique(
    data.MOCK_OVERTIME_RECORDS.map((o) => `${o.employeeId}|${o.date}`),
    'overtime employee and date',
  );
  const minutesOf = (t) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));
  for (const o of data.MOCK_OVERTIME_RECORDS) {
    const key = `overtime ${o.employeeId}|${o.date}`;
    employee(o.employeeId, key);
    if (o.minutes !== (minutesOf(o.endTime) - minutesOf(o.startTime) + 1440) % 1440)
      fail(`${key}: minutes do not match the times`);
    if (o.minutes < 15 || o.minutes > 720) fail(`${key}: minutes out of range`);
    if (o.status === 'Pending' ? o.decidedOn || o.decidedBy : !o.decidedOn || !o.decidedBy)
      fail(`${key}: decision`);
    if (o.decidedBy && !nameToEmployee.has(o.decidedBy)) fail(`${key}: unknown decider`);
    fits(o.reason, 120, `${key} reason`);
  }

  // ---- Payroll
  const structures = byId(data.MOCK_SALARY_STRUCTURES);
  unique(
    data.MOCK_SALARY_ASSIGNMENTS.map((a) => `${a.employeeId}|${a.effectiveFrom}`),
    'salary assignment employee and date',
  );
  for (const a of data.MOCK_SALARY_ASSIGNMENTS) {
    employee(a.employeeId, 'salary assignment');
    if (!structures.has(a.structureId)) fail(`salary assignment ${a.id}: unknown structure`);
    if (!(a.basicSalary > 0) || !isCents(a.basicSalary)) fail(`salary assignment ${a.id}: basic`);
  }
  const runs = data.MOCK_PAYROLL_RUNS;
  unique(
    runs.map((r) => `${r.year}-${r.month}`),
    'payroll period',
  );
  runs.forEach((r, index) => {
    if (r.id !== index + 1) fail(`payroll run ids must be 1..n`);
    if (!['Draft', 'Paid'].includes(r.status)) fail(`run ${r.periodName}: status ${r.status}`);
    if (r.startDate !== `${r.year}-${String(r.month).padStart(2, '0')}-01`)
      fail(`run ${r.periodName}: start date`);
    const slips = data.MOCK_PAYSLIPS.filter((p) => p.runId === r.id);
    if (r.status === 'Draft' && (slips.length > 0 || r.employeeCount !== 0))
      fail(`run ${r.periodName}: a draft has no payslips`);
    if (slips.length !== r.employeeCount) fail(`run ${r.periodName}: employee count`);
    const sum = (pick) => slips.reduce((t, p) => t + cents(pick(p)), 0);
    for (const v of [
      r.totalBasic,
      r.totalAllowances,
      r.totalGross,
      r.totalDeductions,
      r.totalNet,
    ]) {
      if (!isCents(v)) fail(`run ${r.periodName}: totals are not whole cents`);
    }
    if (cents(r.totalBasic) !== sum((p) => p.basicSalary)) fail(`run ${r.periodName}: basic`);
    if (cents(r.totalGross) !== sum((p) => p.grossSalary)) fail(`run ${r.periodName}: gross`);
    if (cents(r.totalDeductions) !== sum((p) => p.totalDeductions))
      fail(`run ${r.periodName}: deductions`);
    if (cents(r.totalNet) !== sum((p) => p.netSalary)) fail(`run ${r.periodName}: net`);
    if (cents(r.totalGross) !== cents(r.totalBasic) + cents(r.totalAllowances))
      fail(`run ${r.periodName}: gross = basic + allowances`);
    if (cents(r.totalNet) !== cents(r.totalGross) - cents(r.totalDeductions))
      fail(`run ${r.periodName}: net = gross - deductions`);
  });
  unique(
    data.MOCK_PAYSLIPS.map((p) => `${p.runId}|${p.employeeId}`),
    'payslip run and employee',
  );
  data.MOCK_PAYSLIPS.forEach((p, index) => {
    const key = `payslip ${p.periodName} ${p.employeeCode}`;
    if (p.id !== index + 1) fail(`payslip ids must be 1..n`);
    employee(p.employeeId, key);
    if (!/^•{4} \d{4}$/.test(p.accountMasked)) fail(`${key}: masked account`);
    for (const v of [p.basicSalary, p.grossSalary, p.totalDeductions, p.netSalary]) {
      if (!isCents(v) || v < 0) fail(`${key}: amounts`);
    }
    if (cents(p.netSalary) !== cents(p.grossSalary) - cents(p.totalDeductions))
      fail(`${key}: net = gross - deductions`);
    if (p.grossSalary < p.basicSalary) fail(`${key}: gross below basic`);
    const earnings = p.earnings.reduce((t, l) => t + cents(l.amount), 0);
    const deductions = p.deductions.reduce((t, l) => t + cents(l.amount), 0);
    if (cents(p.grossSalary) !== cents(p.basicSalary) + earnings)
      fail(`${key}: gross = basic + earning lines`);
    if (cents(p.totalDeductions) !== deductions) fail(`${key}: deductions = deduction lines`);
    if (p.periodDays < 28 || p.periodDays > 31) fail(`${key}: period days`);
    if (p.daysEmployed < 1 || p.daysEmployed > p.periodDays) fail(`${key}: days employed`);
    if (p.workingDays > p.periodDays) fail(`${key}: working days`);
    if (!Number.isInteger(p.unpaidLeaveDays * 10)) fail(`${key}: unpaid leave days`);
    for (const l of [...p.earnings, ...p.deductions]) {
      if (!isCents(l.amount) || l.amount < 0) fail(`${key}: line ${l.code}`);
      fits(l.code, 20, `${key} line code`);
      fits(l.name, 60, `${key} line name`);
      fits(l.basis, 100, `${key} line basis`);
    }
    fits(p.employeeName, 130, `${key} name`);
    fits(p.bankName, 60, `${key} bank`);
  });

  // ---- Recruitment
  const openings = byId(data.MOCK_JOB_OPENINGS);
  const candidates = byId(data.MOCK_CANDIDATES);
  const applications = byId(data.MOCK_APPLICATIONS);
  const departments = byId(data.MOCK_DEPARTMENTS);
  unique(
    data.MOCK_JOB_OPENINGS.map((j) => j.code),
    'job opening code',
  );
  for (const j of data.MOCK_JOB_OPENINGS) {
    if (!/^JOB-\d{4}-\d{3,}$/.test(j.code)) fail(`${j.code}: format`);
    if (!departments.has(j.departmentId)) fail(`${j.code}: department`);
    employee(j.hiringManagerId, `${j.code} hiring manager`);
    if (j.closingDate < j.postedOn) fail(`${j.code}: closes before it is posted`);
    if (j.vacancies < 1 || j.vacancies > 50) fail(`${j.code}: vacancies`);
    if (!['Full-time', 'Part-time', 'Contract', 'Internship'].includes(j.employmentType))
      fail(`${j.code}: employment type`);
    fits(j.title, 80, `${j.code} title`);
  }
  unique(
    data.MOCK_CANDIDATES.map((c) => c.email),
    'candidate email',
  );
  for (const c of data.MOCK_CANDIDATES) {
    if (c.email !== c.email.toLowerCase()) fail(`candidate ${c.id}: email is not lower case`);
    fits(c.email, 100, 'candidate email');
    fits(c.phone, 25, 'candidate phone');
    if (!['Career site', 'Referral', 'Job board', 'LinkedIn', 'Agency'].includes(c.source))
      fail(`candidate ${c.id}: source`);
  }
  unique(
    data.MOCK_APPLICATIONS.map((a) => `${a.jobOpeningId}|${a.candidateId}`),
    'application opening and candidate',
  );
  for (const a of data.MOCK_APPLICATIONS) {
    if (!openings.has(a.jobOpeningId) || !candidates.has(a.candidateId))
      fail(`application ${a.id}: references`);
    if ((a.stage === 'Rejected') !== Boolean(a.rejectionReason?.trim()))
      fail(`application ${a.id}: a rejection reason exactly when Rejected`);
  }
  for (const i of data.MOCK_INTERVIEWS) {
    if (!applications.has(i.applicationId)) fail(`interview ${i.id}: application`);
    employee(i.interviewerId, `interview ${i.id}`);
    if (i.durationMinutes < 15 || i.durationMinutes > 240) fail(`interview ${i.id}: duration`);
    const completed = i.status === 'Completed';
    if (completed !== (i.score !== null) || completed !== (i.recommendation !== null))
      fail(`interview ${i.id}: score and recommendation exactly when completed`);
  }

  // ---- KPI
  const periods = byId(data.MOCK_EVALUATION_PERIODS);
  const templates = byId(data.MOCK_KPI_TEMPLATES);
  const branches = byId(data.MOCK_BRANCHES);
  const positions = byId(data.MOCK_POSITIONS);
  for (const p of data.MOCK_EVALUATION_PERIODS) {
    if (p.endDate <= p.startDate) fail(`period ${p.name}: dates`);
    if (p.submissionDueDate && p.submissionDueDate < p.startDate)
      fail(`period ${p.name}: due date`);
  }
  unique(
    data.MOCK_SCORECARDS.map((s) => `${s.employeeId}|${s.periodId}`),
    'scorecard employee and period',
  );
  const twoDecimals = (v) => v === null || isCents(v);
  for (const s of data.MOCK_SCORECARDS) {
    const key = `scorecard ${s.id}`;
    employee(s.employeeId, key);
    const period = periods.get(s.periodId);
    if (!period || period.companyId !== s.companyId) fail(`${key}: period of another company`);
    if (branches.get(s.branchId)?.companyId !== s.companyId) fail(`${key}: branch`);
    if (departments.get(s.departmentId)?.branchId !== s.branchId) fail(`${key}: department`);
    if (positions.get(s.positionId)?.departmentId !== s.departmentId) fail(`${key}: position`);
    if (s.reviewerEmployeeId === s.employeeId) fail(`${key}: reviews itself`);
    if (s.status !== 'Planning' && !s.planFinalizedOn) fail(`${key}: plan finalized`);
    if (!['Planning', 'Draft'].includes(s.status) && !s.submittedOn) fail(`${key}: submitted`);
    if (['Approved', 'Finalized'].includes(s.status) && s.overallScore === null)
      fail(`${key}: score`);
    if ((s.status === 'Finalized') !== Boolean(s.finalizedOn)) fail(`${key}: finalized date`);
    if (!twoDecimals(s.overallScore)) fail(`${key}: overall score`);
    if (s.managerRating !== null && (s.managerRating < 1 || s.managerRating > 5))
      fail(`${key}: manager rating`);
    unique(
      s.items.map((i) => i.templateId),
      `${key} template`,
    );
    for (const i of s.items) {
      if (!templates.has(i.templateId)) fail(`${key}: template ${i.templateId}`);
      if (!twoDecimals(i.achievementPct) || !twoDecimals(i.weightedScore))
        fail(`${key}: item scores`);
      if ((i.achievementPct === null) !== (i.weightedScore === null))
        fail(`${key}: achievement and weighted score together`);
      if (i.achievementPct !== null && i.actualValue === null) fail(`${key}: score without result`);
    }
    for (const h of s.history) {
      if (!nameToEmployee.has(h.actorName)) fail(`${key}: unknown actor ${h.actorName}`);
      if (['Reject', 'Return'].includes(h.action) && !h.comment?.trim())
        fail(`${key}: ${h.action} without a reason`);
    }
  }

  // ---- Grievances
  const categories = byId(data.MOCK_GRIEVANCE_CATEGORIES);
  const userEmployee = new Map(data.MOCK_USERS.map((u) => [u.id, u.employeeId]));
  const links = new Map(data.MOCK_GRIEVANCE_SUBMITTERS.map((l) => [l.caseId, l]));
  unique(
    data.MOCK_GRIEVANCE_CASES.filter((c) => c.caseNo).map((c) => c.caseNo),
    'case number',
  );
  for (const c of data.MOCK_GRIEVANCE_CASES) {
    const key = `grievance ${c.id}`;
    const link = links.get(c.id);
    if (!link || (link.employeeId === null) !== c.isAnonymous) fail(`${key}: submitter link`);
    if (!categories.has(c.categoryId)) fail(`${key}: category`);
    if (c.isAnonymous && !categories.get(c.categoryId).allowsAnonymous)
      fail(`${key}: anonymous in a named-only category`);
    if (c.isAnonymous && c.companyId !== null) fail(`${key}: anonymous with a placement`);
    const draft = c.status === 'Draft';
    if (draft !== (c.caseNo === null) || draft !== (c.submittedAtUtc === null))
      fail(`${key}: number and submission`);
    if (!draft && !/^GRV-\d{4}-\d{5}$/.test(c.caseNo)) fail(`${key}: number format`);
    if (draft && (c.priority !== null || c.isEscalated || c.companyId !== null))
      fail(`${key}: draft is triaged`);
    if (!draft && (c.subject.trim().length < 5 || c.description.trim().length < 20))
      fail(`${key}: subject or description too short`);
    if ((c.status === 'AwaitingEmployeeResponse') !== (c.returnStatus !== null))
      fail(`${key}: return status`);
    if (c.status === 'Rejected' && !c.rejectionReason) fail(`${key}: rejection reason`);
    if (['Resolved', 'Closed'].includes(c.status) && (!c.resolutionSummary || !c.resolvedAtUtc))
      fail(`${key}: resolution`);
    if (c.status === 'Closed' && !c.closedAtUtc) fail(`${key}: closed time`);
    if (c.archivedAtUtc && !['Closed', 'Rejected'].includes(c.status)) fail(`${key}: archived`);
    if (c.acknowledgedAtUtc && (!c.priority || !c.targetResolutionDate))
      fail(`${key}: acknowledged without triage`);
    fits(c.subject, 200, `${key} subject`);
    fits(c.description, 4000, `${key} description`);
    for (const h of c.history) {
      const submitter = h.actorRole === 'Submitter';
      if (submitter !== ['Submitter', 'Anonymous submitter'].includes(h.actorName))
        fail(`${key}: history actor name`);
      if (submitter && (h.actorUserId !== null || h.actorEmployeeId !== null))
        fail(`${key}: the submitter is identified in the history`);
      if (!submitter && (h.actorEmployeeId === null || !userEmployee.has(h.actorUserId)))
        fail(`${key}: history actor`);
      if (
        ['Reject', 'Reopen', 'Escalate', 'RejectResolution', 'RequestInfo'].includes(h.action) &&
        !h.reason?.trim()
      )
        fail(`${key}: ${h.action} without a reason`);
      if (['Escalate', 'Unassign', 'Archive'].includes(h.action) && h.fromStatus !== h.toStatus)
        fail(`${key}: ${h.action} changes the status`);
    }
    const leads = c.assignments.filter((a) => a.role === 'Lead' && a.unassignedAtUtc === null);
    if (leads.length > 1) fail(`${key}: two active leads`);
    for (const a of c.assignments) {
      employee(a.investigatorEmployeeId, `${key} investigator`);
      if (!userEmployee.has(a.assignedByUserId)) fail(`${key}: assigned by`);
      if (a.unassignedAtUtc && !userEmployee.has(a.unassignedByUserId))
        fail(`${key}: unassigned by`);
    }
    if (c.proposals.filter((p) => p.decision === 'Pending').length > 1)
      fail(`${key}: two pending proposals`);
    for (const p of c.proposals) {
      if (!userEmployee.has(p.proposedByUserId)) fail(`${key}: proposed by`);
      if (p.decision !== 'Pending' && !userEmployee.has(p.decidedByUserId))
        fail(`${key}: decided by`);
      if (p.decidedByUserId !== null && p.decidedByUserId === p.proposedByUserId)
        fail(`${key}: proposal decided by its author`);
    }
    if (c.escalations.filter((x) => x.clearedAtUtc === null).length > 1)
      fail(`${key}: two open escalations`);
    for (const n of c.notes) {
      if (!userEmployee.has(n.authorUserId)) fail(`${key}: note author`);
      if ((n.kind === 'Interview') !== (n.interviewDate !== null)) fail(`${key}: interview date`);
    }
    for (const m of c.communications) {
      const fromSubmitter = m.direction === 'FromSubmitter';
      if (fromSubmitter !== (m.authorRole === 'Submitter')) fail(`${key}: message author role`);
      if (fromSubmitter && (m.authorUserId !== null || m.authorEmployeeId !== null))
        fail(`${key}: the submitter is identified on a message`);
      if (fromSubmitter && m.authorName !== (c.isAnonymous ? 'Anonymous submitter' : 'Submitter'))
        fail(`${key}: submitter message name`);
      if (m.inReplyToId !== null || m.kind === 'InfoResponse')
        fail(`${key}: replies are not supported by the seed`);
    }
    for (const f of c.attachments) {
      if (f.uploadedByRole === 'Submitter' && (f.visibility !== 'Shared' || f.uploadedByUserId))
        fail(`${key}: submitter file`);
      if (f.communicationId !== null || f.noteId !== null)
        fail(`${key}: files on messages or notes are not supported by the seed`);
      if (f.sizeBytes <= 0 || f.sizeBytes > 10485760) fail(`${key}: file size`);
    }
    for (const a of c.correctiveActions) {
      if ((a.status === 'Implemented') !== (a.completedOn !== null))
        fail(`${key}: corrective action completion`);
    }
  }

  return { nameToEmployee, userEmployee };
}

function modulesSql(data, key) {
  const { nameToEmployee, userEmployee } = checkModules(data);
  const today = data.todayIso();
  const generatedAt = new Date().toISOString().slice(0, 16).replace('T', ' ');
  const employees = byId(data.MOCK_EMPLOYEES);
  const nameOf = (id) => `${employees.get(id).firstName} ${employees.get(id).lastName}`;

  // User ids are the accounts of the sample people, looked up when the script runs (sec.AppUser.EmployeeId).
  const actors = new Set();
  /** Nullable user-id column: the person's account, or NULL when they have none. */
  const user = (employeeId) => {
    if (employeeId === null || employeeId === undefined) return 'NULL';
    actors.add(employeeId);
    return `@U${employeeId}`;
  };
  /** NOT NULL user-id column: the person's account, or minus the employee id when they have none (no FK; ids are >= 1). */
  const userOrMarker = (employeeId) => {
    actors.add(employeeId);
    return `COALESCE(@U${employeeId}, -${employeeId})`;
  };
  const userByName = (name) =>
    name && nameToEmployee.has(name) ? user(nameToEmployee.get(name)) : 'NULL';
  const userOfMockUser = (mockUserId) =>
    mockUserId === null ? 'NULL' : user(userEmployee.get(mockUserId));
  const userOrMarkerOfMockUser = (mockUserId) => userOrMarker(userEmployee.get(mockUserId));

  // Reference records by code (looked up when the script runs).
  const codeOf = (list) => new Map(list.map((x) => [x.id, x.code]));
  const shiftCode = codeOf(data.MOCK_SHIFTS);
  const leaveTypeCode = codeOf(data.MOCK_LEAVE_TYPES);
  const componentCode = codeOf(data.MOCK_SALARY_COMPONENTS);
  const structureCode = codeOf(data.MOCK_SALARY_STRUCTURES);
  const kpiCategoryCode = codeOf(data.MOCK_KPI_CATEGORIES);
  const templateCode = codeOf(data.MOCK_KPI_TEMPLATES);
  const grievanceCategoryCode = codeOf(data.MOCK_GRIEVANCE_CATEGORIES);
  const gradeCode = codeOf(data.MOCK_JOB_GRADES);
  const departmentCode = codeOf(data.MOCK_DEPARTMENTS);

  // Why a record can still be missing after step 2 (only shifts are not added by this script).
  const NAME_TAKEN =
    'is missing and could not be added: another record has its name. Rename that record and run again.';
  const lookups = [
    [
      'S',
      data.MOCK_SHIFTS,
      '[att].[Shift]',
      'shift',
      'is missing. Run seed-organization-employees.sql first.',
    ],
    [
      'LT',
      data.MOCK_LEAVE_TYPES,
      '[lve].[LeaveType]',
      'leave type',
      'is missing and could not be added.',
    ],
    [
      'SS',
      data.MOCK_SALARY_STRUCTURES,
      '[pay].[SalaryStructure]',
      'salary structure',
      'is missing and could not be added.',
    ],
    ['KC', data.MOCK_KPI_CATEGORIES, '[kpi].[Category]', 'KPI category', NAME_TAKEN],
    [
      'KT',
      data.MOCK_KPI_TEMPLATES,
      '[kpi].[Template]',
      'KPI template',
      'is missing and could not be added: its KPI category or department is missing.',
    ],
    ['GC', data.MOCK_GRIEVANCE_CATEGORIES, '[grv].[Category]', 'grievance category', NAME_TAKEN],
  ];
  const lookupSql = lookups
    .flatMap(([prefix, list, table, what, why]) =>
      list.map(
        (
          x,
        ) => `    DECLARE @${prefix}${x.id} int = (SELECT [Id] FROM ${table} WHERE [Code] = ${str(x.code)});
    IF @${prefix}${x.id} IS NULL THROW 50010, N'The ${what} ${x.code} ${why}', 1;`,
      ),
    )
    .join('\n');

  // ---- Leave applications ----------------------------------------------------------------------
  const leave = data.MOCK_LEAVE_APPLICATIONS.map((a) => {
    const decided = a.status === 'Approved' || a.status === 'Rejected';
    const cancelled = a.status === 'Cancelled';
    // The API records who cancelled and when (the mock does not): the employee, the day after applying.
    const cancelledOn = data.addDays(a.appliedOn, 1) > today ? today : data.addDays(a.appliedOn, 1);
    const actor = decided ? a.approverId : cancelled ? a.employeeId : null;
    return [
      num(a.id),
      str(a.applicationNo),
      num(a.employeeId),
      `@LT${a.leaveTypeId}`,
      date(a.startDate),
      date(a.endDate),
      num(a.days),
      bit(a.halfDay),
      str(a.reason),
      str(a.documentName),
      `'${a.status}'`,
      date(a.appliedOn),
      decided ? num(a.approverId) : 'NULL',
      user(actor),
      actor === null ? 'NULL' : str(nameOf(actor)),
      decided ? date(a.actionedOn) : cancelled ? date(cancelledOn) : 'NULL',
      decided ? str(a.approverRemarks) : cancelled ? str('Cancelled.') : 'NULL',
    ];
  });

  // ---- Attendance ------------------------------------------------------------------------------
  const attendance = data.MOCK_ATTENDANCE_RECORDS.map((r) => [
    num(r.employeeId),
    date(r.date),
    `@S${r.shiftId}`,
    time(r.checkIn),
    time(r.checkOut),
    num(r.workedMinutes),
    num(r.lateMinutes),
    num(r.earlyLeaveMinutes),
    num(r.overtimeMinutes),
    `'${r.status}'`,
    `'${r.source}'`,
    str(r.remarks),
  ]);
  const adjustments = data.MOCK_ATTENDANCE_ADJUSTMENTS.map((a) => [
    num(a.employeeId),
    date(a.date),
    time(a.currentCheckIn),
    time(a.currentCheckOut),
    time(a.requestedCheckIn),
    time(a.requestedCheckOut),
    a.requestedStatus ? `'${a.requestedStatus}'` : 'NULL',
    str(a.reason),
    `'${a.status}'`,
    date(a.requestedOn),
    userByName(a.decidedBy),
    str(a.decidedBy),
    date(a.decidedOn),
    str(a.remarks),
  ]);
  const overtime = data.MOCK_OVERTIME_RECORDS.map((o) => [
    num(o.employeeId),
    date(o.date),
    time(o.startTime),
    time(o.endTime),
    num(o.minutes),
    str(o.reason),
    `'${o.status}'`,
    date(o.requestedOn),
    userByName(o.decidedBy),
    str(o.decidedBy),
    date(o.decidedOn),
    str(o.remarks),
  ]);

  // ---- Payroll ---------------------------------------------------------------------------------
  // The mock records no processor; the database needs one, other than the approver: May Thu Kyaw (Payroll Officer).
  const PROCESSOR = 4;
  const APPROVER = nameToEmployee.get('Su Myat Noe');
  const assignments = data.MOCK_SALARY_ASSIGNMENTS.map((a) => [
    num(a.employeeId),
    `@SS${a.structureId}`,
    money(a.basicSalary),
    date(a.effectiveFrom),
    str(a.remarks),
  ]);
  const payrollRuns = data.MOCK_PAYROLL_RUNS.map((r) => {
    const paid = r.status === 'Paid';
    if (paid && r.approvedBy !== nameOf(APPROVER)) fail(`run ${r.periodName}: approver`);
    return [
      num(r.id),
      str(r.periodName),
      num(r.year),
      num(r.month),
      date(r.startDate),
      date(r.endDate),
      date(r.payDate),
      `'${r.status}'`,
      num(r.employeeCount),
      money(r.totalBasic),
      money(r.totalAllowances),
      money(r.totalGross),
      money(r.totalDeductions),
      money(r.totalNet),
      date(r.processedOn),
      paid ? userOrMarker(PROCESSOR) : 'NULL',
      paid ? str(nameOf(PROCESSOR)) : 'NULL',
      paid ? userOrMarker(APPROVER) : 'NULL',
      paid ? str(r.approvedBy) : 'NULL',
      date(r.approvedOn),
      date(r.paidOn),
    ];
  });
  const payslips = data.MOCK_PAYSLIPS.map((p) => [
    num(p.id),
    num(p.runId),
    num(p.employeeId),
    str(p.periodName),
    str(p.employeeCode),
    str(p.employeeName),
    num(p.departmentId),
    str(p.departmentName),
    str(p.positionTitle),
    str(p.companyName),
    str(p.bankName),
    str(p.accountMasked),
    money(p.basicSalary),
    money(p.grossSalary),
    money(p.totalDeductions),
    money(p.netSalary),
    num(p.periodDays),
    num(p.daysEmployed),
    num(p.workingDays),
    num(p.unpaidLeaveDays),
    num(p.overtimeMinutes),
    date(p.generatedOn),
  ]);
  const payslipLines = data.MOCK_PAYSLIPS.flatMap((p) => [
    ...p.earnings.map((l, i) => [
      num(p.id),
      `'Earning'`,
      num(i + 1),
      str(l.code),
      str(l.name),
      money(l.amount),
      str(l.basis),
    ]),
    ...p.deductions.map((l, i) => [
      num(p.id),
      `'Deduction'`,
      num(i + 1),
      str(l.code),
      str(l.name),
      money(l.amount),
      str(l.basis),
    ]),
  ]);

  // ---- Recruitment -----------------------------------------------------------------------------
  const openings = data.MOCK_JOB_OPENINGS.map((j) => [
    num(j.id),
    str(j.code),
    str(j.title),
    num(j.departmentId),
    num(j.hiringManagerId),
    `'${j.employmentType}'`,
    num(j.vacancies),
    date(j.postedOn),
    date(j.closingDate),
    str(j.description),
    `'${j.status}'`,
  ]);
  const candidates = data.MOCK_CANDIDATES.map((c) => [
    num(c.id),
    str(c.firstName),
    str(c.lastName),
    str(c.email),
    str(c.phone),
    `'${c.source}'`,
    str(c.currentEmployer),
    num(c.experienceYears),
    str(c.resumeName),
    str(c.notes),
  ]);
  const applications = data.MOCK_APPLICATIONS.map((a) => [
    num(a.id),
    num(a.jobOpeningId),
    num(a.candidateId),
    `'${a.stage}'`,
    date(a.appliedOn),
    str(a.rejectionReason),
    str(a.notes),
  ]);
  const interviews = data.MOCK_INTERVIEWS.map((i) => [
    num(i.id),
    num(i.applicationId),
    `'${i.round}'`,
    date(i.date),
    time(i.startTime),
    num(i.durationMinutes),
    `'${i.mode}'`,
    num(i.interviewerId),
    str(i.location),
    `'${i.status}'`,
    num(i.score),
    i.recommendation ? `'${i.recommendation}'` : 'NULL',
    str(i.feedback),
  ]);

  // ---- KPI ------------------------------------------------------------------------------------
  const periods = data.MOCK_EVALUATION_PERIODS.map((p) => [
    num(p.id),
    num(p.companyId),
    str(p.name),
    `'${p.periodType}'`,
    date(p.startDate),
    date(p.endDate),
    date(p.submissionDueDate),
    bit(p.requiresHrReview),
    `'${p.status}'`,
  ]);
  const actorOfAction = (card, action) => {
    const entry = card.history.find((h) => h.action === action);
    return entry ? nameToEmployee.get(entry.actorName) : null;
  };
  const scorecards = data.MOCK_SCORECARDS.map((s) => [
    num(s.id),
    num(s.employeeId),
    num(s.periodId),
    num(s.companyId),
    num(s.branchId),
    num(s.departmentId),
    num(s.positionId),
    num(s.reviewerEmployeeId),
    `'${s.status}'`,
    str(s.employeeComment),
    num(s.managerRating),
    str(s.managerComment),
    str(s.hrComment),
    num(s.overallScore),
    str(s.ratingLabel),
    // KpiCalculator.CalculationVersion of the API: the version every stored score is calculated with.
    s.overallScore === null ? 'NULL' : '1',
    date(s.planFinalizedOn),
    date(s.submittedOn),
    date(s.reviewedOn),
    date(s.finalizedOn),
    s.status === 'Finalized' ? user(actorOfAction(s, 'Finalize')) : 'NULL',
  ]);
  const scorecardItems = data.MOCK_SCORECARDS.flatMap((s) => {
    // Assigned by whoever finalized the plan (HR); a plan still in Planning by Su Myat Noe, as the mock's HR actor.
    const assignedBy = actorOfAction(s, 'Finalize Plan') ?? APPROVER;
    const period = data.MOCK_EVALUATION_PERIODS.find((p) => p.id === s.periodId);
    const assignedOn = period.startDate > today ? today : period.startDate;
    return s.items.map((i) => [
      num(i.id),
      num(s.id),
      `@KT${i.templateId}`,
      `@KC${i.categoryId}`,
      str(i.kpiCode),
      str(i.kpiName),
      `'${i.measurementType}'`,
      str(i.unit),
      `'${i.direction}'`,
      `'${i.scoringMethod}'`,
      num(i.target),
      num(i.rangeMin),
      num(i.rangeMax),
      num(i.maxRating),
      num(i.weight),
      num(i.maxAchievementPct),
      date(i.startDate),
      date(i.endDate),
      num(i.sortOrder),
      user(assignedBy),
      date(assignedOn),
      num(i.actualValue),
      str(i.employeeComment),
      num(i.achievementPct),
      num(i.weightedScore),
      num(i.managerRating),
      str(i.managerComment),
    ]);
  });
  const reviewActions = data.MOCK_SCORECARDS.flatMap((s) =>
    s.history.map((h) => {
      const actor = nameToEmployee.get(h.actorName);
      return [
        num(s.id),
        `'${h.step}'`,
        `'${h.action}'`,
        `'${h.fromStatus}'`,
        `'${h.toStatus}'`,
        user(actor),
        num(actor),
        str(h.actorName),
        str(h.comment),
        num(h.overallScore),
        utc(h.occurredAtUtc),
      ];
    }),
  );

  // ---- Grievances ------------------------------------------------------------------------------
  const cases = data.MOCK_GRIEVANCE_CASES;
  const links = new Map(data.MOCK_GRIEVANCE_SUBMITTERS.map((l) => [l.caseId, l]));
  const lastChange = (c) =>
    [c.createdAtUtc, ...c.history.map((h) => h.occurredAtUtc)].sort().at(-1);
  const caseRows = cases.map((c) => [
    num(c.id),
    c.caseNo ? `'${c.caseNo}'` : 'NULL',
    `@GC${c.categoryId}`,
    str(c.subject),
    str(c.description),
    date(c.incidentDate),
    str(c.location),
    str(c.involvedPersonText),
    num(c.involvedDepartmentId),
    num(c.subjectEmployeeId),
    bit(c.isAnonymous),
    `'${c.status}'`,
    c.priority ? `'${c.priority}'` : 'NULL',
    date(c.targetResolutionDate),
    c.returnStatus ? `'${c.returnStatus}'` : 'NULL',
    bit(c.isEscalated),
    num(c.companyId),
    num(c.branchId),
    num(c.departmentId),
    num(c.positionId),
    utc(c.declarationAcceptedAtUtc),
    utc(c.submittedAtUtc),
    utc(c.acknowledgedAtUtc),
    utc(c.resolvedAtUtc),
    utc(c.closedAtUtc),
    str(c.resolutionSummary),
    bit(c.resolutionVisibleToSubmitter),
    str(c.rejectionReason),
    utc(c.archivedAtUtc),
    date(c.retentionUntil),
    bit(c.legalHold),
    utc(c.lastSubmitterUpdateAtUtc),
    utc(c.createdAtUtc),
    utc(lastChange(c)),
  ]);
  const submitters = cases.map((c) => {
    const link = links.get(c.id);
    return [
      num(c.id),
      c.isAnonymous ? 'NULL' : num(link.employeeId),
      c.isAnonymous
        ? `0x${pseudonym(key, submitterOf(c.id)).toString('hex').toUpperCase()}`
        : 'NULL',
      utc(link.seenAtUtc),
    ];
  });
  const assignmentRows = cases.flatMap((c) =>
    c.assignments.map((a) => [
      num(c.id),
      num(a.investigatorEmployeeId),
      `'${a.role}'`,
      utc(a.assignedAtUtc),
      userOrMarkerOfMockUser(a.assignedByUserId),
      utc(a.unassignedAtUtc),
      a.unassignedAtUtc ? userOrMarkerOfMockUser(a.unassignedByUserId) : 'NULL',
      str(a.reason),
    ]),
  );
  const historyRows = cases.flatMap((c) =>
    c.history.map((h) => [
      num(c.id),
      `'${h.fromStatus}'`,
      `'${h.toStatus}'`,
      `'${h.action}'`,
      userOfMockUser(h.actorUserId),
      num(h.actorEmployeeId),
      str(h.actorName),
      `'${h.actorRole}'`,
      str(h.reason),
      bit(h.visibleToSubmitter),
      utc(h.occurredAtUtc),
    ]),
  );
  const noteRows = cases.flatMap((c) =>
    c.notes.map((n) => [
      num(c.id),
      `'${n.kind}'`,
      str(n.body),
      date(n.interviewDate),
      str(n.intervieweeText),
      userOrMarkerOfMockUser(n.authorUserId),
      num(n.authorEmployeeId),
      str(n.authorName),
      utc(n.createdAtUtc),
    ]),
  );
  const messageRows = cases.flatMap((c) =>
    c.communications.map((m) => [
      num(c.id),
      `'${m.direction}'`,
      `'${m.kind}'`,
      str(m.body),
      `'${m.authorRole}'`,
      userOfMockUser(m.authorUserId),
      num(m.authorEmployeeId),
      str(m.authorName),
      utc(m.createdAtUtc),
    ]),
  );
  const attachmentRows = cases.flatMap((c) =>
    c.attachments.map((f) => {
      const seed = `apex-hrms-seed:grievance-attachment:${c.id}:${f.id}`;
      return [
        num(c.id),
        `'${f.visibility}'`,
        `'${f.uploadedByRole}'`,
        f.uploadedByRole === 'Submitter' ? 'NULL' : userOfMockUser(f.uploadedByUserId),
        str(f.fileName),
        `'${f.contentType}'`,
        num(f.sizeBytes),
        `'${sha256(`storage:${seed}`).toString('hex').slice(0, 32)}'`,
        `0x${sha256(`content:${seed}`).toString('hex').toUpperCase()}`,
        utc(f.uploadedAtUtc),
        utc(f.removedAtUtc),
        utc(f.uploadedAtUtc),
        utc(f.uploadedAtUtc),
      ];
    }),
  );
  const correctiveRows = cases.flatMap((c) =>
    c.correctiveActions.map((a) => [
      num(c.id),
      str(a.actionType),
      str(a.description),
      str(a.responsibleText),
      date(a.dueDate),
      date(a.completedOn),
      `'${a.status}'`,
      userOfMockUser(a.createdByUserId),
    ]),
  );
  const proposalRows = cases.flatMap((c) =>
    c.proposals.map((p) => [
      num(c.id),
      str(p.summary),
      userOrMarkerOfMockUser(p.proposedByUserId),
      num(p.proposedByEmployeeId),
      utc(p.proposedAtUtc),
      `'${p.decision}'`,
      p.decision === 'Pending' ? 'NULL' : userOrMarkerOfMockUser(p.decidedByUserId),
      utc(p.decidedAtUtc),
      str(p.decisionComment),
    ]),
  );
  const escalationRows = cases.flatMap((c) =>
    c.escalations.map((x) => [
      num(c.id),
      str(x.reason),
      bit(x.isImmediate),
      userOrMarkerOfMockUser(x.escalatedByUserId),
      utc(x.escalatedAtUtc),
      utc(x.clearedAtUtc),
      str(x.outcome),
    ]),
  );
  const caseSequence = Math.max(
    0,
    ...cases.filter((c) => c.caseNo).map((c) => Number(c.caseNo.slice(-5))),
  );
  function submitterOf(caseId) {
    // The submitter of an anonymous sample case: the mock keeps only its demo pseudonym, so find the employee
    // whose demo pseudonym it is.
    const link = links.get(caseId);
    for (const e of data.MOCK_EMPLOYEES) {
      if (data.grievanceDemoPseudonym(e.id) === link.pseudonym) return e.id;
    }
    return fail(`grievance ${caseId}: no employee has its demo pseudonym`);
  }

  // ---- Activity and notifications -------------------------------------------------------------
  const activity = data.MOCK_ACTIVITIES.map((a) => [
    `'${a.category}'`,
    str(a.message),
    userByName(a.actor),
    str(a.actor),
    utc(a.timestamp),
  ]);
  const notifications = data.MOCK_NOTIFICATIONS.map((n) => [
    'NULL',
    str(n.title),
    str(n.message),
    `'${n.icon}'`,
    str(n.link),
    utc(n.timestamp),
    bit(n.read),
    n.read ? utc(n.timestamp) : 'NULL',
  ]);

  // ---- Reference data the sample needs, added when missing ---------------------------------------
  const lt = data.MOCK_LEAVE_TYPES;
  const policies = data.MOCK_LEAVE_POLICIES;
  const anonymousCases = cases.filter((c) => c.isAnonymous);
  const actorList = [...actors].sort((a, b) => a - b);

  const counts = {
    leave: leave.length,
    attendance: attendance.length,
    adjustments: adjustments.length,
    overtime: overtime.length,
    assignments: assignments.length,
    runs: payrollRuns.length,
    paidRuns: data.MOCK_PAYROLL_RUNS.filter((r) => r.status === 'Paid').length,
    payslips: payslips.length,
    payslipLines: payslipLines.length,
  };

  return `-- =================================================================================================
-- Module sample data: DELETES the existing data of the modules and loads the demo's leave, attendance, payroll,
-- recruitment, KPI, grievance, activity and notification records.
-- Generated by ui/scripts/generate-db-seed.mjs on ${generatedAt} UTC from ui/src/app/mock-data. Do not edit
-- by hand: change the mock data and generate again. Dates relative to "today" were fixed on ${today}, and today's
-- attendance shows only the check-ins made before the time it was generated (as the demo does at that time).
-- All data is fictional. Payroll figures are demonstrations, not statutory calculations.
--
-- LOCAL DEVELOPMENT DATABASE ONLY. A human runs this (Claude Code is not allowed to), AFTER
-- seed-organization-employees.sql of the same generation (it refuses to run when the sample employees and
-- organization units are not in the database):
--   sqlcmd -S "(localdb)\\MSSQLLocalDB" -d HR -E -b -i docs\\test-data\\sql\\seed-organization-employees.sql
--   sqlcmd -S "(localdb)\\MSSQLLocalDB" -d HR -E -b -i docs\\test-data\\sql\\seed-modules.sql
-- It can be run again on its own (it deletes the module data first), for example after creating the sample user
-- accounts (see "User ids" below).
--
-- What it does, in one transaction (any error rolls everything back):
--   1. Deletes the data of the modules, as seed-organization-employees.sql does in its step 1 (attendance, leave
--      applications, payroll runs and salary assignments, recruitment, KPI periods and scorecards, grievances with
--      their audit log, activity log, notifications, Excel import history), with the history of the temporal tables.
--   2. Adds the reference records the sample data needs when no record has their code (existing records are never
--      changed): leave types (${lt.map((x) => x.code).join(', ')}), ${policies.length} leave policies (by name), the ${data.MOCK_HOLIDAYS.length} sample holidays
--      (by date), salary components and the structures ${data.MOCK_SALARY_STRUCTURES.map((s) => s.code).join(', ')} (with their components), KPI categories and
--      templates, the default rating bands (only into an empty table) and grievance categories.
--   3. Inserts, with the ids of the demo:
--      Leave        ${counts.leave} leave applications (LV-...; the next number is ${counts.leave + 1})
--      Attendance   ${counts.attendance} attendance records (the last ${60} days and today), ${counts.adjustments} adjustment requests, ${counts.overtime} overtime entries
--      Payroll      ${counts.assignments} salary assignments; ${counts.runs} payroll runs (${counts.paidRuns} Paid months and the current month as Draft),
--                   ${counts.payslips} payslips with ${counts.payslipLines} lines
--      Recruitment  ${openings.length} job openings, ${candidates.length} candidates, ${applications.length} applications, ${interviews.length} interviews
--      KPI          ${periods.length} evaluation periods, ${scorecards.length} scorecards with ${scorecardItems.length} items and ${reviewActions.length} review actions
--      Grievances   ${caseRows.length} cases (${cases.filter((c) => c.status === 'Draft').length} drafts, ${anonymousCases.length} anonymous) with ${historyRows.length} status changes, ${assignmentRows.length} assignments, ${noteRows.length} investigation notes,
--                   ${messageRows.length} messages, ${attachmentRows.length} file records, ${correctiveRows.length} corrective actions, ${proposalRows.length} resolution proposals, ${escalationRows.length} escalations
--      Activity     ${activity.length} activity entries and ${notifications.length} notifications (shown to every user)
--   4. Restarts the number sequences after the sample: leave LV-...-${String(counts.leave + 1).padStart(4, '0')}, job openings JOB-...-${String(openings.length + 1).padStart(3, '0')},
--      grievance cases GRV-...-${String(caseSequence + 1).padStart(5, '0')}.
--
-- Triggers and history: the payroll and grievance "final" triggers refuse to insert a Paid run, its payslips or a
-- Closed case directly (those states are reached through the workflow). They are disabled while the sample is
-- loaded and enabled again before the commit. The temporal tables keep no history for the inserted rows.
--
-- User ids ("who did it"): the sample people act through their user accounts, looked up by employee when the
-- script runs (sec.AppUser.EmployeeId). A person without an account gets NULL, or minus their employee id (for
-- example -4 for May Thu Kyaw) in the columns that cannot be NULL and in grv.Escalation.EscalatedByUserId: user ids
-- start at 1, so it matches no account.
-- Create the sample accounts (Settings > Users) and run this script again to link the history to them.
--
-- Anonymous grievances: the submitter of an anonymous case is stored only as HMACSHA256(key, employee id). The
-- ${anonymousCases.length} anonymous sample cases use the labelled DEVELOPMENT key of api/src/Apex.Hrms.Api/appsettings.Development.json
-- (Grievance:PseudonymKey, "DEVELOPMENT-ONLY ... LocalDB only"). They appear under "My grievances" of their
-- submitter only while the API runs with that development key; with any other key nobody can find them again.
--
-- Files: the grievance file records have no files behind them (downloading one answers "The file of this
-- attachment is not available."). The leave documents and CVs are file names only, as in the demo.
-- =================================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ---- Guards: the local HR database only, with the sample employees and organization ----------------
IF DB_NAME() <> N'HR'
    THROW 50001, N'Run this script in the local HR database only.', 1;
${LOCAL_SERVER_GUARD}
IF EXISTS (
    SELECT v.[Id], v.[Code] FROM (VALUES
${rows(data.MOCK_EMPLOYEES.map((e) => [num(e.id), str(e.employeeCode)]))}
    ) AS v ([Id], [Code])
    EXCEPT SELECT [Id], [EmployeeCode] FROM [hr].[Employee])
   OR EXISTS (
    SELECT v.[Kind], v.[Id], v.[Code] FROM (VALUES
${rows([
  ...data.MOCK_COMPANIES.map((x) => ["'C'", num(x.id), str(x.code)]),
  ...data.MOCK_BRANCHES.map((x) => ["'B'", num(x.id), str(x.code)]),
  ...data.MOCK_DEPARTMENTS.map((x) => ["'D'", num(x.id), str(x.code)]),
  ...data.MOCK_POSITIONS.map((x) => ["'P'", num(x.id), str(x.code)]),
  ...data.MOCK_JOB_GRADES.map((x) => ["'G'", num(x.id), str(x.code)]),
])}
    ) AS v ([Kind], [Id], [Code])
    EXCEPT (SELECT 'C', [Id], [Code] FROM [org].[Company]
            UNION ALL SELECT 'B', [Id], [Code] FROM [org].[Branch]
            UNION ALL SELECT 'D', [Id], [Code] FROM [org].[Department]
            UNION ALL SELECT 'P', [Id], [Code] FROM [org].[Position]
            UNION ALL SELECT 'G', [Id], [Code] FROM [org].[JobGrade]))
    THROW 50003, N'The sample employees and organization units are not in this database (by id and code). Run seed-organization-employees.sql first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

${deleteModuleData('1. Delete the data of the modules')}

${reseedSql(MODULE_IDENTITY_TABLES)}

    -- ---- 2. Reference data the sample needs (added when missing; never changed) ----------------------
    INSERT INTO [lve].[LeaveType]
        ([Code], [Name], [Description], [DefaultDaysPerYear], [IsPaid], [RequiresDocument], [MaxConsecutiveDays],
         [CarryForward], [MaxCarryForwardDays], [Color], [EligibleGender], [Status])
    SELECT v.[Code], v.[Name], v.[Description], v.[DefaultDaysPerYear], v.[IsPaid], v.[RequiresDocument],
           v.[MaxConsecutiveDays], v.[CarryForward], v.[MaxCarryForwardDays], v.[Color], v.[EligibleGender], v.[Status]
    FROM (VALUES
${rows(
  lt.map((t) => [
    str(t.code),
    str(t.name),
    str(t.description),
    num(t.defaultDaysPerYear),
    bit(t.isPaid),
    bit(t.requiresDocument),
    num(t.maxConsecutiveDays),
    bit(t.carryForward),
    num(t.maxCarryForwardDays),
    `'${t.color}'`,
    t.eligibleGender ? `'${t.eligibleGender}'` : 'CAST(NULL AS varchar(30))',
    `'${t.status}'`,
  ]),
)}
    ) AS v ([Code], [Name], [Description], [DefaultDaysPerYear], [IsPaid], [RequiresDocument], [MaxConsecutiveDays],
            [CarryForward], [MaxCarryForwardDays], [Color], [EligibleGender], [Status])
    WHERE NOT EXISTS (SELECT 1 FROM [lve].[LeaveType] AS t WHERE t.[Code] = v.[Code]);

    -- Leave policies by name; skipped when an active policy already covers the same leave type, grade and type.
    INSERT INTO [lve].[LeavePolicy] ([Name], [LeaveTypeId], [JobGradeId], [EmploymentType], [DaysPerYear], [Accrual], [MinServiceMonths], [Status])
    SELECT v.[Name], t.[Id], g.[Id], v.[EmploymentType], v.[DaysPerYear], v.[Accrual], v.[MinServiceMonths], v.[Status]
    FROM (VALUES
${rows(
  policies.map((p) => [
    str(p.name),
    str(leaveTypeCode.get(p.leaveTypeId)),
    p.jobGradeId === null ? 'CAST(NULL AS nvarchar(20))' : str(gradeCode.get(p.jobGradeId)),
    p.employmentType ? `'${p.employmentType}'` : 'CAST(NULL AS varchar(30))',
    num(p.daysPerYear),
    `'${p.accrual}'`,
    num(p.minServiceMonths),
    `'${p.status}'`,
  ]),
)}
    ) AS v ([Name], [LeaveTypeCode], [GradeCode], [EmploymentType], [DaysPerYear], [Accrual], [MinServiceMonths], [Status])
    JOIN [lve].[LeaveType] AS t ON t.[Code] = v.[LeaveTypeCode]
    LEFT JOIN [org].[JobGrade] AS g ON g.[Code] = v.[GradeCode]
    WHERE (v.[GradeCode] IS NULL OR g.[Id] IS NOT NULL)
      AND NOT EXISTS (SELECT 1 FROM [lve].[LeavePolicy] AS p WHERE p.[Name] = v.[Name])
      AND NOT EXISTS (SELECT 1 FROM [lve].[LeavePolicy] AS p
                      WHERE p.[Status] = 'Active' AND v.[Status] = 'Active' AND p.[LeaveTypeId] = t.[Id]
                        AND (p.[JobGradeId] = g.[Id] OR (p.[JobGradeId] IS NULL AND g.[Id] IS NULL))
                        AND (p.[EmploymentType] = v.[EmploymentType] OR (p.[EmploymentType] IS NULL AND v.[EmploymentType] IS NULL)));

    -- Holidays (by date): leave days, payroll working days and the attendance "Holiday" days of the sample use them.
    INSERT INTO [att].[Holiday] ([Date], [Name], [Type])
    SELECT v.[Date], v.[Name], v.[Type]
    FROM (VALUES
${rows(data.MOCK_HOLIDAYS.map((h) => [`CAST(${date(h.date)} AS date)`, str(h.name), `'${h.type}'`]))}
    ) AS v ([Date], [Name], [Type])
    WHERE NOT EXISTS (SELECT 1 FROM [att].[Holiday] AS h WHERE h.[Date] = v.[Date]);

    INSERT INTO [pay].[SalaryComponent] ([Code], [Name], [Type], [Calculation], [Value], [Description], [Status])
    SELECT v.[Code], v.[Name], v.[Type], v.[Calculation], v.[Value], v.[Description], v.[Status]
    FROM (VALUES
${rows(
  data.MOCK_SALARY_COMPONENTS.map((c) => [
    str(c.code),
    str(c.name),
    `'${c.type}'`,
    `'${c.calculation}'`,
    money(c.value),
    str(c.description),
    `'${c.status}'`,
  ]),
)}
    ) AS v ([Code], [Name], [Type], [Calculation], [Value], [Description], [Status])
    WHERE NOT EXISTS (SELECT 1 FROM [pay].[SalaryComponent] AS c WHERE c.[Code] = v.[Code]);

    DECLARE @NewStructure TABLE ([Id] int NOT NULL, [Code] nvarchar(20) NOT NULL);
    INSERT INTO [pay].[SalaryStructure] ([Code], [Name], [Description], [Status])
    OUTPUT inserted.[Id], inserted.[Code] INTO @NewStructure ([Id], [Code])
    SELECT v.[Code], v.[Name], v.[Description], v.[Status]
    FROM (VALUES
${rows(data.MOCK_SALARY_STRUCTURES.map((s) => [str(s.code), str(s.name), str(s.description), `'${s.status}'`]))}
    ) AS v ([Code], [Name], [Description], [Status])
    WHERE NOT EXISTS (SELECT 1 FROM [pay].[SalaryStructure] AS s WHERE s.[Code] = v.[Code]);

    INSERT INTO [pay].[SalaryStructureComponent] ([SalaryStructureId], [SalaryComponentId], [SortOrder])
    SELECT n.[Id], c.[Id], v.[SortOrder]
    FROM (VALUES
${rows(
  data.MOCK_SALARY_STRUCTURES.flatMap((s) =>
    s.componentIds.map((cid, i) => [str(s.code), str(componentCode.get(cid)), num(i + 1)]),
  ),
)}
    ) AS v ([StructureCode], [ComponentCode], [SortOrder])
    JOIN @NewStructure AS n ON n.[Code] = v.[StructureCode]
    JOIN [pay].[SalaryComponent] AS c ON c.[Code] = v.[ComponentCode];

    INSERT INTO [kpi].[Category] ([Code], [Name], [Description], [Status])
    SELECT v.[Code], v.[Name], v.[Description], v.[Status]
    FROM (VALUES
${rows(data.MOCK_KPI_CATEGORIES.map((c) => [str(c.code), str(c.name), str(c.description), `'${c.status}'`]))}
    ) AS v ([Code], [Name], [Description], [Status])
    WHERE NOT EXISTS (SELECT 1 FROM [kpi].[Category] AS c WHERE c.[Code] = v.[Code] OR c.[Name] = v.[Name]);

    INSERT INTO [kpi].[Template]
        ([Code], [Name], [Description], [CategoryId], [DepartmentId], [MeasurementType], [Unit], [Direction], [ScoringMethod],
         [DefaultTarget], [RangeMin], [RangeMax], [MaxRating], [DefaultWeight], [MaxAchievementPct], [Frequency], [Status])
    SELECT v.[Code], v.[Name], v.[Description], c.[Id], d.[Id], v.[MeasurementType], v.[Unit], v.[Direction], v.[ScoringMethod],
           v.[DefaultTarget], v.[RangeMin], v.[RangeMax], v.[MaxRating], v.[DefaultWeight], v.[MaxAchievementPct], v.[Frequency], v.[Status]
    FROM (VALUES
${rows(
  data.MOCK_KPI_TEMPLATES.map((t) => [
    str(t.code),
    str(t.name),
    str(t.description),
    str(kpiCategoryCode.get(t.categoryId)),
    t.departmentId === null
      ? 'CAST(NULL AS nvarchar(20))'
      : str(departmentCode.get(t.departmentId)),
    `'${t.measurementType}'`,
    t.unit === null ? 'CAST(NULL AS nvarchar(20))' : str(t.unit),
    `'${t.direction}'`,
    `'${t.scoringMethod}'`,
    `CAST(${num(t.defaultTarget)} AS decimal(18, 4))`,
    `CAST(${num(t.rangeMin)} AS decimal(18, 4))`,
    `CAST(${num(t.rangeMax)} AS decimal(18, 4))`,
    `CAST(${num(t.maxRating)} AS decimal(18, 4))`,
    `CAST(${num(t.defaultWeight)} AS decimal(5, 2))`,
    `CAST(${num(t.maxAchievementPct)} AS decimal(8, 2))`,
    `'${t.frequency}'`,
    `'${t.status}'`,
  ]),
)}
    ) AS v ([Code], [Name], [Description], [CategoryCode], [DepartmentCode], [MeasurementType], [Unit], [Direction],
            [ScoringMethod], [DefaultTarget], [RangeMin], [RangeMax], [MaxRating], [DefaultWeight], [MaxAchievementPct],
            [Frequency], [Status])
    JOIN [kpi].[Category] AS c ON c.[Code] = v.[CategoryCode]
    LEFT JOIN [org].[Department] AS d ON d.[Code] = v.[DepartmentCode]
    WHERE (v.[DepartmentCode] IS NULL OR d.[Id] IS NOT NULL)
      AND NOT EXISTS (SELECT 1 FROM [kpi].[Template] AS t WHERE t.[Code] = v.[Code]);

    IF NOT EXISTS (SELECT 1 FROM [kpi].[RatingBand])
        INSERT INTO [kpi].[RatingBand] ([Label], [MinScore], [Description])
        VALUES
${rows(data.DEFAULT_RATING_BANDS.map((b) => [str(b.label), num(b.minScore), str(b.description)])).replace(/^ {4}/gm, '        ')};

    INSERT INTO [grv].[Category] ([Code], [Name], [Description], [AllowsAnonymous], [DefaultTargetDays], [SortOrder], [Status])
    SELECT v.[Code], v.[Name], v.[Description], v.[AllowsAnonymous], v.[DefaultTargetDays], v.[SortOrder], v.[Status]
    FROM (VALUES
${rows(
  data.MOCK_GRIEVANCE_CATEGORIES.map((c) => [
    str(c.code),
    str(c.name),
    str(c.description),
    bit(c.allowsAnonymous),
    num(c.defaultTargetDays),
    num(c.sortOrder),
    `'${c.status}'`,
  ]),
)}
    ) AS v ([Code], [Name], [Description], [AllowsAnonymous], [DefaultTargetDays], [SortOrder], [Status])
    WHERE NOT EXISTS (SELECT 1 FROM [grv].[Category] AS c WHERE c.[Code] = v.[Code] OR c.[Name] = v.[Name]);

    -- ---- Reference ids by code ------------------------------------------------------------------------
${lookupSql}

    -- ---- User accounts of the people who act in the sample (NULL: no account) -------------------------
${actorList.map((id) => `    DECLARE @U${id} int = (SELECT [Id] FROM [sec].[AppUser] WHERE [EmployeeId] = ${id}); -- ${nameOf(id)}`).join('\n')}

    -- ---- 3. Leave -----------------------------------------------------------------------------------
    SET IDENTITY_INSERT [lve].[LeaveApplication] ON;
${insertRows(
  `INSERT INTO [lve].[LeaveApplication]
        ([Id], [ApplicationNo], [EmployeeId], [LeaveTypeId], [StartDate], [EndDate], [Days], [HalfDay], [Reason],
         [DocumentName], [Status], [AppliedOn], [ApproverEmployeeId], [ActionedByUserId], [ActionedByName], [ActionedOn],
         [ApproverRemarks])`,
  leave,
)}
    SET IDENTITY_INSERT [lve].[LeaveApplication] OFF;

    -- ---- 3. Attendance ----------------------------------------------------------------------------------
${insertRows(
  `INSERT INTO [att].[AttendanceRecord]
        ([EmployeeId], [Date], [ShiftId], [CheckIn], [CheckOut], [WorkedMinutes], [LateMinutes], [EarlyLeaveMinutes],
         [OvertimeMinutes], [Status], [Source], [Remarks])`,
  attendance,
)}

${insertRows(
  `INSERT INTO [att].[AttendanceAdjustment]
        ([EmployeeId], [Date], [CurrentCheckIn], [CurrentCheckOut], [RequestedCheckIn], [RequestedCheckOut],
         [RequestedStatus], [Reason], [Status], [RequestedOn], [DecidedByUserId], [DecidedByName], [DecidedOn], [Remarks])`,
  adjustments,
)}

${insertRows(
  `INSERT INTO [att].[OvertimeRecord]
        ([EmployeeId], [Date], [StartTime], [EndTime], [Minutes], [Reason], [Status], [RequestedOn], [DecidedByUserId],
         [DecidedByName], [DecidedOn], [Remarks])`,
  overtime,
)}

    -- ---- 3. Payroll (the processor is May Thu Kyaw, the approver Su Myat Noe: never the same person) ----
${insertRows(
  `INSERT INTO [pay].[SalaryAssignment] ([EmployeeId], [SalaryStructureId], [BasicSalary], [EffectiveFrom], [Remarks])`,
  assignments,
)}

    SET IDENTITY_INSERT [pay].[PayrollRun] ON;
${insertRows(
  `INSERT INTO [pay].[PayrollRun]
        ([Id], [PeriodName], [Year], [Month], [StartDate], [EndDate], [PayDate], [Status], [EmployeeCount], [TotalBasic],
         [TotalAllowances], [TotalGross], [TotalDeductions], [TotalNet], [ProcessedOn], [ProcessedByUserId],
         [ProcessedByName], [ApprovedByUserId], [ApprovedByName], [ApprovedOn], [PaidOn])`,
  payrollRuns,
)}
    SET IDENTITY_INSERT [pay].[PayrollRun] OFF;

    SET IDENTITY_INSERT [pay].[Payslip] ON;
${insertRows(
  `INSERT INTO [pay].[Payslip]
        ([Id], [PayrollRunId], [EmployeeId], [PeriodName], [EmployeeCode], [EmployeeName], [DepartmentId],
         [DepartmentName], [PositionTitle], [CompanyName], [BankName], [AccountMasked], [BasicSalary], [GrossSalary],
         [TotalDeductions], [NetSalary], [PeriodDays], [DaysEmployed], [WorkingDays], [UnpaidLeaveDays],
         [OvertimeMinutes], [GeneratedOn])`,
  payslips,
)}
    SET IDENTITY_INSERT [pay].[Payslip] OFF;

${insertRows(
  `INSERT INTO [pay].[PayslipLine] ([PayslipId], [LineType], [SortOrder], [Code], [Name], [Amount], [Basis])`,
  payslipLines,
)}

    -- ---- 3. Recruitment ----------------------------------------------------------------------------------
    SET IDENTITY_INSERT [rec].[JobOpening] ON;
${insertRows(
  `INSERT INTO [rec].[JobOpening]
        ([Id], [Code], [Title], [DepartmentId], [HiringManagerEmployeeId], [EmploymentType], [Vacancies], [PostedOn],
         [ClosingDate], [Description], [Status])`,
  openings,
)}
    SET IDENTITY_INSERT [rec].[JobOpening] OFF;

    SET IDENTITY_INSERT [rec].[Candidate] ON;
${insertRows(
  `INSERT INTO [rec].[Candidate]
        ([Id], [FirstName], [LastName], [Email], [Phone], [Source], [CurrentEmployer], [ExperienceYears], [ResumeName],
         [Notes])`,
  candidates,
)}
    SET IDENTITY_INSERT [rec].[Candidate] OFF;

    SET IDENTITY_INSERT [rec].[Application] ON;
${insertRows(
  `INSERT INTO [rec].[Application] ([Id], [JobOpeningId], [CandidateId], [Stage], [AppliedOn], [RejectionReason], [Notes])`,
  applications,
)}
    SET IDENTITY_INSERT [rec].[Application] OFF;

    SET IDENTITY_INSERT [rec].[Interview] ON;
${insertRows(
  `INSERT INTO [rec].[Interview]
        ([Id], [ApplicationId], [Round], [Date], [StartTime], [DurationMinutes], [Mode], [InterviewerEmployeeId],
         [Location], [Status], [Score], [Recommendation], [Feedback])`,
  interviews,
)}
    SET IDENTITY_INSERT [rec].[Interview] OFF;

    -- ---- 3. KPI -------------------------------------------------------------------------------------------
    SET IDENTITY_INSERT [kpi].[EvaluationPeriod] ON;
${insertRows(
  `INSERT INTO [kpi].[EvaluationPeriod]
        ([Id], [CompanyId], [Name], [PeriodType], [StartDate], [EndDate], [SubmissionDueDate], [RequiresHrReview], [Status])`,
  periods,
)}
    SET IDENTITY_INSERT [kpi].[EvaluationPeriod] OFF;

    SET IDENTITY_INSERT [kpi].[Scorecard] ON;
${insertRows(
  `INSERT INTO [kpi].[Scorecard]
        ([Id], [EmployeeId], [PeriodId], [CompanyId], [BranchId], [DepartmentId], [PositionId], [ReviewerEmployeeId],
         [Status], [EmployeeComment], [ManagerRating], [ManagerComment], [HrComment], [OverallScore], [RatingLabel],
         [CalculationVersion], [PlanFinalizedOn], [SubmittedOn], [ReviewedOn], [FinalizedOn], [FinalizedByUserId])`,
  scorecards,
)}
    SET IDENTITY_INSERT [kpi].[Scorecard] OFF;

    SET IDENTITY_INSERT [kpi].[ScorecardItem] ON;
${insertRows(
  `INSERT INTO [kpi].[ScorecardItem]
        ([Id], [ScorecardId], [TemplateId], [CategoryId], [KpiCode], [KpiName], [MeasurementType], [Unit], [Direction],
         [ScoringMethod], [Target], [RangeMin], [RangeMax], [MaxRating], [Weight], [MaxAchievementPct], [StartDate],
         [EndDate], [SortOrder], [AssignedByUserId], [AssignedOn], [ActualValue], [EmployeeComment], [AchievementPct],
         [WeightedScore], [ManagerRating], [ManagerComment])`,
  scorecardItems,
)}
    SET IDENTITY_INSERT [kpi].[ScorecardItem] OFF;

${insertRows(
  `INSERT INTO [kpi].[ReviewAction]
        ([ScorecardId], [Step], [Action], [FromStatus], [ToStatus], [ActorUserId], [ActorEmployeeId], [ActorName],
         [Comment], [OverallScore], [OccurredAtUtc])`,
  reviewActions,
)}

    -- ---- 3. Grievances (the submitter is never on the case, its history or its messages) ----------------
    SET IDENTITY_INSERT [grv].[Case] ON;
${insertRows(
  `INSERT INTO [grv].[Case]
        ([Id], [CaseNo], [CategoryId], [Subject], [Description], [IncidentDate], [Location], [InvolvedPersonText],
         [InvolvedDepartmentId], [SubjectEmployeeId], [IsAnonymous], [Status], [Priority], [TargetResolutionDate],
         [ReturnStatus], [IsEscalated], [CompanyId], [BranchId], [DepartmentId], [PositionId], [DeclarationAcceptedAtUtc],
         [SubmittedAtUtc], [AcknowledgedAtUtc], [ResolvedAtUtc], [ClosedAtUtc], [ResolutionSummary],
         [ResolutionVisibleToSubmitter], [RejectionReason], [ArchivedAtUtc], [RetentionUntil], [LegalHold],
         [LastSubmitterUpdateAtUtc], [CreatedAtUtc], [ModifiedAtUtc])`,
  caseRows,
)}
    SET IDENTITY_INSERT [grv].[Case] OFF;

    -- Named case: the employee. Anonymous case: HMACSHA256(development key, employee id) only.
${insertRows(
  `INSERT INTO [grv].[CaseSubmitter] ([CaseId], [EmployeeId], [SubmitterPseudonym], [SubmitterSeenAtUtc])`,
  submitters,
)}

${insertRows(
  `INSERT INTO [grv].[StatusHistory]
        ([CaseId], [FromStatus], [ToStatus], [Action], [ActorUserId], [ActorEmployeeId], [ActorName], [ActorRole],
         [Reason], [VisibleToSubmitter], [OccurredAtUtc])`,
  historyRows,
)}

${insertRows(
  `INSERT INTO [grv].[Assignment]
        ([CaseId], [InvestigatorEmployeeId], [Role], [AssignedAtUtc], [AssignedByUserId], [UnassignedAtUtc],
         [UnassignedByUserId], [Reason])`,
  assignmentRows,
)}

${insertRows(
  `INSERT INTO [grv].[InvestigationNote]
        ([CaseId], [Kind], [Body], [InterviewDate], [IntervieweeText], [AuthorUserId], [AuthorEmployeeId], [AuthorName],
         [CreatedAtUtc])`,
  noteRows,
)}

${insertRows(
  `INSERT INTO [grv].[Communication]
        ([CaseId], [Direction], [Kind], [Body], [AuthorRole], [AuthorUserId], [AuthorEmployeeId], [AuthorName],
         [CreatedAtUtc])`,
  messageRows,
)}

    -- File records only: no file exists under these storage keys.
${insertRows(
  `INSERT INTO [grv].[Attachment]
        ([CaseId], [Visibility], [UploadedByRole], [UploadedByUserId], [FileName], [ContentType], [SizeBytes],
         [StorageKey], [Sha256], [UploadedAtUtc], [RemovedAtUtc], [CreatedAtUtc], [ModifiedAtUtc])`,
  attachmentRows,
)}

${insertRows(
  `INSERT INTO [grv].[CorrectiveAction]
        ([CaseId], [ActionType], [Description], [ResponsibleText], [DueDate], [CompletedOn], [Status], [CreatedByUserId])`,
  correctiveRows,
)}

${insertRows(
  `INSERT INTO [grv].[ResolutionProposal]
        ([CaseId], [Summary], [ProposedByUserId], [ProposedByEmployeeId], [ProposedAtUtc], [Decision], [DecidedByUserId],
         [DecidedAtUtc], [DecisionComment])`,
  proposalRows,
)}

${insertRows(
  `INSERT INTO [grv].[Escalation]
        ([CaseId], [Reason], [IsImmediate], [EscalatedByUserId], [EscalatedAtUtc], [ClearedAtUtc], [Outcome])`,
  escalationRows,
)}

    -- ---- 3. Activity and notifications ------------------------------------------------------------------
${insertRows(`INSERT INTO [app].[ActivityLog] ([Category], [Message], [ActorUserId], [ActorName], [OccurredAtUtc])`, activity)}

${insertRows(
  `INSERT INTO [app].[Notification] ([RecipientUserId], [Title], [Message], [Icon], [Link], [CreatedAtUtc], [IsRead], [ReadAtUtc])`,
  notifications,
)}

    -- ---- 4. Number sequences after the sample ------------------------------------------------------------
    ALTER SEQUENCE [lve].[LeaveApplicationNoSeq] RESTART WITH ${counts.leave + 1};
    ALTER SEQUENCE [rec].[JobOpeningCodeSeq] RESTART WITH ${openings.length + 1};
    ALTER SEQUENCE [grv].[CaseNoSeq] RESTART WITH ${caseSequence + 1};

${enableTriggers()}

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
${identityInsertOff(MODULES_IDENTITY_INSERT)}
    THROW;
END CATCH;

-- ---- Summary --------------------------------------------------------------------------------------
SELECT N'lve.LeaveApplication' AS [Table], COUNT(*) AS [Rows] FROM [lve].[LeaveApplication]
UNION ALL SELECT N'att.AttendanceRecord', COUNT(*) FROM [att].[AttendanceRecord]
UNION ALL SELECT N'att.AttendanceAdjustment', COUNT(*) FROM [att].[AttendanceAdjustment]
UNION ALL SELECT N'att.OvertimeRecord', COUNT(*) FROM [att].[OvertimeRecord]
UNION ALL SELECT N'pay.SalaryAssignment', COUNT(*) FROM [pay].[SalaryAssignment]
UNION ALL SELECT N'pay.PayrollRun', COUNT(*) FROM [pay].[PayrollRun]
UNION ALL SELECT N'pay.Payslip', COUNT(*) FROM [pay].[Payslip]
UNION ALL SELECT N'pay.PayslipLine', COUNT(*) FROM [pay].[PayslipLine]
UNION ALL SELECT N'rec.JobOpening', COUNT(*) FROM [rec].[JobOpening]
UNION ALL SELECT N'rec.Candidate', COUNT(*) FROM [rec].[Candidate]
UNION ALL SELECT N'rec.Application', COUNT(*) FROM [rec].[Application]
UNION ALL SELECT N'rec.Interview', COUNT(*) FROM [rec].[Interview]
UNION ALL SELECT N'kpi.EvaluationPeriod', COUNT(*) FROM [kpi].[EvaluationPeriod]
UNION ALL SELECT N'kpi.Scorecard', COUNT(*) FROM [kpi].[Scorecard]
UNION ALL SELECT N'kpi.ScorecardItem', COUNT(*) FROM [kpi].[ScorecardItem]
UNION ALL SELECT N'kpi.ReviewAction', COUNT(*) FROM [kpi].[ReviewAction]
UNION ALL SELECT N'grv.Case', COUNT(*) FROM [grv].[Case]
UNION ALL SELECT N'grv.StatusHistory', COUNT(*) FROM [grv].[StatusHistory]
UNION ALL SELECT N'grv.Communication', COUNT(*) FROM [grv].[Communication]
UNION ALL SELECT N'grv.Attachment', COUNT(*) FROM [grv].[Attachment]
UNION ALL SELECT N'app.ActivityLog', COUNT(*) FROM [app].[ActivityLog]
UNION ALL SELECT N'app.Notification', COUNT(*) FROM [app].[Notification];

-- The sample people who act in the history, and whether they have a user account yet.
SELECT e.[EmployeeCode], CONCAT(e.[FirstName], N' ', e.[LastName]) AS [Name], u.[Email], u.[Status]
FROM [hr].[Employee] AS e
LEFT JOIN [sec].[AppUser] AS u ON u.[EmployeeId] = e.[Id]
WHERE e.[Id] IN (${actorList.join(', ')})
ORDER BY e.[Id];
`;
}

const data = await loadMockData();
const pseudonymKey = await developmentPseudonymKey();
const outputs = [
  [orgFile, sql(data)],
  [modulesFile, modulesSql(data, pseudonymKey)],
];
await mkdir(outDir, { recursive: true });
for (const [file, text] of outputs) {
  // ASCII only, so no code page can change a value (see str()).
  const offending = /[^\x09\x0a\x0d\x20-\x7e]/.exec(text);
  if (offending)
    fail(
      `${path.basename(file)} contains a non-ASCII character near "${text.slice(Math.max(0, offending.index - 40), offending.index + 10)}"`,
    );
  await writeFile(file, text, 'utf8');
  console.log(`Wrote ${path.relative(repoRoot, file)}`);
}
