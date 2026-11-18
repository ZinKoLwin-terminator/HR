# Mock data and mock services

## Mock data (`ui/src/app/mock-data`)

All sample data is centralized here; no screen contains business data.

| Folder          | Contents                                                                                           |
| --------------- | -------------------------------------------------------------------------------------------------- |
| `organization/` | 2 companies, branches, 11 departments, sections, positions, job grades (with salary bands)         |
| `employees/`    | 60 fictional employees (one CEO with no manager), documents, employment history                    |
| `attendance/`   | Shifts, holidays, generated attendance records, overtime, adjustments, schedules                   |
| `leave/`        | Leave types, policies, applications                                                                |
| `payroll/`      | Salary components/structures/assignments, six paid payroll periods + one draft, generated payslips |
| `recruitment/`  | 8 job openings, 22 candidates, 22 applications, 14 interviews                                      |
| `settings/`     | 7 roles, permission grants, 11 users, general settings                                             |
| `activity/`     | Activity feed and notifications                                                                    |
| `kpi/`          | 11 KPI categories, 8 KPI templates, 5 rating bands, 3 evaluation periods (last quarter Closed, this quarter Active, next half-year Draft), sample scorecards from Planning to Finalized and Rejected (`scorecards.mock.ts`, scored with the same calculator as the mock services) |

Rules for sample data:

- It is **fictional** (`@example.com` / `.example` addresses, invented companies and people).
- **Deterministic**: `mock-random.ts` provides seeded helpers, so the same seed gives the same value.
  Dates are generated **relative to today** (e.g. "paid periods = the previous six months"), so the data
  always looks current.
- Records link by numeric id and are checked by `mock-data.integrity.spec.ts` (valid references,
  unique ids, payroll totals matching payslips to the cent, leave vs attendance consistency).
- Seed arrays are cloned into each mock service on creation; editing the app never mutates the seed.

## Mock services (`ui/src/app/mock-services`)

### Base class: `InMemoryCrudService<T, TList, TFilter>`

- State lives in a signal (`store`); `records` exposes a read-only view for **other mock services only**
  (joins and lookups) — screens must use the contracts.
- Every call is asynchronous (`mockResponse`: simulated latency from `APP_CONFIG.mockLatencyMs`,
  currently 250 ms) and returns **deep clones**, so callers cannot mutate the store.
- `query` applies free-text search (`searchFields`), equality filters (`matchesFilters`, overridable
  for ranges), sorting and paging.
- Hooks for rules: `prepare` (derive/normalize before validation), `validate`, `beforeDelete`,
  `describe` (messages and activity log), plus helpers `ensureUnique`, `ensureNotReferenced`,
  `validationError`, `conflictError`.
- Create/update/delete write to the activity feed (`ActivityMockService`).

### Registration

`provideMockDataServices()` (`provide-mock-data-services.ts`) binds every abstract contract to its mock
singleton. It is the **only** place that names mock classes for the app. A few bindings are lazy, so their code stays
out of the initial bundle: `KpiInsightDataService` (bound through `lazyDataService`, loaded on first call) and the KPI
Excel export modules (`LAZY_EXCEL_MODULES`).

### Mock services with workflows or special behaviour

| Service                                   | Behaviour beyond CRUD                                                                  |
| ----------------------------------------- | -------------------------------------------------------------------------------------- |
| `LeaveApplicationMockService`             | Submit/decide workflow, day counting, overlap and balance checks                       |
| `Overtime` / `AttendanceAdjustment` mocks | Pending → approved/rejected                                                            |
| `PayrollRunMockService`                   | `process`, `approve`, `markPaid`, `returnToDraft`; builds payslips with `calculatePay` |
| `PayslipMockService`                      | Read-only; replaced/removed by payroll processing                                      |
| `Salary*MockService`                      | Band, effective-date and reference rules                                               |
| `JobOpeningMockService`                   | `changeStatus`; auto Filled/reopen                                                     |
| `ApplicationMockService`                  | `moveStage` with transition and selection rules                                        |
| `InterviewMockService`                    | Double-booking, completion rules; advances the application stage                       |
| `UserMockService` / `RoleMockService`     | Self-protection, built-in role rules                                                   |
| `RolePermissionMockService`               | Matrix validation; administrator role locked                                           |
| `GeneralSettingsMockService`              | Field validation                                                                       |
| `KpiCategory` / `KpiTemplate` / `RatingBand` mocks | Uniqueness, template rule consistency (`KpiRule`), delete protection, activate/deactivate |
| `EvaluationPeriodMockService`             | `activate`, `complete`, `close`; no overlap per company and type; edits by status      |
| `ScorecardMockService`                    | Assignment and bulk assignment, plan, results, submit, review, evidence (see below)    |
| `KpiInsightMockService`                   | KPI dashboards, reports and the rows of the three KPI exports (read only)              |

