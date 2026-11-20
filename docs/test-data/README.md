# Test data

## Local database: the demo's sample data (`sql/seed-*.sql`)

Two scripts load the demo's sample data into the **local** `HR` database, so the real system (`npm run start:api` → the
.NET API → the local database) shows the same records as the demo and section 1 of `docs/E2E-TEST-SCENARIOS.md`
(employee id = EMP number). Both are generated from `ui/src/app/mock-data` by `ui/scripts/generate-db-seed.mjs`, both
**delete** data, and both refuse any database other than `HR` and any server that is not on this machine. Each runs in one
transaction: an error rolls the whole script back. The header of each script lists every detail.

| Order | Script | What it loads |
| --- | --- | --- |
| 1 | `seed-organization-employees.sql` | Deletes the data of every module, then loads Organization (3 companies, 6 branches, 11 departments, 16 sections, 9 job grades, 33 positions) and Employees (60 employees with bank accounts, documents, employment history, 1 do-not-rehire entry). Adds the sample shifts that are missing (by code) |
| 2 | `seed-modules.sql` | Deletes the module data again and loads it (counts below). Refuses to run unless the employees and organization units of script 1 are in the database. It can be run again on its own |

What `seed-modules.sql` loads (generated 2026-11-19; the attendance count changes with the day and time of generation):

| Module | Tables and rows |
| --- | --- |
| Leave | `lve.LeaveApplication` 29 (LV-yyyy-0001 to 0029: 16 Approved, 7 Submitted, 2 Rejected, 2 Cancelled, 2 Draft) |
| Attendance | `att.AttendanceRecord` 3342 (60 days and today), `att.AttendanceAdjustment` 10, `att.OvertimeRecord` 215 |
| Payroll | `pay.SalaryAssignment` 107, `pay.PayrollRun` 7 (six Paid months, the current month Draft), `pay.Payslip` 328, `pay.PayslipLine` 1690 |
| Recruitment | `rec.JobOpening` 8, `rec.Candidate` 22, `rec.Application` 22, `rec.Interview` 14 |
| KPI | `kpi.EvaluationPeriod` 3, `kpi.Scorecard` 12, `kpi.ScorecardItem` 52, `kpi.ReviewAction` 35 |
| Grievances | `grv.Case` 18 (2 drafts, 6 anonymous), `grv.CaseSubmitter` 18, `grv.StatusHistory` 73, `grv.Assignment` 11, `grv.InvestigationNote` 7, `grv.Communication` 3, `grv.Attachment` 4 (records only), `grv.CorrectiveAction` 2, `grv.ResolutionProposal` 7, `grv.Escalation` 2, `grv.AuditLog` none (the demo starts with an empty log too) |
| Activity | `app.ActivityLog` 9, `app.Notification` 4 (shown to every user) |

Settings, roles and permissions are never touched. **User accounts** (`sec.AppUser`) keep everything except the employee
link: script 1 unlinks every account and links it again to the new employee with the same work email; an account whose
email is no sample employee's work email ends with no employee (`EmployeeId` NULL). Names, roles, status and sign-ins are
not changed. Reference data is **kept**: a reference record the sample needs is only **added when no record has its code** (an existing one is never changed): leave types and the 9 leave
policies (by name), the 15 sample holidays (by date; leave days, payroll working days and the attendance "Holiday" days are
calculated with them), salary components and the structures STAFF, MGMT and EXEC, KPI categories and templates, rating
bands (only into an empty table) and grievance categories. A fresh database therefore needs nothing else. The number
sequences restart at 1 in script 1 and after the sample in script 2 (next leave LV-...-0030, job opening JOB-...-009, grievance GRV-...-00017). The payroll
and grievance "final" triggers (which refuse to insert a Paid run, its payslips or a Closed case directly) are disabled
while the data is loaded and enabled again before the commit; the temporal tables keep no history for the loaded rows.

### How to run it

Generate the scripts on the day you run them (dates are relative to today), then a human runs them:

