# E2E Form Example Data: Organization, Employees, Leave

Every field of every form in the three modules, with valid and invalid example values and the message the user sees.

- **Written for:** QA engineers who write Playwright tests in `ui/e2e/`.
- **Source:** the form configs in `ui/src/app/features/` and the rules in `ui/src/app/mock-services/`, `ui/src/app/shared/forms/form-builder.ts` and `ui/src/app/shared/validators/`. Checked on 2026-11-10.
- **Scope:** mock mode. API mode uses the same forms, but the server may return different messages.
- **Columns:** *Req* = required. *Valid* = accepted. *Invalid* = rejected, with the message the user sees.
- **Date input:** users type `dd/mm/yyyy` (for example `12/04/1996`). Stored value is ISO `yyyy-MM-dd`. Examples below show ISO for clarity.
- **Relative dates:** "today" means the run date. Examples that use a fixed date stay valid on any run date.

## 0. Shared rules

These apply to any field with the matching setting.

| Rule | Applies to | Message |
|---|---|---|
| Required | `required: true` | `<Label> is required.` |
| Blank only | Every text and textarea | `<Label> cannot be blank.` (for example `"   "`) |
| Max length | `maxLength` | `<Label> cannot exceed <n> characters.` |
| Min length | `minLength` | `<Label> must be at least <n> characters.` |
| Email format | `email` type | `Enter a valid email address.` |
| Phone format | `tel` type | `Enter a valid phone number (digits, spaces or dashes, optional +).` Accepts `+95 9 123 456 789`. Rejects `abc` and `12`. |
| Code format | `pattern: code` | `Use upper-case letters, numbers and dashes only.` Accepts `HR-TA`, `EMP061`. Rejects `bad code`, `emp061`. |
| Digits only | `pattern: digits` | `<Label> has an invalid format.` |
| Number range | `number` with min/max | `<Label> must be between <min> and <max>.` |
| Not in future | `notInFuture` | `<Label> cannot be in the future.` |
| Minimum age | `minAge: 18` | `Employee must be at least 18 years old.` |
| Date range | Start and end dates | `End date must be on or after the start date.` |
| Duplicate value | Unique fields | `<Label> "<value>" already exists.` and the field shows `<Label> must be unique.` |
| Invalid date text | Date inputs | `Enter a valid date.` |
| **No UI check** | `url` type (Company and Branch website) | The form builder adds no validator for `url`, so any text is accepted. Do not write a test that expects a URL error. Report it as a gap. |

---

## 1. Employees

### 1.1 Employee form (`/employees/new` and Edit)

The form has seven sections. Position, department and branch choices depend on the field above them.

#### Personal information

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| First name | Yes | Max 60 | `Nilar` | blank → `First name is required.`; 61 chars → `First name cannot exceed 60 characters.` |
| Last name | Yes | Max 60 | `Htay` | blank → `Last name is required.` |
| Gender | Yes | Male / Female / Other | `Female` | empty → `Gender is required.` |
| Date of birth | Yes | Not in future; at least 18 years old | `12/04/1996` | `01/01/2015` → `Employee must be at least 18 years old.`; tomorrow → `Date of birth cannot be in the future.` |
| Marital status | Yes | Single / Married / Divorced / Widowed | `Single` | empty → required |
| Nationality | Yes | Max 60 | `Myanmar` | blank → required |
| National ID / passport no. | Yes | Max 40. No format check | `12/KaMaYa(N)123456` (Myanmar NRC); `P12345678` (passport) | blank → required; 41 chars → `National ID / passport no. cannot exceed 40 characters.` |

**DOB boundary (EMP-E10):** let `T` = run date. `T` − 18 years → accepted. `T` − 18 years + 1 day → `Employee must be at least 18 years old.`

#### Contact information

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Work email | Yes | Email format; max 120; must be unique | `nilar.htay@apexhorizon.example` | `nilar.htay` → `Enter a valid email address.`; `daniel.carter@apexhorizon.example` (EMP005) → `Work email "daniel.carter@apexhorizon.example" already exists.` |
| Personal email | No | Email format; max 120 | `nilar.personal@mail.example` | `nilar` → `Enter a valid email address.` |
| Mobile phone | Yes | Phone format | `+95 9 123 456 789` | `abc` → phone message; blank → required |
| Alternate phone | No | Phone format | `+95 1 555 0144` | `12` → phone message |

