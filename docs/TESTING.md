# Testing

## Commands

| Command                                   | Purpose                                                                             |
| ----------------------------------------- | ----------------------------------------------------------------------------------- |
| `npm run test:ci`                         | All unit tests once (Vitest, jsdom)                                                 |
| `npm test`                                | Unit tests in watch mode                                                            |
| `npm run e2e`                             | Playwright, desktop (1440×900) and mobile (Pixel 7) projects; starts the dev server |
| `npx playwright test e2e/payroll.spec.ts` | One e2e file                                                                        |
| `npx playwright show-report`              | Open the HTML report after a run                                                    |
| `npm run lint` / `npm run build`          | Lint and production build (type errors fail the build)                              |

Run all npm commands from `ui/`. First time only: `npx playwright install chromium`.

E2E tests start their own dev server with `ng serve --configuration e2e` on port **4300**. That
configuration swaps `src/app/mock-data-options.ts` for `mock-data-options.e2e.ts`, so tests always run
on the **sample data without persistence**, whatever the app itself uses (the `feature/empty-data`
branch starts empty and keeps data in the browser). A normal `npm start` on port 4200 is never reused
by the tests. If a server is already running on 4300 it is reused locally.

Locally Playwright uses at most 3 workers: more overload the dev server on Windows (blank pages,
`ERR_ADDRESS_IN_USE`). Tests that depend on today's date must not assume yesterday was a working day;
the attendance dashboard test picks the most recent weekday explicitly.

## What is covered

**Unit tests (Vitest)** — pure logic and the mock-service rules:

- Calculations: payroll, leave balances, attendance derivation, dates, query helpers, validators.
- Data-service behaviour per module (`*-data.spec.ts`): validation, uniqueness, reference protection,
  workflows (leave, payroll processing, recruitment pipeline, interviews, roles/users/permissions).
- Mock-data integrity (references, totals to the cent).
- Component tests for Organization, Employees, Attendance, Leave and Dashboard screens.

**End-to-end tests (Playwright)** — user flows per module (`ui/e2e/*.spec.ts`), including validation
messages, destructive confirmations, workflows (payroll process → approve → paid, pipeline moves,
role/permission editing), unsaved-changes prompts and a mobile check per module.

**Quality sweep** (`ui/e2e/quality.spec.ts`) — visits every sidebar screen on desktop and mobile and
checks: no console/runtime errors, exactly one `h1`, every visible button/link has an accessible
name, every form field is labelled, images have alt text, no horizontal page scroll, page language set.

**Excel import/export** — unit tests build real `.xlsx` files with ExcelJS (valid, corrupt, renamed,
macro/link markers, formulas, wrong types, duplicates, limits), read generated templates, exports and
reports back, and check sheet names, headers, types, formats, drop-downs, the frozen header, formula
neutralising and sensitive-column handling (`excel-engine.spec.ts`, `excel-coercion.spec.ts`,
`modules/employee-excel.spec.ts`). A wizard component test walks every step, and Playwright
(`ui/e2e/excel.spec.ts`) uploads and downloads real files in the browser and parses the downloads.

## Writing e2e tests — gotchas learned

- `page.goto` **reloads the app and resets all in-memory data**. Navigate inside the app (sidebar
  links) when a test needs earlier changes to persist.
- Material `mat-select`: the floating label can sit over the centre; the helpers click the arrow end
  (see `openSelect` in the specs), then wait for the listbox to close.
- Destructive confirmations use role `alertdialog`; other dialogs use `dialog`.
- While a dialog is open the page behind it is `aria-hidden`; wait for it to close before asserting.
- Compare money to the cent in unit tests (floating-point sums).
- Google Fonts requests are aborted by `ui/e2e/fixtures.ts`, so icon fonts do not render in screenshots
  (icon names appear as text). This does not affect the app with network access.

## Known gaps

Be aware of these before relying on the test suite as proof of quality:

- Excel files are tested up to 5,000 rows only; there is no concurrency or memory testing of large imports.
- No component-level unit tests for Payroll, Recruitment and Settings screens (they have data-service
  unit tests and e2e coverage only).
- The accessibility checks are **automated and partial**; a manual review with a screen reader and
  colour-contrast tooling has not been done.
- Dark mode was spot-checked, not reviewed screen by screen.
- The printable payslip has print styles but has not been checked in a real print preview.
- The quality sweep does not verify that lists contain data.
- Mobile e2e coverage is one layout check per module, not every flow.
- No performance profiling beyond the build bundle budget (initial bundle ≈ 913 kB at time of writing).
