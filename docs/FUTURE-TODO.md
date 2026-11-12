# APEX HRMS — future to-do list

Last updated: 2026-11-12. State: every module has an API and API-mode screens, merged on
`integrate/all-features` (latest `421ddb2`). Nothing here is merged to `main` yet.

> **မြန်မာလို အကျဉ်း:** Module အားလုံး ပြီးပါပြီ။ ဒီစာရင်းမှာ (၁) သင် ကိုယ်တိုင် လုပ်ရမယ့်အရာ၊
> (၂) မပြည့်စုံသေးတဲ့ feature များ၊ (၃) security နဲ့ production မတိုင်မီ လိုအပ်ချက်များ၊ (၄) test၊
> (၅) technical debt၊ (၆) document တို့ကို ဦးစားပေးအလိုက် စီထားပါတယ်။ ✅ ကို အမှန်ခြစ်ပြီး ပြီးကြောင်း မှတ်ပါ။

Priority: **P1** = before real use, **P2** = next features, **P3** = nice to have.

---

## 1. Your actions (only you can do these)

- [ ] **P1** Create the first administrator if none exists:
      `cd api` → `dotnet run --project src/Apex.Hrms.Api -- bootstrap-admin --email <you> --name "<name>"`
- [ ] **P1** Tick in **Settings → Permissions** (an existing database does not get new starter grants):
  - [ ] Department Manager → Employees → View
  - [ ] HR Officer → Payroll → Approve
  - [ ] Employee → Attendance → Create
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
- [ ] **P2** Import/export for the other modules in API mode (today only **Employees**; the others come back as
      "unavailable"). Candidates: Organization, Leave balances/applications, Attendance, Payroll assignments.
- [ ] **P3** Background export (202 + download later) for exports above the row limit (today a 400 with a message).
- [ ] **P3** `ExportRequest.layout` ("template" layout) has no field in the UI model.

### Recruitment
- [ ] **P2** Row-level scoping (today anyone with `recruitment:View` sees every opening, candidate and interview).
      Decide the rule (for example: hiring managers see their own openings).
- [ ] **P2** A minimal employee lookup (id + name) for the hiring-manager picker, usable by Recruiter and Department
      Manager without `employees:View` of everyone.

### Settings
- [ ] **P2** Apply general settings in the other screens (date format, currency, timezone, fiscal year start, email
      notifications, weekly digest). Today they are saved but not used.

### Employees and self-service
- [ ] **P2** Employee self-service payslips: the Employee role has no `payroll:View`. Decide whether employees see
      their own payslips (the API already scopes non-approvers to their own).
- [ ] **P3** Store document files (today only metadata: type, title, file name, dates). Needs file storage, virus
      scanning, size and type limits, and access checks.
- [ ] **P3** Manager scope covers **direct reports only** (not indirect reports). Revisit if needed.

### Payroll
- [ ] **P1 before real payroll** Replace the demonstration rules with real, signed-off rules for the jurisdiction
      (tax, social security, rounding). Today the figures are demonstrations only.
- [ ] **P3** `pay.Payslip` requires a bank account: employees without one are skipped with a warning. Decide whether
      cash or cheque payment is needed.
- [ ] **P3** Salary assignment create is not tier-checked: keep `payroll:Create`/`Edit` together with `payroll:Approve`
      in any custom role, or add the check.

### Attendance and leave
- [ ] **P3** Edit a pending adjustment or overtime request (the API has no PUT; today: delete and create again).
- [ ] **P3** A future exit date ends access immediately (product decision: keep or change to "on the exit date").

### Notifications
- [ ] **P2** Check that a broadcast notification's read flag is per user (it was shared by all users earlier).

### Demo build (mock)
- [ ] **P3** Sync the mock role permissions with the new seed (Department Manager `employees:View`, HR Officer
      `payroll:Approve`, Employee `attendance:Create`).

---

## 3. Security and production readiness

- [ ] **P1** Security review of the team and payslip scoping (`Application/Security/SelfService.cs`,
      `OwnRecordScopes.cs`, `CallerTeam.cs`) — implemented and tested, not yet reviewed by `security-auditor`.
- [ ] **P1** Production hosting decision (not started: "Hosting: skip"). Then:
  - [ ] HTTPS, HSTS, secure cookies on the real domain.
  - [ ] `Hosting:ForwardedHeaders` with the real proxy IPs/networks (off by default).
  - [ ] `DataProtection:KeysPath` on a protected, shared, backed-up volume; consider certificate encryption of the keys.
  - [ ] Real SMTP (`Email:Smtp:*`) for password reset mail.
  - [ ] Connection string and secrets from a secret store, never from files in the repo.
  - [ ] Never connect Claude's environment to production.
- [ ] **P2** Multi-instance: the password reset throttle, the sign-in rate limiter and the grant cache safety net are
      per instance (in memory). Use a shared store if several API instances run.
- [ ] **P2** Remember-me sessions: 30 days maximum (`Auth:RememberMeMaxDays`). Confirm the value.
- [ ] **P3** Audit/retention policy for activity logs and dev mail (`App_Data/dev-mail` holds reset tokens in development).
- [ ] **P1** Real HR data only after all of the above, plus a data protection review (PII, bank details, documents).

---

## 4. Testing

- [ ] **P2** Playwright coverage for API mode: all API scenarios in `docs/E2E-TEST-SCENARIOS.md` are **Manual** today
      (the Playwright suite runs the mock build only). Needs an API + test database in CI.
- [ ] **P2** CI job for the .NET API (build, unit tests; integration tests need LocalDB or a SQL Server container).
- [ ] **P2** Integration tests share one database (`HR_IntegrationTests`): never run two suites at once; a failed test
      can leave `TST-` rows that block the next run (drop the database to recover). Consider a per-run database name.
- [ ] **P3** Known flaky end-to-end tests under load: shift management, Excel import wizard (timeouts). Stabilise.
- [ ] **P3** Missing tests noted by agents:
  - [ ] Salary tab error banner (component test).
  - [ ] Savepoint rollback in joined transactions (`EfUnitOfWork`).
  - [ ] Cascade delete of documents with their employee.
  - [ ] Payroll: two simultaneous processes of one period; approve during return-to-draft; a large run (thousands of payslips).
  - [ ] Forwarded headers over real HTTP.

---

## 5. Technical debt

- [ ] **P3** Initial bundle is ~1.18 MB against a 1.00 MB budget (warning in `npm run build`). Lazy-load more.
- [ ] **P3** Mixed CRLF/LF line endings across the repo (add `.gitattributes`).
- [ ] **P3** Stale doc comment in `api/src/Apex.Hrms.Application/Security/CallerPermissions.cs` ("an approver").
- [ ] **P3** Duplicate `App_Data/` rule in `api/.gitignore`.
- [ ] **P3** The Api test project takes 4–8 minutes; find the slow tests.

---

## 6. Documentation

- [ ] **P2** Module guides: `docs/modules/` has only Organization and Payroll. Add Employees, Leave, Attendance,
      Recruitment, Settings, Excel.
- [ ] **P2** Keep `docs/E2E-TEST-SCENARIOS.md` updated with every behaviour change (345 scenarios today).
- [ ] **P3** Audit the in-app guides in `ui/src/app/core/constants` for any remaining "mock only" wording.
- [ ] **P3** A deployment guide once hosting is decided.
