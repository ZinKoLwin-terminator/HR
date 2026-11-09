# APEX HRMS API

.NET 10 Web API (Clean Architecture) for APEX HRMS. EF Core maps onto the existing SQL Server schema owned by the
SSDT project `HR DBPJ`; it never creates or migrates it.

| Project | Role |
| --- | --- |
| `Apex.Hrms.Domain` | Entities, domain rules, `Result` / `AppError`. No dependencies. |
| `Apex.Hrms.Application` | Use-case handlers, FluentValidation, DTOs, ports (`IRepository<>`, `IRecordCounter<>`, `IUnitOfWork`, `IClock`, `ICurrentUser`, `IAccessScope<>`). |
| `Apex.Hrms.Infrastructure` | EF Core 10 SQL Server mapping, repositories, clock, SQL error translation, placeholder current user. |
| `Apex.Hrms.Api` | Minimal API endpoints, authorization policies, ProblemDetails, CORS, OpenAPI. |

Run: put the connection string in `ConnectionStrings:Hrms` (`appsettings.Development.json` holds the LocalDB one,
`Server=(localdb)\MSSQLLocalDB;Database=HR;Integrated Security=True;TrustServerCertificate=True`; use user-secrets or
environment variables for anything with a password), then `dotnet run --project src/Apex.Hrms.Api`.
Build and test from the `api` folder: `dotnet build Apex.Hrms.slnx` and `dotnet test Apex.Hrms.slnx`. The tests never
open a database connection.

Auth is not implemented yet: endpoints are anonymous and `ICurrentUser` is a placeholder. Do not store real data.
See "Authorization seams" below for what is already in place.

## Configuration

| Key | Meaning |
| --- | --- |
| `ConnectionStrings:Hrms` | SQL Server connection string (required). |
| `Business:TimeZone` | Time zone that defines "today" for rules such as "opened date cannot be in the future". Default `Asia/Yangon`. An unknown value stops the API at startup. |
| `Cors:AllowedOrigins` | Origins the browser frontend may call from. |
| `AllowedHosts` | `;`-separated exact host names. `*` is only accepted in Development; any other environment refuses to start with a missing or wildcard value. |

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
| `Unauthorized` / `Forbidden` | 401 / 403 | Reserved for the Identity work. |
| `Unknown` | 500 | Unexpected failure; the message is generic. Quote `traceId` to find it in the server log. |

Database errors that slip past the rules are translated by constraint name, never by echoing the SQL message
(which can contain the duplicate value):

| SQL | Result |
| --- | --- |
| 2601 / 2627 unique violation `UQ_<Table>_Code`, `UQ_Company_Name` | `400` Validation on `code` / `name` |
| 547 CHECK `CK_<Table>_<Field>` | `400` Validation on that field |
| 547 FOREIGN KEY / REFERENCE | `409` Conflict |
| THROW 51001-51004 (paid payroll run is final) | `409` Conflict |

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
  Change` and `Exit` from the commands. `recordedBy` is the display name of the caller (`System` until Identity exists).
* **Field names:** `managerId` (database `ManagerEmployeeId`), `status` in the command bodies, nested `address` and
  `emergencyContact` objects in the personal data; validation errors on them are keyed `address.city`,
  `emergencyContact.relationship`, and so on.
* Documents (`hr.EmployeeDocument`) are mapped and modelled in the domain but have no endpoints yet.
* Organization deletes and moves count employees through the view `hr.EmployeeOrgLink` (placement without personal data),
  not through `hr.Employee`; the view and the sequence must exist in the target database.

## Authorization seams (Identity comes later)

No authentication exists yet, but the structure is in place so Identity (cookie, same-site) can be added in one place:

* `MapCrud` and `MapCommand` take a `PermissionModule` (for example `organization`) and attach a named policy per
  verb: `organization:View` (GET), `organization:Create` (POST), `organization:Edit` (PUT and commands),
  `organization:Delete` (DELETE). The policies are registered in `Authorization/AuthorizationSetup.cs` as permissive
  placeholders with a `TODO(Identity)`; new modules must be added to `PermissionModule.All`.
* `AuthorizationSetup.ConfigureFallbackPolicy` is the hook for "deny by default".
* `ICurrentUser` is registered with `TryAdd`: register the real implementation in the Api layer to replace
  `AnonymousCurrentUser`.
* `IAccessScope<TEntity>` returns a predicate from the `ICurrentUser`; it is applied inside the database query by get all,
  query, get by id, update, delete and every command, so a record outside the scope is `404`. The default allows all.
  Register a closed `IAccessScope<Company>` etc. to narrow a resource.
* `Api.Tests/AuthorizationCoverageTests` fails when an `/api` endpoint is mapped without a policy, and pins the policy of
  every employee route (including the sensitive and bank ones).
* A module that holds personal data declares extra permissions in `PermissionModule` (`employees` has `ViewSensitive`,
  `EditSensitive`, `ViewBank` and `EditBank` next to the four standard ones). They are placeholders too:
  `TODO(Identity)` replaces them with real permission checks.

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
  behind in the `org` tables or in `hr.Employee`; delete those rows in `HR_IntegrationTests` (child tables first)
  and run again.
* **Skipping:** without LocalDB (for example Linux CI) or without the dacpac every test is reported as skipped with the
  reason; `dotnet test` stays green.
* **Run:** `dotnet test api/tests/Apex.Hrms.IntegrationTests`, or everything except them with
  `dotnet test api/Apex.Hrms.slnx --filter "Category!=Integration"`. The first run starts LocalDB (`sqllocaldb start
  MSSQLLocalDB`) and deploys the schema, which takes longer.
