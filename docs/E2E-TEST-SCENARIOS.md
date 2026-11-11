# End-to-End Test Scenarios: All Modules

Playwright scenarios (`ui/e2e/`) for every module of APEX HRMS: sign-in and session, **Organization**, **Employees**, **Leave**, **Attendance**, **Recruitment**, **Settings**, **Dashboard** (with activity and notifications), **Excel** import and export, **Payroll**, and cross-module (X) scenarios.
Each scenario lists its example data, the steps and the expected result.

- **Written for:** QA engineers who write and maintain the Playwright suite.
- **Status column:** `Automated` = already covered by a test in `ui/e2e/` (name given); `Partly automated` = a test covers part of the scenario; `New` = not yet automated; `Unit test` = covered by a Vitest spec, not Playwright; `Confirm against product intent` = the code and the expected behaviour may differ, or the rule is unclear. The scenario is written from the code and must be confirmed before it is asserted.
- **Scope:** Playwright runs the mock build in the `e2e` configuration (sample data, no persistence). Rows marked **API** need the running API and a local database, so they are not in `ui/e2e/` and are run separately.
- **Type:** `+` happy path, `−` rule must reject, `B` boundary, `X` cross-module.
- **Data source:** every record in section 1 was checked against `ui/src/app/mock-data/` on 2026-11-10. Items marked *relative* move with the run date.

## 0. Ground rules for the suite

| Rule | Why |
|---|---|
| Sign in through the fixture (`test` from `ui/e2e/fixtures.ts`). Every test starts as **Su Myat Noe** (EMP002, HR Administrator). | Pages sit behind the login guard. |
| Navigate inside the app for data that must persist (use `searchEmployeesInApp` and the sidebar). Use `page.goto` only for a fresh start. | A `page.goto` reloads the page and resets the in-memory data. |
| Leave dates use **2027** (for example `2027-03-01`). The seeded public holidays end on `2027-01-04`, so March 2027 has no holidays. | Dates in the past, weekends and holidays change the results. |
| Organization CRUD runs on desktop only. Use `test.skip(({ isMobile }) => isMobile, …)` as the existing specs do. | Matches the existing specs. |
| Create test records with a code that no other test uses (for example `BNC`, `STD`). Delete what you create. | Each test should not depend on another test's leftovers. |
| Build and data: `npm start -- --configuration e2e` (set in `ui/playwright.config.ts`) replaces `mock-data-options.ts` with `mock-data-options.e2e.ts`: sample data, no browser persistence. The default dev build starts with empty lists and keeps changes in the browser. | Seed counts in section 1 apply to the e2e build only. Each new page starts from the seed. |
| Sign-in: the mock build needs no real sign-in, because the fixture writes the session to `sessionStorage`. The API build (`api` configuration) requires a real sign-in with a cookie session, and its scenarios are in section 8 (**API**). | The mock build does not check passwords beyond the demo password. |
| Use `page.clock` to fix the browser time for attendance punches, late and half-day checks, and recruitment dates that depend on today. | The check-in punch uses the browser clock, so the result depends on the time of the run. |

## 1. Example data

### 1.0 Seed counts

| Area | Count | Notes |
|---|---|---|
| Companies | 3 | AHT, AHL, AHV (AHV is Inactive) |
| Branches | 6 | AHT has 4, AHL has 2 |
| Departments | 11 | Department ids 1–11 |
| Sections | 16 | |
| Positions | 33 | |
| Job grades | 9 | G1–G9 |
| Employees | 60 | EMP001–EMP060 (employee id = EMP number) |
| Leave types | 7 | AL, CL, ML, MAT, PAT, UL, CPL |
| Leave policies | 9 | |
| Leave applications | 29 | Dates are relative to today |
| Public holidays | up to 2027-01-04 | |
| Shifts | 5 | GEN, WH-AM, WH-PM, SUP (Active); NIGHT (Inactive) |
| Holidays | 15 | 2026-01-01 to 2027-01-04 (sample calendar) |
| Job openings | 8 | Recruitment seed |
| Candidates | 22 | Emails are unique |
| Applications | 22 | Recruitment seed |
| Interviews | 14 | Recruitment seed |
| Roles | 7 | HR Administrator and Employee are built in; Auditor is Inactive |
| User accounts | 11 | One Locked (EMP012), one Inactive (EMP026) |
| Salary components | 8 | HOUSING, TRANSPORT, MEAL, PERFORMANCE, EXECUTIVE, TAX, SOCIAL, PENSION |
| Salary structures | 3 | STAFF, MGMT, EXEC |
| Payroll periods | 7 | Six Paid months and the current month as Draft |

Attendance records, adjustments and overtime are generated from the seed, so their counts are not fixed here. The seed counts apply to the `e2e` build (section 0).

### 1.1 Organization

**Companies**

| Code | Name | Id | Status | Industry | Founded | City | Country |
|---|---|---|---|---|---|---|---|
| `AHT` | APEX Technologies | 1 | Active | Information Technology | 2014-03-12 | Yangon | Myanmar |
| `AHL` | APEX Logistics | 2 | Active | Logistics & Supply Chain | 2018-07-01 | Mandalay | Myanmar |
| `AHV` | APEX Ventures | 3 | **Inactive** | Investment Holding | 2021-02-15 | Singapore | Singapore |

| Code | Legal name | Registration no. | Tax ID | Email | Phone | Website | Address |
|---|---|---|---|---|---|---|---|
| `AHT` | APEX Technologies Co., Ltd. | `REG-2014-001872` | `TIN-100-245-881` | `info@apexhorizon.example` | `+95 1 555 0100` | `https://apexhorizon.example` | No. 88, Pyay Road, Kamayut Township |
| `AHL` | APEX Logistics Co., Ltd. | `REG-2018-004410` | `TIN-100-377-120` | `logistics@apexhorizon.example` | `+95 2 555 0200` | `https://logistics.apexhorizon.example` | Plot 12, Industrial Zone 2, Pyigyitagun Township |
| `AHV` | APEX Ventures Pte. Ltd. | `UEN-202110455K` | `GST-M9-0011234-5` | `ventures@apexhorizon.example` | `+65 6555 0300` | `https://ventures.apexhorizon.example` | 1 Raffles Place, #20-01 |

**Branches**

All branches are Active.

| Company | Code | Name | Type | City | Country | Manager | Opened |
|---|---|---|---|---|---|---|---|
| AHT | `YGN-HQ` | Yangon Head Office | Head Office | Yangon | Myanmar | Thura Aung (EMP001) | 2014-03-12 |
| AHT | `MDY-BR` | Mandalay Branch | Branch | Mandalay | Myanmar | Min Khant Zaw (EMP007) | 2017-09-04 |
| AHT | `NPT-BR` | Nay Pyi Taw Office | Branch | Nay Pyi Taw | Myanmar | Shwe Sin Oo (EMP026) | 2019-05-20 |
| AHT | `SG-RO` | Singapore Regional Office | Regional Office | Singapore | Singapore | Sophia Bennett (EMP011) | 2022-04-01 |
| AHL | `MDY-DC` | Mandalay Distribution Center | Warehouse | Mandalay | Myanmar | Zaw Lin Oo (EMP012) | 2018-07-01 |
| AHL | `YGN-LH` | Yangon Logistics Hub | Warehouse | Yangon | Myanmar | Nay Myo Aung (EMP040) | 2020-01-15 |

| Code | Email | Phone | Address |
|---|---|---|---|
| `YGN-HQ` | `hq@apexhorizon.example` | `+95 1 555 0101` | No. 88, Pyay Road, Kamayut Township |
| `MDY-BR` | `mandalay@apexhorizon.example` | `+95 2 555 0111` | 78th Street, Chanayethazan Township |
| `NPT-BR` | `naypyitaw@apexhorizon.example` | `+95 67 555 0121` | Thapyay Kone Ward, Zabuthiri Township |
| `SG-RO` | `singapore@apexhorizon.example` | `+65 6555 0131` | 10 Anson Road, #15-04 |
| `MDY-DC` | `mdy-dc@apexhorizon.example` | `+95 2 555 0211` | Plot 12, Industrial Zone 2, Pyigyitagun Township |
| `YGN-LH` | `ygn-hub@apexhorizon.example` | `+95 1 555 0221` | Shwe Pyi Thar Industrial Zone 4 |

**Departments (all 11, all Active)**

The cost center is `CC-` plus the department id × 100, in four digits.

| Code | Name | Company / branch | Head | Cost center | Description | Use |
|---|---|---|---|---|---|---|
| `EXEC` | Executive Office | AHT / Yangon Head Office | Thura Aung (EMP001) | `CC-0100` | Corporate leadership and strategy. | |
| `HR` | Human Resources | AHT / Yangon Head Office | Su Myat Noe (EMP002) | `CC-0200` | People operations, talent, payroll and employee relations. | In use, so delete is refused (ORG-E15); employee create |
| `FIN` | Finance & Accounts | AHT / Yangon Head Office | May Thu Kyaw (EMP004) | `CC-0300` | Accounting, treasury, budgeting and reporting. | |
| `ENG` | Engineering | AHT / Yangon Head Office | Kyaw Zin Htet (EMP003) | `CC-0400` | Software design, development and quality assurance. | Sections `ENG-FE`, `ENG-BE`, `ENG-QA` |
| `PRD` | Product Management | AHT / Yangon Head Office | Ei Mon Kyaw (EMP010) | `CC-0500` | Product strategy, roadmap and UX design. | |
| `SAL` | Sales & Business Development | AHT / Mandalay Branch | Min Khant Zaw (EMP007) | `CC-0600` | Enterprise sales and partner channels. | Branch dropdown checks |
| `IT` | IT Infrastructure | AHT / Yangon Head Office | Ye Yint Naing (EMP009) | `CC-0700` | Networks, security and internal IT support. | |
| `CS` | Customer Support | AHT / Nay Pyi Taw Office | Shwe Sin Oo (EMP026) | `CC-0800` | Customer help desk and service quality. | Section `CS-T1` Tier 1 Support belongs here |
| `OPS` | Warehouse Operations | AHL / Mandalay Distribution Center | Zaw Lin Oo (EMP012) | `CC-0900` | Inbound and outbound warehouse operations. | Filter by company (ORG-E13) |
| `LOG` | Fleet & Logistics | AHL / Yangon Logistics Hub | Nay Myo Aung (EMP040) | `CC-1000` | Fleet scheduling, dispatch and delivery. | Filter by company (ORG-E13) |
| `MKT` | Marketing | AHT / Singapore Regional Office | Sophia Bennett (EMP011) | `CC-1100` | Brand, digital marketing and communications. | Deactivation tests (ORG-E16, X-03) |

