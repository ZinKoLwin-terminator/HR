# Excel import and export

A reusable import/export capability shared by all modules. **Status:** the shared foundation and these
modules are implemented: employees, companies, branches, departments, job grades, positions, leave
balances, shift assignments, salary assignments, and the export-only KPI report modules.

> Two builds. The **demo build** (default, GitHub Pages) reads and writes spreadsheets **in the browser**
> behind the data-service contracts; the browser is **not a security boundary** and enforces nothing.
> The **API build** (`npm run start:api`) sends files to the .NET API (`api/`, the `*ExcelModule*` classes),
> which enforces the limits, content checks, permissions and audit (see
> [API-INTEGRATION-PLAN.md](API-INTEGRATION-PLAN.md)).

## What users can do

- **Import Data** (Data Exchange → Import Data, or the Import button on a list): pick a module, download the
  template, upload a `.xlsx` file, check the column mapping, review a row-by-row preview, confirm, see the
  result. A downloadable **validation report** lists every problem (row, field, original value, error,
  suggested correction). Nothing is silently skipped.
- **Import History** (Data Exchange → Import History): every batch with who, when, mode, outcome and the
  recorded problems.
- **Export** (Export button on a list): export the records matching the current search and filters (server-side,
  not just the visible page), all records, or selected records, optionally within a date range, choosing columns.
  Personal and financial columns are off unless explicitly included. Every export is recorded in the activity feed.

## Import workflow

1. **Template** — module + strategy (_Insert only_, _Update only_, _Insert or update_). The template has an
   Instructions sheet, a header-only data sheet (required headers dark blue, optional grey), a Field guide
   (type, allowed values, description, example), drop-down lists, data checks, a hidden Lists sheet and
   module reference sheets (for Employees: _Organization codes_, _Grade and shift codes_).
2. **Upload** — drag and drop or browse; shows file name and size; replace or remove.
3. **Columns** — headers are matched ignoring case, spacing and punctuation (and aliases). If the file follows the
   template nothing needs mapping. Otherwise missing required fields, unknown columns and two headers
   mapping to one field are shown and can be corrected.
4. **Review** — summary cards (total, ready, invalid, duplicate, warning) and a filterable preview table.
5. **Confirm** — _All or nothing_ (any failure rolls everything back; unavailable while rows have problems) or
   _Import valid rows only_. The expected inserts/updates/skips are shown first.
6. **Result** — inserted, updated, skipped, failed; report download; link to history.

### Validation

| Level    | Checks                                                                                                                                                                                                                                                         |
| -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| File     | `.xlsx` only; ZIP signature (renamed files are refused); size, row and worksheet limits; empty workbook or sheet; missing or duplicate headers; macros and external links refused; corrupted workbook                                                          |
| Column   | required, length, pattern, type (whole/decimal with precision, date `yyyy-MM-dd` / `dd MMM yyyy` / Excel dates, time, boolean), allowed values (with "did you mean…"), email, phone. **Formulas are never evaluated** — a formula cell is reported as an error |
| Business | the module's **own data-service rules** run through a dry run (`InMemoryCrudService.dryRun`), so the import cannot be looser than the screens                                                                                                                  |
| Strategy | insert-only rejects existing records, update-only rejects unknown ones; duplicate matching keys inside the file are flagged                                                                                                                                    |

Default limits (`APP_CONFIG.excel`): 5 MB, 5,000 rows, 5 worksheets, 50,000 export rows.

### Matching keys

Employees: **Employee Code** (case-insensitive, unique across the app). Names are never used for matching.
On update, a blank cell **keeps the current value**; required columns must still be filled on every row.

### Transactions

Validation never changes data. _All or nothing_ takes a snapshot, applies the rows and restores the snapshot if
any row fails while saving (including related history entries). _Partial_ keeps the rows that succeed and
lists the rest. A batch can be executed **once**; if data changed between validation and import (for example
another import created the same record) the affected rows fail and are reported.

## Export workflow

Formatted workbook: sheet names, bold headers, frozen header row, filters, column widths, date format
`yyyy-mm-dd`, number formats, typed cells. Text that could run as a formula (starts with `=`, `+`, `-`, `@`, tab or CR)
is prefixed with an apostrophe; ordinary numbers and phone numbers are left alone. Employees export extra
sheets: _Department Summary_ and _Branch Summary_.

## Architecture

```
core/models/excel.models.ts               types shared by UI, contracts and mocks
core/services/data/excel-data.services.ts contracts: catalog, import (template/inspect/validate/execute/history/report), export
shared/components/excel/*                 wizard, upload, mapping, summary, preview, result, export dialog + service
features/data-exchange/*                  Import Data and Import History screens
mock-services/excel/
  excel-file.service.ts                   the ONLY place that uses the spreadsheet library (ExcelJS, lazy-loaded)
  excel-coercion.ts                       header matching, cell conversion and checks, formula neutralising (pure)
  excel-import.mock.service.ts            generic engine: inspect, validate, execute, history, error report
  excel-export.mock.service.ts            catalog + export
  excel-module.ts                         what a module plugs in (definition + adapters, EXCEL_MODULES token)
  modules/employee-excel.module.ts        Employees definition, import/export adapters, reference sheets
```

A module contributes: an `ExcelModuleInfo` (columns, matching key, labels), an import adapter
(`findExisting`, `toModel` resolving business codes to ids, `dryRun`, `apply`, `snapshot`) and an export adapter
(`rows`, optional `extraSheets`). Register it with `{ provide: EXCEL_MODULES, multi: true, useFactory: … }`
in `provideMockDataServices()`. Then add `excel: { moduleCode }` to the list's `CrudPageConfig` (or buttons on a custom list).

