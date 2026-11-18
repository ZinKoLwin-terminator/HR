# Architecture

## Principles

1. **Two data modes behind one set of contracts.** The **demo build** (default, GitHub Pages) runs on centralized
   mock data through in-browser mock services. The **API build** (`npm run start:api`) binds the same contracts to
   HTTP adapters that call the .NET API in `api/`. Screens do not change between the two.
2. **Contracts first.** Screens depend on abstract data services in `core/services/data`, never on
   mock or HTTP classes. Each mode binds the contracts in one place.
3. **The server is the authority.** In API mode the API enforces sign-in, permissions, row scope and every business
   rule. The mock services only imitate those rules for the demo; they protect nothing.
4. **Standalone, signal-based Angular.** Standalone components, `input()`/`output()`, signals,
   `rxResource` for loading, `linkedSignal` for editable state derived from loaded data.
5. **Reusable building blocks** over per-screen code: most list screens are a configuration of a
   generic CRUD page.
6. **Honesty about demo limits.** All sample data is fictional; payroll figures are demonstrations; in the demo build
   sign-in and permissions are a simulation that enforces nothing.

## System overview

| Part | Where | Notes |
| --- | --- | --- |
| UI | `ui/` (Angular, Material/CDK) | Demo build on GitHub Pages (deployed on every push to `main`); API build for local work |
| API | `api/` (.NET 10, Clean Architecture: Domain, Application, Infrastructure, Api) | Minimal API endpoints, use-case handlers, FluentValidation, `Result`/`AppError`; ASP.NET Core Identity cookie sessions, `module:Action` permissions checked on every request, row scope in the queries. See `api/README.md` |
| Database | SSDT project in a separate repository (`HR DBPJ`), one schema per module | Published by a human, to a local database only for now. EF Core maps onto it and never migrates. See [DEPLOYMENT.md](DEPLOYMENT.md) |

Integration branches: every module before KPI on `integrate/all-features`; KPI on `feature/api-kpi`; grievances on
`feature/grievance` (UI) and `feature/api-grievance` (API). None is on `main` yet (see
[API-INTEGRATION-PLAN.md](API-INTEGRATION-PLAN.md)).

## Layers

```
 features/*  (screens)  ──uses──▶  core/services/data/*  (abstract contracts, DI tokens)
                                          ▲                                   ▲
                 bound by provideMockDataServices()            bound by provideApiDataServices()
                                          │                                   │ (registered after the mocks,
                                   mock-services/*                      api-services/*   off unless enabled)
                              (in-browser implementations)          (HTTP adapters → api/)
                                          │ seeded from
                                      mock-data/*  (sample data)
```

- `core/models` — TypeScript interfaces and small constants per domain (e.g. `payroll.models.ts`).
- `core/services/data` — the contracts. `CrudDataService<T, TList, TFilter>` defines
  `getAll / getById / query / create / update / delete`; domain contracts add workflow methods
  (e.g. `PayrollRunDataService.process/approve/markPaid/returnToDraft`,
  `ApplicationDataService.moveStage`).
- `core/utilities` — pure logic with unit tests: `payroll-calc`, `leave-balance`,
  `attendance-calc`, `kpi-calculation`, `grievance-workflow` (the grievance state machine the screens use to decide
  which actions to offer; the API decides).
- `mock-services` — `InMemoryCrudService` plus one subclass per entity; registered by
  `provideMockDataServices()` in `app.config.ts`. Some contracts (KPI insights, the four grievance contracts) and the
  KPI and grievance Excel modules are bound lazily, so their code loads on first use.
- `api-services` — one HTTP adapter per contract, registered by `provideApiDataServices()` after the mocks so its
  bindings win; a no-op unless `api-data-options.ts` enables it (the `api` build configuration does).
- `shared` — `CrudPageComponent`, `DataTableComponent`, `EntityFormDialog`, `ConfirmDialogService`
  (confirm and prompt), `StatusBadge`, `StatCard`, `PageHeader`, `EmptyState`, validators, pipes,
  date/query utilities, chart panel and chart theme.
- `layout` — shell with sidebar (driven by `core/constants/navigation.ts`), header, breadcrumbs,
  footer, theme toggle.

## Routing

