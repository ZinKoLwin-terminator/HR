# Apex Horizon HRMS — Angular frontend (mock data)

An enterprise-style **Human Resource Management System** built as an **Angular frontend only**.
All business data is **sample data** served by in-memory mock services. There is **no backend,
no database, no HTTP calls and no authentication**.

> The data layer is built behind abstract data-service contracts so that API-backed services can
> replace the mocks later without changing screens. See [docs/API-INTEGRATION-PLAN.md](docs/API-INTEGRATION-PLAN.md).

## Modules

| Module       | Screens                                                                                                        |
| ------------ | -------------------------------------------------------------------------------------------------------------- |
| Dashboard    | KPI cards, headcount/department/attendance charts, payroll summary, activity feed                              |
| Organization | Companies, branches, departments, sections, positions, job grades, reporting structure                         |
| Employees    | List, add/edit form, profile (tabs), documents                                                                 |
| Attendance   | Overview, check in/out, records, late & early, shifts, shift assignment, work schedules, overtime, adjustments |
| Leave        | Types, policies, balances, applications, approvals, calendar                                                   |
| Payroll      | Salary structures/components, employee salaries, processing workflow, payslips, history                        |
| Recruitment  | Job openings, candidates, interviews, pipeline board                                                           |
| Settings     | General settings, users, roles, permissions (**UI simulation only — nothing is enforced**)                     |

Details per screen: [docs/FEATURES.md](docs/FEATURES.md).

## Important limitations (please read)

- **All data is fictional sample data** and **resets when the page is reloaded** (nothing is persisted).
- **Payroll figures are demonstration rules, not real tax or statutory calculations.** No payments
  are made and no bank is contacted; "Mark as paid" only records a status.
- **There is no sign-in.** Users, roles and permissions are a UI simulation and provide no security.
- Candidate resumes and employee documents store the **file name only**; file contents are not kept.
- Not yet done: component-level tests for the Phase 8–10 screens, a manual accessibility review with a
  screen reader, a real print-preview check of the payslip, and a backend. See
  [docs/TESTING.md](docs/TESTING.md#known-gaps).

## Requirements

- Node.js ^22.22.3, ^24.15.0 or 26+ (required by Angular 22; developed with Node 26) and npm 11 (`packageManager` is pinned in `package.json`).
- For end-to-end tests: Playwright browsers (`npx playwright install chromium`).

## Install and run

```bash
npm install
npm start            # dev server at http://localhost:4200
```

**Windows PowerShell:** if `npm` is blocked by the script execution policy, use `npm.cmd start`.
**If `localhost` does not respond**, try `http://127.0.0.1:4200` (some machines block IPv6 loopback).

## Scripts

| Command           | What it does                                                                      |
| ----------------- | --------------------------------------------------------------------------------- |
| `npm start`       | Dev server with live reload                                                       |
| `npm run build`   | Production build into `dist/` (initial bundle budget: warning 1 MB, error 1.5 MB) |
| `npm run lint`    | ESLint (including Angular template accessibility rules)                           |
| `npm run format`  | Prettier over `src` and `e2e`                                                     |
| `npm run test:ci` | Unit tests once (Vitest)                                                          |
| `npm test`        | Unit tests in watch mode                                                          |
| `npm run e2e`     | Playwright end-to-end tests (desktop and mobile; starts the dev server)           |

Before sharing changes, run `npm run lint`, `npm run build`, `npm run test:ci` and `npm run e2e`.

## Technology

Angular 22 (standalone components, Signals, `rxResource`), TypeScript 6 (strict), Angular Material 22
(Material 3 theme, light/dark), RxJS, Reactive Forms, SCSS, Chart.js via ng2-charts, Vitest,
Playwright, ESLint + angular-eslint, Prettier.

## Documentation

- [Architecture](docs/ARCHITECTURE.md) — layers, folders, conventions, reusable building blocks
- [Features](docs/FEATURES.md) — what each module does and its business rules
- [Mock data and mock services](docs/MOCK-DATA-AND-SERVICES.md) — where sample data lives, how mock services behave
- [Testing](docs/TESTING.md) — how to run tests, what is covered, known gaps
- [Future API integration plan](docs/API-INTEGRATION-PLAN.md) — how to swap mocks for a real API

## Repository layout

```
src/app/
  core/           models, constants, guards, services, data-service contracts, calculation utilities
  shared/         reusable components (CRUD page, data table, dialogs…), pipes, validators, utilities
  layout/         application shell (header, sidebar, footer, breadcrumbs)
  features/       one lazy-loaded folder per module
  mock-data/      centralized sample data
  mock-services/  in-memory implementations of the data contracts
e2e/              Playwright tests
docs/             project documentation
```
