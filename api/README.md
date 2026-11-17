# APEX HRMS API

.NET 10 Web API (Clean Architecture) for APEX HRMS. EF Core maps onto the existing SQL Server schema owned by the
SSDT project `HR DBPJ`; it never creates or migrates it.

| Project | Role |
| --- | --- |
| `Apex.Hrms.Domain` | Entities, domain rules, `Result` / `AppError`. No dependencies. |
| `Apex.Hrms.Application` | Use-case handlers, FluentValidation, DTOs, ports (`IRepository<>`, `IRecordCounter<>`, `IUnitOfWork`, `IClock`, `ICurrentUser`, `IAccessScope<>`). |
| `Apex.Hrms.Infrastructure` | EF Core 10 SQL Server mapping (including the Identity stores in the `auth` schema), repositories, clock, SQL error translation, account directory, mail senders, audit stamping, placeholder and system current users. |
| `Apex.Hrms.Api` | Minimal API endpoints, authorization policies, ProblemDetails, CORS, OpenAPI. |

Run: put the connection string in `ConnectionStrings:Hrms` (`appsettings.Development.json` holds the LocalDB one,
`Server=(localdb)\MSSQLLocalDB;Database=HR;Integrated Security=True;TrustServerCertificate=True`; use user-secrets or
environment variables for anything with a password), then `dotnet run --project src/Apex.Hrms.Api`.
Build and test from the `api` folder: `dotnet build Apex.Hrms.slnx` and `dotnet test Apex.Hrms.slnx`. The tests never
open a database connection.

Sign-in (phase 1) is implemented: ASP.NET Core Identity with a same-site cookie, see "Authentication" below. Every
endpoint requires a signed-in user except the four anonymous `/api/auth` endpoints. Per-permission checks (which role may
do what) are real (phase 3): see "Permission check" below. Row-level scoping (own record, direct reports for managers, everyone for HR and payroll staff) of leave, attendance, employees and salary data is real
too, see "Row-level scoping" (recruitment included: "HR all, managers own"). Do not store real data until the whole system has been reviewed.
Without a database you cannot sign in: create the first administrator with the `bootstrap-admin` command first.

## Configuration

For production settings, hosting notes and the go-live checklist see [`docs/DEPLOYMENT.md`](../docs/DEPLOYMENT.md).

| Key | Meaning |
| --- | --- |
| `ConnectionStrings:Hrms` | SQL Server connection string (required). |
| `Business:TimeZone` | Time zone that defines "today" for rules such as "opened date cannot be in the future". Default `Asia/Yangon`. An unknown value stops the API at startup. |
| `Business:WeekendDays` | Days of the week that are not working days, for leave day counts and the leave calendar: a list of day names, default `["Saturday", "Sunday"]`. An unknown name stops the API at startup. |
| `Cors:AllowedOrigins` | Origins the browser frontend may call from. |
| `AllowedHosts` | `;`-separated exact host names. `*` is only accepted in Development; any other environment refuses to start with a missing or wildcard value. |
| `Auth:DevelopmentCookieName` | Development only: session cookie name (default `hrms-dev`). Outside Development the name is always `__Host-hrms`. |
| `Auth:RememberMeMaxDays` | Hard end of a "remember me" session in days, however active the user is (default 30, accepted 1 to 365; anything else falls back to 30). |
| `Auth:RateLimit:PermitsPerMinute` | Sign-in, forgot-password and reset-password requests allowed per client IP per minute (default 10). |
| `Auth:PasswordResetUrl` | Optional absolute URL of the frontend reset page; the reset mail links to `{url}?email=...&token=...`. Without it the mail shows the token. |
| `Email:DevelopmentFolder` | Development only: folder for the mail files (default `App_Data/dev-mail` under the API project). |
| `Email:Smtp:Host`, `Email:Smtp:FromAddress` | Required outside Development for password reset mail. |
| `Email:Smtp:Port` (587), `Email:Smtp:EnableSsl` (true), `Email:Smtp:FromName` | Optional. |
| `Email:Smtp:UserName`, `Email:Smtp:Password` | Optional pair (both or neither). **Environment variables or user secrets only**, never an appsettings file. |
| `DataProtection:KeysPath` | Directory where the data protection key ring (session cookie, antiforgery, reset tokens) is stored. Default `App_Data/keys` under the content root; a relative value starts at the content root. Protect it like a secret (see "Authentication"). |
| `Kpi:EvidenceFolder` | Directory where the KPI evidence files (uploads on scorecard items) are kept **permanently**. Default `App_Data/kpi-evidence` under the content root; a relative value starts at the content root. The files are HR records: keep the folder outside any web root and never serve it, give the API's account (and nobody else) read and write access, back it up together with the database (a restored database without its files has attachments that answer `404`), and keep it out of git (`App_Data/kpi-evidence/` is ignored). See "HTTP contract (KPI evidence attachments)". |
| `Kpi:EvidenceUploadsPerMinute` | KPI evidence uploads (`POST /api/kpi/scorecard-items/{itemId}/attachments`) one signed-in account may start per minute (default 10; a positive whole number, anything else falls back to 10). Every attempt counts, whatever its answer; the next one is `429`. In memory per instance. See "HTTP contract (KPI evidence attachments)". |
| `Hosting:ForwardedHeaders:Enabled` | Behind a reverse proxy, take the client IP and scheme from `X-Forwarded-For` and `X-Forwarded-Proto` (default `false`, so local development is unchanged). The client IP feeds the sign-in rate limit and lockout, the scheme feeds HTTPS redirection and secure cookies. When enabled, `UseForwardedHeaders` runs first in the pipeline with a forward limit of 1 and trusts only the proxies below; enabling it with neither list filled stops startup. |
| `Hosting:ForwardedHeaders:KnownProxies` | List of trusted proxy IP addresses (for example `["10.0.0.5"]`; as environment variables `Hosting__ForwardedHeaders__KnownProxies__0`). An invalid address stops startup. |
| `Hosting:ForwardedHeaders:KnownNetworks` | List of trusted proxy networks in CIDR form (for example `["10.0.0.0/8"]`). The framework's loopback defaults are removed: only these lists are trusted. |

Secrets: `dotnet user-secrets set "Email:Smtp:Password" "..." --project src/Apex.Hrms.Api` or the environment variable
`Email__Smtp__Password`. Configuration is read when the host starts: restart after changing it.

The password reset limits (one administrator-triggered reset mail per user per minute, and at most 20 per calling administrator in 10 minutes, in memory per instance; the allowance is given back only when no mail was sent) are not configurable. Outside Development the API also uses HTTPS redirection and HSTS. Every response carries
`X-Content-Type-Options: nosniff` and `Cache-Control: no-store`.

## HTTP contract (organization module)

Resources: `companies`, `branches`, `departments`, `sections`, `job-grades`, `positions`, all under `/api/`.

| Route | Purpose | Body |
| --- | --- | --- |
| `GET /api/{resource}` | all records | none |
| `GET /api/{resource}/query?pageIndex&pageSize&sort=field:asc\|desc&search&...filters` | one page | none |
| `GET /api/{resource}/{id}` | one record | none |
| `POST /api/{resource}` | create, `201` with `Location` | create fields plus `status` |
| `PUT /api/{resource}/{id}` | update | update fields plus `rowVersion` (no `status`) |
| `DELETE /api/{resource}/{id}` | delete, `204` | `{ "rowVersion": "..." }` |
| `POST /api/{resource}/{id}/activate` | set status Active, `200` with the record | `{ "rowVersion": "..." }` |
| `POST /api/{resource}/{id}/deactivate` | set status Inactive, `200` with the record | `{ "rowVersion": "..." }` |

* `search` is at most 100 characters; a longer value is a Validation error on `search`.
* `status` is only sent on create (it is the status the new record starts with). A `status` in a PUT body is ignored;
  status changes only through `activate` and `deactivate`. Activating an active record is a no-op (idempotent).
* Text fields are trimmed by the server; lengths are measured after trimming.
* Every response record includes `rowVersion` (base64 string of the 8-byte SQL `rowversion`). Use the value of the
  last response you received for the record.

### Optimistic concurrency (`rowVersion`)

`rowVersion` is **required** in the JSON body of every update, delete and command. There is no ETag or `If-Match`.

* Missing, blank or malformed (not base64 of 8 bytes): `400` Validation with `errors.rowVersion`.
* Stale (the record changed or was deleted since): `409` Conflict. Reload the record and retry.
* DELETE carries the row version in a JSON body, not in the query string: base64 contains `+`, `/` and `=`, which
  need escaping in URLs, and URLs end up in logs. ASP.NET Core binds a body on DELETE without special handling.
  Browsers and Angular `HttpClient` can send one (`http.delete(url, { body: { rowVersion } })`); a proxy that strips
  DELETE bodies would break it, in which case switch every client and the endpoint to a query parameter together.

### Errors

Every error is `application/problem+json` (RFC 9457):

```json
{
  "type": "...", "title": "Validation failed", "status": 400,
  "detail": "One or more validation errors occurred.",
  "code": "Validation",
  "errors": { "code": ["Company code must be unique."] },
  "traceId": "00-..."
}
```

| `code` | Status | Meaning |
| --- | --- | --- |
| `Validation` | 400 | Bad input; `errors` maps a camelCase field to its messages. Also malformed JSON and other client errors that keep their own status (413, 415 ...). |
| `NotFound` | 404 | No such record, or outside the caller's access scope. |
| `Conflict` | 409 | Stale `rowVersion`, record still referenced, payroll finality, foreign key violation. |
| `Unauthorized` | 401 | Not signed in (or the session ended), a failed sign-in, or an audited write with no signed-in user. Never a redirect. |
| `Forbidden` | 403 | The caller's role does not hold the permission of the route (`module:Action` in `sec.RolePermission`), the caller is not an HR Administrator on a settings endpoint, or the caller acts for another employee without the module's Approve permission (see "Row-level scoping"). |
| `Unknown` | 500 | Unexpected failure; the message is generic. Quote `traceId` to find it in the server log. |
| `Unknown` | 503 | A needed service is not configured: password reset by email without email settings (`POST /api/users/{id}/password-reset`, `POST /api/auth/forgot-password`). The body has `code: "Unknown"`. |

Database errors that slip past the rules are translated by constraint name, never by echoing the SQL message
(which can contain the duplicate value):

| SQL | Result |
| --- | --- |
| 2601 / 2627 unique violation `UQ_<Table>_Code`, `UQ_Company_Name` | `400` Validation on `code` / `name` |
| 547 CHECK `CK_<Table>_<Field>` | `400` Validation on that field |
| 547 FOREIGN KEY / REFERENCE | `409` Conflict |
| `UQ_LeavePolicy_ActiveGroup` | `400` Validation on `leaveTypeId` |
| `UQ_EvaluationPeriod_CompanyId_Name`, `UQ_RatingBand_MinScore` | `400` Validation on `name` / `minScore` |
| `UQ_Scorecard_EmployeeId_PeriodId`, `UQ_ScorecardItem_ScorecardId_TemplateId`, `CK_Scorecard_NotOwnReviewer` | `409` Conflict |
| `UQ_LeaveApplication_ApplicationNo`, `CK_LeaveApplication_NotOwnApprover` | `409` Conflict |
| THROW 51001-51004 (an approved payroll run and its payslips are locked, a paid run is final) | `409` Conflict |

`traceId` is `Activity.Current.Id` or the request trace identifier. The server log for an unexpected error has the
exception type chain, the SQL error number and the stack trace, never the exception message.

### Moving a record under another parent

Changing the parent of a record is checked before the database composite foreign keys would reject it. A `400`
Validation on the parent field explains it, for example
`Branch "Yangon Office" cannot move to another company while it has 3 department(s), 12 employee(s).`

| Record | Parent field | Blocked while it has |
| --- | --- | --- |
| Branch | `companyId` | departments, employees |
| Department | `branchId` | sections, positions, employees |
| Section | `departmentId` | positions, employees |
| Position | `departmentId` | employees |

A position cannot report to itself or, directly or indirectly, to a position that reports to it
(`reportsToPositionId`, chains are walked up to 50 levels).

## HTTP contract (employee module)

The first module with personal data. Which response holds which field is a security decision (review H1/H2), pinned by
tests: **the list row and the detail never contain a national id, date of birth, marital status, nationality, personal
email, phones, address or emergency contact.** Those live behind `/personal`; the salary bank account behind
`/bank-account`.

| Route | Policy | Body | Returns |
| --- | --- | --- | --- |
| `GET /api/employees/query?pageIndex&pageSize&sort&search&departmentId&branchId&shiftId&employmentStatus&employmentType&joiningFrom&joiningTo` | `employees:View` | none | `PagedResult<EmployeeListItemDto>`, `pageSize` at most 200 |
| `GET /api/employees/{id}` | `employees:View` | none | `EmployeeDto` |
| `GET /api/employees/{id}/history` | `employees:View` | none | `EmploymentHistoryEntryDto[]`, oldest first (read only: the server records it) |
| `GET /api/employees/next-code` | `employees:Create` | none | `{ "code": "EMP061" }`, consumes a number of `hr.EmployeeCodeSeq` |
| `POST /api/employees` | `employees:Create` | `EmployeeCreateRequest` (employment and personal fields, `employmentStatus`) | `201` + `Location`, `EmployeeDto` |
| `PUT /api/employees/{id}` | `employees:Edit` | `EmployeeUpdateRequest` (employment fields + `rowVersion`) | `EmployeeDto` |
| `DELETE /api/employees/{id}` | `employees:Delete` | `{ "rowVersion" }` | `204` |
| `GET /api/employees/{id}/personal` | `employees:ViewSensitive` | none | `EmployeePersonalDto` |
| `PUT /api/employees/{id}/personal` | `employees:EditSensitive` | personal fields + `rowVersion` | `EmployeePersonalDto` |
| `GET /api/employees/{id}/bank-account` | `employees:ViewBank` | none | `BankAccountDto` with the full number; `404` when none |
| `PUT /api/employees/{id}/bank-account` | `employees:EditBank` | `{ bankName, branchName?, accountName, accountNumber, rowVersion? }` | `BankAccountDto` |
| `POST /api/employees/{id}/exit` | `employees:Edit` | `{ rowVersion, exitDate, status: "Resigned"\|"Terminated" }` | `EmployeeDto` |
| `POST /api/employees/{id}/reinstate` | `employees:Edit` | `{ rowVersion, status? }` (default `Active`) | `EmployeeDto` |
| `POST /api/employees/{id}/status` | `employees:Edit` | `{ rowVersion, status }` (Active, Probation, On Leave, Suspended) | `EmployeeDto` |

* **No unpaged list.** There is deliberately no `GET /api/employees` (it answers `405`): employees are only read in
  pages of at most 200, so one call cannot dump the workforce.
* **Status never changes through PUT.** `employmentStatus` is only sent on create (Active, Probation, On Leave or
  Suspended); leaving, returning and the other status moves are the three commands above, and each leaves a history
  entry. Activate and deactivate do not exist for employees.
* **Bank account:** a separate row (`hr.EmployeeBankAccount`, one per employee). `EmployeeDto.bankAccountMasked` shows
  only the last four digits (`******7890`); the full number is only in `BankAccountDto`. `rowVersion` of the PUT is the
  one of the account row and is required to change an existing account (`400` when missing, `409` when stale); the first
  account of an employee needs none. The number is 8 to 20 digits.
* **Employee code:** optional on create. Without one the server takes the next number of `hr.EmployeeCodeSeq`, formats it
  `EMP` + at least 3 digits and skips codes already in use. An explicit code is trimmed and stored upper case; at most 12
  characters of `A-Z`, `0-9` and `-`. The work email is trimmed and stored lower case. Both are unique (`400` on
  `employeeCode` / `workEmail`).
* **Rules** (ported from the mock): age at least 18 on the business-time-zone "today"; probation and exit dates not
  before the joining date; resigned or terminated needs an exit date (so only the exit command sets it); company, branch,
  department, section and position must exist and match (branch in company, department in branch, section and position in
  department); inactive company, branch, department or position cannot receive an employee, but an existing assignment may
  stay; a manager must exist, must not have left, must not be the employee and must not create a reporting loop; a
  manager with direct reports (or one who heads a branch, department or section) cannot be deleted.
* **History** is recorded by the server: `Joined` on create, `Promotion` or `Transfer` on a position or organization
  change (promotion when the new job grade has a higher level), `Contract Change` on a new employment type, `Status
  Change` and `Exit` from the commands. `recordedBy` is the display name of the signed-in caller.
* **Field names:** `managerId` (database `ManagerEmployeeId`), `status` in the command bodies, nested `address` and
  `emergencyContact` objects in the personal data; validation errors on them are keyed `address.city`,
  `emergencyContact.relationship`, and so on.