Mock services reference each other through `inject` (or a lazy `Injector.get` where two services need
each other, to avoid circular construction).

### KPI and performance (demo only)

Contracts: `core/services/data/kpi-data.services.ts` (`KpiCategoryDataService`, `KpiTemplateDataService`,
`RatingBandDataService`, `EvaluationPeriodDataService`, `ScorecardDataService`, `KpiInsightDataService`), models in
`core/models/kpi.models.ts` and `kpi-insight.models.ts`. Mocks in `mock-services/kpi/`, seed in `mock-data/kpi/`, and
the demo calculator and figures in `core/utilities/kpi-calculation.ts`, `kpi-figures.ts` and `kpi-evidence.ts`. The
HTTP adapters for the same contracts are in `api-services/kpi/` (see [API-INTEGRATION-PLAN.md](API-INTEGRATION-PLAN.md)).

- **Calculation:** the mock scores with a mirror of the API's `KpiCalculator` and is unit-tested against the shared
  golden cases (`docs/test-data/kpi-calculation-cases.json`, and `kpi-scorecard-cases.json` through
  `core/utilities/kpi-scorecard-cases.spec.ts`), the same files the API tests run. Official scores come from the API
  only.
- **Who is acting:** HR, manager and own come from the simulated session's **role names** (HR Administrator and HR
  Officer: every scorecard; Department Manager: own, direct reports and the scorecards naming them reviewer; anyone
  else: own). The API uses `kpi:Approve` with `employees:Edit` instead. The mock checks no `kpi-setup` or `kpi`
  permission. Nothing here is access control.
- **Rules it repeats:** the workflow and its messages; nobody plans their own scorecard (409 on assign, item update and
  delete, scorecard delete and finalize-plan; bulk assignment skips the caller; an account with no linked employee is
  never refused); nobody reviews their own scorecard; the HR final review not by the approver; the period rules (a
  Returned scorecard may still be changed and resubmitted while the period is Completed, a Draft one needs Active; close
  is refused while a scorecard is in review or returned; the review queue lists only Active and Completed periods);
  Return clears the review draft; weights of exactly 100%; evidence type by content, 10 MB and 5 files per KPI; the
  review visibility rule for the scorecard's own employee (`core/utilities/kpi-review-visibility.ts`, a mirror of the
  API's `ReviewVisibility`, used by the scorecard detail and the employee dashboard feedback); the activity wording
  (every review decision reads `Scorecard of {code} for "{period}" was reviewed`); the manager dashboard's list of at
  most 200 scorecards by employee code, with `employeeCount` and `employeesTruncated`.
- **Differences from the API:** HR, manager and own come from role names (above); `rowVersion` is a counter; Office
  files are recognised by their entry names, not by full ZIP checks; there is no upload rate limit and no virus scan.
- **Evidence:** the bytes are kept **in memory** (a `Blob` cannot be stored in browser storage) and are lost on reload;
  the attachment metadata persists with the scorecards when browser persistence is on.

## Adding sample data or a new entity

1. Add the model to `core/models/<domain>.models.ts`.
2. Add an abstract contract in `core/services/data/<domain>-data.services.ts`.
3. Add seed data under `mock-data/<domain>/` (relative dates, deterministic helpers).
4. Implement a mock service extending `InMemoryCrudService` with its rules.
5. Bind it in `provideMockDataServices()`.
6. Build the screen as a `CrudPageConfig` (or a custom component) injecting **only the contract**.
7. Add unit tests for the rules and an e2e test for the main flow.
