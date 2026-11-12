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
do what) are still placeholders until phase 3. Do not store real data until the permission checks exist.
Without a database you cannot sign in: create the first administrator with the `bootstrap-admin` command first.

## Configuration

| Key | Meaning |
| --- | --- |
| `ConnectionStrings:Hrms` | SQL Server connection string (required). |
| `Business:TimeZone` | Time zone that defines "today" for rules such as "opened date cannot be in the future". Default `Asia/Yangon`. An unknown value stops the API at startup. |
| `Business:WeekendDays` | Days of the week that are not working days, for leave day counts and the leave calendar: a list of day names, default `["Saturday", "Sunday"]`. An unknown name stops the API at startup. |
| `Cors:AllowedOrigins` | Origins the browser frontend may call from. |
| `AllowedHosts` | `;`-separated exact host names. `*` is only accepted in Development; any other environment refuses to start with a missing or wildcard value. |
| `Auth:DevelopmentCookieName` | Development only: session cookie name (default `hrms-dev`). Outside Development the name is always `__Host-hrms`. |
| `Auth:RateLimit:PermitsPerMinute` | Sign-in, forgot-password and reset-password requests allowed per client IP per minute (default 10). |
| `Auth:PasswordResetUrl` | Optional absolute URL of the frontend reset page; the reset mail links to `{url}?email=...&token=...`. Without it the mail shows the token. |
| `Email:DevelopmentFolder` | Development only: folder for the mail files (default `App_Data/dev-mail` under the API project). |
| `Email:Smtp:Host`, `Email:Smtp:FromAddress` | Required outside Development for password reset mail. |
| `Email:Smtp:Port` (587), `Email:Smtp:EnableSsl` (true), `Email:Smtp:FromName` | Optional. |
| `Email:Smtp:UserName`, `Email:Smtp:Password` | Optional pair (both or neither). **Environment variables or user secrets only**, never an appsettings file. |

Secrets: `dotnet user-secrets set "Email:Smtp:Password" "..." --project src/Apex.Hrms.Api` or the environment variable
`Email__Smtp__Password`. Configuration is read when the host starts: restart after changing it.

Outside Development the API also uses HTTPS redirection and HSTS. Every response carries
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
| `Forbidden` | 403 | The caller is not an HR Administrator on a settings endpoint (a real check). The other permission checks come in phase 3. |
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
* Documents (`hr.EmployeeDocument`) are mapped and modelled in the domain but have no endpoints yet.
* Organization deletes and moves count employees through the view `hr.EmployeeOrgLink` (placement without personal data),
  not through `hr.Employee`; the view and the sequence must exist in the target database.

## HTTP contract (leave module)