* **Salary summary:** `GET /api/employees/{id}/salary-summary` (`payroll:View`) returns `EmployeeSalarySummaryDto`
  (`employeeId, basic, allowances, gross, deductions, net, gradeCode, gradeName, bandMin, bandMax, bandPosition, assumptions`).
  **DEMONSTRATION figures, not real payroll**: a full month (the business-time-zone current month, no proration, overtime or
  unpaid leave) at the assignment in force today, worked out with the same `PayCalculator` as payroll runs. `allowances` is gross
  minus basic; `bandPosition` is the rounded 0 to 100 place of the basic in the job-grade band, clamped (0 when the band has
  no width). `404` `Employee #id was not found.` or `<First> <Last> has no salary assignment yet.`
* **Documents** (`hr.EmployeeDocument`) are metadata only (no file is stored or accepted) under `/api/employee-documents`:
  `GET /query?pageIndex&pageSize&sort&search&employeeId&type` and `GET /{id}` need `employees:ViewSensitive`; `POST`, `PUT /{id}`
  (with `rowVersion`) and `DELETE /{id}` (body `{ rowVersion }`) need `employees:EditSensitive`. There is no unpaged list
  (`GET /api/employee-documents` answers `405`). `EmployeeDocumentDto`: `id, employeeId, type, title, fileName, uploadedOn,
  expiryDate (yyyy-MM-dd or null), rowVersion`. Rules: the employee must exist (`400` on `employeeId`) and cannot change on
  update (`400`, as for salary assignments); `type` is one of National ID, Passport, Resume, Offer Letter, Employment Contract,
  Certificate, Work Permit, Medical Report, Other; title 1 to 80 and file name 1 to 260 characters after trimming; the file
  name is a plain name (no `/` or `\`, no control characters, not `.` or `..`); `uploadedOn` defaults to today and cannot be in
  the future; `expiryDate` is free. The search matches title, file name and type text. Activity entries read
  `Document "<title>" created|updated|deleted` (category `employee`); the file name is never written there. Documents are not
  scoped to the own record: only HR roles hold the sensitive permissions. They are deleted with their employee (database cascade).
* **Do-not-rehire list** (`hr.EmployeeBlacklist`, system-versioned) under `/api/employee-blacklist`. Own permissions `employees:ViewBlacklist` (reads) and `employees:EditBlacklist` (changes), held only by HR Administrator in the seed; the reason is sensitive, so there is no unpaged list. `BlacklistEntryDto`: `id, employeeId, employeeCode, employeeName, reason, blacklistedOn, blacklistedBy, liftedOn, liftedBy, liftReason, rowVersion` (`liftedOn` null = active; `blacklistedBy` / `liftedBy` are display names kept at the time).

  | Route | Policy | Body / notes |
  | --- | --- | --- |
  | `GET /api/employee-blacklist/query?pageIndex&pageSize&sort&search&active` | `employees:ViewBlacklist` | `active=true` only entries in force, `false` only lifted, none for both; sort keys `id, employeeId, blacklistedOn (default, desc), liftedOn`; search matches the employee's code or name |
  | `GET /api/employee-blacklist/{id}` | `employees:ViewBlacklist` | `404` unknown id |
  | `GET /api/employees/{id}/blacklist` | `employees:ViewBlacklist` | the active entry; `404` when the employee does not exist or is not listed |
  | `POST /api/employee-blacklist` | `employees:EditBlacklist` | `{ employeeId, reason }`, `201` + `Location` |
  | `POST /api/employee-blacklist/{id}/lift` | `employees:EditBlacklist` | `{ rowVersion, reason }`, `200` with the entry |

  Rules: `reason` is required and at most 500 characters (`400` on `reason`; the same for the lift reason); `employeeId` must exist (`400`); only a former employee (one with an exit date) can be listed (`409` "Only a former employee can be put on the do-not-rehire list."); one active entry per employee (`409` "This employee is already on the do-not-rehire list."; the filtered unique index `UQ_EmployeeBlacklist_ActiveEmployee` is the backstop); lifting a lifted entry is `409`, a stale `rowVersion` `409`; a lifted entry stays as history and the employee can be listed again. An employee with any blacklist history cannot be deleted (`409`, "... used by N do-not-rehire entry(ies)"; the foreign key has no cascade). Activity (category `employee`): `Employee EMP042 added to the do-not-rehire list` / `... removed from the do-not-rehire list`, never the reason.

  **Recruitment check.** `candidates` accept an optional `nationalId` (at most 40 characters; trimmed; never returned; omitted on update keeps the stored value, blank clears it). Candidate and application responses carry `blacklistMatch` (bool) for every caller who may read them (`recruitment:View`): true when the candidate matches an employee with an **active** entry by email (the employee's work or personal email, trimmed, case-insensitive), phone (the employee's phone or alternate phone, digits only) or national id (trimmed, case-insensitive, only when the candidate has one). Nothing else is exposed: not which employee, not the reason. A lifted entry no longer matches.
  **Hire gate.** There is no "hired" stage in this API; selecting a candidate (`POST /api/applications/{id}/stage` with `stage: "Selected"`) is the hiring decision. A matching candidate is refused with `409` "This candidate matches the do-not-rehire list. An HR Administrator must approve the hire with a reason." unless the body also has `blacklistOverrideReason` (at most 300 characters, `400` otherwise) **and** the caller is an HR Administrator (role read live, like the settings routes). A caller who is not an administrator and sends an override reason gets `403` "Only an HR Administrator can approve the hire of a candidate who matches the do-not-rehire list."; an override sent for a candidate without a match is ignored and not stored. An approved override is stored on the application (`BlacklistOverrideReason`, `BlacklistOverrideByUserId`; never returned) and logged as `Hire of application #12 approved despite a do-not-rehire match` (category `recruitment`; no reason, no employee).
* Organization deletes and moves count employees through the view `hr.EmployeeOrgLink` (placement without personal data),
  not through `hr.Employee`; the view and the sequence must exist in the target database.

## HTTP contract (leave module)

Three resources, two reads and four workflow commands. Policies: `leave:View`, `leave:Create`, `leave:Edit`,
`leave:Delete`, `leave:Approve` (checked against the role's grants, see "Permission check"). Balances are never stored (the
schema has no balance table): they are worked out from the leave applications on every request.

| Route | Policy | Body | Returns |
| --- | --- | --- | --- |
| `GET/POST /api/leave-types`, `GET /query`, `GET/PUT/DELETE /{id}`, `POST /{id}/activate` and `/deactivate` | View / Create / View / View / Edit / Delete / Edit | as the organization resources | `LeaveTypeDto` |
| `GET/POST /api/leave-policies`, `GET /query`, `GET/PUT/DELETE /{id}`, `POST /{id}/activate` and `/deactivate` | View / Create / View / View / Edit / Delete / Edit | as the organization resources | `LeavePolicyDto` |
| `GET /api/leave-applications/query?pageIndex&pageSize&sort&search&employeeId&leaveTypeId&status&dateFrom&dateTo` | `leave:View` | none | `PagedResult<LeaveApplicationDto>`; `dateFrom`/`dateTo` match as an overlap with the leave dates; search is application number or reason |
| `GET /api/leave-applications/{id}` | `leave:View` | none | `LeaveApplicationDto` |
| `POST /api/leave-applications` | `leave:Create` | `{ employeeId, leaveTypeId, startDate, endDate, halfDay, reason, documentName? }` | `201` + `Location`, a **Draft** |
| `PUT /api/leave-applications/{id}` | `leave:Edit` | the same fields + `rowVersion` | `LeaveApplicationDto`; drafts only (`409` otherwise) |
| `DELETE /api/leave-applications/{id}` | `leave:Delete` | `{ rowVersion }` | `204`; drafts only (`409` otherwise) |
| `POST /api/leave-applications/{id}/submit` | `leave:Create` | `{ rowVersion }` | Draft to Submitted |
| `POST /api/leave-applications/{id}/approve` | `leave:Approve` | `{ rowVersion, remarks? }` | Submitted to Approved; remarks optional (default `Approved.`) |
| `POST /api/leave-applications/{id}/reject` | `leave:Approve` | `{ rowVersion, remarks }` | Submitted to Rejected; remarks required (`400` on `remarks`) |
| `POST /api/leave-applications/{id}/cancel` | `leave:Create` | `{ rowVersion, remarks? }` | Submitted, or Approved leave that has not started, to Cancelled (default remarks `Cancelled.`) |
| `GET /api/leave/balances?employeeId&year?` | `leave:View` | none | `LeaveBalanceDto[]` (`leaveTypeId, code, name, color, entitled, taken, pending, available`); `year` defaults to the current year; `404` unknown employee, `400` year outside 2000-2100 |
| `GET /api/leave/calendar?from&to&departmentId?&leaveTypeId?&includePending?` | `leave:View` | none | `{ from, to, days: [{ date, isWeekend, holiday, entries: [{ applicationId, employeeId, employeeName, leaveTypeId, leaveTypeName, color, status, halfDay }] }] }`; at most 366 days; `includePending` defaults to true |

`LeaveApplicationDto`: `id, applicationNo, employeeId, leaveTypeId, startDate, endDate, days, halfDay, reason,
documentName, status, appliedOn, approverId, actionedByName, actionedOn, approverRemarks, rowVersion`. Dates are
`yyyy-MM-dd`. `approverId` is the employee who approved or rejected and is null for the built-in administrator;
`actionedByName` is the display name kept at the time of the approval, rejection or cancellation.

* **No unpaged application list:** `GET /api/leave-applications` answers `405` (applications carry reasons, sometimes
  medical). Leave types and policies keep the plain list like the organization resources.
* **Application number** `LV-yyyy-nnnn`: the year of the application date and the next number of
  `lve.LeaveApplicationNoSeq` (a running number that does not restart each year), skipping numbers already in use.
* **Days:** half day = 0.5 (single date); otherwise the working days between the dates, leaving out the weekend days
  (`Business:WeekendDays`, default Saturday and Sunday) and the dates in `att.Holiday`. At most 366 days per request;
  no working day is a `400` on `endDate`. The server ignores a `days` sent by the client.
* **Transition table:** Draft to Submitted; Submitted to Approved or Rejected; Submitted or (Approved and start date after
  today) to Cancelled. Anything else is a `409` with the status named. Drafts cannot be cancelled (as in the mock): they
  are deleted.
* **Create/update rules:** employee exists; leave type active and open to the employee's gender; start not before the
  joining date; days at most the type's maximum consecutive days; no overlap with Submitted or Approved leave of the same
  employee (checked again on submit); reason required.
* **Submit rules:** the document name when the type requires one (only the name is stored; no file upload exists yet);
  enough balance for the year the leave starts in (days must not exceed available). A cancelled or rejected application
  frees its days.
* **Own leave:** the caller whose `employeeId` equals the application's employee cannot approve or reject it (`409`
  `You cannot decide your own leave request.`). The built-in administrator (no employee) can; the database CHECK
  `CK_LeaveApplication_NotOwnApprover` backs the rule for approvers that are employees.
* **Entitlement:** active policies of the type matching the employee's grade and employment type (null matches all)
  are candidates; the most specific wins (grade 2 points, employment type 1 point, then more days); candidates whose
  `minServiceMonths` is not reached are skipped; no candidate means the type's default days; candidates but none
  satisfied means 0; monthly accrual grants `days x month / 12` of the as-of month rounded to half days. "As of" is today
  for the current year and 31 December for any other year. `taken` is Approved days and `pending` is Submitted days of leave
  that starts in the year; `available = max(0, entitled - taken - pending)`.
* **Policies:** at most one active policy per leave type, grade and employment type (`400` on `leaveTypeId`; also on
  activation and when an active policy's group is edited into another's). The database has the filtered unique index
  `UQ_LeavePolicy_ActiveGroup` as backstop.
* **Delete protection:** a leave type used by applications or policies, a job grade used by policies and an employee with
  leave applications (as employee or approver) cannot be deleted (`409`, "Deactivate it instead" wording for types).
* Not ported: the mock marked past attendance records as "On Leave" when leave was approved. The attendance module now
  works from approved leave directly (punch refusal, overview, schedule); no record is rewritten.

## HTTP contract (attendance module)

