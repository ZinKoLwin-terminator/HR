# Architecture

## Principles

1. **Frontend only.** No backend, database or HTTP. Business data comes from centralized mock data
   through dedicated mock services.
2. **Contracts first.** Screens depend on abstract data services in `core/services/data`, never on
   mock classes. Mocks are bound to the contracts in one place.
3. **Standalone, signal-based Angular.** Standalone components, `input()`/`output()`, signals,
   `rxResource` for loading, `linkedSignal` for editable state derived from loaded data.
4. **Reusable building blocks** over per-screen code: most list screens are a configuration of a
   generic CRUD page.
5. **Honesty about demo limits.** Payroll is labelled sample data; permissions are a simulation.

## Layers

```
 features/*  (screens)  ──uses──▶  core/services/data/*  (abstract contracts, DI tokens)
                                          ▲
                                          │ bound by provideMockDataServices()
                                   mock-services/*  (in-memory implementations)
                                          │ seeded from
                                      mock-data/*  (sample data)
```

- `core/models` — TypeScript interfaces and small constants per domain (e.g. `payroll.models.ts`).
- `core/services/data` — the contracts. `CrudDataService<T, TList, TFilter>` defines
  `getAll / getById / query / create / update / delete`; domain contracts add workflow methods
  (e.g. `PayrollRunDataService.process/approve/markPaid/returnToDraft`,
  `ApplicationDataService.moveStage`).
- `core/utilities` — pure calculation logic with unit tests: `payroll-calc`, `leave-balance`,
  `attendance-calc`.
- `mock-services` — `InMemoryCrudService` plus one subclass per entity; registered by
  `provideMockDataServices()` in `app.config.ts`.
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

## The generic CRUD page

`CrudPageComponent` renders a complete list screen from a `CrudPageConfig`:

- `columns` (sortable, hide-below breakpoints, currency/date/status types), `filters`, search,
  optional date-range filter, paging;
- `fields` (a schema for the add/edit dialog: text, textarea, number, select, multiselect, date,
  time, email, tel, file, toggle…, with dependent selects and derived fields);
- `extraActions` (row actions with optional confirm or prompt, e.g. "Process payroll", "Reject…");
- `canEdit` / `canDelete` / `canCreate`, `lockedFilters` and `fixedValues` for embedding in tabs.

Server-side validation errors (`AppError.fieldErrors`) are mapped back onto the form fields.
Screens with unique needs (payroll review, pipeline board, permission matrix, payslip) are written
as dedicated components that still use the same contracts and shared pieces.

## Error model

All data services fail with an `AppError { code: 'NotFound' | 'Validation' | 'Conflict' | 'Unknown',
message, fieldErrors? }`. A future HTTP implementation should map API errors onto the same shape
so no screen changes.

## State and loading

- No global store. Each screen loads what it needs with `rxResource` and reloads after changes.
- Mock services keep their state in signals; they are root singletons. By default data lives only
  while the app stays open and **resets on page reload** (use in-app navigation, not `page.goto`,
  in tests).
- `provideMockDataServices({ emptyData, persist })` changes that. `emptyData: true` starts every
  business list empty (roles, permissions, settings and one built-in administrator remain, see
  `BUILT_IN_ADMIN_EMPLOYEE_ID`). `persist: true` keeps created and edited records in
  `localStorage` (`hrms.data.v1.*`, see `mock-services/persistence.ts`); Settings > General has a
  **Reset data** button (`DemoDataDataService`) that clears only that data. The `feature/empty-data`
  branch enables both in `app.config.ts`. Bump the storage prefix when a model changes shape. All of
  this is mock-only and goes away with the API implementation.

## Styling and accessibility

- Material 3 theme with light/dark (`ThemeService`), design tokens as CSS variables in
  `src/styles.scss`, print rules for payslips.
- ESLint enforces Angular template accessibility rules. Controls need accessible names; tables have
  captions; dialogs use `dialog`/`alertdialog` roles; the pipeline and permission matrix are
  keyboard-operable. See [TESTING.md](TESTING.md) for the automated sweep and its limits.

## Conventions

- Strict TypeScript; no `any` (one lint-excepted DI helper type).
- One lazy route per screen; feature code stays in its feature folder, shared code in `shared`.
- Money is formatted with the `money` pipe; dates are ISO `yyyy-MM-dd` strings handled with
  `shared/utilities/date.util`.
- Never put business data in components: add it to `mock-data` and expose it through a mock service.