**Positions (selected)**

All are Active. The code is `POS-` plus the id in three digits. The description is the title plus " role."

| Id | Code | Title | Department | Section | Job grade | Headcount | Reports to |
|---|---|---|---|---|---|---|---|
| 6 | `POS-006` | HR Executive | HR | Employee Relations (`HR-ER`) | G2 | 2 | HR Manager |
| 3 | `POS-003` | HR Manager | HR | Employee Relations (`HR-ER`) | G6 | 1 | Director of Human Resources |
| 15 | `POS-015` | Software Engineer | ENG | Backend Development (`ENG-BE`) | G3 | 6 | Engineering Manager |
| 12 | `POS-012` | Senior Backend Engineer | ENG | Backend Development (`ENG-BE`) | G4 | 2 | Engineering Manager |
| 14 | `POS-014` | Engineering Manager | ENG | none | G6 | 1 | Head of Engineering |
| 25 | `POS-025` | Customer Support Agent | CS | Tier 1 Support (`CS-T1`) | G1 | 6 | Customer Support Lead |

**Job grades (salary bands, MMK per month)**

All are Active. The level equals the id.

| Grade | Name | Level | Min | Max | Description |
|---|---|---|---|---|---|
| G1 | Entry Level | 1 | 400,000 | 700,000 | Interns, trainees and entry-level operational roles. |
| G2 | Associate | 2 | 600,000 | 1,000,000 | Junior individual contributors. |
| G3 | Professional | 3 | 900,000 | 1,500,000 | Experienced individual contributors. |
| G4 | Senior Professional | 4 | 1,400,000 | 2,200,000 | Senior specialists and subject-matter experts. |
| G5 | Lead / Supervisor | 5 | 2,000,000 | 3,000,000 | Team leads and first-line supervisors. |
| G6 | Manager | 6 | 2,800,000 | 4,200,000 | Department and functional managers. |
| G7 | Senior Manager | 7 | 4,000,000 | 6,000,000 | Senior managers with multiple teams. |
| G8 | Director | 8 | 5,500,000 | 8,500,000 | Heads of business functions. |
| G9 | Executive | 9 | 8,000,000 | 14,000,000 | C-level executives. |

The seed stores the full kyat amounts. Check how the UI formats them before you assert on a displayed value.

### 1.2 Employees

The work email is the first and last name with spaces removed, in lower case, plus `@apexhorizon.example`. Probation status depends on the run date (the seed sets it from the joining date plus 3 months).

| ID | Name | Gender | Type | Status | Position (grade) / department | Joined | Work email | Use |
|---|---|---|---|---|---|---|---|---|
| EMP001 | Thura Aung | Male | Full-time | Active | Chief Executive Officer (G9) / Executive Office | 2014-03-12 | `thura.aung@apexhorizon.example` | Has 6 direct reports, so delete is refused (EMP-E07) |
| EMP002 | Su Myat Noe | Female | Full-time | Active | Director of Human Resources (G8) / HR | 2015-06-01 | `sumyat.noe@apexhorizon.example` | **Signed-in user**, HR Administrator |
| EMP005 | Daniel Carter | Male | Full-time | Active | Engineering Manager (G6) / ENG | 2018-04-16 | `daniel.carter@apexhorizon.example` | Duplicate work email (EMP-E15) |
| EMP006 | Hnin Ei Phyu | Female | Full-time | Active | HR Manager (G6) / HR | 2017-02-20 | `hninei.phyu@apexhorizon.example` | Leave applicant. Annual entitlement 18 (grade G6 policy) |
| EMP017 | Phyu Sin Thant | Female | Full-time | Active | QA Engineer (G3) / ENG | 2020-06-15 | `phyusin.thant@apexhorizon.example` | Has a **Submitted** Annual leave that starts in 3 days (*relative*). Approval test data |
| EMP026 | Shwe Sin Oo | Female | Full-time | Active | Customer Support Lead (G5) / CS | 2019-05-20 | `shwesin.oo@apexhorizon.example` | Has a **Submitted** Annual leave that starts in 8 days (*relative*). Approval test data |
| EMP032 | Emily Wong | Female | Full-time | Active | Digital Marketing Specialist (G3) / MKT | 2023-01-09 | `emily.wong@apexhorizon.example` | Existing Marketing employee (X-03) |
| EMP034 | Soe Moe Kyaw | Male | Full-time | **Resigned** | Warehouse Associate (G1) / OPS | 2021-03-15 | `soemoe.kyaw@apexhorizon.example` | Exit date 2026-08-31 |
| EMP035 | Aye Chan Moe | Female | Full-time | **On Leave** | Account Executive (G3) / SAL | 2022-10-03 | `ayechan.moe@apexhorizon.example` | On Leave status |
| EMP038 | Bo Bo Han | Male | **Contract** | Active | Delivery Driver (G1) / LOG | 2022-02-14 | `bobo.han@apexhorizon.example` | Has a Rejected Annual leave (*relative*) |
| EMP042 | Ko Ko Gyi | Male | Full-time | **Suspended** | Warehouse Associate (G1) / OPS | 2023-05-22 | `koko.gyi@apexhorizon.example` | Suspended status |
| EMP044 | Tun Tun Naing | Male | **Contract** | **Terminated** | Delivery Driver (G1) / LOG | 2023-11-06 | `tuntun.naing@apexhorizon.example` | Exit date 2026-06-15 |
| EMP046 | Hein Thant Zin | Male | Full-time | Active | Software Engineer (G3) / ENG | 2024-09-09 | `heinthant.zin@apexhorizon.example` | Promotion test (EMP-E18). Has an Approved Annual leave (*relative*) |
| EMP050 | Rahul Sharma | Male | Full-time | **Probation** | Software Engineer (G3) / ENG | *relative*: 12 days before today | `rahul.sharma@apexhorizon.example` | Under 3 months' service. Annual entitlement 0 (LV-E16) |
| EMP054 | Zarni Tun | Male | **Contract** | Active | Delivery Driver (G1) / LOG | 2025-08-18 | `zarni.tun@apexhorizon.example` | Resign without an exit date is refused (EMP-E17) |
| EMP056 | Sai Htoo Aung | Male | Full-time | Active | IT Support Technician (G2) / IT | 2026-01-12 | `saihtoo.aung@apexhorizon.example` | No seeded leave. Clean record for balance tests |
| EMP058 | Chan Myae Aung | Male | **Intern** | **Probation** | Engineering Intern (G1) / ENG | *relative*: 20 days before today | `chanmyae.aung@apexhorizon.example` | Intern policy applies (6 days) |
| EMP060 | Andrew Lim | Male | Full-time | **Probation** | Partner Manager (G4) / SAL | *relative*: 50 days before today | `andrew.lim@apexhorizon.example` | No direct reports, so safe to delete (EMP-E06) |
| New | **Nilar Htay** | Female | Full-time | **Probation** | HR Executive (G2) / HR | Today | `nilar.htay@apexhorizon.example` | Created by the test. Gets code `EMP061` (the next free code) |

The other personal fields of the seeded employees (date of birth, marital status, nationality, phone, national ID, address, emergency contact, bank) are generated. Read them from the app, not from this document.

Other seeded employees include Ingyin Thwe (EMP053, intern, *relative*), Nang Hseng Hom (EMP057, *relative*) and Phyu Sin Thant (EMP017).

### 1.3 Leave

**Leave types**

All are Active.

| Code | Name | Default days | Max consecutive | Gender | Document | Paid | Carry forward | Max carry | Colour |
|---|---|---|---|---|---|---|---|---|---|
| `AL` | Annual Leave | 15 | 10 | — | No | Yes | **Yes** | 5 | `#3f6fd8` |
| `CL` | Casual Leave | 6 | 3 | — | No | Yes | No | 0 | `#0f9d76` |
| `ML` | Medical Leave | 30 | 30 | — | **Yes** | Yes | No | 0 | `#d84a3f` |
| `MAT` | Maternity Leave | 98 | 98 | Female only | **Yes** | Yes | No | 0 | `#c2408f` |
| `PAT` | Paternity Leave | 15 | 15 | Male only | **Yes** | Yes | No | 0 | `#8a4fd1` |
| `UL` | Unpaid Leave | 30 | 30 | — | No | **No** | No | 0 | `#6b7280` |
| `CPL` | Compassionate Leave | 5 | 5 | — | No | Yes | No | 0 | `#c77700` |

| Code | Description |
|---|---|
| `AL` | Paid vacation leave accrued annually. |
| `CL` | Short paid leave for personal matters. |
| `ML` | Paid sick leave. Medical certificate required beyond 2 days. |
| `MAT` | Leave for childbirth and post-natal care. |
| `PAT` | Leave for fathers following the birth of a child. |
| `UL` | Leave without pay, subject to management approval. |
| `CPL` | Paid leave on bereavement of an immediate family member. |

**Leave policies (all Active)**

Job grade and employment type are empty when the scope is "All".

| Policy | Leave type | Job grade | Employment type | Days / year | Accrual | Min. service (months) | Status |
|---|---|---|---|---|---|---|---|
| Annual leave — standard (after 3 months) | AL | — | — | 15 | Upfront | 3 | Active |
| Annual leave — managers | AL | G6 | — | 18 | Upfront | 0 | Active |
| Annual leave — senior managers | AL | G7 | — | 20 | Upfront | 0 | Active |
| Annual leave — directors | AL | G8 | — | 22 | Upfront | 0 | Active |
| Annual leave — executives | AL | G9 | — | 25 | Upfront | 0 | Active |
| Annual leave — contract staff | AL | — | Contract | 10 | Upfront | 0 | Active |
| Annual leave — interns | AL | — | Intern | 6 | Monthly | 0 | Active |
| Casual leave — all staff (monthly accrual) | CL | — | — | 6 | Monthly | 0 | Active |
| Medical leave — interns | ML | — | Intern | 10 | Upfront | 0 | Active |

**Seeded applications used by the scenarios**

The end date is the start date plus the span minus 1. The working days, applied on, decided by and remarks are derived by the app. None is a half day.

| Employee | Type | Start | Span (calendar days) | Status | Reason |
|---|---|---|---|---|---|
| EMP017 Phyu Sin Thant | AL | +3 days (*relative*) | 3 | Submitted | Short holiday at Ngapali beach. |
| EMP026 Shwe Sin Oo | AL | +8 days (*relative*) | 5 | Submitted | Year-end family trip. |
| EMP038 Bo Bo Han | AL | −20 days (*relative*) | 3 | Rejected | Personal travel. |
| EMP046 Hein Thant Zin | AL | −6 days (*relative*) | 1 | Approved | Food poisoning. |
| EMP049 Yoon Wadi Kyaw | AL | +20 days (*relative*) | 3 | Draft | Planning a short trip. |