Policies (checked against the role's grants): `attendance:View|Create|Edit|Delete|Approve`. Times travel as `"HH:mm"`
(24 hours, to the minute; `"HH:mm:ss"` is also accepted on input); dates as `yyyy-MM-dd`. Every update, delete and command
needs `rowVersion` as described for the organization module.

| Route | Policy | Notes |
| --- | --- | --- |
| `/api/shifts` (get all, `/query`, `/{id}`, POST, PUT, DELETE, `/{id}/activate`, `/{id}/deactivate`) | View, Edit (create), Edit, Delete, Edit | Like an organization resource, but create needs Edit (every employee holds Create to punch). Fields: `code, name, startTime, endTime, breakMinutes, graceMinutes, workDays (0 = Sunday ... 6), isNightShift, color, status`. Delete is refused (`409`) while employees are assigned or attendance records used the shift. |
| `/api/holidays` (get all, `/query?dateFrom&dateTo&type`, `/{id}`, POST, PUT, DELETE) | View, Edit (create), Edit, Delete | `{ date, name, type: Public\|Company }` (create needs Edit). One holiday per date (`400` on `date`; the table has no unique index, so this is checked by the API only). |
| `GET /api/attendance-records/query`, `GET /api/attendance-records/{id}` | View | Read only, list shape with names. Filters: `employeeId, departmentId, branchId, status, dateFrom, dateTo, lateOnly, earlyOnly, overtimeOnly`; `search` matches employee name or code; sort by `date, employeeId, checkIn, checkOut, workedMinutes, lateMinutes, earlyLeaveMinutes, overtimeMinutes, status`. |
| `POST /api/attendance/punches` | Create | Body `{ employeeId, kind: "CheckIn"\|"CheckOut" }` and nothing else: the server sets the date and the time (business time zone). Returns the day record. A signed-in employee may punch only for themself (`403`); the built-in administrator may punch for anyone. |
| `GET /api/attendance-adjustments/query`, `/{id}`; `POST`; `DELETE /{id}` (pending only) | View, Create, Delete | Body `{ employeeId, date, requestedCheckIn?, requestedCheckOut?, requestedStatus?, reason }`. |
| `POST /api/attendance-adjustments/{id}/approve`, `/reject` | Approve | Body `{ rowVersion, remarks? }`; reject needs remarks. Approve applies the request to the record in the same save. |
| `GET /api/overtime-records/query`, `/{id}`; `POST`; `DELETE /{id}` (pending only) | View, Create, Delete | Body `{ employeeId, date, startTime, endTime, reason }`; `minutes` is derived. |
| `POST /api/overtime-records/{id}/approve`, `/reject` | Approve | Body `{ rowVersion, remarks? }`. |
| `GET /api/attendance/overview?date&branchId` | View | `{ summary, byDepartment, late, absent, trend }`. |
| `GET /api/attendance/schedule?weekStart&departmentId&branchId` | View | `{ weekStart, days, rows: [{ ..., cells: [{ date, kind: Shift\|Off\|Holiday\|Leave, label }] }] }`. |

Rules (the attendance rules are demonstrations, not statutory calculations):

* **Derivation** (shared by punches and approved adjustments): late when the check-in is after shift start + grace, counted
  from the shift start; worked = check-out - check-in - break, and a day is a Half Day when that is under 240 minutes (then
  the break is not deducted and there is no early leave); early leave = minutes before the shift end; overtime = minutes after
  the end, only when more than 30 (then all of them). Night shifts roll the end and an earlier check-out into the next day.
* **Punches:** one record per employee and day (database unique key). Check-in once; check-out needs a check-in, once, and
  must be after it unless the shift is a night shift. A night shift's check-out after midnight closes the previous day's
  open record. A day of approved whole-day leave refuses punches (`409`); working on a holiday or a day off makes the day
  Present with no late or early minutes and a "Worked on a ..." remark. The shift is the employee's own or the first one.
* **Adjustments:** the employee needs a record on that date (the table links to it), the date may not be in the future,
  something must be requested, the check-out must follow the check-in unless night shift, and one pending request per
  employee and date. Nobody decides their own request (`409`); `decidedBy` is the approver's name at that time.
* **Overtime:** 15 minutes to 12 hours (start to end, across midnight), one entry per employee and date, date not in the future.
* **Not provided:** PUT of a pending adjustment or overtime request (delete it and file it again); `recordId` on adjustments.
* **Overview:** built from the employees expected that day (employed, working weekday of their shift, not a holiday); without a
  record they are On Leave (approved whole-day leave) or Absent (for today: "not checked in yet"; a future day counts nothing).

## HTTP contract (payroll module)

**All payroll figures are DEMONSTRATION figures.** The calculation is ported from the frontend's sample rules; the tax and
social security percentages are whatever an administrator configures on a salary component. Nothing here is a statutory tax,
social security or labour-law calculation, and no jurisdiction has signed it off. Do not use it to pay real people.
Salary amounts and payslips are personal data: every route needs a payroll permission, salaries and payslips are listed in
pages only (no "get all"), and a payslip carries the masked bank account only (`•••• 7890`).

Permissions are exactly the seeded payroll actions: View, Create, Edit, Approve (there is no payroll Delete: deleting a
component, structure, salary assignment or draft period needs Edit). Rules and wording follow the frontend mock services; the
database triggers lock an approved run (and its payslips, lines and warnings) and make a paid run final.

| Route | Policy | Body / notes |
| --- | --- | --- |
| `GET/POST /api/salary-components`, `GET /query`, `GET/PUT/DELETE /{id}`, `POST /{id}/activate\|deactivate` | View / Create / Edit | `code, name, type (Earning\|Deduction), calculation (Fixed\|PercentOfBasic\|PercentOfGross), value, description, status`; a percentage is above 0 and at most 100 |
| `/api/salary-structures` (same shape) | View / Create / Edit | `code, name, description, componentIds` (display order; at least one, at least one earning) |
| `GET /api/salary-assignments/query?employeeId&structureId`, `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` | View / Create / Edit | `employeeId, structureId, basicSalary, effectiveFrom, remarks`; basic inside the employee's job-grade band, not before the joining date, one per employee and date, employee cannot change, the last one of an employee cannot be deleted. History = query by `employeeId`, `sort=effectiveFrom:desc` |
| `GET /api/payroll-runs`, `/query?status&year`, `/{id}`, `POST` (`year, month, payDate?`), `PUT /{id}`, `DELETE /{id}` | View / Create / Edit | Period not in the future, one per month; change and delete only in Draft |
| `POST /api/payroll-runs/{id}/process` | Edit | `{rowVersion}`; Draft to Pending Approval; earlier periods must be Approved or Paid first |
| `POST /api/payroll-runs/{id}/approve` | Approve | `{rowVersion}`; the user who processed the run cannot approve it (409) |
| `POST /api/payroll-runs/{id}/mark-paid` | Approve | `{rowVersion}`; Approved to Paid, records only (no money moves) |
| `POST /api/payroll-runs/{id}/return-to-draft` | Approve | `{rowVersion, reason?}` (reason at most 200 characters); payslips and warnings are deleted after the run is saved as Draft, in one transaction |
| `GET /api/payslips/query?runId&employeeId&departmentId`, `GET /api/payslips/{id}` | View | Read only |

**My payslips** (employee self-service, no payroll permission, only the "signed in" policy):

| Route | Notes |
| --- | --- |
| `GET /api/me/payslips?pageIndex&pageSize` | `PagedResult<PayslipDto>` (same shape, masked account only) of the caller's **own** employee, newest run first (`pageSize` default 12, at most 200). Only payslips of **Approved or Paid** runs: never Draft or Pending Approval. A caller with no linked employee gets an empty page. |
| `GET /api/me/payslips/{id}` | The same, one payslip; another employee's, an unreleased run's and any id for a caller without an employee are `404`. |

Calculation per employee and period: part-month employment prorates basic and fixed components by calendar days; earnings are
fixed amounts or a percentage of the prorated basic; approved overtime adds `basic / (working days x 8) x 1.5 x hours`; gross is
basic plus earnings; deductions are fixed, a percentage of basic or of gross, plus unpaid leave `basic / working days` per day;
net is gross minus deductions; every amount is rounded to 2 decimals (halves away from zero). Working days come from the
configured weekend and the holidays. An employee without a salary assignment, or without a usable bank account (the payslip
table requires a masked account), is skipped with a warning.

## HTTP contract (KPI master data)

Four resources under `/api/kpi`: categories, templates, evaluation periods and rating bands. Assigning KPIs and the scorecard
plan are in "HTTP contract (KPI assignments)" below; submission and review, evidence attachments, and the dashboards, reports and
Excel exports have sections of their own further down. Activity entries use the category `performance`. The KPI scoring
itself (`KpiRule`, `KpiCalculator`, calculation version 1) lives in the domain and is not exposed here.

Policies (checked against the role's grants, see "Permission check"): setup is the module `kpi-setup` with
`View|Create|Edit|Delete` (HR); the period picker is the module `kpi` with `View` (everyone who uses scorecards:
`kpi` also has `Create` and `Approve`: `Approve` decides the scorecard row scope and opens the review routes). A caller with `kpi:View` only cannot read
the setup, and `kpi-setup` alone does not open the picker. Every route needs a signed-in user (`401` otherwise).

| Route | Policy | Body | Returns |
| --- | --- | --- | --- |
| `GET/POST /api/kpi/categories`, `GET /query`, `GET/PUT/DELETE /{id}`, `POST /{id}/activate` and `/deactivate` | `kpi-setup:` View / Create / View / View / Edit / Delete / Edit | as the organization resources | `KpiCategoryDto` |
| `GET/POST /api/kpi/templates`, `GET /query`, `GET/PUT/DELETE /{id}`, `POST /{id}/activate` and `/deactivate` | as above | as the organization resources | `KpiTemplateDto` |
| `GET/POST /api/kpi/rating-bands`, `GET /query`, `GET/PUT/DELETE /{id}`, `POST /{id}/activate` and `/deactivate` | as above | as the organization resources | `RatingBandDto` |
| `GET/POST /api/kpi/periods`, `GET /query`, `GET/PUT/DELETE /{id}` | `kpi-setup:` View / Create / View / View / Edit / Delete | create fields (no status), update fields plus `rowVersion` | `EvaluationPeriodDto` |
| `POST /api/kpi/periods/{id}/activate` | `kpi-setup:Edit` | `{ rowVersion }` | Draft to Active |
| `POST /api/kpi/periods/{id}/complete` | `kpi-setup:Edit` | `{ rowVersion }` | Active to Completed |
| `POST /api/kpi/periods/{id}/close` | `kpi-setup:Edit` | `{ rowVersion }` | Completed to Closed (final) |
| `GET /api/kpi/periods/options?companyId?` | `kpi:View` | none | `EvaluationPeriodOptionDto[]`, periods that are not Draft, newest start first |

The paging, `sort=field:asc|desc` whitelist (an unknown field is a `400` on `sort`), `search`, `rowVersion` and error rules
are those of the organization module. Dates are `yyyy-MM-dd`; decimals travel as JSON numbers.

* `KpiCategoryDto`: `id, code, name, description, status, rowVersion`. Create: `{ code, name, description?, status }`.
  Code at most 20 characters (upper-case letters, digits and dashes, as every coded record), name at most 100 and unique,
  description at most 300. Sort: `id, code, name, status`; filter: `status`; search: code or name.
* `KpiTemplateDto`: `id, code, name, description, categoryId, departmentId, measurementType, unit, direction, scoringMethod,
  defaultTarget, rangeMin, rangeMax, maxRating, defaultWeight, maxAchievementPct, frequency, status, rowVersion`. The four
  text enums use the texts of the database CHECK constraints: `measurementType` `Number|Percentage|Currency|Hours|Rating|Boolean`;
  `direction` `Higher Is Better|Lower Is Better|Exact Target|Range Based`; `scoringMethod` `Direct Percentage|Capped Percentage|Rating
  Based|Boolean|Range Based`; `frequency` `Monthly|Quarterly|Half-Yearly|Yearly|Custom`. Create: the same fields without
  `id`/`rowVersion`, plus `status`. Sort: `id, code, name, categoryId, defaultWeight, scoringMethod, frequency, status`; filters:
  `status, categoryId, departmentId, scoringMethod`; search: code or name.
  * **Shape (FluentValidation):** code at most 20, name at most 150, description at most 500, unit at most 20; known enum texts;
    at most 4 decimals on the target, range and rating scale, 2 on the weight and the cap.
  * **Scoring rule (domain, `KpiRule.Validate`, the same rules as the CHECK constraints), errors on the request field:** rating
    scoring and the Rating measurement go together (`maxRating` above 0 only then); Boolean scoring and the Boolean measurement
    go together; Range Based scoring and the Range Based direction go together (`rangeMin` above 0 and `rangeMax` above
    `rangeMin` only then); Capped Percentage needs `maxAchievementPct`; Lower Is Better needs it too (a result of 0 scores the cap);
    Direct Percentage must not have one; the cap is 1 to 500; `defaultTarget` is not negative and above 0 for Higher Is Better and
    Exact Target (the target error is reported on `defaultTarget`); `defaultWeight` is above 0 and at most 100.
  * **Other records:** the code is unique; `categoryId` must exist and be Active when the template is created or its category
    changes (`400` on `categoryId`); `departmentId` is optional and must exist (`400` on `departmentId`).
  * Editing a template never changes scorecard items already assigned from it: items hold their own copy of the code, name,
    target, weight and scoring rules.
  * **Delete protection:** a template with assigned KPIs (scorecard items) cannot be deleted (`409`, with the count).
* `RatingBandDto`: `id, label, minScore, description, status, rowVersion`. Create: `{ label, minScore, description?, status }`.
  Label at most 60 and unique, `minScore` 0 to 500 with at most 2 decimals and unique, description at most 300. Sort: `id, label,
  minScore, status`; filter: `status`; search: label. A band's `minScore` is the lowest overall score that gets the label.
* `EvaluationPeriodDto`: `id, companyId, name, periodType, startDate, endDate, submissionDueDate, requiresHrReview, status,
  rowVersion`. `periodType` is a frequency text (`Monthly|Quarterly|Half-Yearly|Yearly|Custom`); `status` is
  `Draft|Active|Completed|Closed`. Create: `{ companyId, name, periodType, startDate, endDate, submissionDueDate?,
  requiresHrReview }` (always starts in Draft; there is no `status` field and a `status` in a PUT body is ignored). Sort: `id, name,
  companyId, periodType, startDate, endDate, status`; filters: `companyId, periodType, status, year` (year of the start date);
  search: name.
  * **Dates:** `endDate` after `startDate` (`400` on `endDate`); `submissionDueDate`, when given, not before `startDate` (`400`
    on `submissionDueDate`; it may be after `endDate`).
  * **Company:** must exist, and be Active when the period is created or its company changes (`400` on `companyId`).
  * **Name:** at most 100 characters, unique within the company (`400` on `name`; the same name may exist in another company).
  * **No overlap:** two periods of the same type in the same company cannot share a day. Dates are inclusive, so a period that
    starts on the day another ends overlaps it. A clash is `409` and the message names the period it clashes with and its dates.
    Periods of another type or another company never clash. Checked on create and when the company, type or dates change.
  * **Edits by status:** Draft: every field. Active: only `name` and `submissionDueDate`; any other change is `409` ("only its
    name and submission due date can be changed"). Completed and Closed: no edit (`409`). `400` validation errors come before
    these `409`s.
  * **Commands:** `activate` (Draft to Active), `complete` (Active to Completed), `close` (Completed to Closed). A command out of
    order is `409` naming the period and its status; a command is not idempotent. Each logs an activity entry
    (`Evaluation period "X" activated|completed|closed`).
  * **Delete:** only a Draft period, and only when no scorecard uses it (`409` with the status or the scorecard count). A
    company with periods cannot be deleted (`409`).
* `EvaluationPeriodOptionDto` (the picker): `id, name, companyId, periodType, startDate, endDate, status, requiresHrReview,
  submissionDueDate` (null when the period has none; added in G1); no `rowVersion`. Only Active, Completed and Closed periods are listed; `companyId` narrows the list; unfiltered, every company.
* **Other delete protection:** a category used by templates or assigned KPIs, and a department used by templates, cannot be
  deleted (`409`, with the counts; the category and template wording ends in "Deactivate it instead.").
* **Database errors** that slip past the rules: `UQ_Category_Code|Name`, `UQ_Template_Code`, `UQ_RatingBand_Label|MinScore` and
  `UQ_EvaluationPeriod_CompanyId_Name` are `400` on `code`, `name`, `label`, `minScore` or `name`; every `CK_Template_*`,
  `CK_EvaluationPeriod_*`, `CK_RatingBand_*` and `CK_Category_*` is `400` on the field it guards (for example
  `CK_Template_LowerCapped` on `maxAchievementPct`); a foreign key violation is `409`. Never a `500`.
* The tables `Template`, `EvaluationPeriod`, `Scorecard` and `ScorecardItem` are system-versioned: every change is kept in the
  `*History` table (SQL Server owns it; the API never reads or writes it).

## HTTP contract (KPI assignments)

HR assigns KPI templates to employees for an evaluation period, plans a scorecard and finalizes the plan. A scorecard is one
employee's KPI plan and result for one period (`kpi.Scorecard`), with its KPIs (`kpi.ScorecardItem`) and its review history
(`kpi.ReviewAction`). This phase covers the status **Planning** and the move to **Draft**; submission and review (next section), attachments and
later phases. Activity entries use the category `performance` and name an employee by code and a period by
name, never a comment.

| Route | Policy | Body | Returns |
| --- | --- | --- | --- |
| `GET /api/kpi/scorecards/query` | `kpi:View`, rows scoped (below) | filters `periodId, employeeId, departmentId, companyId, status`, `search`, `sort`, paging | `PagedResult<ScorecardDto>` |
| `GET /api/kpi/scorecards/{id}` | `kpi:View`, scoped: out of scope is `404` | none | `ScorecardDetailDto` |
| `POST /api/kpi/assignments` | `kpi-setup:Create` | `{ employeeId, periodId, items: [{ templateId, target?, weight?, startDate?, endDate?, sortOrder? }] }` (1 to 50 items) | `201` + `Location: /api/kpi/scorecards/{id}` when the call created the scorecard, `200` when it added items to the one in Planning; both return `ScorecardDetailDto` |
| `POST /api/kpi/assignments/bulk` | `kpi-setup:Create` | `{ periodId, employeeIds?, departmentId?, branchId?, positionId?, jobGradeId?, items }` | `200` `{ assigned: [{ employeeId, employeeCode, scorecardId, itemsAdded }], skipped: [{ employeeId, employeeCode, reason }] }` |
| `PUT /api/kpi/scorecard-items/{id}` | `kpi-setup:Edit` | `{ target, weight, startDate, endDate, sortOrder?, rowVersion }` | `200` `ScorecardItemDto` |
| `DELETE /api/kpi/scorecard-items/{id}` | `kpi-setup:Edit` | `{ rowVersion }` | `204` |
| `DELETE /api/kpi/scorecards/{id}` | `kpi-setup:Delete` | `{ rowVersion }` | `204` |
| `POST /api/kpi/scorecards/{id}/finalize-plan` | `kpi-setup:Edit` | `{ rowVersion }` | `200` `ScorecardDetailDto` (status Draft) |

* `ScorecardDto` (list): `id, employeeId, employeeCode, employeeName, periodId, periodName, departmentId, departmentName, status,
  itemCount, totalWeight, overallScore, ratingLabel, reviewerEmployeeId, reviewerName, planFinalizedOn, submittedOn, finalizedOn,
  submissionDueDate, rowVersion`. `submissionDueDate` (added in G1) is the period's, or null when the period has none, so a list can show
  a "Late" flag (`submittedOn` after it, or still Draft or Returned after it) without reading each scorecard; the same field is on
  `/api/kpi/my-scorecards` and `/api/kpi/reviews/pending`. Only the employee code and name identify a person. `status` is `Planning|Draft|Submitted|Under
  Review|Returned|Resubmitted|Approved|Rejected|Finalized`. Sort (`sort=field:asc|desc`, an unknown field is `400` on `sort`):
  `id, employeeCode, employeeName, periodName, departmentName, status, overallScore, planFinalizedOn, modifiedAt`; `search`
  (at most 100 characters) matches the employee code or name; an unknown `status` filter matches nothing. Every read is untracked and
  projected: one query for the count, one for the page and one for the reviewers' names, whatever the page size.
* `ScorecardDetailDto`: the list fields plus `items` (by `sortOrder`) and `history` (oldest first). `ScorecardItemDto`: `id,
  templateId, categoryId, categoryName, kpiCode, kpiName, measurementType, unit, direction, scoringMethod, target, rangeMin,
  rangeMax, maxRating, weight, maxAchievementPct, startDate, endDate, sortOrder, actualValue, employeeComment, achievementPct,
  weightedScore, managerRating, managerComment, rowVersion` (texts as in the template). `ReviewActionDto`: `step`
  (`Employee|Manager|HR`), `action` (`Finalize Plan`...), `fromStatus, toStatus, actorUserId, actorName, comment, overallScore,
  occurredAtUtc`. `actorUserId` (added in G1) is the account that recorded the step (null if none was recorded): compare it with the
  signed-in user's id, not the display name, for example to hide the HR final-review buttons from the user who recorded the manager-stage
  Approve. This is a convenience for the screen only; the server enforces that separation of duties by id itself (`409`).
* **Scoping of reads.** The module `kpi` follows the leave tiers (`SelfService.TierOf("kpi")`, `ScorecardScope`):
  * **All** (HR): `kpi:Approve` **and** `employees:Edit` sees every scorecard. **Granting `employees:Edit` together with `kpi:Approve`
    therefore also grants the KPI HR rights** (every scorecard, the HR dashboard, the HR final review), exactly as it does for leave and
    attendance; give `employees:Edit` only to HR staff.
  * **Team** (a manager): `kpi:Approve` without `employees:Edit` sees their own scorecards, their direct reports' scorecards and every
    scorecard that names them as the reviewer (fixed when the plan was finalized, so it stays after the employee moves to another manager).
  * **Own** (an employee): anyone else sees only their own scorecards, and **never one in Planning**: HR's targets are still in progress,
    so until the plan is finalized the scorecard does not exist for its employee (`404` on get and on every write route, absent from the
    list, the reports and the dashboard). The Team and All tiers do see Planning scorecards (a manager may follow a report's plan, and the
    HR assignment screens need them).

  The predicate is applied inside the query (`IAccessScope<Scorecard>`), so a scorecard outside it is `404` on get and absent from the
  list, the total and every filter or search. A caller with no linked employee below the HR tier sees nothing. Items have no scope of
  their own: they are read only through their scorecard.
* **HR writes are not row-scoped.** The assignment, item and plan routes need only their `kpi-setup` permission and act on any
  employee's scorecard (HR plans for everyone, like the other HR master operations); they never use the `kpi` tier. Nothing is trusted
  from the body: the employee, period and templates are looked up and checked.
* **Nobody plans their own scorecard** (separation of duties). When the caller's linked employee is the scorecard's employee, the
  single assignment, the item update and delete, the scorecard delete and the finalize-plan are `409` "You cannot plan your own
  scorecard." (after the `400` row-version check and the `404`); the bulk assignment skips that employee with that reason. The built-in
  administrator, who has no linked employee, is never refused for this. Another HR user plans the HR person's scorecard.
* **Assignment rules** (the first failure is returned, in this order). Request shape (`400`, camel-case fields such as
  `items[0].templateId`, `items[1].weight`): 1 to 50 items, `templateId` above 0, `target` not negative with at most 4 decimals,
  `weight` above 0 and at most 100 with at most 2 decimals, `sortOrder` 0 to 32767, `endDate` not before `startDate`. Then the period
  exists (`400` on `periodId`) and is **Draft or Active** (`409` when Completed or Closed); the employee exists (`400` on `employeeId`),
  has no exit date, is not Resigned or Terminated, and belongs to the period's company (`400` on `employeeId`); each template exists and
  is Active, appears once and, if it has a department, is the employee's department (`400` on `items[i].templateId`); dates default to
  the period's and must lie within it (`items[i].startDate|endDate`); the snapshot rule must pass `KpiRule.Validate()` (an overridden
  `target` of 0 on Higher Is Better is `400` on `items[i].target`); the weights together, with those already on the scorecard, cannot
  exceed 100% (`400` on `items`, naming the total; they must add up to exactly 100% only at finalize). An existing scorecard must be in
  Planning (`409` "Employee EMP-0007 already has a scorecard for this period that is past planning; ...": the text never names the
  scorecard's current status, because `kpi-setup` does not let the caller read the scorecard's progress) and must not hold a requested
  template (`409` naming the KPI code). The caller's own employee is refused before the templates are checked (above).
  * A new scorecard is created in Planning with `companyId, branchId, departmentId, positionId` copied from the employee **now** (the
    scorecard keeps them if the employee moves later) and no reviewer. Each item copies its template (category, code, name,
    measurement, unit, direction, scoring method, range, rating scale, cap); `target` and `weight` default to the template's
    `defaultTarget` and `defaultWeight`; `sortOrder` defaults to the next free position (after the highest of the scorecard, in request
    order). `assignedOn` is today (business time zone), `assignedByUserId` the caller. Editing the template later never changes the item.
  * `201` versus `200`: `201` with a `Location` header means the call created the scorecard; `200` means it added items to a scorecard
    that already existed. Both answer with the whole scorecard.
* **Bulk assignment.** Exactly one selector kind: `employeeIds` (1 to 500; duplicates are ignored) **or** one or more of
  `departmentId, branchId, positionId, jobGradeId` (combined with AND, current employees of the period's company only); both or neither
  is `400` on `employeeIds`, more than 500 ids is `400` on `employeeIds`, and filters that match more than 500 employees are `400` on
  `filters`. A bad request, period (`400` or `409`), template or item fails the whole call before anything is written. An
  employee-level problem only **skips** that employee with a `reason`: the caller's own employee ("You cannot plan your own
  scorecard."), not found (`employeeCode` is null), has left, other company, department template mismatch, a scorecard past planning
  (without its status), a requested KPI already assigned, weights above 100%. The employees, the existing
  scorecards of the period and their items are loaded in set-based queries (never one query per employee). The scorecards and items of
  the employees that pass are written in one transaction; the run records **one** activity entry (`KPIs assigned in bulk for "2026
  Q4": 12 employee(s) assigned, 3 skipped`), and none when nobody was assigned. Running the same request again assigns nothing twice
  (every employee is skipped as "already assigned").
* **Item update** (`PUT`): only while the scorecard is Planning (`409` after), the period Draft or Active (`409`), dates within the
  period, the rule re-validated with the new target (`400` on `target`), the weights of the scorecard at most 100% (`400` on `weight`).
  The template snapshot fields are not editable. `sortOrder` keeps its value when left out. **Item delete** and **scorecard delete**:
  only in Planning (`409` otherwise) and in any period state, so an abandoned plan can always be cleaned up; a scorecard with review
  actions cannot be deleted (`409`); its items go with it.
* **Finalize the plan** (`POST .../finalize-plan`): the scorecard must be in Planning, the period Draft or Active (`409`), and the item
  weights must satisfy `KpiCalculator.ValidateWeights`: at least one KPI and a total of exactly 100% (`400` on `weights`, for example
  "The KPI weights must add up to 100%. They add up to 75.00%."). It sets `reviewerEmployeeId` to the employee's **current** manager,
  `planFinalizedOn` to today and the status to Draft, and appends a review action (`step` HR, `action` Finalize Plan, `Planning` to
  `Draft`, the actor's account, linked employee (none for the built-in administrator) and display name, the time). **An employee without
  a manager gets no reviewer**: only reviewers of the HR tier can review that scorecard later. A manager equal to the employee (a data
  error) is refused with `409`.
* **Concurrency.** Every change to a scorecard or item needs its `rowVersion` (`400` missing, `409` stale), checked again by the
  database. On top of that every plan write (assign, bulk, item update and delete, scorecard delete, finalize) runs in a transaction that
  holds the application lock `kpi-plan`, so a finalize never misses a KPI added at the same moment, two assignments cannot both pass the
  100% check, and two finalize calls cannot both succeed (the second is `409`). Waiting for the lock lasts at most 15 seconds, then `409`.
* **Database errors** that slip past the rules: `UQ_Scorecard_EmployeeId_PeriodId` and `UQ_ScorecardItem_ScorecardId_TemplateId` (a
  race) are `409`, as is `CK_Scorecard_NotOwnReviewer`; the other `CK_Scorecard_*` and `CK_ScorecardItem_*` are validation errors; a foreign
  key violation is `409`. `kpi.ReviewAction` is append-only: the API's database login has no UPDATE or DELETE on it (a test checks that
  the deployed schema carries the DENY).
* **Delete protection:** an employee that scorecards point at (as the scorecard's employee or its reviewer, or as the actor of a review
  step) and a company, branch, department or position that scorecards copied cannot be deleted (`409` with the counts); branches,
  departments and positions also cannot move under another parent while they have scorecards (the composite foreign keys forbid it).

## HTTP contract (KPI submission and review)

The employee enters results and submits a scorecard; the employee's manager (the reviewer) or HR reviews it. Every write body carries the
scorecard's `rowVersion`; the status changes only through the commands below and every command answers `200` with the
`ScorecardDetailDto` (which also carries `reviewedOn, periodStatus, submissionDueDate, requiresHrReview, employeeComment, managerRating,
managerComment, hrComment`). One row version guards the whole scorecard: saving results or a review draft also moves it, so two
concurrent writes on the same version give one `200` and one `409`.

```
Draft --submit--> Submitted --start-review--> Under Review --approve--> Approved --finalize (HR)--> Finalized
Submitted / Resubmitted / Under Review --approve, period without HR review--> Finalized (at once)
Submitted / Resubmitted / Under Review / Approved --return (reason)--> Returned --submit--> Resubmitted (then as Submitted)
Submitted / Resubmitted / Under Review / Approved --reject (reason)--> Rejected   (final)
```

| Route | Policy | Body | Notes |
| --- | --- | --- | --- |
| `GET /api/kpi/my-scorecards` | `kpi:View` | filters, `sort`, paging | the caller's own scorecards except Planning, newest period first; no linked employee: empty page |
| `PUT /api/kpi/scorecards/{id}/results` | `kpi:Create` | `{ rowVersion, employeeComment?, items: [{ id, actualValue?, employeeComment? }] }` | own employee only; Draft or Returned; period Active; saves a draft, no scorecard score |
| `POST /api/kpi/scorecards/{id}/submit` | `kpi:Create` | `{ rowVersion }` | own employee only; Draft to Submitted, Returned to Resubmitted; period Active; every KPI needs a result |
| `POST /api/kpi/scorecards/{id}/start-review` | `kpi:Approve` | `{ rowVersion }` | Submitted or Resubmitted to Under Review |
| `PUT /api/kpi/scorecards/{id}/review` | `kpi:Approve` | `{ rowVersion, managerRating?, managerComment?, items: [{ id, managerRating?, managerComment? }] }` | Submitted, Resubmitted or Under Review; ratings 1 to 5, at most 2 decimals; no status change, no history |
| `POST /api/kpi/scorecards/{id}/approve` | `kpi:Approve` | `{ rowVersion, comment? }` | manager stage; score recalculated on the server; period `requiresHrReview`: Approved, else Finalized |
| `POST /api/kpi/scorecards/{id}/return` and `/reject` | `kpi:Approve` | `{ rowVersion, comment }` | comment required; manager stage (history step `Manager`) or Approved (step `HR`) |
| `POST /api/kpi/scorecards/{id}/finalize` | `kpi:Approve`, HR tier | `{ rowVersion, comment? }` | Approved to Finalized; the comment is the HR comment |
| `GET /api/kpi/reviews/pending` | `kpi:Approve` | filters `periodId, departmentId, status`, `search`, paging | oldest submission first |

History: each command appends one `ReviewActionDto` (`step` Employee, Manager or HR; `action` `Submit`, `Resubmit`, `Start Review`,
`Approve`, `Return`, `Reject`, `Finalize`) with the actor, the comment and the overall score at that time. Status texts: `Draft`,
`Submitted`, `Under Review`, `Returned`, `Resubmitted`, `Approved`, `Rejected`, `Finalized`.

* **Who may act.** Results and submit: only the scorecard's own employee (`ICurrentUser.EmployeeId`); someone else who can see the row
  gets `403` "You can only enter results on your own scorecard." (submit: "You can only submit your own scorecard."), outside their
  scope `404`. Nobody acts as reviewer on their own scorecard, whatever their grants: `409` "You cannot review your own scorecard.".
  Manager stage: the scorecard's `reviewerEmployeeId` or the HR tier (`kpi:Approve` with `employees:Edit`, which includes the built-in
  administrator); anyone else `403` "Only the employee's reviewer or HR can review this scorecard." (`404` when outside their scope). A
  scorecard with no reviewer can only be reviewed by the HR tier. The reviewer is fixed when the plan is finalized: a manager who stops
  being the employee's manager still reviews it, and the new manager sees it (direct report) but cannot review it.
* **HR stage** (Approved: finalize, return, reject): HR tier only (`403` "Only HR can give the final review of a scorecard."), and not the
  user who recorded the manager-stage approval (`409` "The person who approved this scorecard cannot also give the HR final review.").
* **Period.** Results and submit need an Active period; review actions an Active or Completed one; a Draft or Closed period is `409`
  for every write. A submission after the period's `submissionDueDate` is allowed; compare `submittedOn` with the due date to flag it.
* **Validation.** Items must belong to the scorecard and appear once (`400` on `items[i].id`); an invalid result is `400` on
  `items[i].actualValue` (Boolean must be 0 or 1, a rating within its scale, no negative when lower is better, at most 4 decimals);
  comments at most 1000 characters; a missing result on submit is `400` on `items[i].actualValue` for each KPI (index in sort order). A
  blank return or reject reason is `400` on `comment` "A reason is required.". A wrong status is `409` naming the current status; a stale
  `rowVersion` is `409`.
* **Scoring.** Only `KpiCalculator` scores: at submit and again at approve (from the stored results), with `calculationVersion` 1 and
  the label of the active rating band the score reaches. Return clears the scorecard's score (the history entry keeps it); the earlier
  figures of an item stay in the temporal `kpi.ScorecardItemHistory`.
* **Return clears the review draft.** Returning (from the manager stage or from Approved) also clears the scorecard's `managerRating`,
  `managerComment` and `hrComment` and every item's `managerRating` and `managerComment`: a returned scorecard keeps no review draft, and
  the reviewer reviews the resubmitted results afresh. The reason is the comment of the `Return` history entry, which is where the screen
  reads it.
* **What the employee reads of the review (review visibility).** One rule (`ReviewVisibility`), applied wherever review fields are
  answered: the scorecard detail of `GET /api/kpi/scorecards/{id}` and of every write that answers with it (including the employee's own
  `PUT .../results` and `POST .../submit`), the item answer of `PUT /api/kpi/scorecard-items/{id}`, and the `feedback` of the employee
  dashboard. It applies only when the caller's linked employee **is the scorecard's employee**; anyone else (the reviewer, a manager, HR,
  the built-in administrator) reads everything their scope allows. For the owner, a hidden field is `null`, exactly like an empty one:

  | Field | Visible to the scorecard's own employee when the status is |
  | --- | --- |
  | `managerRating`, `managerComment` (scorecard and every `items[]` entry; dashboard `feedback`) | Finalized or Rejected (not Approved: HR can still return it) |
  | `hrComment` | Finalized |
  | `history[].comment` of `Return` and `Reject` | always (the employee needs the reason) |
  | `history[].comment` of every other entry (the manager's `Approve`, the HR `Finalize`) | Finalized or Rejected |

  The history entries themselves (step, action, statuses, actor, score, time) are never hidden. Nothing stored changes: it is a rule of
  the answer, not of the data. The Own tier never sees a Planning scorecard at all (see "Scoping of reads").
* **Review queue.** A manager sees manager-stage scorecards (Submitted, Resubmitted, Under Review) naming them reviewer; the HR tier sees
  every manager-stage scorecard and every Approved one except those it approved itself; never the caller's own scorecard.
* **Activity** entries (category `performance`) name the employee code and the period only, never a comment or a result. Because anyone
  with `activity:View` reads the feed for the whole company, a review decision is never told apart: approve, return, reject and the HR
  final review all read `Scorecard of EMP-0002 for "2026 Q4" was reviewed`, and a resubmission reads `... submitted` like the first
  submission. "Review of the scorecard ... started", "Review notes saved ..." and "KPI plan ... finalized" reveal no judgement and stay.

## HTTP contract (KPI evidence attachments)

The employee attaches evidence (a PDF, an image, a Word or Excel file) to a KPI of their own scorecard. The database keeps metadata only
(`kpi.Attachment`); the bytes live in the evidence folder (`Kpi:EvidenceFolder`, see Configuration) under a server-generated key. Every
scorecard item in `ScorecardDetailDto.items[]` now carries `attachments: [{ id, fileName, contentType, sizeBytes, uploadedOn }]` (at most 5,
oldest first, `[]` when none; filled in the scorecard detail only, loaded in one query for all items). The storage key is never exposed.

| Route | Policy | Body | Answer |
| --- | --- | --- | --- |
| `POST /api/kpi/scorecard-items/{itemId}/attachments` | `kpi:Create`, the scorecard's own employee | `multipart/form-data`: text field `rowVersion` (the **scorecard's**) and exactly one file field named `file` | `201`, `Location: /api/kpi/attachments/{id}`, `{ attachment: { id, fileName, contentType, sizeBytes, uploadedOn }, scorecardRowVersion }` |
| `GET /api/kpi/attachments/{id}` | `kpi:View`, whoever can see the scorecard | none | `200` the file (see headers below) |
| `DELETE /api/kpi/attachments/{id}` | `kpi:Create`, the scorecard's own employee | JSON `{ "rowVersion": "..." }` (the scorecard's) | `200` `{ scorecardRowVersion }` |

Both writes use the `X-XSRF-TOKEN` antiforgery header like every unsafe method; an upload without it is refused (`400`) before its body is
read. Adding or removing a file changes the scorecard's row version (one token guards the whole scorecard): use the `scorecardRowVersion`
of the answer for the next write, or reload the scorecard. Neither answer carries the scorecard detail (so no review field).

* **Who and when.** Only the scorecard's own employee adds or removes evidence (someone else who can see the scorecard, a reviewer or HR
  included, gets `403` "You can only add or remove evidence on your own scorecard."; outside their scope `404`). The scorecard must be Draft
  or Returned (`409` otherwise) and its period Active (`409`), and the `rowVersion` current (`409`; missing or malformed: `400` on
  `rowVersion`). The employee, the scorecard's reviewer and manager, and HR download (the scorecard scope of `GET /api/kpi/scorecards/{id}`:
  anyone outside it gets `404`, also for an attachment id that does not exist). Evidence does not change the score and is not a workflow step.