#### Address information

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Address line 1 | Yes | Max 120 | `No. 5, Parami Road` | blank → required |
| Address line 2 | No | Max 120 | `Floor 3, Room 302` | 121 chars → `Address line 2 cannot exceed 120 characters.` |
| City | Yes | Max 60 | `Yangon` | blank → required |
| State / region | No | Max 60 | `Yangon Region` | — |
| Postal code | No | Max 12 | `11011` | 13 chars → `Postal code cannot exceed 12 characters.` |
| Country | Yes | Max 60 | `Myanmar` | blank → required |

#### Emergency contact

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Contact name | Yes | Max 80 | `Htay Win` | blank → required |
| Relationship | Yes | Spouse / Father / Mother / Sibling / Child / Friend / Other | `Father` | empty → required |
| Contact phone | Yes | Phone format | `+95 9 987 654 321` | `call me` → phone message |

#### Employment information

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Employee code | Yes | Code format; max 12; **locked after creation**; pre-filled with the next code (`EMP061`) | `EMP061` (pre-filled) | `emp061` → `Use upper-case letters, numbers and dashes only.`; `EMP005` → `Employee code "EMP005" already exists.` |
| Employment type | Yes | Full-time / Part-time / Contract / Intern | `Full-time` | empty → required |
| Employment status | Yes | Active / Probation / On Leave / Suspended / Resigned / Terminated | `Probation` (default for new hires) | empty → required |
| Joining date | Yes | Date | `10/11/2026` (run date) | empty → `Joining date is required.` |
| Probation end date | No | On or after joining date | Joining + 3 months (default) | Before joining → `The probation cannot end before the joining date.` |
| Exit date | Required when status is Resigned or Terminated | On or after joining date | `20/11/2026` (only with Resigned) | Status Resigned, empty → `An exit date is required for resigned or terminated employees.`; before joining → `The exit date cannot be before the joining date.` |

#### Organization assignment

Each field depends on the one above it. Changing a parent clears the child.

| Field | Req | Options come from | Valid | Invalid → message |
|---|---|---|---|---|
| Company | Yes | All companies | `APEX Technologies` (AHT) | `APEX Ventures` (AHV, Inactive) → `This company is inactive.` (on save) |
| Branch | Yes | Branches of the company | `Yangon Head Office` (YGN-HQ) | Branch of another company (only via API) → `This branch does not belong to the chosen company.` |
| Department | Yes | Departments of the branch | `Human Resources` (HR) | Department of another branch → `This department does not belong to the chosen branch.` |
| Section | No | Sections of the department | `Talent Acquisition` (HR-TA) or empty | Section of another department → `This section does not belong to the chosen department.` |
| Position | Yes | Positions of the **department** | `HR Executive` (id 6) | Position of another department (only via API) → `This position belongs to a different department.` |
| Job grade | Yes | All grades | `G2 · Associate` (fills from the position) | Empty → required |
| Reporting manager | No | Current employees, excluding this employee | `Su Myat Noe (EMP002)` | Self (only via API) → `An employee cannot be their own manager.` |
| Work shift | No | Active shifts | `General Shift (09:00–18:00)` | Inactive shift (only via API) → `This shift is inactive.` |

Shifts in the seed: `General Shift` 09:00–18:00 · `Warehouse Morning` 06:00–14:30 · `Warehouse Evening` 14:00–22:30 · `Support Rotational` 08:00–17:00 · `Night Shift` 22:00–06:00 (Inactive, so it is not offered).

#### Bank information

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Bank name | Yes | Max 60 | `KBZ Bank` | blank → required |
| Bank branch | No | Max 60 | `Yangon Main Branch` | — |
| Account holder name | Yes | Max 80 | `NILAR HTAY` | blank → required |
| Account number | Yes | Digits only; 8–20 characters | `123456789012` | `12-34` → `Account number has an invalid format.`; `1234567` (7 digits) → `Account number must be at least 8 characters.`; 21 digits → `Account number cannot exceed 20 characters.` |