**Test dates for new applications:** EMP006 Hnin Ei Phyu, `2027-03-01` to `2027-03-02` (Mon–Tue), reason `Doctor visit`. Weekend-only dates: `2027-03-06` to `2027-03-07` (Sat–Sun).

### 1.4 Every field of each form, with a seeded value

One real seed record per form, with a value for **every** field of that form. Rules, limits and invalid examples are in [E2E-FORM-EXAMPLE-DATA.md](E2E-FORM-EXAMPLE-DATA.md). Ids are internal and are not shown in the forms. Fields marked *derived* are set by the app, not typed by the user.

**Company form: `AHT`**

| Field | Value |
|---|---|
| Company code | `AHT` |
| Company name | `APEX Technologies` |
| Legal name | `APEX Technologies Co., Ltd.` |
| Registration number | `REG-2014-001872` |
| Tax ID | `TIN-100-245-881` |
| Industry | `Information Technology` |
| Founded date | `2014-03-12` |
| Email | `info@apexhorizon.example` |
| Phone | `+95 1 555 0100` |
| Website | `https://apexhorizon.example` |
| Address | `No. 88, Pyay Road, Kamayut Township` |
| City | `Yangon` |
| Country | `Myanmar` |
| Status | `Active` |

**Branch form: `YGN-HQ`**

| Field | Value |
|---|---|
| Company | `APEX Technologies` |
| Branch code | `YGN-HQ` |
| Branch name | `Yangon Head Office` |
| Branch type | `Head Office` (others: Branch, Regional Office, Warehouse) |
| Email | `hq@apexhorizon.example` |
| Phone | `+95 1 555 0101` |
| Address | `No. 88, Pyay Road, Kamayut Township` |
| City | `Yangon` |
| Country | `Myanmar` |
| Branch manager | `Thura Aung (EMP001)` |
| Opened date | `2014-03-12` |
| Status | `Active` |

**Department form: `HR`**

| Field | Value |
|---|---|
| Company | `APEX Technologies` |
| Branch | `Yangon Head Office` |
| Department code | `HR` |
| Department name | `Human Resources` |
| Department head | `Su Myat Noe (EMP002)` |
| Cost center | `CC-0200` (the seed uses `CC-` + id × 100, four digits) |
| Description | `People operations, talent, payroll and employee relations.` |
| Status | `Active` |

**Section form: `ENG-BE`**

| Field | Value |
|---|---|
| Department | `Engineering` |
| Section code | `ENG-BE` |
| Section name | `Backend Development` |
| Supervisor | `Pyae Phyo Kyaw (EMP016)` |
| Description | `Backend Development team.` |
| Status | `Active` |

**Position form: `POS-006`**

| Field | Value |
|---|---|
| Department | `Human Resources` |
| Section | `Employee Relations` (`HR-ER`) |
| Position code | `POS-006` (the seed uses `POS-` + id, three digits) |
| Position title | `HR Executive` |
| Job grade | `G2 · Associate` |
| Approved headcount | `2` |
| Reports to | `HR Manager (Human Resources)` |
| Status | `Active` |
| Description | `HR Executive role.` |

**Job grade form: `G2`**

| Field | Value |
|---|---|
| Grade code | `G2` |
| Grade name | `Associate` |
| Level | `2` (the seed uses level = id) |
| Status | `Active` |
| Minimum monthly salary (sample) | `600000` |
| Maximum monthly salary (sample) | `1000000` |
| Description | `Junior individual contributors.` |

**Leave type form: `AL`**

| Field | Value |
|---|---|
| Code | `AL` |
| Leave type name | `Annual Leave` |
| Description | `Paid vacation leave accrued annually.` |
| Default days per year | `15` |
| Maximum consecutive days | `10` |
| Eligibility | empty (everyone). `MAT` = Women only, `PAT` = Men only |
| Calendar colour | `#3f6fd8` |
| Paid leave | On |
| Supporting document required | Off |
| Unused days carry forward | **On** |
| Maximum carry-forward days | `5` |
| Status | `Active` |

**Leave policy form: "Annual leave — managers"**

| Field | Value |
|---|---|
| Policy name | `Annual leave — managers` |
| Leave type | `Annual Leave` |
| Days per year | `18` |
| Job grade | `G6 · Manager` |
| Employment type | empty (all types) |
| Accrual | `Upfront` (full amount at the start of the year) |
| Minimum service (months) | `0` |
| Status | `Active` |

**Leave application form: Hnin Ei Phyu, Medical Leave** (the data the E2E test types)

| Field | Value |
|---|---|
| Employee | `Hnin Ei Phyu (EMP006)` |
| Leave type | `Medical Leave` |
| Half day | Off |
| Start date | `2027-03-01` |
| End date | `2027-03-02` |
| Number of days | `2` (*derived*: working days only) |
| Reason | `Doctor visit` |
| Supporting document | `medical-cert.pdf` (required before submit for Medical Leave) |
| Application no., status, applied on | *Derived*: `LV-YYYY-NNNN`, `Draft`, run date |
| Decided by, decided on, approver remarks | *Derived*: set when approved or rejected |

**Employee form: Su Myat Noe (EMP002) and the new hire**

The seed fills the employee form's personal, contact, address, emergency and bank fields with generated values (phone numbers, national ID, bank name and so on). They are stable between runs, but do not assert on them. These fields are fixed:

| Field | EMP002 (seed) | Nilar Htay (the E2E create test types this) |
|---|---|---|
| First name / Last name | `Su Myat` / `Noe` | `Nilar` / `Htay` |
| Gender | `Female` | `Female` |
| Date of birth | generated | `1996-04-12` |
| Marital status | generated | `Single` |
| Nationality | `Myanmar` | `Myanmar` |
| National ID / passport no. | generated | `12/KaMaYa(N)123456` |
| Work email | `sumyat.noe@apexhorizon.example` | `nilar.htay@apexhorizon.example` |
| Personal email | generated | `nilar.personal@mail.example` |
| Mobile phone / Alternate phone | generated / empty | `+95 9 123 456 789` / `+95 1 555 0144` |
| Address line 1 / line 2 | generated / empty | `No. 5, Parami Road` / `Floor 3, Room 302` |
| City / State / Postal code / Country | `Yangon` / generated / generated / `Myanmar` | `Yangon` / `Yangon Region` / `11011` / `Myanmar` |
| Emergency contact name / relationship / phone | generated | `Htay Win` / `Father` / `+95 9 987 654 321` |
| Employee code | `EMP002` | `EMP061` |
| Employment type / status | `Full-time` / `Active` | `Full-time` / `Probation` |
| Joining date | `2015-06-01` | run date |
| Probation end date | `2015-09-01` (joining + 3 months) | joining + 3 months |
| Exit date | empty | empty |
| Company / Branch / Department | `APEX Technologies` / `Yangon Head Office` / `Human Resources` | same |
| Section | empty | empty (or `Talent Acquisition`) |
| Position / Job grade | `Director of Human Resources` / `G8 · Director` | `HR Executive` / `G2 · Associate` |
| Reporting manager | `Thura Aung (EMP001)` | `Su Myat Noe (EMP002)` |
| Work shift | `General Shift (09:00–18:00)` (the seed default for HR) | `General Shift (09:00–18:00)` |
| Bank name / branch / holder / account number | generated | `KBZ Bank` / `Yangon Main Branch` / `NILAR HTAY` / `123456789012` |

**Employee document form:** the seed has no document you need. Use `Employment Contract`, title `Contract 2026`, file `contract-2026.pdf`, uploaded on the run date, expiry empty. A Work Permit uses expiry `2027-06-30`.

**Check before you rely on it**

- Leave type `AL` has **carry-forward on (5 days)** in the seed, but neither the mock nor the API applies it to balances. The flag and the maximum are stored and validated only. `docs/TEST-SCENARIOS.md` (BAL-08) describes balances without carry-forward, which matches the code today. Do not write a test that expects carried-over days.
- Values marked *generated* come from a seeded random generator in `ui/src/app/mock-data/employees/employees.mock.ts`. Read them from the app, not from this document.

Confirm codes and names against `ui/src/app/mock-data/` before you hard-code them.

---

## 2. Organization

### 2.1 Companies

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| ORG-E01 | + | Search, then filter by status | Search `logistics`; Status = Inactive | 1 row `APEX Logistics`; then 1 row `APEX Ventures` | Automated: *companies › searches and filters* |
| ORG-E02 | − | Submit an empty create form, then a bad code | Code `bad code` | "Company code is required." then "Use upper-case letters, numbers and dashes only." | Automated: *validates, creates, edits…* |
| ORG-E03 | − | Create with a duplicate code | Code `AHT`, name `Brand New Co` | "Company code "AHT" already exists." Nothing saved | Automated (same test) |
| ORG-E04 | + | Fix the code and create | Code `BNC`, name `Brand New Co`, status Active | Dialog closes; toast "created successfully"; 4 rows | Automated (same test) |
| ORG-E05 | + | Edit a company | `BNC`: Industry → `E-commerce` | Code field is disabled; row shows `E-commerce` | Automated (same test) |
| ORG-E06 | + | Deactivate, then delete with Cancel and then Confirm | `BNC` | Row shows Inactive; Cancel keeps 4 rows; Delete removes it (3 rows) | Automated (same test) |
| ORG-E07 | − | Delete a company in use | `AHT` (has branches and employees) | Refused with a conflict message; "Deactivate it instead" | Automated: *refuses to delete a company that is still in use* |
| ORG-E08 | + | Show record details, switch to Edit | `AHL` | Details panel shows; Edit opens the form | Automated: *shows record details…* |
| ORG-E09 | − | Duplicate company name | Name `APEX Technologies` (code `BNC`) | Rejected on the name field | New |

### 2.2 Branches and departments

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| ORG-E10 | + | Branch dropdown follows the company | Company `AHT` → open Branch | 4 options: Mandalay Branch, Nay Pyi Taw Office, Singapore Regional Office, Yangon Head Office | Automated: *branch dropdown depends on the selected company* |
| ORG-E11 | + | Switch company to `AHL` | Company `AHL` | Branch options become Mandalay Distribution Center and Yangon Logistics Hub only | New |
| ORG-E12 | + | Paginate and sort departments | 11 departments, page size 10 | Header shows "1 – 10 of 11"; Next page shows 1 row; sort by Code (`CS` first, then `SAL`) | Automated: *paginates and sorts* |
| ORG-E13 | + | Filter departments by company | Company `AHL` | Only Warehouse Operations (`OPS`, MDY-DC) and Fleet & Logistics (`LOG`, YGN-LH) | Automated: *filters by company* |
| ORG-E14 | − | Department with a branch of another company | Company `AHL`, branch `YGN-HQ` | Rejected on the branch field | New |
| ORG-E15 | − | Delete a department that has employees | `HR` Human Resources (EMP002 is head) | Conflict message with the usage count; "Deactivate it instead" | New |
| ORG-E16 | + | Deactivate a department and check the employee form | Deactivate `MKT` Marketing (Singapore), then open Add employee → Department | Marketing is not offered as a new choice | New (cross-module, see X-03) |