* **Checked before the body is read.** The upload route first checks what its item id alone decides: the item exists and its scorecard is
  in the caller's scope (`404`), the caller is its employee (`403`), the scorecard is Draft or Returned and its period Active (`409`). Only
  then is the multipart body read, so a refused upload never buffers a file. The upload checks everything again (with the row version)
  before it stores anything.
* **One 404 per route resource.** A missing record and a record outside the caller's scope answer the same message, naming only the route's
  own resource: "KPI item #{itemId} was not found." on the upload, "Attachment #{id} was not found." on the download and the removal. The
  scorecard behind the item or attachment is never named.
* **Rate limit.** At most 10 uploads per minute per signed-in account (`Kpi:EvidenceUploadsPerMinute`; fixed window, no queue). Every
  attempt counts, whatever its answer. The next one is `429` ProblemDetails ("Too many evidence uploads. Wait a minute and try again.") with
  `Retry-After`. Downloads and removals are not limited. The counters are in memory per API instance (see `docs/DEPLOYMENT.md` on several
  instances). The rate limiter runs after authentication so that it can partition by account.
* **Limits.** At most 5 files per KPI (`409` "A KPI can have at most 5 evidence files. Remove one before adding another."). A file is 1 byte
  to 10 MB (10,485,760 bytes; `400` on `file`, "The file is empty." and "The file is larger than 10 MB."). The whole request may be at most 11 MB
  (the file limit plus multipart overhead): a request that declares more, or streams more, is cut off with `413` before it is buffered. The
  file part is read straight into one pooled working buffer (cleared after use) and copied once into an array of its exact size, so an
  upload costs about twice the file size in memory at its peak, never a growing stream.