Each module exports a `*_ROUTES` array in `features/<module>/<module>.routes.ts` and is
lazy-loaded from `app.routes.ts`. Routes carry `title` and `data.breadcrumb`. Component input
binding is enabled, so route/query parameters (for example `:id`, `?role=`) are plain `input()`s.
Screens with editable state use the `unsavedChangesGuard`.

## Grievance module (confidential data)

`features/grievances` (routes in `grievance.routes.ts`) serves three case-team workspaces (management, investigation,
review) from one list and one detail component, chosen by the route's `workspace`. Differences from the other modules:

- Four permission modules (`grievance`, `grievance-investigation`, `grievance-management`, `grievance-review`) instead
  of one; shared API routes accept any of the tiers and the server decides per case.
- Its own access log (`grv.AuditLog`) and no entries in the company-wide activity feed; in-app notifications are
  addressed per recipient (the mock keeps the recipient in a private map).
- Anonymous submitters are linked by a keyed pseudonym held only by the API (`Grievance:PseudonymKey`); the mock's
  pseudonym is a public demo hash.

Rules and limits: [FEATURES.md](FEATURES.md) "Grievances"; customer guide: [modules/grievance.md](modules/grievance.md).

## The generic CRUD page

`CrudPageComponent` renders a complete list screen from a `CrudPageConfig`:

- `columns` (sortable, hide-below breakpoints, currency/date/status types), `filters`, search,
  optional date-range filter, paging;
- `fields` (a schema for the add/edit dialog: text, textarea, number, select, multiselect, date,
  time, email, tel, file, toggle…, with dependent selects and derived fields);
- `extraActions` (row actions with optional confirm or prompt, e.g. "Process payroll", "Reject…");
- `canEdit` / `canDelete` / `canCreate`, `lockedFilters` and `fixedValues` for embedding in tabs.
  `canEdit(row)` applies to both the row menu and the Edit button of the details dialog.

Server-side validation errors (`AppError.fieldErrors`) are mapped back onto the form fields.
Screens with unique needs (payroll review, pipeline board, permission matrix, payslip) are written
as dedicated components that still use the same contracts and shared pieces.

## Error model

All data services fail with an `AppError { code: 'NotFound' | 'Validation' | 'Conflict' | 'Unauthorized' |
'Forbidden' | 'Unknown', message, fieldErrors?, traceId? }`. The HTTP adapters map the API's problem details onto
the same shape (`api-services/http-error.mapper.ts`), so no screen changes between the two modes.

## State and loading

- No global store. Each screen loads what it needs with `rxResource` and reloads after changes.
- Mock services keep their state in signals; they are root singletons. Without persistence, data lives only
  while the app stays open and **resets on page reload** (use in-app navigation, not `page.goto`,
  in tests).
- `provideMockDataServices({ emptyData, persist })` changes that. `emptyData: true` starts every
  business list empty (roles, permissions, settings, reference data such as the grievance categories and one
  built-in administrator remain, see `BUILT_IN_ADMIN_EMPLOYEE_ID`). `persist: true` keeps created and edited records in
  `localStorage` (`hrms.data.v1.*`, see `mock-services/persistence.ts`); Settings > General has a
  **Reset data** button (`DemoDataDataService`) that clears only that data. The options come from
  `ui/src/app/mock-data-options.ts` (currently both on); the E2E build swaps in `mock-data-options.e2e.ts` (full sample
  data, no persistence). Bump the storage prefix when a model changes shape; a browser that saved data before a new
  module (for example its role permissions) needs **Reset data**. All of this is mock-only; in API mode the data is
  in SQL Server.

## Styling and accessibility

- Material 3 theme with light/dark (`ThemeService`), design tokens as CSS variables in
  `ui/src/styles.scss`, print rules for payslips.
- ESLint enforces Angular template accessibility rules. Controls need accessible names; tables have
  captions; dialogs use `dialog`/`alertdialog` roles; the pipeline and permission matrix are
  keyboard-operable. See [TESTING.md](TESTING.md) for the automated sweep and its limits.

## Conventions

- Strict TypeScript; no `any` (one lint-excepted DI helper type).
- One lazy route per screen; feature code stays in its feature folder, shared code in `shared`.
- Money is formatted with the `money` pipe; dates are ISO `yyyy-MM-dd` strings handled with
  `shared/utilities/date.util`.
- Never put business data in components: add it to `mock-data` and expose it through a mock service.