**Full valid employee (EMP-E13):** First `Nilar`, Last `Htay`, Gender `Female`, DOB `12/04/1996`, Marital `Single`, Nationality `Myanmar`, National ID `12/KaMaYa(N)123456`, Work email `nilar.htay@apexhorizon.example`, Mobile `+95 9 123 456 789`, Address `No. 5, Parami Road`, City `Yangon`, Country `Myanmar`, Contact `Htay Win`, Relationship `Father`, Contact phone `+95 9 987 654 321`, Employee code `EMP061`, Type `Full-time`, Status `Probation`, Joining date (run date), Company `APEX Technologies`, Branch `Yangon Head Office`, Department `Human Resources`, Position `HR Executive`, Bank `KBZ Bank`, Account holder `NILAR HTAY`, Account `123456789012`.

### 1.2 Employee document form (profile → Documents, and the Documents page)

On a profile, the employee field is hidden because the document belongs to that employee.

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Employee | Yes (Documents page only) | Current employees | `Nilar Htay (EMP061)` | empty → required |
| Document type | Yes | National ID / Passport / Resume / Offer Letter / Employment Contract / Certificate / Work Permit / Medical Report / Other | `Employment Contract` | empty → required |
| Title | Yes | Max 80 | `Contract 2026` | blank → required |
| File | Yes | PDF, Word, PNG or JPG; up to 5 MB. File name only is stored | `contract-2026.pdf` (PDF, 200 KB) | `virus.exe` → `Allowed file types: PDF, Word, PNG or JPG.`; 6 MB file → `The file is larger than 5 MB.` |
| Uploaded on | Yes | Not in future | Run date | Tomorrow → `Uploaded on cannot be in the future.` |
| Expiry date | No | Date | `30/06/2027` for Work Permit; empty for Passport | — |

---

## 2. Organization

### 2.1 Company

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Company code | Yes | Code format; max 10; locked after creation | `BNC` | `bad code` → code message; `AHT` → `Company code "AHT" already exists.` |
| Company name | Yes | Max 100; unique | `Brand New Co` | `APEX Technologies` → `Company name "APEX Technologies" already exists.` |
| Legal name | Yes | Max 150 | `Brand New Co Ltd` | blank → required |
| Registration number | No | Max 40 | `REG-2026-000111` | — |
| Tax ID | No | Max 40 | `TIN-100-999-001` | — |
| Industry | Yes | Max 80 | `E-commerce` | blank → required |
| Founded date | Yes | Not in future | `01/05/2020` | Tomorrow → `Founded date cannot be in the future.` |
| Email | Yes | Email format; max 120 | `hello@brandnew.example` | `hello` → `Enter a valid email address.` |
| Phone | Yes | Phone format | `+95 1 555 0999` | `abc` → phone message |
| Website | No | Max 150. **No format check (gap)** | `https://brandnew.example` | Any text is accepted |
| Address | No | Max 200 | `No. 9, Bogyoke Road` | — |
| City | Yes | Max 60 | `Yangon` | blank → required |
| Country | Yes | Max 60 | `Myanmar` | blank → required |
| Status | Yes | Active / Inactive | `Active` | empty → required |

### 2.2 Branch

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Company | Yes | All companies | `APEX Technologies` | empty → required |
| Branch code | Yes | Code format; max 12; locked after creation | `BNC-HQ` | `YGN-HQ` → `Branch code "YGN-HQ" already exists.` |
| Branch name | Yes | Max 100 | `Brand New Branch` | blank → required |
| Branch type | Yes | Head Office / Branch / Regional Office / Warehouse | `Branch` | empty → required |
| Email | No | Email format; max 120 | `branch@brandnew.example` | `branch` → `Enter a valid email address.` |
| Phone | No | Phone format | `+95 1 555 0198` | `12` → phone message |
| Address | No | Max 200 | `No. 22, Pyay Road` | — |
| City | Yes | Max 60 | `Mandalay` | blank → required |
| Country | Yes | Max 60 | `Myanmar` | blank → required |
| Branch manager | No | Current employees | `Su Myat Noe (EMP002)` or empty | — |
| Opened date | Yes | Not in future | `15/01/2024` | Tomorrow → `Opened date cannot be in the future.` |
| Status | Yes | Active / Inactive | `Active` | empty → required |