* **Allowed types, decided by content.** PDF (`%PDF-`), PNG, JPEG, and Office Open XML (a ZIP that is really a Word document, with
  `[Content_Types].xml` and `word/document.xml`, or an Excel workbook, with `xl/workbook.xml`) only as `.docx` or `.xlsx`. The extension must
  agree with the content (`.pdf`, `.png`, `.jpg` or `.jpeg`, `.docx`, `.xlsx`; any case). The client's `Content-Type` is ignored: the stored
  and served content type is the detected one (`application/pdf`, `image/png`, `image/jpeg`, the Word or Excel OOXML type). Anything else (an
  executable, HTML, SVG, any other ZIP, a mismatched extension) is `400` on `file`: "Allowed evidence files are PDF, PNG, JPEG, Word (.docx)
  and Excel (.xlsx), up to 10 MB." Before a ZIP is opened, its end records must describe one archive on one disk with 1 to 10,000 entries
  (also through a ZIP64 record) and a central directory that ends where the end records begin; whatever the ZIP reader throws on corrupt
  content is the same `400`, never a `500` (a test feeds it truncated, mutated, spanned, ZIP64 and empty-directory archives).
* **File name.** Only the last segment after `/` or `\` is kept; control characters, bidirectional overrides, line separators and
  `< > : " | ? *` are removed; the name is trimmed and at most 260 characters (the extension is kept). A name with nothing left becomes
  `evidence.<ext>`. The name is shown and used as the download name only: it is never part of a storage path, and it is never written to a log
  or to the activity feed.
* **Download.** `Content-Type` is the stored type, `Content-Disposition: attachment` with an RFC 6266 / 5987 encoded `filename` (never
  `inline`), `X-Content-Type-Options: nosniff` and `Cache-Control: no-store` (the global headers). A record whose file is missing from the
  folder is `404` and a warning with the attachment id only.
* **Write order and failures.** Upload: the file is stored first under a new key (`Guid.NewGuid().ToString("N")`, 32 lower-case hex
  characters, as the `CK_Attachment_StorageKey` check requires), then the row is saved together with the scorecard's new row version and
  the activity line; if that save fails the stored file is deleted again. Delete: the row goes first (with the new row version), then the
  file; a file that cannot be deleted is logged by attachment id and does not fail the request. A key read from the database is checked
  against `^[0-9a-f]{32}$` before any path is built. Because the database cascades a deleted item to its attachments, a file can only be
  orphaned by direct database work, never by the API (items are only deletable in Planning, where no evidence exists).
* **Activity** (category `performance`): "Evidence added to the scorecard of EMP-0002 (Q4 2026)" and "Evidence removed from ...". Never the
  file name.
* **Errors.** `400` `file` (type, size, empty, not multipart, not exactly one `file` part), `400` `rowVersion`, `403` (not the owner), `404`
  (item or attachment missing or not visible), `409` (stale `rowVersion`, status, period, 5 files per KPI), `413` (request too large), `429`
  (upload rate limit).

**Security note: there is no virus or malware scanning.** The API checks type, size and name and never executes or renders a file (every
download is an attachment, with `nosniff`), but it cannot tell whether a PDF or an Office file carries malicious content. A production
deployment should add, before going live: a malware scan of every upload (an antivirus or a scanning service, with the file held back until it
is clean), encryption at rest for the evidence folder (disk or volume encryption) and for its backups, an access-controlled and monitored
folder or an object store with private buckets, the removal of macros and active content from Office files (or a rejection of macro formats,
which the allowed list already excludes), an agreed retention and deletion policy for evidence of employees who leave, and a reverse proxy
request-size limit equal to the one above. Treat the downloads as untrusted content on the user's machine.

## HTTP contract (KPI dashboards, reports and export)

Read-only figures over the scorecards (phase G1). Nothing here writes. Every query starts from the caller's **scorecard scope**
(`ScorecardScope`: Own, never in Planning; Team = own + direct reports + the scorecards naming the caller as reviewer; All = HR tier, `kpi:Approve` with
`employees:Edit`), so a figure can never include a scorecard the caller could not open. Aggregates (counts, sums, minimums, maximums,
groupings) run in SQL Server on untracked projections. Each dashboard is a fixed number of queries, whatever the number of scorecards
(employee at most 4, manager 3, HR 10). Only the employee code and name identify a person. The one exception to "no comments" is the
employee's own manager feedback on their dashboard, shown only once the decision is final (see "What the employee reads of the review").
PDF output is the browser's print of the screen: the API has no PDF library.

| Route | Policy | Query | Returns |
| --- | --- | --- | --- |
| `GET /api/kpi/dashboard/employee` | `kpi:View` | `periodId?` | `KpiEmployeeDashboardDto` |
| `GET /api/kpi/dashboard/manager` | `kpi:Approve` | `periodId?` | `KpiManagerDashboardDto` |
| `GET /api/kpi/dashboard/hr` | `kpi:Approve` + HR tier (else `403` "The HR dashboard needs HR rights.") | `periodId?, companyId?` | `KpiHrDashboardDto` |
| `GET /api/kpi/reports/summary` | `kpi:View` | report filters, `search`, `sort`, paging | `PagedResult<KpiPeriodSummaryDto>` |
| `GET /api/kpi/reports/employee/{employeeId}` | `kpi:View`; outside the scope `404` | report filters, `sort`, paging | `KpiEmployeeReportDto` |
| `GET /api/kpi/reports/department/{departmentId}` | `kpi:View`; unknown department, or one the caller may not read about, `404` | report filters, `search`, `sort`, paging | `KpiDepartmentReportDto` |
| `GET /api/kpi/reports/ranking` | `kpi:View` | report filters, `search`, paging (`sort` only `rank:asc`) | `PagedResult<KpiRankingRowDto>` |
| `GET /api/kpi/reports/kpi-achievement` | `kpi:View` | report filters, `search`, `sort`, paging | `PagedResult<KpiAchievementRowDto>` |
| `GET /api/kpi/reports/target-vs-actual` | `kpi:View` | report filters, `search`, `sort`, paging | `PagedResult<KpiTargetVsActualRowDto>` |

**Report filters** (query string, all optional, combined with AND): `companyId, branchId, departmentId, positionId, employeeId` (the
placement **copied onto the scorecard**), `periodId`, `status` (a scorecard status text; unknown is `400` on `status`), `periodType`
(`Monthly|Quarterly|Half-Yearly|Yearly|Custom`, and `Annual` as another name for `Yearly`; unknown is `400` on `periodType`), `year` (the
year the period starts in, 1900 to 9998, else `400` on `year`), `categoryId` (scorecards with at least one KPI of the category; the item
reports also show only those KPIs). The Monthly, Quarterly and Annual reports are the same report with `periodType` (and `year`).
`search` (at most 100 characters, else `400` on `search`) matches the employee code or name. `sort=field:asc|desc` takes only the fields
listed per report (anything else is `400` on `sort`); the id always breaks ties. Paging is `pageIndex` and `pageSize` (1 to 200) as
everywhere. A route id (`employeeId`, `departmentId`) replaces the matching filter.

**Metric definitions** (one definition each, in `KpiFigures`):

* **Average score**: the mean `overallScore` of the **Finalized** scorecards of the set. Rejected scorecards, scorecards still in the
  workflow (Submitted, Approved and so on) and unscored ones never count. The database returns the sum and the count; the API divides and
  rounds to 2 decimals, away from zero. `null` when there is no Finalized scorecard (never 0). Minimum and maximum: of the same scores.
* **Completion rate** (`completionRatePct`): Finalized ÷ (every scorecard of the set that is past Planning) × 100, 2 decimals, away from
  zero. `0` when nothing is past Planning (no division by zero).
* **Ranking**: Finalized scorecards with a score, highest first. **Standard competition ranking**: equal scores share a rank and the next
  rank skips (90, 90, 80 is 1, 1, 3). Ties are listed by employee code, then scorecard id. A page that starts inside a tie keeps the shared
  rank (the first rank of a page is 1 + the number of higher scores in the whole set). Ranks are over the filtered set, so pass `periodId`
  for a ranking within one period.
* **Attention threshold** (HR dashboard): the `minScore` of the **lowest Active rating band whose `minScore` is at least 60** (taken as
  the "Meets Expectations" band), or **60** when there is no such band. A Finalized scorecard scoring **below** it requires attention.
  The threshold used is returned (`attentionThreshold`).
* **Default period** (when `periodId` is left out): the most recent **Active** period among the candidates (latest `startDate`, then
  the highest id), else the most recent candidate whatever its status, else none (an empty dashboard, `period: null`). Candidates:
  employee dashboard, the periods of the caller's own scorecards except Planning; manager dashboard, the periods of the manager's set;
  HR dashboard, every period that is not Draft (of `companyId` when given); department report, the periods of the visible scorecards of
  that department. An explicit `periodId` that does not exist is `404`.

**Employee dashboard** (`KpiEmployeeDashboardDto`): only the caller's own scorecards (their linked employee inside the scope); Planning
scorecards are not shown, as in My scorecards. A caller with no linked employee, or with no scorecard, gets an empty dashboard (`200`).

* `period`: `{ id, name, periodType, startDate, endDate, status, submissionDueDate }` or null.
* `scorecard`: `{ id, status, overallScore, ratingLabel, reviewerEmployeeId, reviewerName, submittedOn, finalizedOn }` or null.
* `items[]`: `{ id, kpiCode, kpiName, unit, target, actualValue, achievementPct, weight, weightedScore }` by sort order.
* `pending`: `{ isPending, dueDate, isOverdue }`. `isPending` is true when the scorecard is Draft or Returned and its period Active;
  `dueDate` is the period's `submissionDueDate`; `isOverdue` is pending and today (business time zone) after the due date.
* `feedback`: `{ managerRating, managerComment }` or null when there is no scorecard. Both are the stored values only when the scorecard
  is Finalized or Rejected, and `null` in every other status (the review visibility rule of "HTTP contract (KPI submission and review)").
* `history[]`: scorecards of periods that start before the current one, newest first, at most 12: `{ scorecardId, periodId,
  periodName, periodType, startDate, endDate, status, overallScore, ratingLabel }`.

**Manager dashboard** (`KpiManagerDashboardDto`): the scorecards of the period of the caller's **direct reports** and those naming the
caller as **reviewer**, never the caller's own (even an HR-tier caller sees only this set here). No linked employee: an empty set.

* `teamSize`: the scorecards of the set in the period (one per employee and period, so the employees with a scorecard).
* `byStatus[]`: `{ status, count }` for all nine statuses in workflow order, zeros included.
* `pendingReviews`: Submitted, Resubmitted or Under Review and naming the caller as reviewer.
* `approvedOrFinalized`: Approved plus Finalized. `averageScore`: Finalized only (above).
* `employees[]`: `{ scorecardId, employeeId, employeeCode, employeeName, departmentId, departmentName, status, overallScore,
  ratingLabel }`, highest score first, unscored last, then employee code.

**HR dashboard** (`KpiHrDashboardDto`): every scorecard of the period (of `companyId` when given). `period`, `companyId`, then:

* `employeesWithKpis`: scorecards with at least one KPI (one per employee). `totalAssignments`: the KPIs (scorecard items) of the period.
* `completedEvaluations`: Finalized. `pendingSubmissions`: Draft or Returned. `pendingApprovals`: Submitted, Resubmitted, Under Review
  or Approved.
* `averageScore`, `completionRatePct`: above. `byStatus[]`: as on the manager dashboard.
* `departments[]`: `{ departmentId, departmentName, scorecards, finalized, averageScore }` per department copied onto the scorecards,
  by name.
* `topPerformers[]`: Finalized, the 5 highest scores, ties by employee code (`KpiEmployeeScoreDto` as above).
* `attentionThreshold`, `requiringAttentionCount` (all of them) and `requiringAttention[]` (lowest score first, at most 50).
* `ratingDistribution[]`: `{ ratingLabel, count }` over the Finalized scorecards, by the band's `minScore` (highest first), labels of
  no current band after them; `ratingLabel` is null for a score below every band.

**Reports.**

* `summary`: one row per period that has a matching scorecard, `{ periodId, periodName, periodType, startDate, endDate, periodStatus,
  scorecards (all, Planning included), inPlanning, finalized, rejected, averageScore, minScore, maxScore, completionRatePct }`. Sort:
  `periodStart` (default, newest first), `periodName`, `periodType`. Three queries per page (the count, the periods, their figures).
* `employee/{employeeId}`: `{ employeeId, employeeCode, employeeName, finalized, averageScore, scorecards: PagedResult }` where each
  scorecard is `{ scorecardId, periodId, periodName, periodType, startDate, endDate, departmentId, departmentName, status, overallScore,
  ratingLabel, items[] }` and each item `{ id, kpiCode, kpiName, categoryName, unit, target, actualValue, achievementPct, weight,
  weightedScore }`. `finalized` and `averageScore` cover every matching scorecard, not just the page. The employee must be in the
  caller's scope: themselves, a direct report or anyone by tier, or the employee of a scorecard the caller reviews; anyone else, and an
  unknown id, is `404`. Sort: `periodStart` (default, newest first), `periodName`, `overallScore`, `status`.
* `department/{departmentId}`: `{ departmentId, departmentName, period, scorecards, finalized, averageScore, rows: PagedResult }` with
  rows `{ scorecardId, employeeId, employeeCode, employeeName, departmentId, departmentName, periodId, periodName, status, overallScore,
  ratingLabel }`. It uses the department **copied onto the scorecard** when it was created, so an employee who moved later still counts
  in the department they were in during the period. One period: `periodId` or the default period. Sort: `employeeCode` (default),
  `employeeName`, `overallScore`, `ratingLabel`, `status`. **Who may ask:** a caller with `organization:View`, the HR tier, or a caller
  who sees at least one scorecard copied onto that department (any period, through their scope). Anyone else gets the same `404`
  "Department #id was not found." as for an id that does not exist, so the route never reveals a department's name.
* `ranking`: rows `{ rank, scorecardId, employeeId, employeeCode, employeeName, departmentId, departmentName, periodId, periodName,
  overallScore, ratingLabel }`. The order is fixed (rank, then employee code); `sort` other than `rank:asc` is `400` on `sort`.
* `kpi-achievement`: one row per KPI template with an assigned KPI in the matching scorecards, `{ templateId, kpiCode, kpiName,
  categoryId, categoryName, assigned, counted, averageAchievementPct, minAchievementPct, maxAchievementPct }`. `assigned` counts every
  assigned KPI; `counted` and the figures cover the KPIs of Finalized scorecards that have an achievement. Code and name are the
  template's current ones. Sort: `kpiCode` (default), `kpiName`, `categoryName`, `assigned`, `counted`, `averageAchievement`.
* `target-vs-actual`: one row per KPI, every status (shown), `{ itemId, scorecardId, employeeId, employeeCode, employeeName,
  departmentId, departmentName, periodId, periodName, status, kpiCode, kpiName, categoryName, unit, target, actualValue, achievementPct,
  weight, weightedScore }`. Default order: newest period, then employee code, then KPI order. Sort: `periodStart`, `periodName`,
  `employeeCode`, `employeeName`, `departmentName`, `kpiCode`, `kpiName`, `categoryName`, `achievementPct`, `weightedScore`, `status`.

Rejected scorecards never move an average, a minimum, a maximum or a rank, but they appear in the row reports (`department`,
`employee`, `target-vs-actual`, the scorecard export) with their status.

