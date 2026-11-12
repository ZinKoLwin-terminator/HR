# APEX HRMS — future to-do list

Last updated: 2026-11-13. State: every module has an API and API-mode screens, merged on
`integrate/all-features` (latest `787482c`). Nothing here is merged to `main` yet.

> **မြန်မာလို အကျဉ်း:** Module အားလုံး ပြီးပါပြီ။ ဒီစာရင်းမှာ (၁) သင် ကိုယ်တိုင် လုပ်ရမယ့်အရာ၊
> (၂) မပြည့်စုံသေးတဲ့ feature များ၊ (၃) security နဲ့ production မတိုင်မီ လိုအပ်ချက်များ၊ (၄) test၊
> (၅) technical debt၊ (၆) document တို့ကို ဦးစားပေးအလိုက် စီထားပါတယ်။ ✅ ကို အမှန်ခြစ်ပြီး ပြီးကြောင်း မှတ်ပါ။

Priority: **P1** = before real use, **P2** = next features, **P3** = nice to have. Items added on
2026-11-13 carry a proposed priority; confirm it when you pick the item up.

---

## 1. Your actions (only you can do these)

- [ ] **P1** Create the first administrator if none exists:
      `cd api` → `dotnet run --project src/Apex.Hrms.Api -- bootstrap-admin --email <you> --name "<name>"`
- [ ] **P1** Tick in **Settings → Permissions** (an existing database does not get new starter grants):
  - [ ] Department Manager → Employees → View
  - [ ] HR Officer → Payroll → Approve
  - [ ] Employee → Attendance → Create
- [x] **P1** Schema for the do-not-rehire list published to the local database by you (database branch
      `feature/schema-blacklist`: blacklist table, candidate national ID, application override columns,
      permissions `ViewBlacklist` and `EditBlacklist`).
- [ ] **P1** Database repo (`D:\New folder\HR DBPJ\HR DBPJ`): merge `feature/schema-permissions-sync` into `main`,
      then `git push origin main` (local `main` is ahead and was never pushed; the hook blocks Claude from pushing to main).
- [ ] **P1** Decide when to merge `integrate/all-features` into `main` in the app repo. **Merging to `main` deploys the
      live demo (GitHub Pages).** Review the diff and CI first.
- [ ] **P3** Decide what to do with the untracked `docs/E2E-FORM-EXAMPLE-DATA.md` (commit or delete).
- [ ] **P3** Remove old git worktrees under `.claude/worktrees/` once their branches are merged
      (`git worktree list`, then `git worktree remove <path>`).

---

## 2. Features not complete yet

### Excel
- [x] **P2** Import/export in API mode for the other modules (API done): organization (`companies`, `branches`,
      `departments`, `sections`, `positions`, `job-grades`), `shift-assignments` and `salary-assignments` import and
      export; `leave-applications`, `leave-balances` and `attendance-records` export only.
- [ ] **P2** Excel screens for the 11 new API modules (UI pending; the UI offers Employees only). The catalog
      (`GET /api/excel/modules`) already reports `canImport` and `canExport` per caller.
- [ ] **P3** Background export (202 + download later) for exports above the row limit (today a 400 with a message).
- [ ] **P3** `ExportRequest.layout` ("template" layout): the API accepts it; the UI model has no field for it.

### Printing
- [x] **P2** Print current view and print all matching rows (up to 1000) on the CRUD lists (browser print only).
- [x] **P2** Payslip print (masked account only), and print all payslips of a payroll run (one per page).
- [x] **P2** Profile print: sections follow `employees:ViewSensitive` and `employees:ViewBank`.
- [x] **P2** Letters: employment certificate, experience letter (needs an exit date), leave approval letter (needs an
      Approved leave application and `leave:View`). Print is disabled until the reference and signatory are typed.
- [ ] **P2** Check the printed output on a real printer and as PDF (PRN-13 in `docs/E2E-TEST-SCENARIOS.md`): not run.
- [ ] **P3** Print buttons on the non-CRUD lists: Employee list, attendance, leave balances, payslip list.

### Do-not-rehire list
- [x] **P2** Add a former employee (exit date required), lift with a reason, keep the history; one entry in force per
      employee; reason required, at most 500 characters.
- [x] **P2** Permissions `employees:ViewBlacklist` and `employees:EditBlacklist` (HR Administrator only in the seed).
      The reason is shown only in the list's details and in the profile section.