### Library choice

ExcelJS 4.4 (MIT): reads and writes, supports drop-down validation, styles, freeze panes and filters.
Built as a lazy chunk (≈ 944 kB raw / 218 kB gzip) loaded only on first use. **Known costs:** the package has not
had a release since Dec 2024, and `npm audit` reports 9 _moderate_ findings in its dependency tree
(`archiver`/`glob`/`minimatch`, and a `uuid` buffer advisory). They are build-time/transitive and are not part of the browser
bundle (`exceljs.min.js` is prebuilt); ExcelJS calls `uuidv4()` without a buffer. Re-check before production
use; the wrapper (`excel-file.service.ts`) makes the library replaceable.

## Employee rules now enforced by the service

So that the form and the import behave the same, these rules moved from the form into
`EmployeeMockService.validate`: age ≥ 18, dates (probation/exit not before joining, exit date for
Resigned/Terminated), and a consistent organization assignment (company → branch → department → section/position all
existing and matching; inactive units refused when newly assigned; manager and shift must exist).
History entries (Joined, Promotion, Transfer…) are recorded for every way of saving.

## KPI export modules (API and demo build)

The .NET API (`api/`, branch `feature/api-kpi`) adds three **export-only** modules for the KPI and performance data
(`api/src/Apex.Hrms.Application/Performance/Insights/KpiExcelModules.cs`: `KpiScorecardExcelModule`, `KpiRankingExcelModule`,
`KpiTargetVsActualExcelModule`). They are in the catalog that `GET /api/excel/modules` returns (`canImport` false, so there is no
template and no import): scorecards change only through the planning and review workflow, never by upload. The exact contract is in
`api/README.md`, sections "HTTP contract (KPI dashboards, reports and export)" and "HTTP contract (Excel import and export)".

**The demo build includes them too.** `ui/src/app/mock-services/excel/modules/kpi-excel.modules.ts` mirrors the same three
definitions (same codes, labels, sheet names and columns; export date filter "Period dates") and is registered in
`provideMockDataServices()` through `LAZY_EXCEL_MODULES`, so the definitions and the KPI insight mock that builds the rows load
only on first use. In the demo the rows come from `KpiInsightMockService.exportRows`, over the scorecards the simulated session
may see (scope by role name, see [MOCK-DATA-AND-SERVICES.md](MOCK-DATA-AND-SERVICES.md)); with the API the server builds them.

**Where the Export button is** (Performance > Reports, `kpi-report-definitions.ts`): Summary by period, Employee performance
history and Department performance export `kpi-scorecards`; Ranking exports `kpi-ranking`; Target vs actual exports
`kpi-target-vs-actual`; KPI achievement has no export. The export dialog takes the report's current filters. E2E: KPI-43 in
`docs/E2E-TEST-SCENARIOS.md` (ranking export of the filtered rows).

| Module code            | Sheet / label        | Columns                                                                                                                                               |
| ---------------------- | -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `kpi-scorecards`       | KPI scorecards       | Employee Code, Employee Name, Department, Period, Status, KPIs, Total Weight, Overall Score, Rating, Reviewer, Plan Finalized On, Submitted On, Submission Due, Finalized On |
| `kpi-ranking`          | KPI ranking          | Rank, Employee Code, Employee Name, Department, Period, Overall Score, Rating (Finalized scorecards only; equal scores share a rank: 1, 1, 3)          |
| `kpi-target-vs-actual` | KPI target vs actual | Employee Code, Employee Name, Department, Period, Status, KPI Code, KPI, Category, Unit, Target, Actual, Achievement %, Weight, Weighted Score         |

- **Permissions:** the export route needs `excel:Export`, and each module needs `kpi:View` on top, checked by name like every other module.
  In the demo build only the `excel:*` keys hide the button; nothing is enforced.
- **Rows:** the same rows as the KPI reports for the caller: their own scorecards, their team's (direct reports and the scorecards they
  review), or everyone's for HR (`kpi:Approve` with `employees:Edit`).
- **Filters:** `filters` takes the report filter names (`companyId`, `branchId`, `departmentId`, `positionId`, `employeeId`, `categoryId`,
  `periodId`, `status`, `periodType`, `year`); `search` matches the employee code or name; `dateFrom`/`dateTo` keep the periods that
  overlap the range; `selected` exports take scorecard ids.
- **Limits:** the usual export row limit applies (50,000 rows; a larger export is refused with a message to narrow the filters).
- **Privacy:** never a comment (employee, manager or HR), a manager rating or an evidence file. Only the employee code and name identify a person.
- PDF stays the browser's print of the report screens.

## Not done yet / limitations

- Organization, Attendance, Leave and Payroll import/export (planned phases 4–7). **Payroll import scope is still to be agreed.**
- No background jobs or progress for very large files (everything runs in the browser; limits keep it small).
- Zip-bomb protection is limited to the compressed file-size cap; a server must also limit the uncompressed size.
- No permission checks (the permission matrix is only a simulation); exports are not restricted per role. The KPI exports
  are the exception in the demo: they follow the KPI mock's scope, which is decided by the simulated session's role names,
  not by permissions.
- Import history and uploads are kept in memory and cleared on reload (a server would persist batches and keep files in temporary storage).