**Excel export** (see "HTTP contract (Excel import and export)"): `POST /api/excel/exports/{module}` with `excel:Export` (the route)
**and** `kpi:View` (checked by name, as for every module); export only (`canImport` false, no template). The modules read the same
scope with the same filters as the reports (`filters` takes the report filter names; `search`; `dateFrom`/`dateTo` keep periods that
overlap the range; `scope: "selected"` takes scorecard ids, and ids outside the scope are left out). The usual row limit applies
(`Excel:MaxExportRows`, 50,000: a larger export is `400`). No module writes a comment, a manager rating or an evidence file.

| Module | Columns |
| --- | --- |
| `kpi-scorecards` | Employee Code, Employee Name, Department, Period, Status, KPIs, Total Weight, Overall Score, Rating, Reviewer, Plan Finalized On, Submitted On, Submission Due, Finalized On (newest period first, then employee code) |
| `kpi-ranking` | Rank, Employee Code, Employee Name, Department, Period, Overall Score, Rating (Finalized only; ranks over the exported rows, so for `selected` among the selected scorecards) |
| `kpi-target-vs-actual` | Employee Code, Employee Name, Department, Period, Status, KPI Code, KPI, Category, Unit, Target, Actual, Achievement %, Weight, Weighted Score |

**Indexes.** The queries are served by the indexes the SSDT project already has (`IX_Scorecard_PeriodId_Status` with its included
`EmployeeId, DepartmentId, OverallScore`; `IX_Scorecard_DepartmentId_PeriodId`; `IX_Scorecard_ReviewerEmployeeId_Status`;
`UQ_Scorecard_EmployeeId_PeriodId`; `UQ_ScorecardItem_ScorecardId_TemplateId`; `IX_ScorecardItem_CategoryId`). No index was added.

## HTTP contract (dashboard, activity and notifications)

| Route | Policy | Purpose |
| --- | --- | --- |
| `GET /api/dashboard/summary` | `dashboard:View` | The dashboard figures and chart series (shape of `DashboardSummary` in `dashboard.models.ts`). |
| `GET /api/activity/query?pageIndex&pageSize&sort&search&category&actor&dateFrom&dateTo` | `activity:View` | One page of the activity feed, newest first. |
| `GET /api/activity/{id}` | `activity:View` | One entry (`id` is a bigint). |
| `GET /api/notifications/query?pageIndex&pageSize&unreadOnly` | signed in | The caller's own notifications and the broadcasts, newest first. |
| `POST /api/notifications/{id}/read` | signed in | Marks one notification read; `200` with it. Another user's id is `404`. |
| `POST /api/notifications/read-all` | signed in | Marks every visible unread notification read; `204`. |

* The activity log is append-only: entries are written by the handlers of the change they describe (organization,
  employees, leave, attendance), in the same unit of work, and there is no route that creates, changes or deletes one.
  Activity sort keys: `timestamp` (default, descending), `category`, `actor`. `category` is one of `employee`,
  `organization`, `attendance`, `leave`, `payroll`, `recruitment`, `system`; `dateFrom` and `dateTo` are inclusive days in the
  business time zone. A bad filter is `400` with the field in `errors`.
* Activity messages use the wording of the frontend mock services with ids and codes; an employee is named by code, never by
  name, and no national id, bank detail or salary is ever written.
* `app.Notification` has one `IsRead` flag per row: reading a broadcast (no recipient) marks it read for everyone. A user can
  only reach their own rows and the broadcasts (every store query filters on the recipient, another user's id is `404`).
  Proposal for per-user read state of broadcasts (needs a schema change, not applied): a table
  `app.NotificationRead (NotificationId bigint NOT NULL, UserId int NOT NULL, ReadAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  PK (NotificationId, UserId), FK NotificationId -> app.Notification ON DELETE CASCADE, FK UserId -> sec.AppUser ON DELETE CASCADE)`
  with an index on `UserId`. Broadcasts would then be read when a row exists for the caller; `IsRead`/`ReadAtUtc` would stay
  for rows with a recipient. The API change is `EfNotificationStore` plus the two mark-read handlers; the response shape does not change.
* The dashboard recruitment and payroll figures are read with plain SQL from `rec.*` and `pay.PayrollRun` (recruitment has its own
  API, see below; there is no payroll module yet); the payroll totals are demonstration figures.
* The dashboard needs only `dashboard:View`, but its company-wide parts need their own grant, so an Employee-role caller gets none of them: `payroll` (the latest processed run and its totals) is `null` without `payroll:View`; `recruitment` (open jobs, candidates, interviews, funnel) is `null` without `recruitment:View`; `recentActivities` is `[]` without `activity:View`; birthday events are left out of `upcomingEvents` without `employees:ViewSensitive`. Headcount, attendance, pending leave and the department chart stay in the caller's row scope. The UI must tolerate a `null` payroll and recruitment, and an empty activity list.
## HTTP contract (recruitment module)

Policies (checked against the role's grants): `recruitment:View|Create|Edit|Delete` (the mock has no approval step, so there is
no `Approve`). Dates are `yyyy-MM-dd`, times `"HH:mm"`. Every update, delete and command needs `rowVersion` as described for
the organization module. Candidates, applications and interviews hold personal data and interviewers' opinions (score,
recommendation, feedback); the unpaged "get all" routes the frontend contract needs are kept for the four resources.

| Route | Policy | Notes |
| --- | --- | --- |
| `/api/job-openings` (get all, `/query`, `/{id}`, POST, PUT, DELETE) | View, Create, Edit, Delete | Fields: `code` (server, `JOB-yyyy-nnn` from `rec.JobOpeningCodeSeq`, year of `postedOn`, used codes skipped), `title, departmentId, hiringManagerId, employmentType (Full-time\|Part-time\|Contract\|Internship), vacancies (1-50), postedOn (optional on POST, today), closingDate, description, status`. Starts as `Draft`; the status never changes through PUT. A closed or filled opening cannot be edited (`409`); vacancies cannot drop below the selected count. Filters: `status, departmentId, hiringManagerId, employmentType`. |
| `POST /api/job-openings/{id}/open`, `/reopen` | Edit | Draft, On Hold or Closed to Open (the two verbs are the same transition). Needs `closingDate` today or later and a free vacancy (`409`). |
| `POST /api/job-openings/{id}/hold`, `/close` | Edit | Open to On Hold; Draft, Open or On Hold to Closed. `Filled` has no command: it follows the selected count. |
| `GET /api/job-openings/{id}/pipeline`, `GET /api/recruitment/pipeline?jobOpeningId` | View | `{ jobOpeningId, columns: [{ stage, count, cards: [{ applicationId, jobOpeningId, jobTitle, candidateId, candidateName, experienceYears, stage, appliedOn, rejectionReason, nextInterview: { id, round, date, startTime }\|null, completedInterviews, rowVersion }] }] }`; always the five stages in order, cards by candidate name. |
| `GET /api/recruitment/counts` | View | `{ jobOpenings: [{ jobOpeningId, applications, selected }], candidates: [{ candidateId, applications }] }`; ids without applications are not listed (count 0). |
| `/api/candidates` (get all, `/query`, `/{id}`, POST, PUT, DELETE) | View, Create, Edit, Delete | `firstName, lastName, email (stored lower-case, unique), phone, source (Career site\|Referral\|Job board\|LinkedIn\|Agency), currentEmployer, experienceYears (0-50, one decimal), resumeName, notes`. Filter: `source`. |
| `/api/applications` (get all, `/query`, `/{id}`, POST, PUT, DELETE) | View, Create, Edit, Delete | POST `{ jobOpeningId, candidateId, notes? }`: starts at `Applied`, dated today by the server, only for an `Open` opening, once per opening and candidate. PUT `{ jobOpeningId, candidateId, notes, rowVersion }` changes the notes only (another opening or candidate is `400`). Filters: `jobOpeningId, candidateId, stage`. |
| `POST /api/applications/{id}/stage` | Edit | Body `{ rowVersion, stage, reason?, blacklistOverrideReason? }`. See the stage rules below; `blacklistOverrideReason` only matters when selecting a candidate who matches the do-not-rehire list (see the employee module). |
| `/api/interviews` (get all, `/query`, `/{id}`, POST, PUT, DELETE) | View, Create, Edit, Delete | `applicationId, round (Phone screen\|Technical\|HR\|Final), date, startTime, durationMinutes (15-240), mode (Onsite\|Video\|Phone), interviewerId, location, status, score, recommendation, feedback`. POST also takes optional `status` (`Scheduled`, default, or `Completed` to record a past interview with `score`, `recommendation`, `feedback`). PUT reschedules (no status or outcome fields). Filters: `status, interviewerId, applicationId, dateFrom, dateTo`. |
| `POST /api/interviews/{id}/outcome` | Edit | Body `{ rowVersion, score (1-5), recommendation (Strong hire\|Hire\|No hire), feedback? }`; completes a scheduled interview, or corrects a completed one. |
| `POST /api/interviews/{id}/cancel` | Edit | Body `{ rowVersion, reason? }`; the reason is kept as the feedback. |

Rules (ported from the mock services):

* **Openings:** department must exist and the hiring manager must be a current employee (`400` on `departmentId` /
  `hiringManagerId`); closing date not before the posting date. Status table: Draft to Open or Closed, Open to On Hold or
  Closed, On Hold to Open or Closed, Closed to Open, Filled to nothing (`409`, "A draft job opening cannot be set to On Hold.").
  Becoming Open: closing date today or later ("The closing date has passed. Edit the opening before reopening it.") and
  selected count below the vacancies. When the selected count reaches the vacancies an Open or On Hold opening becomes
  Filled in the same save; when a selection drops out a Filled opening is Open again.
* **Stages:** Applied to Screening or Rejected; Screening to Interview or Rejected; Interview to Selected or Rejected; Selected
  to Rejected; Rejected to nothing (`409`). Rejecting needs a non-blank `reason` (`400` on `reason`, at most 200
  characters) and a rejection cancels the application's scheduled interviews ("Cancelled: the application was rejected.").
  Any other move needs an Open or On Hold opening (`409`); other stages store no reason. Selecting needs a completed
  interview recommending Hire or Strong hire and a free vacancy (`409`).
* **Interviews:** the application must be in Screening or Interview to schedule (`400` on `applicationId`); scheduling moves
  a Screening application to Interview; the date may not be in the past by the server's business clock (`400` on `date`;
  rescheduling only checks it when the date or time changes); a completed interview needs a score of 1 to 5 and a
  recommendation and may not be in the future (`400`); the interviewer must be a current employee (`400` on
  `interviewerId`) and cannot have another scheduled interview that overlaps on the same day (`400` on `startTime`,
  "The interviewer already has an interview at 10:00 on that day."); a cancelled interview is locked (`409`); a completed
  interview cannot be cancelled or deleted (`409`). The application of an interview cannot change.