1. **Stop the API** (and close any SSMS window with an open transaction on the database).
2. **Take a copy-only backup first**, so you can go back (any folder the SQL Server service can write to):

   ```bash
   sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -Q "BACKUP DATABASE [HR] TO DISK = N'C:\Backup\HR-before-seed.bak' WITH COPY_ONLY, INIT"
   ```

3. Generate and run the scripts:

   ```bash
   cd ui && node scripts/generate-db-seed.mjs
   cd ..
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-organization-employees.sql
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-modules.sql
   ```

4. Check that the database is back in its normal state:

   ```sql
   -- Every trigger enabled: is_disabled = 0 on every row.
   SELECT OBJECT_SCHEMA_NAME(parent_id) AS s, name, is_disabled FROM sys.triggers WHERE parent_class = 1;
   -- 10 system-versioned tables, each with its history table.
   SELECT SCHEMA_NAME(schema_id) AS s, name, OBJECT_NAME(history_table_id) AS h FROM sys.tables WHERE temporal_type = 2;
   -- No disabled or untrusted foreign key: no rows.
   SELECT name FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1;
   ```

### Sign-in accounts

The scripts create **no user accounts and no passwords**: a fresh database has none. Accounts are kept across runs and
linked to their employee again by email (only that link changes, see above).

1. Create the first administrator (interactive; the password is typed at a hidden prompt, at least 12 characters). With
   Su Myat Noe's work email the account is linked to EMP002 when the scripts run:
   `cd api` → `dotnet run --project src/Apex.Hrms.Api -- bootstrap-admin --email sumyat.noe@apexhorizon.example --name "Su Myat Noe"`
2. Run the two scripts, sign in as Su Myat Noe and create the other sample accounts in **Settings > Users** (employee, role,
   initial password of at least 12 characters): EMP001 Thura Aung (Department Manager), EMP004 May Thu Kyaw (Payroll
   Officer), EMP011 Sophia Bennett (Recruiter), EMP009 Ye Yint Naing (HR Officer), EMP005 Daniel Carter (Employee), and if
   needed EMP003, EMP007, EMP010 (Department Manager), EMP012 and EMP026 (Employee).
3. Run `seed-modules.sql` again, so that the history of the sample ("approved by", "processed by", review and grievance
   steps) points at these accounts. Before that, a person without an account is NULL in those columns, or minus their
   employee id where the column cannot be NULL and in `grv.Escalation.EscalatedByUserId` (for example `-4` for May Thu
   Kyaw).

### Where the database differs from the demo

- **Passwords:** the demo's `Welcome@123` is refused (11 characters; the API needs at least 12). Each account has its own
  password, wrong passwords lock it, and reset works by email (development: `api/src/Apex.Hrms.Api/App_Data/dev-mail`).
- **EMP012 Locked and EMP026 Inactive:** an account cannot be created as Locked. Deactivate EMP026 in Settings > Users; EMP012
  shows Locked only for the 15 minutes after five wrong passwords.
- **Payroll:** every paid run was processed by May Thu Kyaw and approved by Su Myat Noe (the demo shows no processor; the
  database needs one, and never the approver).
- **Cancelled leave** (LV-...-0026 and 0027) records who cancelled it and when (the employee, the day after applying, remark
  "Cancelled."); the demo leaves this empty.
- **KPI:** scored scorecards carry calculation version 1; scorecard items record who assigned them and when (the HR person
  who finalized the plan, on the first day of the period).
- **Anonymous grievances** (6 cases) store only `HMACSHA256(key, employee id)`, computed with the labelled development key of
  `api/src/Apex.Hrms.Api/appsettings.Development.json` (`Grievance:PseudonymKey`). Their submitters (Daniel Carter has two)
  see them under "My grievances" only while the API runs with that development key; with any other key nobody can find
  them again, by design.
- **Grievance files:** the 4 file records have no file behind them; downloading one answers "The file of this attachment is
  not available." (the demo serves a sample PDF).