- [x] **P2** Recruitment flag "Matches do-not-rehire list — contact HR" (email, phone or national ID match with an entry
      in force). National ID is write-only.
- [x] **P2** Hire gate: selecting a matching candidate needs an HR Administrator's override with a reason (at most 300
      characters). Anyone else is refused (403 for an override).
- [ ] **P2** Confirm that "Selected" is the hiring decision the gate applies to (the default chosen in this update;
      not confirmed by you).
- [ ] **P3** Mock build: the two permissions are not enforced (demo limitation; the API enforces them).

### Recruitment
- [x] **P2** Row-level scoping: `recruitment:Edit` sees everything; other callers see the openings they manage and their
      applications, interviews and candidates. Out of scope is 404.
- [x] **P2** Minimal employee lookup for the pickers (`GET /api/employees/lookup`, current employees, no personal fields).
- [ ] **P3** The hiring-manager picker loads the first 200 current employees (`MANAGER_PICKER_SIZE`). A former
      interviewer is not in that list, so the interview list shows "—" for them.

### Settings
- [ ] **P2** Apply general settings in the other screens (date format, currency, timezone, fiscal year start, email
      notifications, weekly digest). Today they are saved but not used. **In progress.**

### Employees and self-service
- [x] **P2** Employee self-service payslips: **My Payslips** (`/my/payslips`) shows the caller's own payslips of Approved
      and Paid runs. No `payroll:View` is needed.
- [ ] **P3** Store document files (today only metadata: type, title, file name, dates). Needs file storage, virus
      scanning, size and type limits, and access checks.
- [ ] **P3** Manager scope covers **direct reports only** (not indirect reports). Revisit if needed.

### Payroll
- [ ] **P1 before real payroll** Replace the demonstration rules with real, signed-off rules for the jurisdiction
      (tax, social security, rounding). Today the figures are demonstrations only.
- [ ] **P3** `pay.Payslip` requires a bank account: employees without one are skipped with a warning. Decide whether
      cash or cheque payment is needed.
- [x] **P3** Salary assignment create is tier-checked first (a creator outside the tier gets 404 before any band or date
      rule), and nobody changes their own salary (409). Keep `payroll:Create` and `Edit` together with `payroll:Approve`
      in a custom role, so the creator can see what they made.
- [ ] **P3** Mock payroll warnings name the employee (code and name); the API names the employee by code only. Align
      the mock (proposed priority).

### Attendance and leave
- [ ] **P3** Edit a pending adjustment or overtime request (the API has no PUT; today: delete and create again).
- [ ] **P3** A future exit date ends access immediately (product decision: keep or change to "on the exit date").

### Notifications
- [ ] **P2** Per-user read flag for broadcast notifications. Today one `IsRead` flag per row is shared by everyone, so
      reading a broadcast marks it read for all users. The schema proposal is in `api/README.md` (HTTP contract,
      dashboard, activity and notifications). Awaiting a decision (needs a schema change).

### Demo build (mock)
- [x] **P3** Sync the mock role permissions with the new seed (Department Manager `employees:View`, HR Officer
      `payroll:Approve`, Employee `attendance:Create`).

---

## 3. Security and production readiness

- [x] **P1** Security review of the team, payslip and recruitment scoping, with the fixes merged with it: dashboard
      figures need their own grants (payroll, recruitment, activity, birthdays); nobody changes their own salary (409);
      scope is checked before create (404); a session whose cookie and live account disagree is refused; a punch by an
      account with no employee needs the "All" tier; payroll warnings name the employee by code only; password reset
      throttle per caller as well as per target.
- [ ] **P1** Production hosting decision (not started: "Hosting: skip"). Then:
  - [ ] HTTPS, HSTS, secure cookies on the real domain.
  - [ ] `Hosting:ForwardedHeaders` with the real proxy IPs/networks (off by default).
  - [ ] `DataProtection:KeysPath` on a protected, shared, backed-up volume; consider certificate encryption of the keys.
  - [ ] Real SMTP (`Email:Smtp:*`) for password reset mail.
  - [ ] Connection string and secrets from a secret store, never from files in the repo.
  - [ ] Never connect Claude's environment to production.
