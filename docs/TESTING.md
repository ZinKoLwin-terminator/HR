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

First time only: `npx playwright install chromium`. If a dev server is already running on port 4200
it is reused locally.

## What is covered

**Unit tests (Vitest)** — pure logic and the mock-service rules:

- Calculations: payroll, leave balances, attendance derivation, dates, query helpers, validators.
- Data-service behaviour per module (`*-data.spec.ts`): validation, uniqueness, reference protection,
  workflows (leave, payroll processing, recruitment pipeline, interviews, roles/users/permissions).
- Mock-data integrity (references, totals to the cent).
- Component tests for Organization, Employees, Attendance, Leave and Dashboard screens.

**End-to-end tests (Playwright)** — user flows per module (`e2e/*.spec.ts`), including validation
messages, destructive confirmations, workflows (payroll process → approve → paid, pipeline moves,
role/permission editing), unsaved-changes prompts and a mobile check per module.

**Quality sweep** (`e2e/quality.spec.ts`) — visits every sidebar screen on desktop and mobile and
checks: no console/runtime errors, exactly one `h1`, every visible button/link has an accessible
name, every form field is labelled, images have alt text, no horizontal page scroll, page language set.

## Writing e2e tests — gotchas learned

- `page.goto` **reloads the app and resets all in-memory data**. Navigate inside the app (sidebar
  links) when a test needs earlier changes to persist.
- Material `mat-select`: the floating label can sit over the centre; the helpers click the arrow end
  (see `openSelect` in the specs), then wait for the listbox to close.
- Destructive confirmations use role `alertdialog`; other dialogs use `dialog`.
- While a dialog is open the page behind it is `aria-hidden`; wait for it to close before asserting.
- Compare money to the cent in unit tests (floating-point sums).
- Google Fonts requests are aborted by `e2e/fixtures.ts`, so icon fonts do not render in screenshots
  (icon names appear as text). This does not affect the app with network access.

## Known gaps

Be aware of these before relying on the test suite as proof of quality:

- No component-level unit tests for Payroll, Recruitment and Settings screens (they have data-service
  unit tests and e2e coverage only).
- The accessibility checks are **automated and partial**; a manual review with a screen reader and
  colour-contrast tooling has not been done.
- Dark mode was spot-checked, not reviewed screen by screen.
- The printable payslip has print styles but has not been checked in a real print preview.
- The quality sweep does not verify that lists contain data.
- Mobile e2e coverage is one layout check per module, not every flow.
- No performance profiling beyond the build bundle budget (initial bundle ≈ 913 kB at time of writing).