### 2.3 Positions and job grades

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| ORG-E17 | − | Job grade maximum below minimum | Min 700,000, max 650,000 (grade G1) | Cross-field error; cannot save | Automated: *cross-field rule: maximum salary…* |
| ORG-E18 | B | Maximum equal to minimum | G1 min 400,000: max 400,000 → accepted; max 399,999 → rejected | Boundary on the max field | New |
| ORG-E19 | − | Position reports to itself | Position `HR Executive` (id 6), reporting position = itself | "cannot report to itself" | New |
| ORG-E20 | − | Position in a section of another department | Department `ENG` Engineering, section `CS-T1` Tier 1 Support (belongs to `CS`) | Rejected on section | New |

### 2.4 Reporting structure

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| ORG-E21 | + | Chart, expand, collapse, search | Search `Thura` | Chart renders; search highlights Thura Aung (CEO) | Automated: *shows the chart, expands, collapses and searches* |

---

## 3. Employees

### 3.1 List, search and profile

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| EMP-E01 | + | Page, search, filter, sort, clear | Search `Daniel`; status Probation | Rows update; "Clear" restores all | Automated: *pages, searches, filters, sorts and clears* |
| EMP-E02 | + | Joining date range | Joined from `01/01/2014`, to `31/12/2015` | 3 rows: Thura Aung (2014-03-12), Su Myat Noe (2015-06-01), Kyaw Zin Htet (2015-09-14) | Automated: *filters by joining date range* (check the row count) |
| EMP-E03 | + | Search after navigating from another page | Term `EMP005` via `searchEmployeesInApp` | Single match: Daniel Carter | Automated: *searches the list after navigating…* |
| EMP-E04 | + | Profile tabs | EMP002 Su Myat Noe | Overview, Employment, History, Documents, Salary, Attendance, Leave all open | Automated: *shows the summary and every tab* |
| EMP-E05 | − | Unknown employee deep link | `/employees/9999` | "Not found"-type message, no crash (confirm the exact text in the UI) | Automated: *deep links to a tab and reports unknown employees* |
| EMP-E06 | + | Open a profile from a row, delete after Confirm | EMP060 Andrew Lim (no direct reports) | Confirmation names the employee; row removed | Automated: *opens a profile from the row and deletes only after confirmation* |
| EMP-E07 | − | Delete a manager | EMP001 Thura Aung (6 direct reports) | Conflict: manages 6 employees; delete refused | New |

### 3.2 Create and edit

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| EMP-E08 | + | Add-employee defaults | Open `/employees/new` | Code pre-filled `EMP061`; heading "Add employee" | Automated (create test) |
| EMP-E09 | − | Required fields | Click Create with an empty form | "Please correct N fields before saving"; "First name is required." | Automated (create test) |
| EMP-E10 | B | Date of birth at 18 years | Let `T` = the run date. DOB = `T` − 18 years → accepted; DOB = `T` − 18 years + 1 day → "at least 18 years old" | Boundary on DOB | New (compute dates in the test) |
| EMP-E11 | − | Probation end before start | Start `2026-11-10`, probation end `2020-01-01` | "End date must be on or after the start date." | Automated (create test) |
| EMP-E12 | − | Invalid phone | Mobile `abc` | "Enter a valid phone number" | Automated (create test) |
| EMP-E13 | + | Create a full employee | Nilar Htay, female, DOB 1996-04-12, Single, National ID `12/KaMaYa(N)123456`, work email `nilar.htay@apexhorizon.example`, company APEX Technologies, branch Yangon Head Office, department Human Resources, position HR Executive (grade G2 fills in) | Saved as EMP061; history "Joined as HR Executive" | Automated (create test) |
| EMP-E14 | − | Duplicate employee code | Code `EMP005` | Rejected: code already exists | New |
| EMP-E15 | − | Duplicate work email | `daniel.carter@apexhorizon.example` | "already exists" on the work email field | Automated: *refuses a duplicate work email…* |
| EMP-E16 | − | Branch of another company | Company APEX Logistics, branch Yangon Head Office | Rejected on branch | New |
| EMP-E17 | − | Resign without an exit date | EMP054 Zarni Tun: status Resigned, exit date empty | "An exit date is required…" | New |
| EMP-E18 | + | Edit a position (promotion) | EMP046 Hein Thant Zin: Software Engineer (id 15, G3) → Senior Backend Engineer (id 12, G4) | History entry "Promotion" | New |
| EMP-E19 | + | Unsaved changes warning | Change first name, then navigate away | Warning; "Stay" keeps the edit | Automated: *warns before discarding unsaved changes* |

### 3.3 Documents and salary

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| EMP-E20 | − | Upload a disallowed file | `virus.exe` (application/octet-stream) | "Allowed file types: PDF, Word, PNG or JPG." | Automated: *uploads, edits and deletes a document* |
| EMP-E21 | + | Upload, filter and delete a valid document | `contract-2026.pdf`, type Contract | Listed; filter by type; delete after confirmation | Automated: same test and *filters by type and shows expiry warnings* |
| EMP-E22 | + | Salary tab shows the current assignment | EMP001 Thura Aung | Salary figures match the seeded assignment (read them from the seed before asserting) | New |
| EMP-E23 | − | Salary tab for a new employee | EMP061 (just created) | "has no salary assignment yet" | New |

---

## 4. Leave

### 4.1 Leave types and policies

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| LV-E01 | + | Create, edit, delete a leave type | Name `Study Leave`, code `STD`, 5 days | Created, edited, then deleted | Automated: *validates, creates, edits and deletes; protects types in use* |
| LV-E02 | − | Duplicate code | Code `AL` | Rejected: code exists | New |
| LV-E03 | − | Delete a type in use | `AL` Annual Leave | Conflict: used by applications and policies | Automated (same test) |
| LV-E04 | − | Max consecutive below 1 | Max consecutive `0` | Rejected | New |
| LV-E05 | − | Second active policy for the same scope | Second active Annual policy with no grade and no employment type | "An active policy already exists…" | Automated: *lists policies and refuses a clashing active policy* |

### 4.2 Applications

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| LV-E06 | + | Eligible types follow gender | Employee EMP006 Hnin Ei Phyu (female) | Maternity Leave shown; Paternity hidden | Automated: *shows calculated days and eligible types…* |
| LV-E07 | + | Days calculation | `2027-03-01` → `2027-03-05` = 5; → `2027-03-09` = 7 | Number of days read-only and updates | Automated (same test) |
| LV-E08 | + | Half day | Half-day switch on a single date | Days = 0.5 | Automated (same test) |
| LV-E09 | − | Half day with a range | Start `2027-03-01`, end `2027-03-02`, Half day on | "single date" error | New |
| LV-E10 | − | End before start | End `2027-02-20` | "End date must be on or after the start date." | Automated (same test) |
| LV-E11 | − | Weekend-only dates | `2027-03-06` – `2027-03-07` | "no working days" | Automated (same test) |
| LV-E12 | − | Missing reason | Dates valid, reason empty | Required-field error; nothing created | New |
| LV-E13 | + | Create, then submit a Medical leave without a document | EMP006, `2027-03-01` – `2027-03-02`, reason `Doctor visit` | Created as LV-YYYY-NNNN, Draft; submit → "needs a supporting document" | Automated (same test) |
| LV-E14 | − | Overlap with an existing application | Second application for EMP006, `2027-03-02` – `2027-03-03`, after LV-E13 is submitted | Rejected: "overlap LV-…" | New |
| LV-E15 | B | Casual leave consecutive limit | EMP006 Casual `2027-03-08` – `2027-03-10` (3 days) accepted; `2027-03-08` – `2027-03-11` (4 days) rejected | "limited to 3 consecutive days" | New |
| LV-E16 | − | Insufficient balance | EMP050 Rahul Sharma, Annual 1 day (entitlement 0) | "Insufficient Annual Leave balance: 1 day(s) requested, 0 available." | New |
| LV-E17 | + | Delete a draft after confirmation | The draft from LV-E13 | Row removed | Automated: *deletes a draft after confirmation* |
| LV-E18 | − | Cancel a submitted leave that has not started | Submitted leave from LV-E13 | Cancelled; days released | Automated (submit and cancel test) |

### 4.3 Approvals

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| LV-E19 | + | Approve with remarks | Submitted leave of EMP006 (approver Su Myat Noe) | Status Approved; remark stored; history entry | Automated: *approves with remarks, rejects only with a reason…* |
| LV-E20 | − | Reject without a reason | Same request | "A reason is required." | Automated (same test) |
| LV-E21 | + | Reject with a reason | Same request | Rejected; days released | Automated (same test) |
| LV-E22 | − | Decide your own request | Submit a leave for EMP002 Su Myat Noe, then try to approve it as Su Myat Noe | "You cannot decide your own leave request" | Automated: *refuses to let the signed-in user decide their own request* |

### 4.4 Balances and calendar

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| LV-E23 | + | Policy-based entitlement | EMP056 Annual → 15 (standard); EMP005 Daniel Carter Annual → 18 (G6); EMP058 Chan Myae Aung Annual → 6 (intern) | Entitlement per employee matches | Automated: *shows policy-based entitlements…* |
| LV-E24 | + | Profile uses the same balances | EMP056 profile → Leave tab | Same numbers as the Balances page | Automated: *the employee profile uses the same entitlements* |
| LV-E25 | + | Calendar: day view, filters, month navigation | Month of `2027-03`; include pending off, then on | Approved always shown; submitted only when included; weekends marked | Automated: *shows leave by day, filters, month navigation and day details* |
| LV-E26 | + | Mobile calendar becomes a list | 390 px width | List view; applications still usable | Automated: *the calendar becomes a list and applications stay usable* |

---

