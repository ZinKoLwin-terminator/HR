# Future API integration plan

> **Status (updated 2026-11-18):** this started as a plan only. The .NET API now exists in `api/`, and the HTTP
> adapters in `ui/src/app/api-services` are bound by `provideApiDataServices()` (`npm run start:api`). Every module
> before KPI is integrated on `integrate/all-features`; the KPI module is integrated on `feature/api-kpi`; the grievance
> module has its API on `feature/api-grievance` and its screens and HTTP adapters on `feature/grievance`. Nothing is on
> `main` yet, and the GitHub Pages demo still runs the mock build. The steps below are kept as the design record; see
> "Integration status" for what is done.

## Integration status

| Module | Contracts (`core/services/data`) | HTTP adapters (`api-services/`) | Status |
| --- | --- | --- | --- |
| KPI and performance | `kpi-data.services.ts`: `KpiCategoryDataService`, `KpiTemplateDataService`, `RatingBandDataService`, `EvaluationPeriodDataService`, `ScorecardDataService`, `KpiInsightDataService` | `kpi/kpi-setup-http.services.ts` (`KpiCategoryHttpService`, `KpiTemplateHttpService`, `RatingBandHttpService`, `EvaluationPeriodHttpService`), `kpi/scorecard-http.service.ts` (`ScorecardHttpService`, including the multipart evidence upload), `kpi/kpi-insight-http.service.ts` (`KpiInsightHttpService`, lazy-loaded), `kpi/kpi-http.util.ts` | **Done on `feature/api-kpi`** (API, adapters, screens and E2E). Needs the database branch `feature/schema-kpi` published to a local database. The three KPI Excel exports go through the shared Excel adapter |
| Grievances | `grievance-data.services.ts`: `GrievanceCategoryDataService`, `MyGrievanceDataService`, `GrievanceCaseDataService`, `GrievanceInsightDataService` | `grievance/grievance-category-http.service.ts`, `grievance/my-grievance-http.service.ts`, `grievance/grievance-case-http.service.ts`, `grievance/grievance-insight-http.service.ts` (all four lazy-loaded), `grievance/grievance-http.support.ts` | **API on `feature/api-grievance`, adapters and screens on `feature/grievance`**, not merged with each other or `main`. Needs the database branch `feature/schema-grievance` published to a local database by a human, and a `Grievance:PseudonymKey` (see [DEPLOYMENT.md](DEPLOYMENT.md)). The `grievance-cases` Excel export goes through the shared Excel adapter. E2E against the API: not written yet |
| Every other module | as listed in `provide-api-data-services.ts` | as listed there | Done on `integrate/all-features`. Only the demo-data contract stays on the mock |

KPI endpoint families (full contract in `api/README.md`, "HTTP contract (KPI ...)"):

| Contract | HTTP shape |
| --- | --- |
| KPI setup CRUD (`query`, `getById`, `create`, `update`, `delete`) | `/api/kpi/categories`, `/templates`, `/rating-bands`, `/periods` (activate/deactivate for the first three: `POST /{id}/activate` and `/deactivate`) |
| `EvaluationPeriodDataService.activate/complete/close/options` | `POST /api/kpi/periods/{id}/activate`, `/complete`, `/close`; `GET /api/kpi/periods/options` |
| `ScorecardDataService` reads | `GET /api/kpi/scorecards/query`, `/scorecards/{id}`, `/my-scorecards`, `/reviews/pending` |
| `assign`, `bulkAssign`, `updateItem`, `deleteItem`, `deleteScorecard`, `finalizePlan` | `POST /api/kpi/assignments`, `/assignments/bulk`; `PUT`/`DELETE /api/kpi/scorecard-items/{id}`; `DELETE /api/kpi/scorecards/{id}`; `POST .../finalize-plan` |
| `saveResults`, `submit`, `startReview`, `saveReview`, `approve`, `returnForRevision`, `reject`, `finalize` | `PUT .../results`, `POST .../submit`, `POST .../start-review`, `PUT .../review`, `POST .../approve`, `/return`, `/reject`, `/finalize` |
| `uploadEvidence`, `downloadEvidence`, `deleteEvidence` | `POST /api/kpi/scorecard-items/{itemId}/attachments` (multipart), `GET` and `DELETE /api/kpi/attachments/{id}` |
| `KpiInsightDataService` | `GET /api/kpi/dashboard/employee`, `/manager`, `/hr`; `GET /api/kpi/reports/summary`, `/employee/{id}`, `/department/{id}`, `/ranking`, `/kpi-achievement`, `/target-vs-actual` |