- **Activity and notifications** keep the times of the moment the script was generated (the demo counts "minutes ago" from
  the page load). The 4 notifications are broadcasts: marking one read marks it for everybody.
- **Today's attendance** shows the check-ins made before the script was generated.
- **Permissions** come from the database (a role sees only its menus), unlike the demo where every role sees everything. A
  database published before a module existed may lack that module's starter grants (Settings > Permissions).

# Excel test files (Employees import)

Use with **Data Exchange → Import Data → Employees** (or the Import button on the Employees list).
Each file was checked against the app's own import engine on 2026-11-04. State resets on reload,
so after a successful import reload the app before the next file.

| File | Strategy | What it tests | Expected result |
|---|---|---|---|
| `1-employees-valid.xlsx` | Insert only | XLS-02, XLS-15, all-or-nothing success | 3 rows valid (EMP061–063). Dates are written three ways: Excel dates, `yyyy-MM-dd` text, `04 Nov 1990` text. Import creates 3 employees, each with a "Joined" history row |
| `2-employees-with-errors.xlsx` | Insert only | XLS-08, 09, 12, 13, 14, 16, EMP-17 | 15 rows: 1 valid, 2 duplicate, 12 invalid (see below). All-or-nothing is unavailable; "Import valid rows only" imports EMP061 only |
| `3-employees-update-only.xlsx` | Update only | EMP-16 (b), XLS-09, XLS-10 | Row 2 EMP005 invalid (manager = itself); row 3 EMP999 invalid (unknown code); row 4 EMP060 valid update (blank optional cells keep their values) |
| `4-employees-bad-headers.xlsx` | Insert only | XLS-06, XLS-07 | Goes to the **column mapping** step: `Last Name` is missing, `Nickname` is unknown, and `Emp Code` is matched through its alias |
| `5-not-really-excel.xlsx` | any | XLS-03 | Refused: "The file is not a valid Excel workbook" |

## File 2, row by row

| Row | Code | Problem | Message |
|---|---|---|---|
| 2 | EMP061 | none | Valid |
| 3 | EMP061 | Same code again | Duplicate of row 2 in this file |
| 4 | EMP005 | Code already exists | A record with this Employee Code already exists |
| 5 | EMP064 | Work email of Daniel Carter | Work email must be unique |
| 6 | EMP065 | Born 2010-01-01 | Employees must be at least 18 years old |
| 7 | EMP066 | Company AHL + branch YGN-HQ | Branch "YGN-HQ" does not exist in company APEX Logistics |
| 8 | EMP067 | Manager EMP999 | Manager "EMP999" does not exist |
| 9 | EMP068 | Resigned, no exit date | An exit date is required for resigned or terminated employees |
| 10 | EMP069 | First Name is a formula | Formulas are not allowed |
| 11 | EMP070 | Gender `Unknown` | Not an allowed value |
| 12 | EMP071 | Phone `abc` | Not a valid phone number |
| 13 | EMP072 | Probation ends before joining | The probation cannot end before the joining date |
| 14 | EMP073 | Joining date typed as text `04/11/2026` | Joining Date is not a valid date (only `yyyy-MM-dd`, `dd MMM yyyy` and real Excel dates are accepted) |
| 15 | EMP074 | Bank account `12ab` | Must be at least 8 characters |
| 16 | EMP075 | Position POS-026 (Warehouse Manager) in Engineering | Position "POS-026" does not exist in department Engineering |

## Notes

- Codes used: company `AHT`/`AHL`, branch `YGN-HQ`/`MDY-BR`, departments `ENG`/`HR`/`SAL`, positions `POS-015` Software Engineer,
  `POS-006` HR Executive, `POS-014` Engineering Manager, `POS-020` Partner Manager, `POS-026` Warehouse Manager, shift `GEN`.
  The template's **Organization codes** sheet lists all of them.
- File 3 overwrites EMP005 and EMP060 with the values in the file (this only affects the in-memory data).