### 2.3 Department

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Company | Yes | All companies | `APEX Technologies` | empty → required |
| Branch | Yes | Branches of the company | `Yangon Head Office` | Choose a company first (the list is empty) |
| Department code | Yes | Code format; max 10; locked after creation | `LEG` | `HR` → `Department code "HR" already exists.` |
| Department name | Yes | Max 100 | `Legal Affairs` | blank → required |
| Department head | No | Current employees | `Su Myat Noe (EMP002)` or empty | — |
| Cost center | No | Max 20 | `CC-LEG-001` | 21 chars → `Cost center cannot exceed 20 characters.` |
| Description | No | Max 300 | `Contracts and compliance.` | 301 chars → `Description cannot exceed 300 characters.` |
| Status | Yes | Active / Inactive | `Active` | empty → required |

### 2.4 Section

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Department | Yes | All departments | `Engineering` (ENG) | empty → required |
| Section code | Yes | Code format; max 12; locked after creation | `ENG-DEV` | `ENG-FE` → `Section code "ENG-FE" already exists.` |
| Section name | Yes | Max 100 | `DevOps` | blank → required |
| Supervisor | No | Current employees | `Daniel Carter (EMP005)` | — |
| Description | No | Max 300 | `Build and release pipelines.` | — |
| Status | Yes | Active / Inactive | `Active` | empty → required |

### 2.5 Position

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Department | Yes | All departments | `Engineering` (ENG) | empty → required |
| Section | No | Sections of the department | `Backend Development` (ENG-BE) or empty | — (only sections of the chosen department are offered) |
| Position code | Yes | Code format; max 12; locked after creation | `POS-NEW-01` | `POS-NEW 01` → code message |
| Position title | Yes | Max 100 | `DevOps Engineer` | blank → required |
| Job grade | Yes | All grades | `G3 · Professional` | empty → required |
| Approved headcount | Yes | Whole number, 1–500 | `2` | `0` → `Approved headcount must be between 1 and 500.`; `501` → same; empty → required |
| Reports to | No | Other positions (not this one) | `Engineering Manager (Engineering)` or empty | Itself (only via API) → `A position cannot report to itself.` |
| Status | Yes | Active / Inactive | `Active` | empty → required |
| Description | No | Max 300 | `Builds and runs release pipelines.` | — |

### 2.6 Job grade

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Grade code | Yes | Code format; max 6; locked after creation | `G10` | `G1` → `Grade code "G1" already exists.` |
| Grade name | Yes | Max 60 | `Expert` | blank → required |
| Level | Yes | Whole number 1–20 (1 = most junior) | `10` | `0` or `21` → `Level must be between 1 and 20.` |
| Status | Yes | Active / Inactive | `Active` | empty → required |
| Minimum monthly salary (sample) | Yes | 0 to 1,000,000,000 | `1500000` | `-1` → `Minimum monthly salary (sample) must be between 0 and 1000000000.` |
| Maximum monthly salary (sample) | Yes | 0 to 1,000,000,000; ≥ minimum | `2500000` | Max below min (for example min 700000, max 650000) → `Maximum must be greater than or equal to minimum.` |
| Description | No | Max 300 | `Senior specialists with 10+ years.` | — |

**Boundary (ORG-E18):** min `400000`, max `400000` → accepted. Max `399999` → rejected with the min/max message.

---

## 3. Leave

### 3.1 Leave type

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Code | Yes | Code format; max 6; locked after creation | `STD` | `AL` → `Leave type code "AL" already exists.` |
| Leave type name | Yes | Max 60 | `Study Leave` | blank → required |
| Description | No | Max 250 | `Exams and study programmes.` | — |
| Default days per year | Yes | 0–365 | `5` | `-1` or `366` → `Default days per year must be between 0 and 365.` |
| Maximum consecutive days | Yes | 1–365 | `5` | `0` → `Maximum consecutive days must be between 1 and 365.` |
| Eligibility | No | Women only / Men only / empty = everyone | empty | — |
| Calendar colour | No | Colour picker | `#2563eb` | — |
| Paid leave | No | Toggle | On | — |
| Supporting document required | No | Toggle | On for Medical | — |
| Unused days carry forward | No | Toggle | Off | — |
| Maximum carry-forward days | Yes | 0–365; must not exceed the yearly days | `0` | `10` with 5 yearly → `Carry-forward days cannot exceed the yearly entitlement.` |
| Status | Yes | Active / Inactive | `Active` | empty → required |

