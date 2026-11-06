# Excel test files (Employees import)

Use with **Data Exchange → Import Data → Employees** (or the Import button on the Employees list).
Each file was checked against the app's own import engine on 2026-11-04. State resets on reload,
so after a successful import reload the app before the next file.

| File | Strategy | What it tests | Expected result |
|---|---|---|---|
| `1-employees-valid.xlsx` | Insert only | XLS-02, XLS-15, all-or-nothing success | 3 rows valid (EMP061–063). Dates are written three ways: Excel dates, `yyyy-MM-dd` text, `04 Nov 1990` text. Import creates 3 employees, each with a "Joined" history row |
| `2-employees-with-errors.xlsx` | Insert only | XLS-08, 09, 12, 13, 14, 16, EMP-17 | 15 rows: 1 valid, 2 duplicate, 12 invalid (see below). All-or-nothing is unavailable; "Import valid rows only" imports EMP061 only |
| `3-employees-update-only.xlsx` | Update only | EMP-16 (b), XLS-09, XLS-10 | Row 2 EMP005 invalid (manager = itself); row 3 EMP999 invalid (unknown code); row 4 EMP060 valid update (blank optional cells keep their values) |
| `4-employees-bad-headers.xlsx` | Insert only | XLS-06, XLS-07 | Goes to the **column mapping** step: `Last Name` is missing, `Nickname` is unknown, and `Emp Code` is matched through its alias |
| `5-not-really-excel.xlsx` | any | XLS-03 | Refused: "The file is not a valid Excel workbook" |

## File 2, row by row

| Row | Code | Problem | Message |
|---|---|---|---|
| 2 | EMP061 | none | Valid |
| 3 | EMP061 | Same code again | Duplicate of row 2 in this file |
| 4 | EMP005 | Code already exists | A record with this Employee Code already exists |
| 5 | EMP064 | Work email of Daniel Carter | Work email must be unique |
| 6 | EMP065 | Born 2010-01-01 | Employees must be at least 18 years old |
| 7 | EMP066 | Company AHL + branch YGN-HQ | Branch "YGN-HQ" does not exist in company APEX Logistics |
| 8 | EMP067 | Manager EMP999 | Manager "EMP999" does not exist |
| 9 | EMP068 | Resigned, no exit date | An exit date is required for resigned or terminated employees |
| 10 | EMP069 | First Name is a formula | Formulas are not allowed |
| 11 | EMP070 | Gender `Unknown` | Not an allowed value |
| 12 | EMP071 | Phone `abc` | Not a valid phone number |
| 13 | EMP072 | Probation ends before joining | The probation cannot end before the joining date |
| 14 | EMP073 | Joining date typed as text `04/11/2026` | Joining Date is not a valid date (only `yyyy-MM-dd`, `dd MMM yyyy` and real Excel dates are accepted) |
| 15 | EMP074 | Bank account `12ab` | Must be at least 8 characters |
| 16 | EMP075 | Position POS-026 (Warehouse Manager) in Engineering | Position "POS-026" does not exist in department Engineering |

## Notes

- Codes used: company `AHT`/`AHL`, branch `YGN-HQ`/`MDY-BR`, departments `ENG`/`HR`/`SAL`, positions `POS-015` Software Engineer,
  `POS-006` HR Executive, `POS-014` Engineering Manager, `POS-020` Partner Manager, `POS-026` Warehouse Manager, shift `GEN`.
  The template's **Organization codes** sheet lists all of them.
- File 3 overwrites EMP005 and EMP060 with the values in the file (this only affects the in-memory data).
