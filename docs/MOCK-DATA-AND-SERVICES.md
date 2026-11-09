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
singleton. It is the **only** place that names mock classes for the app.

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

Mock services reference each other through `inject` (or a lazy `Injector.get` where two services need
each other, to avoid circular construction).

## Adding sample data or a new entity

1. Add the model to `core/models/<domain>.models.ts`.
2. Add an abstract contract in `core/services/data/<domain>-data.services.ts`.
3. Add seed data under `mock-data/<domain>/` (relative dates, deterministic helpers).
4. Implement a mock service extending `InMemoryCrudService` with its rules.
5. Bind it in `provideMockDataServices()`.
6. Build the screen as a `CrudPageConfig` (or a custom component) injecting **only the contract**.
7. Add unit tests for the rules and an e2e test for the main flow.