**Seeded types for reference:** Annual 15 (max 10 consecutive, carry-forward 5) · Casual 6 (max 3) · Medical 30 (document) · Maternity 98 (women only, document) · Paternity 15 (men only, document) · Unpaid 30 · Compassionate 5.

### 3.2 Leave policy

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Policy name | Yes | Max 80; unique | `Annual leave — team leads (G5)` | Existing name → `Policy name "…" already exists.` |
| Leave type | Yes | All types | `Annual Leave` | empty → required |
| Days per year | Yes | 0–365 | `20` | `400` → `Days per year must be between 0 and 365.` |
| Job grade | No | Empty = all grades | `G5 · Lead / Supervisor` | — |
| Employment type | No | Empty = all types | `Full-time` | — |
| Accrual | Yes | Upfront (full amount at the start of the year) / Monthly (accrues monthly) | `Upfront` | empty → required |
| Minimum service (months) | Yes | 0–120 | `3` | `121` → `Minimum service (months) must be between 0 and 120.` |
| Status | Yes | Active / Inactive | `Active` | empty → required |

**Clash:** a second active policy for the same leave type, grade and employment type → `An active policy already exists for this leave type, grade and employment type.` (LV-E05: Annual, grade empty, type empty.)

### 3.3 Leave application

| Field | Req | Rules | Valid | Invalid → message |
|---|---|---|---|---|
| Employee | Yes | All employees | `Hnin Ei Phyu (EMP006)` | empty → required |
| Leave type | Yes | Active types; women see Maternity, men see Paternity | `Medical Leave` for EMP006 | Paternity shown for a woman → not offered (the list is filtered) |
| Half day | No | Toggle; single date only | On with start = end | On with different dates → `A half day must start and end on the same date.` |
| Start date | Yes | Not before the employee's joining date | `01/03/2027` | Before joining (for example EMP050 Rahul Sharma: start 20 days before his joining date) → `Leave cannot start before the joining date.` |
| End date | Yes | On or after start; within the type's max consecutive days | `02/03/2027` | Before start (`20/02/2027` with start `01/03/2027`) → `End date must be on or after the start date.`; longer than max consecutive → `At most <n> days at a time.` |
| Number of days | Read-only | Working days only; half day = 0.5 | `2` for Mon–Tue | Weekend-only range → `The selected dates contain no working days.` |
| Reason | Yes | Max 250 | `Doctor visit` | blank → required; 251 chars → `Reason cannot exceed 250 characters.` |
| Supporting document | Required before submit for types that need one | PDF / Word / PNG / JPG (demo: name only, not stored) | `medical-cert.pdf` | Submit without it → `A supporting document is required.` |

**Working-day rules:** weekends and holidays are excluded. Holidays in the seed run until `2027-01-04`, so March 2027 is clean.

**Other save-time rules**

| Case | Message |
|---|---|
| Overlap with a **Submitted or Approved** application for the same employee (Drafts and Rejected do not block) | `These dates overlap LV-YYYY-NNNN (<start> – <end>).` |
| Casual Leave above 3 consecutive days | `At most 3 days at a time.` |
| Submit when balance is short | `Insufficient <type> balance: <requested> day(s) requested, <available> available.` |
| Edit or delete a Submitted or Approved application | `Only draft applications can be changed. Cancel a submitted one instead.` |

**Decision dialogs**

| Action | Prompt | Required | Valid | Invalid → message |
|---|---|---|---|---|
| Submit for approval | Confirm only | — | Confirm | — |
| Approve | Remarks | No | `Approved for the family trip.` (default `Approved.` if empty) | — |
| Reject | Reason for rejection | **Yes** | `Overlaps the project deadline.` | Empty → `A reason is required.` |
| Decide own request | — | — | — | Approve as the requester → `You cannot decide your own leave request.` |
| Cancel leave | Reason | No | `Plans changed.` (default `Cancelled.`) | Started leave → `Leave that has already started cannot be cancelled.` |