Three resources, two reads and four workflow commands. Policies: `leave:View`, `leave:Create`, `leave:Edit`,
`leave:Delete`, `leave:Approve` (placeholders until phase 3, like every module policy). Balances are never stored (the
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

Policies (placeholders until phase 3, like leave): `attendance:View|Create|Edit|Delete|Approve`. Times travel as `"HH:mm"`
(24 hours, to the minute; `"HH:mm:ss"` is also accepted on input); dates as `yyyy-MM-dd`. Every update, delete and command
needs `rowVersion` as described for the organization module.

| Route | Policy | Notes |
| --- | --- | --- |
| `/api/shifts` (get all, `/query`, `/{id}`, POST, PUT, DELETE, `/{id}/activate`, `/{id}/deactivate`) | View, Create, Edit, Delete, Edit | Like an organization resource. Fields: `code, name, startTime, endTime, breakMinutes, graceMinutes, workDays (0 = Sunday ... 6), isNightShift, color, status`. Delete is refused (`409`) while employees are assigned or attendance records used the shift. |
| `/api/holidays` (get all, `/query?dateFrom&dateTo&type`, `/{id}`, POST, PUT, DELETE) | View, Create, Edit, Delete | `{ date, name, type: Public\|Company }`. One holiday per date (`400` on `date`; the table has no unique index, so this is checked by the API only). |
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

Calculation per employee and period: part-month employment prorates basic and fixed components by calendar days; earnings are
fixed amounts or a percentage of the prorated basic; approved overtime adds `basic / (working days x 8) x 1.5 x hours`; gross is
basic plus earnings; deductions are fixed, a percentage of basic or of gross, plus unpaid leave `basic / working days` per day;
net is gross minus deductions; every amount is rounded to 2 decimals (halves away from zero). Working days come from the
configured weekend and the holidays. An employee without a salary assignment, or without a usable bank account (the payslip
table requires a masked account), is skipped with a warning.

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
* `app.Notification` has one `IsRead` flag per row: reading a broadcast (no recipient) marks it read for everyone.
* The dashboard recruitment and payroll figures are read with plain SQL from `rec.*` and `pay.PayrollRun` (recruitment has its own
  API, see below; there is no payroll module yet); the payroll totals are demonstration figures.
## HTTP contract (recruitment module)

Policies (placeholders until phase 3): `recruitment:View|Create|Edit|Delete` (the mock has no approval step, so there is
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
| `POST /api/applications/{id}/stage` | Edit | Body `{ rowVersion, stage, reason? }`. See the stage rules below. |
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
| `POST /api/users/{id}/password-reset` | Edit | No body. Sends the same reset mail as forgot-password; `200 { message }`. `503` (`code: "Unknown"`) when email is not configured, before anything else happens. Only for an active user with an active role (`409` otherwise). |
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

Ported from the frontend mock (`ui/src/app/mock-services/excel`). Only the **employees** module exists there, so it is the
only one in the catalog; the other modules are listed as unavailable with the reason. ClosedXML (MIT) reads and writes the
workbooks. Every route needs `excel:View`, `excel:Import` or `excel:Export` (placeholder policies, like the module policies),
and the module's own policies on top, checked by name (employees import: `employees:Create`, `Edit`, `EditSensitive`,
`EditBank`; export: `employees:View`, plus `ViewSensitive` and `ViewBank` when `includeSensitive` is true).

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
  maximum; `rememberMe: true` a persistent cookie with a 7 day sliding window. On every request the session is also
  checked against the database: it ends when the account stops being Active (or its role inactive) and when the
  password is reset (security stamp). Logout deletes the cookie in the browser; a copied cookie stays valid until it
  expires or one of those checks fails (cookie sessions have no server-side list to revoke from).
* **Cookie:** outside Development `__Host-hrms`: HttpOnly, Secure, SameSite=Strict, Path=/, no Domain. In Development
  there is no `__Host-` prefix (it needs https) and the name comes from `Auth:DevelopmentCookieName`; Secure follows the
  request scheme. One code path (`Auth/AuthCookies`, `Auth/AuthenticationSetup`).
* **CORS** stays without `AllowCredentials`: the frontend reaches the API through the dev proxy on its own origin.
* **Data protection keys are not persisted yet** (decision pending): cookies and reset tokens do not survive an API
  restart (with the framework default key ring; on a container or a shared host they may not even survive a recycle).
  Everyone has to sign in again after a restart.

### Antiforgery

Every unsafe method (POST, PUT, PATCH, DELETE) must send the header `X-XSRF-TOKEN` with the value of the `XSRF-TOKEN`
cookie (Angular `HttpClient` does this by default); otherwise `400` Validation. Call `GET /api/auth/antiforgery` before
the first sign-in or reset. The token is tied to the signed-in user, so login and logout both set a fresh `XSRF-TOKEN`
cookie in their response; use the new value afterwards. Enforced in `Auth/AntiforgeryMiddleware` after authentication.

### Deny by default

`AuthorizationSetup` sets a fallback policy that requires a signed-in user, so an endpoint without its own policy is
protected. A named policy replaces the fallback for its endpoint, so each placeholder policy (`organization:View` ...)
requires a signed-in user too; checking the permission itself is phase 3 (`TODO(Permissions)`). Only the four anonymous
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

### Authorization seams (permissions come in phase 3)

* `MapCrud` and `MapCommand` take a `PermissionModule` (for example `organization`) and attach a named policy per
  verb: `organization:View` (GET), `organization:Create` (POST), `organization:Edit` (PUT and commands),
  `organization:Delete` (DELETE). The policies are registered in `Authorization/AuthorizationSetup.cs`; they require a
  signed-in user and check nothing else yet. New modules must be added to `PermissionModule.All`.
* `IAccessScope<TEntity>` returns a predicate from the `ICurrentUser`; it is applied inside the database query by get all,
  query, get by id, update, delete and every command, so a record outside the scope is `404`. The default allows all.
  Register a closed `IAccessScope<Company>` etc. to narrow a resource.
* `Api.Tests/AuthorizationCoverageTests` fails when an `/api` endpoint (outside `/api/auth`) is mapped without a policy,
  and pins the policy of every employee route (including the sensitive and bank ones). `AuthEndpointSecurityTests` pins
  the anonymous endpoints and the fallback policy.
* The settings endpoints are the first with a real check: policy `hr-administrator` (`AdministratorRequirement`) is added on
  top of the module policy, so those endpoints carry two policies. `SettingsAccessTests` runs the 403/200 behaviour without a
  database and `AuthorizationCoverageTests` pins both policies of every settings route.
* A module that holds personal data declares extra permissions in `PermissionModule` (`employees` has `ViewSensitive`,
  `EditSensitive`, `ViewBank` and `EditBank` next to the four standard ones).

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
  (name prefix); delete those rows in `HR_IntegrationTests` (child tables first) and run again.
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