Grievance endpoint families (all under `/api/grievances`; full contract in `api/README.md`, "HTTP contract (grievances)"):

| Contract | HTTP shape |
| --- | --- |
| `GrievanceCategoryDataService` (CRUD; a status change is activate/deactivate) | `/api/grievances/categories` (`MapCrud`), `POST /categories/{id}/activate`, `/deactivate` |
| `MyGrievanceDataService` reads: `options`, `query`, `getById`, `dashboard`, `downloadAttachment` | `GET /options`, `/my`, `/my/{id}`, `/my/dashboard`, `/my/attachments/{id}` |
| `createDraft`, `updateDraft`, `deleteDraft`, `submit`, `sendMessage`, `uploadAttachment`, `markSeen` | `POST /my` (201), `PUT /my/{id}`, `DELETE /my/{id}` (body `{ rowVersion }`), `POST /my/{id}/submit`, `/messages`, `/attachments` (multipart `rowVersion`, `file`; rate limited), `/seen` (204) |
| `GrievanceCaseDataService` reads | `GET /cases/query`, `/investigations`, `/reviews/pending`, `/cases/{id}`, `/cases/{id}/history`, `/cases/{id}/audit`, `/audit`, `/investigators`, `/employees`, `/attachments/{id}` |
| The 14 commands (`acknowledge` ... `archive`) | `POST /cases/{id}/acknowledge`, `/start-review`, `/assign`, `/unassign`, `/start-investigation`, `/request-info`, `/propose-resolution`, `/approve-resolution`, `/reject-resolution`, `/reject`, `/close`, `/reopen`, `/escalate`, `/archive`, each with `{ rowVersion, ... }` |
| `triage`, `sendMessage`, `addNote`, `uploadAttachment`, `addCorrectiveAction`, `updateCorrectiveAction` | `PUT /cases/{id}/triage`; `POST /cases/{id}/messages`, `/notes`, `/attachments` (multipart `rowVersion`, `visibility`, `file`; rate limited), `/actions` (201); `PUT /case-actions/{id}` |
| `GrievanceInsightDataService` | `GET /dashboard/hr`, `/dashboard/review`, `/reports/cases`, `/reports/summary` |

Shared routes ask for ANY of the tiers' permissions (`grievance-case-team`, `grievance-case-work`, `grievance-insight`
policies) and the handler decides on the case itself (404 outside the scope, 403 for the wrong capacity). The adapters
send only the sort keys the API accepts (`GRIEVANCE_MY_SORT_KEYS`, `GRIEVANCE_CASE_SORT_KEYS`, `GRIEVANCE_REPORT_SORT_KEYS`) and
map a conflict-of-interest `400` onto the investigator field.

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

## Excel import/export

The contracts in `core/services/data/excel-data.services.ts` map to endpoints:

| Contract method          | Suggested HTTP shape                                                                                                             |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| `getModules`             | `GET /api/excel/modules`                                                                                                         |
| `getTemplate(module)`    | `GET /api/excel/templates/{module}`                                                                                              |
| `inspect(module, file)`  | `POST /api/excel/imports/{module}/inspect` (multipart; the server keeps the file temporarily and returns an upload id)           |
| `validate(request)`      | `POST /api/excel/imports/{module}/validate` (returns the preview and a batch id)                                                 |
| `execute(batchId, mode)` | `POST /api/excel/imports/{batchId}/execute` (by batch id, so no file is re-uploaded and no transaction stays open during review) |
| `getHistory`, `getBatch` | `GET /api/excel/imports/history`, `GET /api/excel/imports/{batchId}`                                                             |
| `getErrorReport`         | `GET /api/excel/imports/{batchId}/error-report`                                                                                  |
| `export(request)`        | `POST /api/excel/exports/{module}` (large exports: `202` + status + download link)                                               |

On the server: repeat the content checks and limits, parse with a library licensed for your use
(ClosedXML or Open XML SDK; EPPlus needs a commercial licence), reuse the same command and validation
pipeline as normal API calls, persist batches and errors (SSDT tables), keep uploads in temporary
storage with expiry, authorise every endpoint, and audit imports and exports.

## Things to keep or change on the frontend

- Keep: models, contracts, screens, shared components, e2e flows.
- Change: the single provider function; any code using `records`/`findById` outside mock services
  (there should be none — screens only use contracts).
- Remove when done: `mock-data`, `mock-services`, the "Sample data" badge and footer wording.