**Status values:** Draft · Submitted · Approved · Rejected · Cancelled.

---

## 4. Filter forms (no data is saved)

Filters on list and calendar screens. Filters combine with AND. Each applied filter shows a removable chip, and "Clear filters" resets them all. Filters are not validated like forms: an empty value means "All".

### 4.1 Employee list (`/employees/list`)

| Filter | Control | Values | Example | Expected |
|---|---|---|---|---|
| Search | Search box | Employee code or name | `EMP005` or `Daniel` | Matching rows only |
| Joined from | Date input | `dd/mm/yyyy` (valid date only) | `01/01/2014` | Filters once the date is valid |
| Joined to | Date input | `dd/mm/yyyy`; days before "Joined from" are disabled | `31/12/2015` | 3 rows (see EMP-E02) |
| Status | Select | All · Active · Probation · On Leave · Suspended · Resigned · Terminated | `Probation` | Probation employees only (count depends on run date) |
| Department | Select | All + 11 departments | `Engineering` | Engineering staff (e.g. EMP005, EMP017, EMP046) |
| Branch | Select | All + 6 branches | `Yangon Head Office` | Staff at YGN-HQ |
| Employment type | Select | All · Full-time · Part-time · Contract · Intern | `Contract` | Contract staff (e.g. EMP038, EMP044, EMP054) |

Combined example: Department `Engineering` + Status `Probation` → Engineering staff on probation only (run-date dependent).

### 4.2 Leave applications (`/leave/applications`)

| Filter | Control | Values | Example |
|---|---|---|---|
| Employee | Select | All employees, shown as `Name (EMP code)` | `Hnin Ei Phyu (EMP006)` |
| Leave type | Select | All + 7 types | `Medical Leave` |
| Status | Select | All · Draft · Submitted · Approved · Rejected · Cancelled | `Submitted` |
| Leave from / Leave to | Date inputs | `dd/mm/yyyy`; matches applications that overlap the range | `01/03/2027` – `31/03/2027` |

**Pending approvals** screen: the status is locked to Submitted. Filters are Employee and Leave type only.
**Decision history** screen: the status is limited to Approved, Rejected and Cancelled. Filters are Employee, Leave type and Status.

### 4.3 Leave balances (`/leave/balances`)

| Filter | Control | Values | Example |
|---|---|---|---|
| Year | Select | Current year and the previous year (for example 2026 and 2025 in 2026) | Previous year |
| Department | Select | All + departments | `Human Resources` |
| Branch | Select | All + branches | `Yangon Head Office` |

Choosing a year reloads the balances for that year. Opening a row shows the detail dialog for the selected year.

### 4.4 Leave calendar (`/leave/calendar`)

| Control | Values | Default | Example |
|---|---|---|---|
| Previous month / Next month | Buttons | — | Move to `March 2027` |
| Include pending | Toggle | **On** | Off → only Approved leave shows; On → Submitted leave also shows |
| Department | Select | All | `Engineering` |
| Leave type | Select | All | `Annual Leave` |

### 4.5 Organization lists

| Screen | Search label | Other filters |
|---|---|---|
| Companies | Search by code, name or city | Status (Active / Inactive) |
| Branches | Search by code, name or city | — |
| Departments | Search by code, name or cost center | Company (only that company's departments) |
| Sections | Search by code or name | — |
| Positions | Search by code or title | — |
| Job grades | Search by code or name | — |

Example: Companies, search `logistics` and Status `Inactive` → no rows. Clear the status to see `APEX Logistics`.

### 4.6 Reporting structure

No form. The screen has a search box (example `Thura`) that highlights a person in the chart.

## 5. Gaps to report

1. **Website (url):** no format check in the UI. A test that expects an error would fail.
2. **Document upload:** the file is checked by name and size only, not stored.
3. **Server-only messages:** rules marked "only via API" (cross-company branch, inactive shift, self-manager, self-report) cannot be reached from the mock UI because the picker filters those options. Test them in API mode or through unit tests.
4. **Exact message text:** the messages above come from the code. Compare with the rendered UI in your first run.