* **Delete protection** (`409`, `"name" cannot be deleted because it is used by N application(s). Deactivate it instead.`):
  an opening or candidate with applications, an application with interviews (and a selected application: "Reject it
  first."), a department with openings, an employee who manages openings or conducts interviews.
* **Not ported or changed:** the mock let a form change an interview's application and status through one update; here the
  status and outcome move through commands and the application never changes. Editing a completed interview through PUT
  keeps its outcome. The audit trail and "actor name" snapshots need columns the `rec` tables do not have (see the schema
  note in the module report); the audit user columns are stamped as everywhere else.
* **Activity feed:** every recruitment change writes its entry in the same save, under the `recruitment` category, with the
  wording of the mock services. The subject is the opening (`JOB-2026-007 Backend Engineer`), the candidate and job
  (`Ada Test for Backend Engineer`: the candidate name is the only personal data, as in the mock) or the interview
  (`Technical · Ada Test for Backend Engineer`); no email, phone, salary or interview feedback. A refused change writes nothing.
  * Candidates, openings, applications, interviews: `Candidate "Ada Test" created`, `... updated`, `... deleted` (the label is
    `Candidate`, `Job opening`, `Application` or `Interview`).
  * Opening commands (open, reopen, hold, close): `Job opening "JOB-2026-007 Backend Engineer" set to Open` (or `On Hold`, `Closed`).
  * Stage move: `Ada Test for Backend Engineer moved to Screening`. When the move fills the opening, `Job opening "..." filled`
    is written first; when a selection drops out, `Job opening "..." reopened`.
  * Interview outcome and cancel are an update in the mock, so they log `Interview "..." updated`. The interviews cancelled by a
    rejection and the application moving to Interview when one is scheduled log nothing, as in the mock.

## HTTP contract (settings module: users, roles, permissions, general settings)

**Administrator only.** Every route below needs the policy of its verb for the `settings` module (`settings:View|Create|Edit|Delete`,
still a placeholder) **and a real check on top**: the caller must be an HR Administrator. The check
(`Authorization/AdministratorRequirement.cs`, policy `hr-administrator`) reads the signed-in account and its role from
`sec.AppUser` on every request, not from the cookie, so a demoted, deactivated or locked administrator is refused at once. The
built-in administrator (no employee) has the same role and passes the same check. Anyone else gets `403` (`401` when signed
out). The single exception is `GET /api/general-settings`, which any signed-in caller with `settings:View` may read.

**There is no sign-up.** Accounts exist only because an administrator created them (`POST /api/users`). The new user signs in
with the initial password or uses forgot-password (the reset-by-email flow).

| Route | Policy | Notes |
| --- | --- | --- |
| `GET /api/users/query`, `GET /api/users/{id}` | View | Paged only, no "get all" (the list holds emails). `UserDto`: `{ id, employeeId (null for the built-in administrator), displayName, email, roleId, status (Active\|Inactive\|Locked), lastLoginOn (UTC)\|null, rowVersion }`. `Locked` is also shown while the sign-in lockout after failed attempts lasts. Filters: `roleId, status`; search: name or email; sort: `id, displayName, email, roleId, status, lastLoginOn`. |
| `POST /api/users` | Create | Body `{ employeeId, roleId, status?, initialPassword }`; `201` with the `UserDto`. See the rules below. No credential is ever in a response. |
| `PUT /api/users/{id}` | Edit | Body `{ roleId, rowVersion }`: only the role changes (the employee never does; status moves are commands). |
| `POST /api/users/{id}/activate`, `/deactivate`, `/unlock` | Edit | Body `{ rowVersion }`; returns the `UserDto`. |
| `POST /api/users/{id}/password-reset` | Edit | No body (an empty JSON object is fine). Sends the same reset mail as forgot-password (same token, link and mail sender or dev mail sink); `202 Accepted` with no body: the administrator never sees the token or the link. `404` unknown id; `409` (with a message) when the account is not Active ("Activate the account before sending a password reset."), its role is inactive, its employee has left, or it has no email or sign-in; `503` (`code: "Unknown"`) when email is not configured, before anything else happens, or when the mail server refuses; `429` (`code: "Unknown"`, `Retry-After`) for a second request for the same user within a minute. It changes nothing about the account (password, lock state, sessions); sessions end when the user uses the link, because the reset changes the security stamp. Activity note: `Password reset email sent for user "<display name>"` (no address, no token). |
| `DELETE /api/users/{id}` | Delete | Body `{ rowVersion }`; `204`. Removes the account and its login. |
| `/api/roles` (get all, `/query`, `/{id}`, POST, PUT, DELETE) | View, Create, Edit, Delete | `RoleDto`: `{ id, name, description, isSystem, status, rowVersion }`. POST `{ name, description?, status? }` (default Active); PUT `{ name, description?, rowVersion }`. Filter: `status`. |
| `POST /api/roles/{id}/activate`, `/deactivate` | Edit | Body `{ rowVersion }`. |
| `GET /api/role-permissions` | View | The matrix: `{ modules: [{ key, label, actions }], roles: [{ id, roleId, grants: { moduleKey: [action] }, rowVersion }] }`. Modules and actions come from `sec.PermissionModule` and `sec.PermissionModuleAction` (the post-deployment script), so they include `ViewSensitive`, `ViewBank` and so on where defined. |
| `GET /api/role-permissions/{roleId}` | View | `{ id, roleId, grants, rowVersion }`; empty `grants` for a role that has none. |
| `PUT /api/role-permissions/{roleId}` | Edit | Body `{ grants: { moduleKey: [action] }, rowVersion }`; replaces all grants of the role in one transaction. `rowVersion` is the one of the role; the save moves it, so two administrators cannot overwrite each other (`409`). |
| `GET /api/general-settings` | View | `{ id (1), organizationName, contactEmail, timezone, dateFormat, currencyCode, fiscalYearStartMonth, emailNotifications, weeklyDigest, rowVersion }`. |
| `PUT /api/general-settings` | Edit + administrator | The same fields plus `rowVersion`. |

Rules (ported from the mock services, with the wording of the mock):

* **Create user:** the employee must exist and be current (no exit date) (`400` on `employeeId`, "Choose a current employee."); one
  user per employee ("This employee already has a user account."); the role must exist and be active (`400` on `roleId`,
  "Choose an active role."); `status` is `Active` (default) or `Inactive`, never `Locked`. The email is the employee's work email,
  lower-cased and unique (`400` on `employeeId` when another account has it). The display name is the employee's name.
* **Passwords:** `initialPassword` needs at least 12 characters (the one rule of `SecurityPolicy`; `400` on `initialPassword`). It
  is handed straight to the ASP.NET Core Identity password hasher and stored only as the salted hash in `auth.User`. It is not
  returned, logged, written to the activity feed or kept anywhere else; the activity entry reads `User "Mia Testwell" created`
  (category `system`, display name only: no email, no password). If saving the account fails after the login was created, the
  login is removed again. Reset tokens exist only inside the mail.
* **Edit user:** an administrator cannot change their own role, deactivate or delete their own account (`409`, "You cannot change
  your own role or deactivate your own account." / "You cannot delete your own account."); there must always be one active HR
  Administrator who can sign in (`409`, "There must always be one active HR Administrator.") when a demotion, deactivation or
  delete would remove the last one. A deactivated user (or one whose role is inactive) is refused at sign-in and an open session
  ends at its next request (`SessionValidator`). A locked account is `409` "This account is locked. Unlock it first." on
  activate; unlock clears the lockout and the failed-attempt count and turns a `Locked` status back to `Active`. Activating
  needs an active role.
* **Roles:** names are required, at most 60 characters, unique (`400`). A built-in role (`isSystem`) cannot be renamed (`400` on
  `name`, "A built-in role cannot be renamed."), deactivated ("A built-in role cannot be deactivated.") or deleted ("A built-in
  role cannot be deleted."), both `409`; its description can change. A role with active users cannot be deactivated ("2 active
  user(s) still have this role. Move them to another role first.") and a role any user holds cannot be deleted ("\"X\" cannot
  be deleted because it is used by 1 user(s). Deactivate it instead."), both `409`.
* **Permissions:** unknown module or an action the module does not have is `400`; a grant other than `View` needs `View` of the
  same module (`400` on the module key, "Leave: View is required to grant other permissions."; the database enforces it too).
  The HR Administrator always keeps every grant: a save that leaves out any of its grants is `409` ("HR Administrator always
  has full access and cannot be changed."). Saving is recorded as `Permissions of role "X" updated`.
* **General settings:** the field errors and messages of the mock (`organizationName` required, `contactEmail` valid, `timezone`
  and `dateFormat` from the supported lists (`Asia/Yangon, Asia/Bangkok, Asia/Singapore, Asia/Kolkata, Europe/London,
  America/New_York, UTC`; `yyyy-MM-dd, dd/MM/yyyy, MM/dd/yyyy, dd MMM yyyy`), `currencyCode` three capital letters,
  `fiscalYearStartMonth` 1-12); the summary is "Please fix the highlighted settings.". The row is a singleton created by the
  post-deployment script: if it is missing the answer is `404`.
* An employee who has a user account cannot be deleted (`409`, "... used by 1 user account(s)").
## HTTP contract (Excel import and export)

Ported from the frontend mock (`ui/src/app/mock-services/excel`). Only the **employees** module exists there; the other
modules below were added on the API side by the same conventions (business codes for references, a `Status` column, the
shape rules and messages of `excel-coercion`) because the mock has no definitions to port. Modules still without columns
(`leave-types`, `leave-policies`, `shifts`, `holidays`) are listed as unavailable with the reason. ClosedXML (MIT) reads and
writes the workbooks. Every route needs `excel:View`, `excel:Import` or `excel:Export` (placeholder policies, like the module
policies), and the module's own policies on top, checked by name (employees import: `employees:Create`, `Edit`,
`EditSensitive`, `EditBank`; export: `employees:View`, plus `ViewSensitive` and `ViewBank` when `includeSensitive` is true).

| Module code | Import | Export | Policies (import / export) | Match key |
| --- | --- | --- | --- | --- |
| `companies`, `branches`, `departments`, `sections`, `positions`, `job-grades` | yes | yes | `organization:Create` + `Edit` / `organization:View` | `code` (Company Code, Branch Code ...); parents by code (`companyCode`, `branchCode`, `departmentCode`, `jobGradeCode`, `reportsToCode`); optional `Status` Active or Inactive |
| `leave-applications`, `leave-balances` | no | yes | none / `leave:View` | export only: balances are worked out (nothing stored to import) and an import would bypass the submit/approve workflow |
| `attendance-records` | no | yes | none / `attendance:View` | export only: records come from punches and approved adjustments |
| `shift-assignments` | yes | yes | `attendance:Edit` + `Approve` + `employees:Edit` / `attendance:View` | `employeeCode`; a row sets `shiftCode` through the employee update handler; an employee who has a shift counts as an existing assignment |
| `salary-assignments` | yes | yes | `payroll:View` + `Create` + `Edit` + `Approve` / `payroll:View` + `Approve` | `employeeCode` + `effectiveFrom`; `structureCode`, `basicSalary` (masked in previews and stored issues), `remarks` |
| `kpi-scorecards`, `kpi-ranking`, `kpi-target-vs-actual` | no | yes | none / `kpi:View` | export only: scorecards change only through the planning and review workflow. Same scope, filters and search as the KPI reports (see "HTTP contract (KPI dashboards, reports and export)"); never a comment, a manager rating or an evidence file |

* **Catalog.** `GET /api/excel/modules` lists every module; `canImport` and `canExport` are true only when the caller holds
  all of that module's policies above. Employees follow the same rule.
* **Rows are applied by the module's own handlers** (create, update, activate/deactivate, employee update, salary
  assignment create/update), so uniqueness, parent/child rules, reference protection, the job grade band, the joining date and
  one assignment per employee and start date are the API's, with the API's messages. A blank optional cell keeps the current value.
* **Exports read through the list handlers** (or the employee list for balances and shift assignments), so the caller's tier
  applies: leave and attendance export own rows, the team's, or everyone's (leave/attendance `Approve` and `employees:Edit` is
  everyone; `Approve` alone is the team); salary assignments follow the salary scope. Filters, search, `selectedIds`, `dateFrom` and
  `dateTo` (leave dates, attendance date, salary effective date) work like the list. `leave-balances` takes the filter `year`.
* **Salary data.** The `Basic Salary` import column is sensitive (masked in previews and in stored issues) and the template
  layout export therefore needs `includeSensitive`; the report export column is not sensitive (the export already needs
  `payroll:Approve`). The activity feed and history record counts only, never amounts. Figures are demonstration figures.

| Route | Policy | Body | Returns |
| --- | --- | --- | --- |
| `GET /api/excel/modules` | `excel:View` | none | `{ modules: ExcelModuleInfo[], unavailable: [{ code, label, canImport, canExport, reason }] }` |
| `GET /api/excel/templates/{module}` | `excel:Import` | none | `.xlsx` (Instructions, data sheet with headers only, Field guide, code lists, hidden Lists) |
| `POST /api/excel/imports/{module}/inspect` | `excel:Import` | multipart, one file part | `FileInspection` (`uploadId`, sheet, headers, suggested `mapping`, `missingRequired`, `unknownHeaders`, `ambiguousHeaders`, `needsMapping`) |
| `POST /api/excel/imports/{module}/validate` | `excel:Import` | `{ uploadId, strategy: InsertOnly\|UpdateOnly\|InsertOrUpdate, mapping? }` | `ImportPreview` with a `batchId`, per row `rowNumber`, `status`, `action`, `issues` |
| `POST /api/excel/imports/{batchId}/execute` | `excel:Import` | `{ mode: AllOrNothing\|Partial }` | `ImportResult` (`inserted`, `updated`, `skipped`, `failed`, `rolledBack`, `message`) |
| `GET /api/excel/imports/history?pageIndex&pageSize` | `excel:View` | none | `PagedResult<ImportBatchSummary>`, newest first |
| `GET /api/excel/imports/{batchId}` | `excel:View` | none | `ImportBatchDetail` (summary fields plus `issues` with `rowNumber`) |
| `GET /api/excel/imports/{batchId}/error-report` | `excel:View` | none | `.xlsx` "Validation report" |
| `POST /api/excel/exports/{module}` | `excel:Export` | `{ scope: Filtered\|All\|Selected, search?, filters?, selectedIds?, dateFrom?, dateTo?, columnKeys?, includeSensitive?, layout? }` | `.xlsx` |

* **Validation runs the real handlers.** `validate` applies every row through the create and update handlers of the API
  inside one transaction and then rolls the transaction back, so a row is valid only if the API would accept it, and the
  messages are the API's. `execute` does the same and commits: `AllOrNothing` rolls the whole batch back when one row fails,
  `Partial` keeps the rows that pass (a savepoint per row). The batch row, its issues and one activity entry are written
  afterwards, so a rolled back import still leaves its record.
* **Employees: what a row does.** Insert = create (status Active, Probation, On Leave or Suspended: the API refuses to create a
  Resigned or Terminated employee) plus the bank account. Update = employment update, personal update and bank update, each only
  when something differs (a re-import of an export is a no-op); a different `employmentStatus` runs the exit, reinstate or status
  command; blank optional cells keep the current value. Because it goes through the API rules, `Employment Status` Resigned or
  Terminated cannot be inserted by import (the mock allowed it).
* **Limits** (`Excel` configuration section): `MaxFileBytes` 5 MB, `MaxRows` 5000, `MaxSheets` 5 (the template's own guide and
  code sheets are not counted), `MaxExportRows` 50000, `UploadMinutes` 30. Only `.xlsx`: other extensions, a non-ZIP body, macros
  (`vbaProject.bin`), external links and oversized packages are refused with the mock's messages. Formulas are flagged, never
  evaluated.
* **Temporary storage.** `inspect` keeps the file under `Excel:StorageFolder` (default `App_Data/excel-uploads` under the content
  root) with an expiry; `validate` stores a copy under the batch id; `execute` deletes it; expired files are purged on the next
  inspect and read. Cell values are never logged. Sensitive columns are masked (`••••`) in previews and stored issues.
* **Export.** Sensitive columns are left out unless `includeSensitive`. `layout: "template"` writes the import columns (business
  codes) so the file can be imported again. Text that could run as a formula is neutralised. Always synchronous: an export over
  the row limit is a `400`; there is no `202` status URL.
* Errors use the usual problem shape: 400 Validation (`errors.file`, `errors.mapping` ...), 404 NotFound, 409 Conflict (a batch
  runs once; all-or-nothing with problems), 403 Forbidden when a module policy fails.

## Authentication (phase 1)

ASP.NET Core Identity (standard stores, no roles) with a same-site cookie. Credentials live in `auth.User`
(`HR` database, schema `auth`, owned by the SSDT project); the business account is `sec.AppUser`, linked through
`ExternalSubject` = `auth.User.Id`, and carries the role. Sign-in resolves the business account by that link and refuses
an Inactive or Locked account, an inactive role or an identity user without an account, with the same answer as a wrong
password.

### Endpoints (`/api/auth`)

| Route | Auth | Body | Returns |
| --- | --- | --- | --- |
| `GET /api/auth/antiforgery` | anonymous | none | `204`; sets the `XSRF-TOKEN` cookie (readable) and the hidden antiforgery cookie |
| `POST /api/auth/login` | anonymous, antiforgery, rate limited | `{ email, password, rememberMe }` | `200` `{ id, employeeId, displayName, email, roleName, permissions[] }` and the session cookie; failures: `400` blank fields, `401` generic, `429` rate limited |
| `POST /api/auth/logout` | signed in, antiforgery | none | `204`; deletes the session cookie |
| `GET /api/auth/me` | signed in | none | the same object as login, read live (`401` when not signed in) |
| `POST /api/auth/forgot-password` | anonymous, antiforgery, rate limited | `{ email }` | always `200` `{ message }` for any address; `503` when email is not configured (outside Development) |
| `POST /api/auth/reset-password` | anonymous, antiforgery, rate limited | `{ email, token, newPassword }` | `204`; `400` `errors.token` for a bad, used or expired token or unknown address (same answer), `400` `errors.newPassword` for a password under 12 characters |

* `permissions` are `module:Action` keys such as `employees:View` and `organization:Delete` (the same names as the
  endpoint policies). They are read from the database on every `me`/login, not stored in the cookie.
* Every failed sign-in is `401` `code: "Unauthorized"` with the same `detail`. The real reason (unknown account, wrong
  password, locked out, no linked account, inactive or locked account, inactive role) is logged with the identity user id
  but never the email, the password or a token.
* **Password rule:** at least 12 characters, nothing else. **Lockout:** 5 failed sign-ins lock the account for 15 minutes
  (the sixth attempt with the right password is refused too); a successful sign-in resets the count; a successful password
  reset lifts the lock. Email is unique.
* **Sessions:** `rememberMe: false` gives a browser-session cookie with a 30 minute sliding window and an 8 hour absolute
  maximum; `rememberMe: true` a persistent cookie with a 7 day sliding window and an absolute maximum of `Auth:RememberMeMaxDays`
  (30 days by default): sliding renewal never extends it, so the user signs in again at the end. On every request the session is also
  checked against the database: it ends when the account stops being Active (or its role inactive) and when the
  password is reset (security stamp). Logout deletes the cookie in the browser; a copied cookie stays valid until it
  expires or one of those checks fails (cookie sessions have no server-side list to revoke from).
* **Cookie:** outside Development `__Host-hrms`: HttpOnly, Secure, SameSite=Strict, Path=/, no Domain. In Development
  there is no `__Host-` prefix (it needs https) and the name comes from `Auth:DevelopmentCookieName`; Secure follows the
  request scheme. One code path (`Auth/AuthCookies`, `Auth/AuthenticationSetup`).
* **CORS** stays without `AllowCredentials`: the frontend reaches the API through the dev proxy on its own origin.
* **Data protection keys** are persisted to the directory `DataProtection:KeysPath` (default `App_Data/keys` under the content root,
  git-ignored), under the application name `Apex.Hrms`, so sessions, antiforgery tokens and reset tokens survive a restart and
  instances that share the directory share the keys. **The directory holds the secret that signs and encrypts sessions: in production
  put it on a protected path that only the API account can read and write (and back it up), never in the repository or a public
  folder.** Several instances must point at the same shared directory. Optionally the keys can be encrypted at rest with a
  certificate (`ProtectKeysWithCertificate`); that is not wired in here, so add it in `Hosting/HostingSetup` if the deployment needs it.
  A host that cannot write the directory fails when the first key is created.

### Antiforgery

Every unsafe method (POST, PUT, PATCH, DELETE) must send the header `X-XSRF-TOKEN` with the value of the `XSRF-TOKEN`
cookie (Angular `HttpClient` does this by default); otherwise `400` Validation. Call `GET /api/auth/antiforgery` before
the first sign-in or reset. The token is tied to the signed-in user, so login and logout both set a fresh `XSRF-TOKEN`
cookie in their response; use the new value afterwards. Enforced in `Auth/AntiforgeryMiddleware` after authentication.

### Deny by default

`AuthorizationSetup` sets a fallback policy that requires a signed-in user, so an endpoint without its own policy is
protected. A named policy replaces the fallback for its endpoint, so each permission policy (`organization:View` ...)
requires a signed-in user itself and then the permission (`PermissionRequirement`). Only the four anonymous
`/api/auth` endpoints and the OpenAPI document opt out with `AllowAnonymous`; a test pins that list. A 401 or 403 is a status
code, never a redirect.

### Audit attribution

`ICurrentUser` (`UserId` = `sec.AppUser.Id`, `EmployeeId`, `IsAuthenticated`, `IsSystem`, `DisplayName`) is backed by the
claims of the cookie (`hrms:uid`, `hrms:eid`, name). `AuditStampInterceptor` rejects an audited row that is added or
changed while `UserId` is null (`UnauthenticatedWriteException`, a `401` Unauthorized result from the handlers), unless
the caller is the explicit system identity (`services.UseSystemIdentity()`, used by the bootstrap command and deployment
seeds; never by the web host).

### Create the first administrator

```
dotnet run --project src/Apex.Hrms.Api -- bootstrap-admin --email admin@your-company.example [--name "Display name"]
```

A command line tool, never an HTTP endpoint (`Program.cs` runs it before the web host is built). It needs an interactive
terminal: it shows the target database, asks you to type `yes`, then asks for the password twice with no echo (piped
passwords are refused; there is no password argument, setting or file). It refuses when an active linked HR Administrator
already exists, and otherwise creates `auth.User` and `sec.AppUser` (no employee, role `HR Administrator`) in one
transaction. The database project must have been deployed first (the role comes from its post-deployment script). It uses
the connection string of the environment it runs in (`ASPNETCORE_ENVIRONMENT`, user secrets): check the printed target.
Exit codes: 0 created, 1 refused or cancelled, 2 usage.

### Password reset mail

* **Development:** `DevelopmentMailSink` writes each mail to `src/Apex.Hrms.Api/App_Data/dev-mail/*.eml` (git-ignored; the
  folder holds reset tokens, never share it). Nothing is sent and the log only says that a file was written.
* **Other environments:** `SmtpEmailSender` (`System.Net.Mail.SmtpClient`, TLS by default) with `Email:Smtp:*`. When
  `Host` or `FromAddress` is missing, forgot-password answers `503` before looking up any address, so mail is never silently
  dropped and nothing is revealed. A send failure after the lookup is logged (exception type only) and the answer stays the
  generic `200`.
* Tokens come from Identity's `GeneratePasswordResetTokenAsync`, work once and expire after 1 hour. Only an Active
  account receives a mail.

### Permission check

Every module policy (`employees:ViewBank`, `leave:Approve`, `excel:Export` ...) is a `PermissionRequirement(module, action)`
handled by `Authorization/PermissionHandler`, on every request:

1. The account is read live from the database (`IAccountDirectory.FindByIdAsync`, by the `hrms:uid` claim, never by a claim
   about the role). It must be Active, have an active role, and its employee must have no exit date.
2. The grants are the rows of `sec.RolePermission` for the role of that account, read through `IPermissionStore.GrantsAsync`
   and cached per role in the singleton `RoleGrantCache`. The account, its status and its role are never cached, so a
   deactivated user, an inactive role or a leaver is refused at once; only the grant sets are cached.
3. A cached grant set is valid only while the role's `RowVersion`, read in the account query that runs on every request, equals
   the one it was loaded under. Saving the grants always updates the role row in the same transaction (`ReplaceGrantsAsync`), so
   a change moves the row version and every API instance reloads at its next request: exact invalidation, no time window and
   no check-then-store race (the version is read before the grants, so a stale load can only be stored under an old version,
   which the next request replaces). The Settings handlers also drop the entry of the role they changed. `RoleGrantCache.Lifetime`
   (5 minutes) is only a safety net for grants edited directly in the database, which do not move the row version.
   The held grants are also handed to the request's `CallerPermissions`, which the row-level scopes read.
4. A policy that is not a module and action of `PermissionModule.All` is refused, and HR Administrator has no special path:
   it passes because the seed gives that role every action. The Excel endpoints ask the same policies by name for the
   module of the file (and the sensitive export policies), so they are enforced too.

The settings routes keep the extra `hr-administrator` requirement. `PermissionPolicyHttpTests` (403 without the grant, 204
with it, no database), `PermissionHandlerTests` and the integration `PermissionsHttpTests` cover it. The UI should hide what
the user lacks using the `permissions` of `GET /api/auth/me`; the server stays the authority.

### Leavers and administrators

* Recording an employee's exit (`ExitEmployeeHandler`, also used by the Excel import) switches off the linked account and
  changes the Identity security stamp in the same transaction, so open sessions end. Sign-in and the session check also refuse
  an account whose employee has an exit date.
* Creating a user (identity user plus account) and deleting one run in one database transaction on the shared context
  (`IUnitOfWork.BeginTransactionAsync`); nothing is repaired by hand after a failure.
* A demotion, deactivation, delete or exit that could remove an HR Administrator takes the application lock
  `hrms:administrator-changes` (`sp_getapplock`, owned by the transaction) before it checks the last-administrator rule, so two
  concurrent requests cannot both pass it. A request that cannot get the lock within 15 seconds is a `409`.
* Only administrators who can sign in count towards "one active HR Administrator": an account whose employee has an exit date does not.
  Deactivating, deleting or demoting (and the automatic deactivation on exit) rotates the security stamp, so an open session ends at once
  and cannot come back by reactivating the account.
* Activating the account of a leaver is a `409` ("This user's employee has left the company, so the account cannot be activated.").
* Recording an exit needs `employees:Edit`, but when the employee's account holds the HR Administrator role the caller must be an HR Administrator
  too (`403`, "Only an HR Administrator can record the exit of an HR Administrator."), and nobody records their own exit (`409`,
  "You cannot record your own exit.").
* A transaction that joins one the caller already holds (the Excel import) creates a savepoint and rolls back to it when disposed without a commit;
  the administrator application lock belongs to the outer transaction and is held until that ends. Verified by `JoinedTransactionTests`:
  a joined scope that commits keeps the lock until the outer transaction ends; a joined scope disposed without a commit gives it
  up, because SQL Server releases locks taken after a savepoint when it rolls back to it (nothing of that scope survives, so no
  rule check is left unprotected). The savepoint name is at most 32 characters (SQL Server limit): `uow_` plus 28 hex digits.

### Row-level scoping

Per-employee data is scoped inside the database predicate by `IAccessScope<T>`, from the caller's grants (`ICallerPermissions`) and team (`ICallerTeam`). Both are filled per request by `PermissionHandler` (the grants from `RoleGrantCache`, the team by one query through `ITeamDirectory`); before any permission check both are empty (own rows only, the safe side). The rules live in `Application/Security/SelfService`; the scopes are in `OwnRecordScopes`.

A caller's **team** is their own employee record plus their **direct reports**: the employees whose `hr.Employee.ManagerEmployeeId` is the caller's employee. Not the head of a department, not indirect reports.

| Data | All | Team (own record and direct reports) | Own record only |
| --- | --- | --- | --- |
| Leave applications, balances, calendar; attendance records, adjustments, overtime, attendance overview, work schedule (per module) | holds the module's `Approve` **and** `employees:Edit` (HR Officer, HR Administrator in the seed) | holds the module's `Approve` but not `employees:Edit` (Department Manager) | everyone else (Employee) |
| Employees (list, query, get, profile, personal, history, bank, salary summary reads, documents, the employees Excel export, the dashboard's employee figures) | holds `employees:Edit` **or** `payroll:View` | everyone else | not applicable: a caller with no linked employee sees nobody |
| Job openings, candidates, applications, interviews, the pipeline and counts | holds `recruitment:Edit` (Recruiter, HR Officer, HR Administrator) | not applicable (see below) | not applicable: a caller with only `recruitment:View` (Department Manager) sees the openings they are hiring manager of and what hangs on them; no linked employee means nothing |
| `GET /api/me/payslips` | not applicable | not applicable | every signed-in caller, always only their own employee's payslips of Approved or Paid runs |
| Payslips, salary assignments, the employee salary summary | holds `payroll:Approve` (Payroll Officer, HR Administrator) | not applicable | everyone else with the route's `payroll:View` (Auditor) |
| KPI scorecards (list, get, submission and review; module `kpi`) | holds `kpi:Approve` **and** `employees:Edit` (HR Administrator) | holds `kpi:Approve` but not `employees:Edit` (a manager): own, direct reports and the scorecards that name them reviewer | everyone else with `kpi:View` (Employee). The HR plan writes (`kpi-setup`) are not scoped |

Payroll runs (totals only) are not scoped. A caller with no linked employee (the built-in administrator) sees what their tier gives to everyone and nothing otherwise; the HR Administrator holds every grant, so it is in the "All" tier everywhere.

* An employee outside the caller's tier is `404` on get, update, delete and every command (cancel, submit, approve, reject ...), and absent from lists, the leave calendar, `/api/leave/balances?employeeId=`, the attendance overview, the work schedule and the dashboard's figures. A manager can therefore approve or reject only the requests of direct reports (a non-report's request is `404`).
* Nobody decides their own request: a manager's own leave, adjustment or overtime request is visible to them but approving or rejecting it is `409` Conflict ("You cannot decide your own request.", a domain rule that predates the tiers). Only an HR-tier approver with a different employee, or the built-in administrator (no employee), decides it.
* Creating or changing (leave PUT) a record for another employee is `403` Forbidden, like punches: leave applications, attendance adjustments and overtime. HR tier may file for anyone; the manager tier for their own record and direct reports ("You can only act for your own record and your direct reports."); the own tier for themselves ("You can only act for your own employee record.").
* Salary assignments: nobody creates, updates or deletes an assignment of their own employee record (`409` "You cannot change your own salary."), whatever their grants. A create runs the tier check first: a creator without `payroll:Approve` gets `404` for any employee but themselves, before any validation rule (band, start date, clash) can answer about that employee. Keep `payroll:Create` and `payroll:Edit` together with `payroll:Approve`, or the creator cannot see what they made.
* System code is not scoped: payroll processing, deletion checks, reference counts, hiring (which creates an employee) and the Excel import all read through `IRepository<T>` without a scope. Only handlers that inject `IAccessScope<T>` are scoped, and they run inside a request whose permission check filled the grants.
* Creating shifts and holidays needs `attendance:Edit` (not Create), because every employee holds `attendance:Create` to punch.
* Excel import batches (history, detail, error report, execute) belong to their uploader and to HR Administrators (`404` otherwise); detail and error report also run the module's view policies (`employees:View` for employees). The module check runs after the batch lookup, so an unknown batch answers `404` before it. The employees import needs `employees:Create`, `Edit`, `EditSensitive` and `EditBank`, so it always runs in the "All" tier.
* Recruitment is scoped, see "Recruitment scoping" below.
* The own-record rules read the employee id of the session cookie; the team is read from the live account. When the two differ (the account was linked to another employee after sign-in) every permission check is refused until the user signs in again. An account with no linked employee punches for another employee only in the "All" attendance tier. Payroll run warnings name an employee by code only.
* Seed note: the starter grants in the database project give Department Manager `employees:View` (team-scoped: themselves and direct reports), HR Officer `payroll:View,Approve` (every payslip; never approving a run they processed) (so HR Officer is in the all-payslips tier, as intended) and Employee `attendance:View,Create` (own punches and requests). Starter grants are written only into an empty roles table, so an existing database needs them added in Settings > Permissions.

### Recruitment scoping

"HR all, managers own", inside the database query (`IAccessScope<T>` for `JobOpening`, `Candidate`, `JobApplication`, `Interview`; rules in `Application/Security/RecruitmentScope.cs`). A record outside the caller's scope is `404` on read, update, delete and every command, and absent from lists, `/api/recruitment/pipeline` and `/api/recruitment/counts`.

| Caller | Sees |
| --- | --- |
| holds `recruitment:Edit` (Recruiter, HR Officer, HR Administrator in the seed) | every opening, candidate, application and interview |
| holds `recruitment:View` only (Department Manager) with a linked employee | the openings whose `HiringManagerEmployeeId` is their employee, the applications and interviews of those openings, the candidates who applied to those openings |
| no linked employee and no `recruitment:Edit` | nothing |

* The managed ids are read once per request by the permission check (`IRecruitmentDirectory`, only on `recruitment:*` policies and only for callers without Edit); until then the scopes see nothing, which is the safe side. A caller with `recruitment:Create` but not `Edit` can only apply to, or schedule interviews for, records inside their scope (`400` on `jobOpeningId` / `applicationId`).
* **Employee picker** `GET /api/employees/lookup?search&pageIndex&pageSize` (policy `employee-lookup`: `recruitment:Edit` **or** `employees:View`, checked by one requirement, `AnyPermissionRequirement`). Returns `PagedResult<{ id, employeeCode, displayName, jobTitle, departmentName }>` for current employees only (no exit date, not Resigned or Terminated), ordered by first name, `pageSize` at most 200 (default 20), `search` (at most 100 characters) matches code or name. It is deliberately not scoped and holds no personal data (no email, phone, dates, status, salary, national id).

### Authorization seams

* `MapCrud` and `MapCommand` take a `PermissionModule` (for example `organization`) and attach a named policy per
  verb: `organization:View` (GET), `organization:Create` (POST), `organization:Edit` (PUT and commands),
  `organization:Delete` (DELETE). The policies are registered in `Authorization/AuthorizationSetup.cs`; they require a
  signed-in user and the permission (see "Permission check"). New modules must be added to `PermissionModule.All` and to the
  seed of the database project (`sec.PermissionModuleAction`), or nobody can be granted them.
* `IAccessScope<TEntity>` (see "Row-level scoping") returns a predicate from the `ICurrentUser`; it is applied inside the database query by get all,
  query, get by id, update, delete and every command, so a record outside the scope is `404`. The default allows all.
  Register a closed `IAccessScope<Company>` etc. to narrow a resource.
* `Api.Tests/AuthorizationCoverageTests` fails when an `/api` endpoint (outside `/api/auth`) is mapped without a policy,
  and pins the policy of every employee route (including the sensitive and bank ones). `AuthEndpointSecurityTests` pins
  the anonymous endpoints and the fallback policy.
* The settings endpoints are the first with a real check: policy `hr-administrator` (`AdministratorRequirement`) is added on
  top of the module policy, so those endpoints carry two policies. `SettingsAccessTests` runs the 403/200 behaviour without a
  database and `AuthorizationCoverageTests` pins both policies of every settings route.
* A module that holds personal data declares extra permissions in `PermissionModule` (`employees` has `ViewSensitive`,
  `EditSensitive`, `ViewBank`, `EditBank`, `ViewBlacklist` and `EditBlacklist` next to the four standard ones; the last two have
  sort order 12 and 13 in the database seed).
* A route used by two kinds of caller names a requirement met by any one of several permissions (`AnyPermissionRequirement`,
  policy `employee-lookup`); it goes through the same live check as every permission.

## Adding a resource (pattern to copy)

1. Domain: entity with `Create`, `Validate`, `Update` and domain methods that return `Result` (no status in `Update`).
2. Application: `...Dto`, `...CreateRequest` / `...UpdateRequest`, validator, mapping, rules (cross-record checks run
   on normalized details), query specification; register with `AddCrudResource` (or the single `AddResource`, `AddCreate`,
   `AddUpdate`, `AddDelete`, `AddEntityCommand` pieces for a workflow resource) and declare references with `AddReference`.
3. Infrastructure: entity configuration (plain foreign keys; do not model composite or alternate keys over parent columns).
4. Api: `MapCrud(route, tag, module, operations)` and `MapCommand` / `MapStatusCommands`. A workflow resource (payroll
   run) passes `CrudOperations.Read | CrudOperations.Create` so no PUT or DELETE is exposed.

## Integration tests (SQL Server)

`tests/Apex.Hrms.IntegrationTests` runs the real stack (`AddApplication` + `AddInfrastructure`, and the API through
`WebApplicationFactory`) against SQL Server, because fakes cannot catch EF model, constraint or SQL translation bugs.

* **Database:** LocalDB `(localdb)\MSSQLLocalDB`, Integrated Security, throwaway database **`HR_IntegrationTests`**. The
  name is a constant in `Support/SqlServerEnvironment.cs`; the tests never read it from configuration and never touch
  the `HR` database.
* **Schema:** deployed once per run from the SSDT dacpac with `DacServices.Deploy` (`CreateNewDatabase = false`,
  `BlockOnPossibleDataLoss = true`, `DropObjectsNotInSource = false`). The default path is
  `D:\New folder\HR DBPJ\HR DBPJ\bin\Debug\HR DBPJ.dacpac`; set the environment variable `HRMS_DACPAC` to use another.
  Build the database project first if the dacpac is missing or out of date.
* **Isolation:** tests commit their rows and remove them again by their tag (`TST-` + 8 hex characters, see
  `Support/TestRows`). Organization rows carry the tag in their code; employee rows carry it at the start of the work
  email (an employee code holds only 12 characters). The cleanup deletes employees and the rows that hang on them
  (documents, history, bank account) first, then the organization tables child to parent. No `DROP`, `TRUNCATE` or
  unfiltered `DELETE`. The fixture fails the run, at the start and at the end, when a row with a test tag is left
  behind in the `org` tables, in `hr.Employee`, in `auth.User` / `sec.AppUser` (email prefix `TST-`) or in `sec.Role`
  (name prefix), or in the `kpi` tables (codes of categories and templates, names of periods and rating labels start with the
  tag; the `*History` tables of the temporal tables keep their rows, SQL Server owns them); delete those rows in
  `HR_IntegrationTests` (child tables first) and run again.
* **Signed-in tests:** audited writes need a signed-in user, so the fixture creates one `TST-` administrator per run
  (random password, plain SQL, removed at the end) and the HTTP clients sign in as that account through
  `/api/auth/login` (`tests/Shared/BrowserHandler.cs` keeps cookies and copies `XSRF-TOKEN` into `X-XSRF-TOKEN` like the
  frontend). Tests that call the handlers directly get a fake `ICurrentUser` with a `TST-` display name. The test host
  writes its mail to a temporary folder and is set through host settings (the API reads its settings while it registers
  services, before `ConfigureAppConfiguration` of a test host applies).
* **Skipping:** without LocalDB (for example Linux CI) or without the dacpac every test is reported as skipped with the
  reason; `dotnet test` stays green.
* **Run:** `dotnet test api/tests/Apex.Hrms.IntegrationTests`, or everything except them with
  `dotnet test api/Apex.Hrms.slnx --filter "Category!=Integration"`. The first run starts LocalDB (`sqllocaldb start
  MSSQLLocalDB`) and deploys the schema, which takes longer.
* **CI:** the `api` job in `.github/workflows/ci.yml` (ubuntu-latest, .NET 10) restores, builds with
  `-warnaserror` and runs `--filter "Category!=Integration"`, uploading the .trx results as an artifact. The integration
  tests are not run in CI: they need LocalDB and the dacpac from the separate database repository. Running them later
  would take a SQL Server service container, the dacpac published as an artifact from that repository, and a connection
  setting in `SqlServerEnvironment` (today LocalDB only).
