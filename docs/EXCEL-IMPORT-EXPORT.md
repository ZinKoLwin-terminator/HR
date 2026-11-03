# Excel import and export

A reusable import/export capability shared by all modules. **Status:** the shared foundation and the
**Employees** module are implemented; Organization, Attendance, Leave and Payroll are planned.

> Frontend-only: there is no backend. Spreadsheets are read and written **in the browser** behind the
> same data-service contracts a real API would implement (see [API-INTEGRATION-PLAN.md](API-INTEGRATION-PLAN.md)).
> The browser is **not a security boundary** — real enforcement (limits, content checks, permissions,
> audit) must be repeated on a server.

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

## Not done yet / limitations

- Organization, Attendance, Leave and Payroll import/export (planned phases 4–7). **Payroll import scope is still to be agreed.**
- No background jobs or progress for very large files (everything runs in the browser; limits keep it small).
- Zip-bomb protection is limited to the compressed file-size cap; a server must also limit the uncompressed size.
- No permission checks (the permission matrix is only a simulation); exports are not restricted per role.
- Import history and uploads are kept in memory and cleared on reload (a server would persist batches and keep files in temporary storage).