## 5. Cross-module (X)

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| X-01 | X | New employee gets no annual entitlement | Create Nilar Htay (EMP-E13), then open her Annual leave balance and apply for 1 day | Entitlement 0 (under 3 months' service, no grade policy for G2 staff); submit → "Insufficient … balance" | New |
| X-02 | X | Leave shows on the profile | Approve a leave for EMP006 (LV-E19), open her profile → Leave tab | Approved leave listed with the same dates and days | New |
| X-03 | X | Deactivated department is not offered | Deactivate `MKT` Marketing (ORG-E16), open Add employee → Department | Marketing not in the list; existing Marketing employees (for example EMP032 Emily Wong) still show their department | New |
| X-04 | X | Approved leave blocks attendance (smoke only) | Approve leave for EMP056 on a weekday in 2027 that has no record | Leave appears in the calendar; attendance for that day is "On Leave" once the day exists | New (optional; attendance is out of scope here) |
| X-05 | X | Branch delete after departments are moved | Deactivate, then delete a branch that has no departments | Delete succeeds only once no child records remain | New |
| X-06 | X | Approved leave replaces past attendance | Approve a leave for EMP056 Sai Htoo Aung that covers a past date with a Present or Late record (no leave on that day yet), then open Attendance Records for that day | The record becomes On Leave with no check-in or check-out, and the Attendance Overview counts it under On leave. Records after today are not changed | New |
| X-07 | X | Unpaid leave reduces payroll | Approve an Unpaid Leave (`UL`) application for EMP056 that covers 2 working days inside the current month (the Draft period). Process the period, then open the EMP056 payslip. Repeat without the leave in a second run | The payslip has an "Unpaid leave" deduction of 2 day(s) at basic divided by working days. Net equals gross minus deductions, and the run without leave shows a higher net | New |
| X-08 | X | A selected candidate does not become an employee | Select a candidate who has a completed Strong hire interview (REC-19). Then open Employee List and the candidate's record | No employee is created and the candidate stays at Selected. Creating the employee is a manual Add employee step (EMP-E13). Confirm against product intent: whether a conversion step is planned | New. Confirm against product intent |
| X-09 | X | Approved overtime is paid | Add two overtime entries for EMP056 in the current month on dates up to today: 18:00 to 20:00 and a second one on another day. Approve the first, leave the second Pending. Process the period | The payslip has an "Overtime pay" line for 2 hours only. The pending entry is not paid. The amount is 2 times (basic divided by working days times 8) times 1.5 | New |
| X-10 | X | Inactive shift cannot be assigned to an employee | Open Edit employee for EMP056 and choose shift NIGHT (Inactive) | Refused on the shift field: "This shift is inactive." An employee already on an inactive shift keeps it | New |

---

## 6. Suggested run order

1. **Smoke (about 15 minutes):** AUTH-01, AUTH-11, ORG-E01, ORG-E04, ORG-E07, EMP-E01, EMP-E13, EMP-E15, LV-E06, LV-E13, LV-E19, LV-E22, ATT-11, REC-13, SET-01, DSH-03, XLS-02, PAY-16.
2. **Full run, safest first:** sign-in (section 8), Organization, Employees, Leave, Attendance (section 9), Recruitment (section 10), Settings (section 11), Dashboard, activity and notifications (section 12), Excel (section 13), Payroll last (section 14). Each test starts from the seed, so the order does not change results. It helps reading failures: the first failing group points at the lowest module that broke. Each spec file runs its own `describe` blocks.
3. **Cross-module:** X-01 to X-10 after the module tests pass. X-06 to X-10 depend on the attendance, payroll and recruitment tests.
4. **API build:** rows marked **API** run in a separate job, once a test API and local database are available. They are not run by the Playwright mock suite.

## 7. Known gaps to note in reports

- Leave balances and rules use the current date. A test that runs in a different year can change results. The scenarios use 2027 dates to reduce this risk.
- Relative records (EMP017, EMP026, EMP038, EMP046, EMP049, EMP050, EMP053, EMP057, EMP058, EMP060) move with the run date. Re-check their status and dates before a run.
- Sign-in is simulated in the mock build: one shared demo password, no failure count or lockout, no session timeout, and the reset stand-in accepts any token (AUTH-06, AUTH-24). The API build has real sign-in (cookie session, lockout, reset by email), but its rows (**API**) need a running API and database.
- Permissions are not enforced in either build. Roles and the permission matrix are stored and shown, but every signed-in user can open every screen and act (SET-20). Role-based tests cannot pass yet.
- API build coverage: sign-in, Organization, Employees (including employment history) and Leave use the API. Attendance, Recruitment, Settings, Payroll, Excel, Dashboard and activity stay on mocks. The Documents tab and the Salary tab of an employee profile also stay on mocks.
- The HR administrator has no special right over leave. In both builds the only leave rule on decisions is that nobody decides their own request (LV-E22).
- Drafts cannot be cancelled. A draft can only be deleted; cancel applies to submitted leave, and to approved leave that has not started.
- Payroll has no separation of duties: the user who processes a run can also approve it (PAY-24). Payroll figures are demonstration figures, not statutory calculations.
- Candidates are not converted to employees (X-08). An interview clash check covers the interviewer only (REC-27). Holiday dates are not unique (ATT-10).
- Notifications are seeded only. No action in the app creates one (DSH-11).
- The mock sign-in tells the user that an account is deactivated or locked. The API gives the generic message for both (AUTH-04, AUTH-16).
- Import history is kept in memory and is cleared when the page is reloaded (XLS-13).

---

## 8. Sign-in and session

The mock build signs in against the Users list in Settings with one shared demo password, `Welcome@123` (`ui/src/app/mock-services/auth/auth-mock.service.ts`). This is a simulation and gives no security. Rows marked **API** need the API and a local database (`api/`, `/api/auth/*`).

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| AUTH-01 | + | Sign in as the HR administrator and reach the dashboard | `sumyat.noe@apexhorizon.example`, `Welcome@123` | URL is `/dashboard` | Automated: auth.spec › redirects signed-out visitors… (sign-in step) |
| AUTH-02 | − | Wrong password | Same email, password `wrong` | Alert "Incorrect email or password."; stays on `/login` | Automated: auth.spec › shows an error for wrong credentials |
| AUTH-03 | − | Unknown email gets the same message (mock) | `nobody@apexhorizon.example`, `Welcome@123` | "Incorrect email or password." (the same text as AUTH-02) | New |
| AUTH-04 | − | Deactivated account (mock) | EMP026 Shwe Sin Oo (user status Inactive); her work email from Settings > Users | "This account is deactivated. Contact an administrator." The mock reveals the status; the API does not (AUTH-16) | New |
| AUTH-05 | − | Locked account (mock) | EMP012 Zaw Lin Oo (user status Locked); email from Settings > Users | "This account is locked. Ask an administrator to unlock it." | New |
| AUTH-06 | − | Failed attempts do not lock in the mock | Six wrong passwords for an active user, then the right password | The wrong ones show "Incorrect email or password."; the mock keeps no failure count, so the right password signs in | New (mock only) |
| AUTH-07 | − | Empty fields | Click Sign in with both fields empty | "Enter a valid email address." and "Enter your password." | Automated: auth.spec › validates required fields |
| AUTH-08 | + | Remember me chooses the storage (mock) | Tick "Keep me signed in" and sign in; then repeat with the box unticked | Ticked: the session is in `localStorage` under `hrms.session`. Unticked: it is in `sessionStorage` only | New |
| AUTH-09 | + | Protected page redirects with returnUrl | Signed out, open `/employees/list` | URL `/login?returnUrl=%2Femployees%2Flist`; after sign-in the app returns to `/employees/list` | Automated: auth.spec › redirects signed-out visitors… |
| AUTH-10 | − | Unsafe returnUrl is ignored | Open `/login?returnUrl=//example.invalid/evil`, then sign in | Lands on `/dashboard`. Only in-app paths starting with one slash are followed (`login.component.ts`) | New |
| AUTH-11 | + | Sign out | User menu > Sign out; then open `/dashboard` | `/login`; `/dashboard` redirects to `/login` | Automated: auth.spec › signs out from the user menu |
| AUTH-12 | + | Forgot password link (mock) | On `/login`, click "Forgot password?" | Toast: "Password resets are handled by your HR administrator. Please contact them." | New (mock only) |
| AUTH-13 | + | API: Remember me keeps the session after reload | **API**. Tick "Remember me", sign in, reload the page | Still signed in (7-day sliding cookie). Without the box the session ends after 30 minutes idle, or after 8 hours at most | New (API) |
| AUTH-14 | − | API: generic failure and lockout | **API**. Five wrong passwords for an existing account, then the correct password. Also one unknown email | Every failure shows "Sign-in failed. Check your details or contact your administrator." The 5th failure locks the account for 15 minutes; the correct password is refused until then. The unknown email gets the same text | New (API). The 15-minute wait is an API test with a fake clock |
| AUTH-15 | − | API: rate limit per IP | **API**. Send 11 sign-in requests from one client within one minute | The 11th returns 429 "Too many attempts. Wait a minute and try again." (default 10 per minute, `Auth:RateLimit:PermitsPerMinute`) | New (API) |
| AUTH-16 | − | API: deactivated account gets the generic message | **API**. Sign in as a user whose status is Inactive | "Sign-in failed. Check your details or contact your administrator." | New (API) |
| AUTH-17 | + | API: forgot password gives one answer | **API**. Submit a known address, then an unknown one, on `/forgot-password` | Both show "If an account exists, a reset link has been sent." (the screen's own text) | New (API) |
| AUTH-18 | − | Forgot password with a bad address | Type `not-an-email` and submit | Validation message on the email field; no request is sent | New |
| AUTH-19 | + | API: reset with the link from the dev mail | **API**. Request a reset for a known account. Open the newest file in `App_Data/dev-mail`, open the link, enter a password of at least 12 characters twice | Redirect to `/login?reset=1` with "Your password has been reset. Sign in with your new password." | New (API) |
| AUTH-20 | − | Password under 12 characters | **API**. Reset with an 11-character password | Field error on the new password. The server text is "Use at least 12 characters." (the mock says "Password must be at least 12 characters.") | New (API) |
| AUTH-21 | − | Reset link cannot be used twice | **API**. Open the link from AUTH-19 again with a new password | "The reset link is invalid or has expired. Request a new one." The first reset changed the security stamp | New (API) |
| AUTH-22 | − | Expired reset link | **API**. Request a link, then move the clock more than 1 hour ahead | Same message as AUTH-21. Run as an API test with a fake `TimeProvider`, not in Playwright | New (API test) |
| AUTH-23 | − | A reset ends open sessions | **API**. Sign in in one browser, reset the password in another, then use the first browser | The first session is signed out (401 on `/api` calls) | New (API) |
| AUTH-24 | − | Mock reset accepts any token | Mock build: open `/reset-password?email=...&token=anything`, enter a 12-character password twice | Succeeds. The mock does not check tokens, reuse or expiry. This is a known difference from the API, not a rule to test for | New (mock only) |
| AUTH-25 | − | API: forgot password when email is not configured | **API**. Outside the Development environment, with no `Email:Smtp` settings, submit an address on `/forgot-password` | The API returns 503 "Password reset unavailable". The screen shows the same confirmation for any error except a connectivity failure. Confirm against product intent: a 503 may look like success to the user | New (API). Confirm against product intent |

## 9. Attendance

Attendance is mock-only in this document: there is no attendance API yet. Rules come from `ui/src/app/core/utilities/attendance-calc.ts` and `ui/src/app/mock-services/attendance/`:

- Late: check-in after the shift start plus the grace minutes. Late minutes count from the shift start.
- Worked time: check-out minus check-in minus the break. When the gross time minus the break is under 4 hours, the status is Half Day and no break is deducted.
- Overtime (on the record): minutes after the scheduled end, only once they pass 30 minutes.
- A night shift moves the end to the next day.
- Weekend and holiday punches are kept as Present, with the remark "Worked on a weekend" or "Worked on a holiday".

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| ATT-01 | + | Create a day shift | Code `TST-DAY`, name `Test Day Shift`, 08:00 to 16:00, break 60, grace 10, Monday to Friday, default colour, Active | The row shows "08:00 – 16:00" and a success toast | Partly automated: attendance.spec › validates times, creates, edits and deletes a shift |
| ATT-02 | − | Duplicate shift code | Code `GEN` | Validation error on the Code field (already in use) | New |
| ATT-03 | − | Day shift ends before it starts | Start 18:00, end 09:00, night shift off | Error on End time: "Must be after the start time (or mark the shift as a night shift)." | Partly automated: shift management › validates times |
| ATT-04 | + | Night shift | Start 22:00, end 06:00, Night shift on | Saved. The list shows "22:00 – 06:00 (+1 day)" | New |
| ATT-05 | − | No working day | Untick every day | "Choose at least one working day." with "Choose at least one day." on the days field | New |
| ATT-06 | + | Weekend working days | Shift `TST-SAT`: Monday to Saturday (Saturday is a working day, Sunday is not), assigned to EMP056 | Work Schedules: Saturday shows the shift times, Sunday shows "Off" | New |
| ATT-07 | + | Calendar colour | Set colour `#0f9d76` on `TST-DAY` | Schedule cells for employees on that shift use the saved colour | New |
| ATT-08 | − | Shift in use cannot be deleted | Delete `GEN` (assigned to employees) | Refused, with the count of assigned employees | Partly automated: shift management › protects assigned shifts |
| ATT-09 | + | Holiday added | Date `2027-03-20`, name `Test Holiday`, type Company | Listed in holidays. Work Schedules shows a Holiday cell with that name on that date | New |
| ATT-10 | B | Two holidays on one date | Add a second holiday on `2027-03-20` | Accepted: the mock has no uniqueness check. Confirm against product intent: should a second holiday on the same date be refused? | New. Confirm against product intent |
| ATT-11 | + | Check in | Check In / Out, EMP056 Sai Htoo Aung (no leave today), Check in | Toast "checked in at HH:MM"; the heading shows "Checked in at HH:MM"; status Present, or Late if after start plus grace | New |
| ATT-12 | + | Late arrival | Shift `TST-LATE`: 00:00 to 23:59, grace 0, assigned to EMP056. Set the browser time to 10:00 with `page.clock`, then check in | Toast ends with "marked late (N min)"; record status Late | New |
| ATT-13 | + | Half day | Browser time 09:00 check in, then 09:30 check out | Status Half Day; worked time 30 minutes (no break deducted) | New |
| ATT-14 | + | Full day | Shift GEN (09:00 to 18:00, break 60). Check in 09:00, check out 18:30 | Worked 8 h 30 min; status Present; overtime 0 (30 minutes is not past the threshold) | New |
| ATT-15 | − | Check out before check in | Check out with no check-in today | "Check in before checking out." | New |
| ATT-16 | − | Second check in | Check in twice on the same day | "Already checked in at HH:MM." | New |
| ATT-17 | − | Check-out not after check-in (day shift) | Check in 10:00, then check out 09:00 | "The check-out time must be after the check-in time." | New |
| ATT-18 | + | Weekend punch | Set the browser date to a Sunday with `page.clock`, then check in | Status Present; remark "Worked on a weekend"; late 0 | New |
| ATT-19 | + | Records filter | Attendance Records: filter status Late, then the late-only filter | Only rows with late minutes above 0 | Partly automated: attendance.spec › lists late arrivals and early departures |
| ATT-20 | + | Status values | Records list for a seeded week | Each status is one of Present, Absent, Late, Half Day, On Leave, Weekend or Holiday. Absent has no check-in. On Leave has no times | New |
| ATT-21 | + | Adjustment request | Attendance Adjustments: EMP006 Hnin Ei Phyu, a past date with a record, requested check-in 09:05 | Request is Pending, dated today | Partly automated: attendance adjustments › creates, edits and deletes a pending one |
| ATT-22 | − | Adjustment for a future date | Date tomorrow | "The date cannot be in the future." on the Date field | New |
| ATT-23 | − | Adjustment for a day with no record | Employee and date with no attendance record | "There is no attendance record for this employee on that date." | New |
| ATT-24 | − | Second pending request for the same day | Same employee and date as ATT-21 | "A pending request already exists for this employee and date." | New |
| ATT-25 | + | Approve adjustment updates the record | Approve the request from ATT-21 | The record shows check-in 09:05; status recalculated; source Manual; remark "Adjusted (ADJ-NNNN)" | Automated: attendance adjustments › approving an adjustment updates the attendance record |
| ATT-26 | + | Reject adjustment without a remark | Reject a pending request with no remark | Status Rejected; remark "Rejected."; the record is unchanged. Confirm against product intent: attendance rejection needs no reason, while leave rejection does (LV-E20) | Partly automated: adjustments › approves and rejects requests. Confirm against product intent |
| ATT-27 | − | Decide twice | Approve a request that is already approved | "This request was already approved." | New |
| ATT-28 | − | Edit an approved adjustment | Edit an approved request | "Only pending requests can be changed." | New |
| ATT-29 | + | Overtime entry | EMP006, a date up to today, 18:00 to 20:00 | Saved as 120 minutes, Pending | Partly automated: overtime › approves, rejects, adds and validates overtime |
| ATT-30 | B | Overtime minimum | 14 minutes, then 15 minutes | 14 refused: "Overtime must be between 15 minutes and 12 hours." 15 accepted | Partly automated: overtime › validates |
| ATT-31 | B | Overtime maximum | 22:00 to 10:00 (12 hours, crosses midnight), then 22:00 to 10:01 | 12 hours accepted; 12 hours 1 minute refused with the same message | New |
| ATT-32 | − | Second overtime entry on one day | Second entry for the same employee and date | "Overtime is already recorded for this employee on that date." | New |
| ATT-33 | + | Approve and reject overtime | Approve one entry; reject another with no remark | Approved with remark "Approved."; rejected with remark "Rejected."; the decider is the signed-in user | Partly automated: overtime › approves, rejects |
| ATT-34 | + | Approved overtime and the attendance record | Approve an entry for a day that has a record with overtime minutes | Confirm against product intent: the record's overtime minutes do not change on approval. Only payroll reads approved overtime | New. Confirm against product intent |
| ATT-35 | + | Overview totals | Attendance Dashboard for a date with records, then read the summary | Scheduled equals Present + Late + Half Day + Absent + On Leave (weekends and holidays excluded). Attendance rate equals (Present + Late + Half Day) divided by Scheduled, as a percentage with one decimal | Partly automated: attendance dashboard › shows the day summary, charts and exception lists |
| ATT-36 | + | Exception lists | Overview for a day with more than 8 late and 8 absent records | Late list shows the 8 with most late minutes; absent list shows 8 | New |
| ATT-37 | + | Work schedule | Work Schedules: open the week, move to the next week, filter by department | Shift cells show shift times; non-working days show Off; holidays show their name; approved leave shows "On leave" | Partly automated: work schedules › shows the weekly roster and moves between weeks |
| ATT-38 | + | Shift assignment | Assign `TST-DAY` to two employees at once | Both show the shift in Work Schedules | Automated: shift assignment › assigns a shift to several employees at once |
| ATT-39 | − | Punch for another date | The check-in page only offers today. The service accepts any date | Confirm against product intent: the brief expects server time and today only. The mock uses the browser clock and does not reject other dates. Decide whether the server should refuse punches for other dates | New. Confirm against product intent |

## 10. Recruitment

Recruitment is mock-only in this document. Stage moves follow `STAGE_TRANSITIONS` in `ui/src/app/core/models/recruitment.models.ts`:

- Applied to Screening or Rejected; Screening to Interview or Rejected; Interview to Selected or Rejected; Selected to Rejected only. Rejected is final.
- Job openings move Draft to Open or Closed, Open to On Hold or Closed, On Hold to Open or Closed, Closed to Open. Filled cannot change.
- Rejecting an application cancels its scheduled interviews.

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| REC-01 | + | Create a job opening as Draft | Title `Test Analyst`, department Human Resources, hiring manager Su Myat Noe (EMP002), vacancies 1, posted today, closing `2027-03-31`, Full-time | Status Draft. Code is `JOB-` plus the posting year plus the id in three digits | Partly automated: recruitment › creates a job opening as a draft, publishes it… |
| REC-02 | − | Required and range checks | No department; hiring manager EMP034 Soe Moe Kyaw (has an exit date); vacancies 0, then 51; closing date before the posting date | "Choose a department."; "Choose a current employee as hiring manager."; "Vacancies must be between 1 and 50."; "The closing date cannot be before the posting date." | New |
| REC-03 | B | Publish with a closing date of today | Draft with closing date today; publish | Status Open | New |
| REC-04 | − | Publish after the closing date has passed | Draft with closing date today; set the browser date to tomorrow with `page.clock`; publish | Refused: "The closing date has passed. Edit the opening before reopening it." The status stays Draft. Confirm against product intent: the wording says reopening, but the same check runs on publish | New. Confirm against product intent |
| REC-05 | + | Close and reopen | Open job: Close, then Reopen | Closed, then Open (closing date still in the future, not all vacancies filled) | New |
| REC-06 | + | On hold | Open to On Hold, back to Open, then Closed | Each move allowed | New |
| REC-07 | − | Filled opening cannot change | Set up as REC-21, then try to set the filled job to Open | "A filled job opening cannot be set to Open." | New |
| REC-08 | − | Closed opening cannot be edited | Edit a Closed opening | "A closed job opening cannot be edited." | New |
| REC-09 | − | Vacancies below the selected count | Job with two selected candidates; set vacancies to 1 | "2 candidate(s) are already selected for this job." | New |
| REC-10 | + | Candidate email is saved in lower case | Email `  Mya.Test@Candidate.Example ` (fictional) | Saved as `mya.test@candidate.example` | New |
| REC-11 | − | Duplicate candidate email | The same address as REC-10, in other letters and case | Refused on the Email field, as already in use | Partly automated: candidates › manages candidates with validation |
| REC-12 | − | Experience out of range | 51 years | "Experience must be between 0 and 50 years." | Partly automated: manages candidates with validation |
| REC-13 | + | Add an application from the pipeline | Open job; candidate `Mya Test`; add application | Stage Applied; applied date today | Partly automated: adds an application from the pipeline and schedules and cancels an interview |
| REC-14 | − | Application to a job that is not open | Job On Hold; add an application | `"<title>" is on hold and not accepting applications.` (Draft or Closed give the matching status word) | New |
| REC-15 | − | Same candidate applies twice | Second application for the same candidate and job | "This candidate has already applied for this job." | New |
| REC-16 | + | Stage moves | Applied to Screening, to Interview, then (after REC-19 setup) to Selected | Each move accepted | Partly automated: moves candidates through the pipeline |
| REC-17 | − | Move that is not in the table | Applied to Interview | `An application at "Applied" cannot move to "Interview".` | New |
| REC-18 | − | Select without a positive interview | Interview stage, no completed interview; move to Selected | 'Selecting a candidate needs a completed interview with a "Hire" recommendation.' A completed interview with "No hire" is refused too | New |
| REC-19 | + | Select after a Strong hire | Completed interview with "Strong hire" for the application; move to Selected | Stage Selected | New |
| REC-20 | − | Rejection needs a reason | Reject with a blank reason | "Enter a reason for rejecting this application." and "A reason is required." on the field | Partly automated: moves candidates through the pipeline, rejecting with a reason |
| REC-21 | + | Vacancy filled automatically, then reopened | Job with one vacancy; select one candidate (job becomes Filled); then reject that candidate (job becomes Open) | Job Filled after selection; Open again after the rejection. The activity log shows "filled" then "reopened" | New |
| REC-22 | − | Selected application cannot be deleted | Delete an application at Selected | "A selected application cannot be deleted. Reject it first." | New |
| REC-23 | + | Rejection cancels scheduled interviews | Reject an application that has a scheduled interview | The interview becomes Cancelled with the note "Cancelled: the application was rejected." | New |
| REC-24 | − | Interview in the past | Schedule an interview for yesterday | "A scheduled interview cannot be in the past." | New |
| REC-25 | − | Interview for an application at Applied | Schedule an interview for an application at Applied | "Interviews can be scheduled for candidates in Screening or Interview (this one is Applied)." | New |
| REC-26 | + | Scheduling moves Screening to Interview | Application at Screening; schedule an interview for a future date | Stage becomes Interview | New |
| REC-27 | − | Interviewer double booked | Two scheduled interviews for the same interviewer on the same day, overlapping | "The interviewer already has an interview at HH:MM on that day." The check covers the interviewer only. Confirm against product intent: a candidate with two overlapping interviews is not refused | New. Confirm against product intent |
| REC-28 | B | Interview duration | 14 minutes, then 15, 240 and 241 minutes | 14 and 241 refused: "The duration must be between 15 and 240 minutes."; 15 and 240 accepted | New |
| REC-29 | − | Completed needs a score and a recommendation | Mark an interview Completed with no score; then with no recommendation | "A completed interview needs a score from 1 to 5."; "A completed interview needs a recommendation." | New |
| REC-30 | B | Score must be a whole number from 1 to 5 | Score 0, 6 and 2.5 | Each refused with "A completed interview needs a score from 1 to 5." | New |
| REC-31 | − | Future interview cannot be completed | Completed with a future date | "A future interview cannot be marked completed." | New |
| REC-32 | − | Score or recommendation on a scheduled interview | Scheduled interview with a recommendation | "Only completed interviews can have a score or recommendation." | New |
| REC-33 | − | Hiring record is protected | Edit a cancelled interview; delete a completed one | "A cancelled interview cannot be changed. Schedule a new one instead."; "A completed interview is part of the hiring record and cannot be deleted." | New |
| REC-34 | − | Opening with applications cannot be deleted | Delete an opening that has applications | Refused, with the count of applications | New |

## 11. Settings

Settings is mock-only in this document. Roles and permissions are simulated: nothing in the app enforces them (SET-20). The built-in administrator is the HR Administrator role.

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| SET-01 | + | Save general settings | Organization name `APEX Group Test`; other fields unchanged | Saved. Activity entry "General settings updated (simulation)" | Partly automated: settings › saves general settings, validating the currency code |
| SET-02 | − | Required and format checks | Organization name empty; contact email `hr-at-example` | "Organization name is required."; "Enter a valid email address." | New |
| SET-03 | − | Currency code | `mmk` in lower case, then `MMKS` | "Use a 3-letter code such as MMK." | Partly automated: saves general settings, validating the currency code |
| SET-04 | + | Reset the form | Change a value, then click Reset | The form returns to the saved values. Reset is disabled when nothing has changed | New |
| SET-05 | + | Reset data is hidden in the e2e build | Open Settings > General in the e2e build | No "Reset data" button, because persistence is off. In the default dev build the button is shown; it asks "Reset all data?" and then removes the saved changes | New |
| SET-06 | + | Create a user for an active employee | Employee EMP056 Sai Htoo Aung (no account yet), role Recruiter, status Active | Name and email come from the employee record; saved | New |
| SET-07 | − | One account per employee | EMP002 Su Myat Noe again | "A user already exists for this employee." | Partly automated: settings › manages users: unique employee, own account protected, unlock |
| SET-08 | − | Exited employee | EMP044 Tun Tun Naing (Terminated, exit date 2026-06-15) | "Choose a current employee." | New |
| SET-09 | − | Own role and status | Edit Su Myat Noe: role to Employee, or status Inactive | "You cannot change your own role or deactivate your own account." | Partly automated: settings › manages users: unique employee, own account protected, unlock |
| SET-10 | − | Delete own account | Delete Su Myat Noe | "You cannot delete your own account." | New |
| SET-11 | − | Always one active HR Administrator | Create a second administrator for EMP056 Sai Htoo Aung (role HR Administrator). Deactivate the other administrator, then try to deactivate or demote the last one | The first change is allowed while two are active. Afterwards "There must always be one active HR Administrator." blocks the last one. The self-check runs first, so use another signed-in administrator to test this | New |
| SET-12 | + | Unlock a locked user | EMP012 Zaw Lin Oo (Locked): "Unlock account" | Status Active; toast "… unlocked." (mock only; the API has no unlock action) | Partly automated: settings › manages users: unique employee, own account protected, unlock |
| SET-13 | − | Built-in role name | Rename HR Administrator | "A built-in role cannot be renamed." | New |
| SET-14 | − | Built-in roles cannot be deactivated or deleted | Deactivate, then delete, HR Administrator or Employee | "A built-in role cannot be deactivated."; "A built-in role cannot be deleted." | New |
| SET-15 | − | Role in use | Deactivate Recruiter (one active user); then delete Recruiter | Deactivation: "1 active user(s) still have this role. Move them to another role first." Deletion is refused because a user still has the role | New |
| SET-16 | + | Create a role; duplicate name refused | Role `Test Coordinator`; then a second role with the same name | Created as a non-system role, Active. Duplicate name refused on the Name field | Partly automated: settings › manages roles and edits permissions with the View rule |
| SET-17 | − | HR Administrator matrix is fixed | Try to save a change to the HR Administrator permissions | "HR Administrator always has full access and cannot be changed." | New |
| SET-18 | − | View is required | Grant Create on Recruitment without View | "Recruitment: View is required to grant other permissions." on that module | Partly automated: settings › manages roles and edits permissions with the View rule |
| SET-19 | + | Save the permission matrix | Grant View on Recruitment to Recruiter; Save; then change it and Reset | Saved. Activity "Permissions of role "Recruiter" updated (simulation)". Reset returns to the last saved state | New |
| SET-20 | + | Permissions are not enforced (limitation) | Remove every grant from a role; sign in as a user with that role (demo password); open the HR screens | The screens still open and actions still work. This documents the limitation; it is not a pass for an access-control feature | New |
| SET-21 | + | Account for an employee who is not Active | Create a user for an employee with status Suspended or On Leave (no exit date) | Confirm against product intent: the rule checks the exit date only, so such an employee is accepted. Decide whether the status should also be checked | New. Confirm against product intent |

## 12. Dashboard, activity and notifications

Figures come from the live mock stores, so they change when data changes. Relative dates move with the run date. Only the seeded list of notifications exists; see DSH-11.

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| DSH-01 | + | Total employees | Dashboard "Total employees" card, read against the Employee List | Equals the employees whose status is Active, Probation, On Leave or Suspended. Resigned and Terminated are excluded | Partly automated: dashboard › shows KPI cards with sample figures |
| DSH-02 | + | New joiners | "New joiners" card with the hint "Last 30 days" | Counts employees whose joining date is within the 30 days up to today | New |
| DSH-03 | + | Pending leave approvals | Dashboard leave approvals panel, with EMP017 and EMP026 Submitted leave in the seed | The count equals Submitted applications. The list shows up to five, earliest start date first | Partly automated: dashboard › lists pending leave approvals and upcoming events |
| DSH-04 | + | Attendance today | Dashboard attendance card on a working day, then on a weekend | Scheduled excludes weekends and holidays. On a non-working day the card shows the most recent working day. Rate equals (Present + Late + Half Day) divided by Scheduled | New |
| DSH-05 | + | Recruitment summary | Dashboard recruitment card and funnel | Open jobs equal Open status; open positions equal the sum of their vacancies; upcoming interviews are Scheduled from today; the funnel counts applications per stage | New |
| DSH-06 | + | Payroll summary | Dashboard payroll card | Uses the latest period that is not a Draft (a Paid month in the seed), with the change in net pay against the previous period | New |
| DSH-07 | + | Upcoming events | Dashboard upcoming events | Holidays, birthdays, work anniversaries, probation reviews and approved leave starts within 30 days; at most 10, sorted by date | Partly automated: dashboard › lists pending leave approvals and upcoming events |
| DSH-08 | + | A change writes an activity entry | Create shift `TST-DAY` (ATT-01); then open the dashboard | The newest activity entry in the attendance category names the shift, with the signed-in user as actor. The dashboard shows the six most recent entries | New |
| DSH-09 | + | A workflow writes an activity entry | Approve a submitted leave application | Entry "LV-… approved" in the leave category, with the approver as actor | New |
| DSH-10 | + | Notifications: read state | Open the bell menu; open one notification; then use the mark-all action | The opened notification is marked read; after mark-all every notification is read | New |
| DSH-11 | − | Changes do not add notifications | Create or approve a record, then open the bell menu. Confirm against product intent: no code adds notifications | The list is unchanged (the seeded notifications only) | New. Confirm against product intent |
| DSH-12 | + | KPI card links | Click a KPI card | Navigates to the related screen | Automated: dashboard › KPI card links navigate |

## 13. Excel import and export

Excel import is registered for **Employees** only (`ui/src/app/mock-services/excel/modules/employee-excel.module.ts`). Fixtures are named in `docs/test-data/` (see its README). They are fictional. Check their codes against the seed before you assert on them. The file parser accepts `.xlsx` only, runs no formulas and refuses macros and external links. Limits: 5 MB per file, 5,000 data rows, 5 worksheets.

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| XLS-01 | + | Download the template | Import Data > Employees > Download template | File `employees-import-template.xlsx` with an Instructions sheet, an `Employees` sheet (header row only), a Field guide sheet and a hidden Lists sheet | Partly automated: imports employees through the wizard: template, preview, partial import, history and report |
| XLS-02 | + | Valid file | `1-employees-valid.xlsx`; strategy Insert or update; mode All or nothing | Every row Valid; import reports "N inserted, 0 updated." | Partly automated: imports employees through the wizard (same test) |
| XLS-03 | + | Update-only file | `3-employees-update-only.xlsx`; strategy Update only | Matching codes are updated. A code that is not in the seed is Invalid: "No existing record has this Employee Code, so it cannot be updated." | New |
| XLS-04 | − | Insert only refuses existing records | `1-employees-valid.xlsx` with strategy Insert only, after XLS-02 | Rows are Duplicate: "A record with this Employee Code already exists." | New |
| XLS-05 | − | File with errors | `2-employees-with-errors.xlsx` | Preview shows Invalid and Duplicate rows with a message for each. Mode defaults to "Import valid rows only". "All or nothing" is disabled | New |
| XLS-06 | + | Error report | After XLS-05, download the report | File `employees-import-report-IMP-….xlsx` with the columns Row, Field, Original value, Severity, Error and Suggested correction, one line per issue | New |
| XLS-07 | − | Headers do not match the template | `4-employees-bad-headers.xlsx` | The wizard asks for column mapping. Validation is refused until required columns are mapped: 'The required column "…" is not mapped.' | Partly automated: asks for column mapping when the file does not follow the template |
| XLS-08 | − | Not a real Excel workbook | `5-not-really-excel.xlsx` | "The file is not a valid Excel workbook (its content does not match the .xlsx format)." Check the fixture content before asserting, because this document does not verify it | Automated: rejects files that are not real Excel workbooks |
| XLS-09 | − | Other formats | A copy of an Excel file saved as `.xls`, `.csv` or `.xlsm` (created in the test) | "Only Excel workbooks (.xlsx) can be imported. Other formats, including .xls, .xlsm and .csv, are not accepted." | New (generated file) |
| XLS-10 | − | Macros and external links | Generated workbook containing a macro project part, then one with an external link part | "The workbook contains macros, which are not allowed. …"; "The workbook contains links to other workbooks, which are not allowed. …" | New (generated file) |
| XLS-11 | B | Size and row limits | A file over 5 MB; a sheet with 5,001 data rows | "The file is too large. The maximum size is 5 MB."; "The worksheet has more than 5000 data rows. Split the file into smaller files." | New (generated file) |
| XLS-12 | − | Empty file or no header | A zero-byte `.xlsx`; a worksheet whose first row is blank | "The file is empty."; "The first row must contain the column headers." | New (generated file) |
| XLS-13 | + | Import history | After XLS-02, open Import History, then open the batch | The batch is listed as Completed with its row counts and uploader. The details dialog lists the issues. After a page reload the history is empty | New |
| XLS-14 | + | Export the filtered employee list | Employee List: search `Daniel`, then Export | Workbook contains only the matching rows. Activity "Exported N Employees record(s) to Excel" | Automated: Excel export › exports the employees matching the current filters as a formatted workbook |
| XLS-15 | + | Sensitive columns | Export with and without the sensitive columns | Sensitive columns are included only when requested | Automated: Excel export › exports everything with sensitive columns only when requested |
| XLS-16 | − | Export without columns | Clear every column | Refused: at least one column is required | Automated: Excel export › requires at least one column |
| XLS-17 | − | Export for other modules | Ask for an export of a module other than Employees (service level) | Refused: "Export is not available for …" (only Employees is registered) | New (service level) |

## 14. Payroll

All money in this section is demonstration data. The rules in `ui/src/app/core/utilities/payroll-calc.ts` (`PAYROLL_RULES`: overtime at 1.5 times the hourly rate, 8 hours per working day) are sample rules. They are not tax, social-security or labour-law calculations, and the mock payroll must never be used for real pay.

Process rules, from `ui/src/app/mock-services/payroll/`: a run moves Draft to Pending Approval (process), to Approved (approve), to Paid (mark paid). Return to draft works from Pending Approval or Approved. An earlier period that is still Draft or Pending Approval blocks processing of a later one. A period cannot be in the future.

| ID | Type | Scenario and steps | Example data | Expected result | Status |
|---|---|---|---|---|---|
| PAY-01 | + | Create an earning component | Code `TST-ALW`, Earning, Fixed 25000, Active | Saved; basis shown as "Fixed 25000" | New |
| PAY-02 | − | Percentage above 100 | Calculation PercentOfBasic, value 120 | "A percentage cannot exceed 100." | New |
| PAY-03 | − | Zero value | Value 0 | "The amount or percentage must be greater than zero." | New |
| PAY-04 | − | Duplicate component code | Code `HOUSING` | Refused on the Code field, as already in use | New |
| PAY-05 | + | Structure with an earning | Code `TST-STF`; components HOUSING and TAX | Saved | New |
| PAY-06 | − | Structure without an earning | Only TAX | "A structure needs at least one earning (an allowance)." | New |
| PAY-07 | − | Component used by a structure | Delete HOUSING | Refused, with the count of salary structures | Partly automated: payroll setup › manages salary components and protects those used by a structure |
| PAY-08 | + | Salary within the grade band | EMP056 Sai Htoo Aung (G2, band 600,000 to 1,000,000); structure STAFF; basic 800,000; effective `2026-12-01` | Saved | New |
| PAY-09 | − | Basic outside the grade band | Same employee, basic 1,100,000 | "Basic salary must be within the G2 band (600000 – 1000000)." | New |
| PAY-10 | − | Salary before joining | Same employee, effective `2026-01-01` (joined `2026-01-12`) | "The salary cannot start before the joining date." | New |
| PAY-11 | − | Second assignment on the same date | Same employee, effective `2026-12-01` again | "This employee already has a salary starting on that date." | New |
| PAY-12 | − | Last assignment cannot be deleted | Delete the only assignment of an employee | "An employee needs at least one salary assignment. Add a new one instead." | New |
| PAY-13 | + | Create the current period | Period for the current month; pay date later in the same month | Created as Draft. The period name is the month and year | Partly automated: payroll processing workflow › processes, reviews, approves and pays the current period |
| PAY-14 | − | Period in the future | Month after the current one | "A payroll period cannot be in the future." | New |
| PAY-15 | − | Pay date and duplicate month | Pay date before the period start; then a second period for an existing month | "The pay date cannot be before the period starts."; "A payroll period for … already exists." | New |
| PAY-16 | + | Process a draft | Current-month Draft; Process | Status Pending Approval; employee count shown; payslips created. Employees without an assignment are listed as warnings: "… has no salary assignment and was skipped." | Automated: payroll processing workflow › processes, reviews, approves and pays the current period, then shows payslips |
| PAY-17 | − | Period order is enforced | Create a Draft for a month earlier than the seeded six months; then process the current month | "Finish <period> (Draft) before processing <period>." | Partly automated: payroll processing workflow › returns a processed period to draft and enforces period order |
| PAY-18 | − | Approve before processing | Approve a Draft | "Only a period pending approval can be approved (this one is Draft)." | New |
| PAY-19 | + | Approve, then mark paid | Pending Approval: Approve (approver is the signed-in user, date today); then Mark paid | Approved, then Paid (sample) | Partly automated: payroll processing workflow › processes, reviews, approves and pays |
| PAY-20 | − | Mark paid before approval | Pending Approval: Mark paid, if the action is offered | "Only an approved period can be marked as paid (this one is Pending Approval)." | New |
| PAY-21 | + | Return to draft | Pending Approval: Return to draft with a reason | Payslips are removed and totals reset to zero. The activity entry records the reason | Automated: payroll processing workflow › returns a processed period to draft |
| PAY-22 | − | Paid period cannot return to draft | Paid period: Return to draft | "Only a period pending approval or approved can be returned to draft (this one is Paid)." | New |
| PAY-23 | − | Only a draft can change or be deleted | Edit or delete a Pending Approval period | "Only a draft period can be changed. Return it to draft first."; delete: "Only a draft period can be deleted." | New |
| PAY-24 | − | Separation of duties | A user processes a run, then approves the same run | Expected: refused (the rule this suite assumes). The mock approves it, because `approve` has no check for this. Confirm against product intent: separation of duties is not implemented | New. Confirm against product intent |
| PAY-25 | + | Payslip content | Payslips, then the EMP056 payslip | Shows basic, earnings with their basis, deductions, gross, total deductions and net (net equals gross minus deductions). Bank account is masked as "•••• " with the last four digits | New |
| PAY-26 | + | Calculation rules | Mid-month joiner; approved overtime; approved unpaid leave | Covered by unit tests: `ui/src/app/core/utilities/payroll-calc.spec.ts`. The E2E suite spot-checks one line of one payslip | Unit test |
| PAY-27 | + | Payroll history | Payroll History | Processed periods only (Drafts are excluded), newest first, with a table alternative to the chart | Automated: payroll processing workflow › shows payroll history with a table alternative to the chart |
| PAY-28 | + | Salary summary on a profile | Employee profile of an employee with an assignment | Shows the current salary summary. The Salary tab stays on the mock in the API build | Automated: payroll processing workflow › shows the salary summary on an employee profile |
| PAY-29 | − | Employee who joined after the period | Joining date after the period end | No payslip for that period (the employee is not employed then) | New |