- [x] **P1** Production settings guide: `docs/DEPLOYMENT.md` (host-neutral; environment variables, go-live checklist).
      `appsettings.Production.json` was not created (blocked by the repository's file permissions); its suggested
      content is in section 9 of the guide for a human to add, or set the same values as environment variables.
- [ ] **P1** Health endpoints (liveness, and readiness = database reachable); the API has none today.
- [ ] **P1** A production API-mode UI build (`api-production`: optimised, hashed, no source maps). The `api`
      configuration is a development build.
- [ ] **P2** Same-origin serving: the API has no static files or SPA fallback, so a reverse proxy serves the UI and
      forwards `/api` (the `__Host-` cookies need one origin).
- [ ] **P3** `Cors:AllowedOrigins`: the base file's `localhost:4200` stays at index 0 unless overridden.
- [ ] **P2** Multi-instance: the password reset throttle (per target and per caller), the sign-in rate limiter and the
      grant cache safety net are per instance (in memory). Use a shared store if several API instances run.
- [ ] **P2** Remember-me sessions: 30 days maximum (`Auth:RememberMeMaxDays`). Confirm the value.
- [ ] **P3** Audit/retention policy for activity logs and dev mail (`App_Data/dev-mail` holds reset tokens in development).
- [ ] **P1** Real HR data only after all of the above, plus a data protection review (PII, bank details, documents).

---

## 4. Testing

- [ ] **P2** Playwright coverage for API mode: all API scenarios in `docs/E2E-TEST-SCENARIOS.md` are **Manual** today
      (the Playwright suite runs the mock build only). Needs an API + test database in CI.
- [x] **P2** CI job for the .NET API: build with warnings as errors, and unit tests (`Category!=Integration`). Runs on every
      push and pull request. The SQL Server integration tests stay out of CI.
- [ ] **P2** Linux run of the API CI job not verified yet (the job targets `ubuntu-latest`).
- [ ] **P2** Integration tests share one database (`HR_IntegrationTests`): never run two suites at once; a failed test
      can leave `TST-` rows that block the next run (drop the database to recover). Consider a per-run database name.
- [ ] **P3** Known flaky end-to-end tests under load: shift management and the Excel import wizard (timeouts). Stabilise.
  - [x] Shift management: the edit focus race is fixed (attendance spec).
  - [ ] Excel import wizard timeouts (not covered by this merge).
- [ ] **P3** Missing tests noted by agents:
  - [x] Salary tab error banner (component test, `profile-salary-tab.component.spec.ts`).
  - [x] Savepoint rollback in joined transactions (`JoinedTransactionTests`, integration project).
  - [x] Cascade delete of documents with their employee (`EmployeeExtrasHttpTests`, integration project).
  - [ ] Payroll: two simultaneous processes of one period; approve during return-to-draft; a large run (thousands of payslips).
  - [ ] Forwarded headers over real HTTP (today: unit tests of the settings only).
- [ ] **P3** Playwright coverage for the new print paths (print all matching rows, PRN-02; print all payslips, PRN-05) and
      the letters' blocked states (PRN-08, PRN-09). Do-not-rehire screens (mock) have unit tests only.

---

## 5. Technical debt

- [ ] **P3** Initial bundle is ~1.18 MB against a 1.00 MB budget (warning in `npm run build`). Lazy-load more.
      Unchanged by this merge.
- [x] **P3** Mixed CRLF/LF line endings: `.gitattributes` added.
- [ ] **P3** Renormalize the existing files to the new line-ending rules, in its own commit.
- [x] **P3** Stale doc comment in `api/src/Apex.Hrms.Application/Security/CallerPermissions.cs` ("an approver"): no longer present.
- [x] **P3** Duplicate `App_Data/` rule in `api/.gitignore`: one rule left.
- [x] **P3** The Api test project takes 4–8 minutes: speed-up merged. The new time is not measured here.
- [ ] **P3** Pin the CI actions by commit SHA (`.github/workflows/ci.yml` uses tags such as `actions/checkout@v4`).
      Proposed priority.

---

## 6. Documentation

- [ ] **P2** Module guides: `docs/modules/` has only Organization and Payroll. Add Employees, Leave, Attendance,
      Recruitment, Settings, Excel. Printing and the do-not-rehire list need a guide too (proposed).
- [ ] **P2** Keep `docs/E2E-TEST-SCENARIOS.md` updated with every behaviour change (406 scenarios as of 2026-11-13).
- [ ] **P3** Audit the in-app guides in `ui/src/app/core/constants` for any remaining "mock only" wording, and check the
      printing and Excel guides against the code.
- [ ] **P3** Host-specific deployment steps once hosting is decided (the host-neutral guide is `docs/DEPLOYMENT.md`).
