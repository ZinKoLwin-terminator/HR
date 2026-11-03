# Future API integration plan

> **Status: plan only.** No backend exists, and none should be started without an explicit decision.
> This document describes how the current frontend is prepared for one.

## Why the swap is small

Screens inject abstract classes from `core/services/data` (for example `PayrollRunDataService`). The
mock services are bound to those classes in a single function, `provideMockDataServices()`. An API
implementation only needs to implement the same classes and be bound the same way.

```ts
// app.config.ts (future)
providers: [
  provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
  provideApiDataServices(), // instead of provideMockDataServices()
];
```

## Steps

1. **Agree the API contract first.** Start from the models in `core/models` and the contract methods
   in `core/services/data`; each CRUD contract maps to a resource, each workflow method to an action:

   | Contract method                                                | Suggested HTTP shape                                                     |
   | -------------------------------------------------------------- | ------------------------------------------------------------------------ |
   | `getAll`, `getById`                                            | `GET /resource`, `GET /resource/{id}`                                    |
   | `query(PageRequest)`                                           | `GET /resource?pageIndex&pageSize&sort&search&<filters>` → `PagedResult` |
   | `create / update / delete`                                     | `POST`, `PUT`/`PATCH`, `DELETE`                                          |
   | `PayrollRunDataService.process/approve/markPaid/returnToDraft` | `POST /payroll-runs/{id}/process` etc.                                   |
   | `ApplicationDataService.moveStage`                             | `POST /applications/{id}/stage`                                          |
   | `JobOpeningDataService.changeStatus`                           | `POST /job-openings/{id}/status`                                         |
   | `RolePermissionDataService.save`                               | `PUT /roles/{id}/permissions`                                            |

2. **Create API implementations under e.g. `api-services/`**, one class per contract, using
   `HttpClient`. Keep the contract signatures; add a small base class equivalent to
   `InMemoryCrudService` for the common CRUD/query mapping.
3. **Map errors to `AppError`.** HTTP 404 → `NotFound`, 400/422 → `Validation` (with `fieldErrors`),
   409 → `Conflict`, everything else → `Unknown`. Screens already display these consistently.
4. **Move the business rules to the server.** The rules in the mock services are the specification
   (see [FEATURES.md](FEATURES.md)); the server must enforce them (uniqueness, reference protection,
   workflow transitions, pay/leave/attendance calculations). Keep the TypeScript calculators only for
   previews, or retire them.
5. **Authentication and authorization.** Replace `CurrentUserService` with the real identity, add an
   auth interceptor and route guards. The Settings screens (users, roles, permissions) are currently a
   **simulation**: real enforcement must be designed and implemented server-side, and the UI should hide
   or disable actions from the server-provided permission set.
6. **Files.** Employee documents and candidate resumes store only file names today; they need real
   upload/download endpoints and storage.
7. **Payroll.** The calculation is demonstration logic. Real payroll needs jurisdiction-specific tax and
   statutory rules, auditability, period locking and (if applicable) payment/bank integration, all
   reviewed by the business and compliance owners.
8. **Switch gradually.** The provider can be swapped per contract (bind some contracts to API classes
   and keep others on mocks) so modules can migrate one at a time.
9. **Tests.** Reuse the unit specs against the API services with a test server or HTTP mocking, and keep
   the Playwright flows (they exercise screens, not data sources). Remove the dependence on "data resets
   on reload" in tests by seeding the test backend.

## Things to keep or change on the frontend

- Keep: models, contracts, screens, shared components, e2e flows.
- Change: the single provider function; any code using `records`/`findById` outside mock services
  (there should be none — screens only use contracts).
- Remove when done: `mock-data`, `mock-services`, the "Sample data" badge and footer wording.
