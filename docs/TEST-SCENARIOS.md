# HRMS Test Scenarios by Module

**ဘာသာစကား / Languages:** [English](#english) · [မြန်မာဘာသာ](#myanmar)

<a id="english"></a>

Derived from the implemented rules (see [HR-BUSINESS-ANALYSIS.md](HR-BUSINESS-ANALYSIS.md)). Expected results describe what the **current code does**.

- **Type:** `+` positive, `−` negative (rule must reject), `B` boundary, `X` cross-module, `G` **Known gap** (current behavior is debatable; the test documents it so a change is noticed).
- **Environment:** frontend only. State resets on reload, so navigate in-app (no `page.goto` between steps). Weekend = Sat/Sun. Logged-in user is always Su Myat Noe (employee ID 2, HR Administrator).
- **Today:** examples assume today = Wed 2026-11-04. Adjust if you run on another date.
- **Automation:** see section 13 for which suite covers which module. Calculator cases (ATT-0x, BAL-xx, PAY-Cxx) map to `attendance-calc.spec.ts`, `leave-balance.spec.ts`, `payroll-calc.spec.ts`.
- **Dates:** type dates as `dd/mm/yyyy` in the app (e.g. 04/11/2026); Excel files use `yyyy-MM-dd` or `dd MMM yyyy`. Examples below are written in ISO for clarity.
- **Where to start:** run the **smoke suite (section 12)** after every build; run full module tables before release.
- **Test data:** see **section 0** for the exact seeded companies, shifts, holidays, policies and the employees (EMP001, EMP050, ...) each scenario uses. Counts were checked on 2026-11-04. Reload resets everything, so destructive tests can run on seed records.
- **Result recording:** for each run note the build, browser, date, ID, Pass/Fail and a screenshot for failures. A `G` row that now behaves differently is a **change to report**, not necessarily a bug.

---

## 0. Seed Data Reference

Scenarios below use these seeded records (`src/app/mock-data`). **Fixed** data never changes; **relative** data moves with today's date, so re-check counts when you run on another day. Figures were checked against the seed as of **2026-11-04**.

### 0.1 Fixed data

| Area | Records |
|---|---|
| Companies | AHT Apex Horizon Technologies (Active), AHL Apex Horizon Logistics (Active), AHV Apex Horizon Ventures (**Inactive**, no branches or staff) |
| Branches | YGN-HQ Yangon Head Office, MDY-BR Mandalay Branch, NPT-BR Nay Pyi Taw Office, SG-RO Singapore Regional Office (AHT); MDY-DC Mandalay Distribution Center, YGN-LH Yangon Logistics Hub (AHL) |
| Departments (11) | Executive, Human Resources, Finance & Accounts, Engineering, Product Management, Sales (Mandalay), IT Infrastructure, Customer Support (Nay Pyi Taw), Warehouse Operations (MDY-DC), Fleet & Logistics (YGN-LH), Marketing (Singapore) |
| Job grades | G1 400–700, G2 600–1000, G3 900–1500, G4 1400–2200, G5 2000–3000, G6 2800–4200, G7 4000–6000, G8 5500–8500, G9 8000–14000 |
| Shifts | GEN 09:00–18:00 break 60 grace 15 Mon–Fri; WH-AM 06:00–14:30 break 30 grace 10 Mon–Sat; WH-PM 14:00–22:30; SUP 08:00–17:00 break 60 grace 10 Mon–Sat; NIGHT 22:00–06:00 (**Inactive**) |
| Holidays near today | Fri 20/11/2026 Company Foundation Day (Company), Mon 23/11/2026 Full Moon Festival, 25/12/2026 Christmas, 31/12/2026 Year-End |
| Leave types | AL 15 (max 10 consecutive), CL 6 (max 3), ML 30 (document), MAT 98 (female, document), PAT 15 (male, document), UL 30 (unpaid), CPL 5 |
| Leave policies | Annual: standard 15 after 3 months, G6 18, G7 20, G8 22, G9 25, Contract 10, Intern 6 monthly; Casual 6 monthly; Medical for interns 10 |
| Salary components | HOUSING 10% basic, TRANSPORT 50, MEAL 30, PERFORMANCE 5% basic, EXECUTIVE 300, TAX 5% gross, SOCIAL 2% gross, PENSION 3% basic (**Inactive**, unused) |
| Salary structures | STAFF (G1–G5), MGMT (G6–G7), EXEC (G8–G9) |
| Roles | HR Administrator and Employee are built-in; Auditor is Inactive; Department Manager has 4 users |
| Job openings | Senior Angular Developer (Open, 2), QA Engineer (Filled), Product Designer (Open), Account Executive (Open, 2), Customer Support Agent (Open, 3), HR Intern (On Hold), Financial Analyst (Closed, closing date passed), DevOps Engineer (Draft) |

### 0.2 Useful employees

| ID | Name | Why it is useful |
|---|---|---|
| EMP001 | Thura Aung | CEO. Manages 6 people (EMP002, 003, 004, 007, 011, 012), so delete is refused. Male |
| EMP002 | Su Myat Noe | **The signed-in user.** HR Director, the only HR Administrator, shift GEN. Female |
| EMP005 | Daniel Carter | Engineering Manager (G6). Code and email used for duplicate tests |
| EMP012 | Zaw Lin Oo | Warehouse Manager, shift WH-AM, user account **Locked** |
| EMP017, EMP026 | Phyu Sin Thant, Shwe Sin Oo | Submitted Annual leave waiting for a decision; EMP017 also has a Pending attendance adjustment |
| EMP019 | Wai Yan Soe | Software Engineer (G3), used for transfer and position-change tests |
| EMP020, EMP031 | Yamin Aye, Linn Htet Maung | Approved leave that has already started (cannot be cancelled) |
| EMP034 | Soe Moe Kyaw | **Resigned**, exit 2026-08-31 (not on the November run) |
| EMP035 | Aye Chan Moe | On Leave (98-day maternity) |
| EMP038, EMP054 | Bo Bo Han, Zarni Tun | Contract staff |
| EMP042 | Ko Ko Gyi | Suspended |
| EMP044 | Tun Tun Naing | **Terminated**, exit 2026-06-15 (not on the November run) |
| EMP047 | Lwin Mar Htwe | Part-time |
| EMP048 | Aung Myo Oo | Evening shift WH-PM; **only one** salary assignment |
| EMP050 | Rahul Sharma | Relative: joined 12 days ago, Probation, under 3 months, annual entitlement 0 |
| EMP052 | Phone Myint Kyaw | Male, Submitted Paternity leave |
| EMP053, EMP058 | Ingyin Thwe, Chan Myae Aung | Interns (relative joining: 2 and 20 days ago); EMP053 is G1 |
| EMP056 | Sai Htoo Aung | No seeded leave, joined 2026-01-12: clean employee for leave tests |
| EMP057, EMP060 | Nang Hseng Hom, Andrew Lim | Joined 35 and 50 days ago; EMP060 has no direct reports and can be deleted |

### 0.3 Relative data (moves with today's date)

- Attendance: 60 days of generated history; overtime is derived from records with at least one hour of overtime; adjustments attach to those records.
- Leave applications start relative to today, so "starts in 3 days" shifts each day.
- Payroll: the last six months are **Paid** (May–Oct 2026); the **current month is a Draft** (November 2026).
- Probation: employees whose joining date is within three months (EMP050, 053, 057, 058, 060 today).
- Every seeded employee has a salary assignment. Long-serving staff have two (joining date and 01/01 of this year).

### 0.4 What the seed data cannot show

- **A user without a salary assignment** (PAY-P08, XMOD-02): create a new employee first.
- **Exact punch times** (ATT-02 to ATT-08): the Check-in screen uses the real clock. Use adjustments or the unit tests.
- **The last-administrator guard** (SET-08): the signed-in user is the only administrator, so the own-account rule fires first.
- **Branch or department change without a position change** (EMP-23): the organisation chain validation may prevent it.
- **Passwords and sign-in**: there is no authentication, so role-based access cannot be tested (SET-10).

## 1. Employee Management

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| EMP-01 | + | Open the add-employee form | Defaults: Full-time, status Probation, probation end = today + 3 months |
| EMP-02 | + | Create a new employee (e.g. Nilar Htay: company AHT, branch Yangon Head Office, department Engineering, position Software Engineer), fill all required fields, save | Created as **EMP061**; history row "Joined as Software Engineer" |
| EMP-03 | − | Save with employee code `EMP005` (also `emp005`) | Rejected: "Employee code ... already exists" |
| EMP-04 | − | Save with work email `daniel.carter@apexhorizon.example` (also `Daniel.Carter@apexhorizon.example`) | Rejected on work email |
| EMP-05 | − | Date of birth = tomorrow | "cannot be in the future" |
| EMP-06 | B | Date of birth `04/11/2008` (exactly 18 years before 2026-11-04) / `05/11/2008` | Accepted / Rejected ("at least 18") |
| EMP-07 | − | Joining date `02/11/2026`, probation end `01/10/2026` | Rejected on probation end |
| EMP-08 | − | Joining date `02/11/2026`, exit date `01/10/2026` | Rejected on exit date |
| EMP-09 | − | Edit EMP054 Zarni Tun: status Resigned, exit date empty | "An exit date is required ..." |
| EMP-10 | − | Company Apex Horizon Logistics with branch Yangon Head Office (an AHT branch) | "branch does not belong to the chosen company" |
| EMP-11 | − | Branch Yangon Head Office with department Warehouse Operations (which sits in Mandalay Distribution Center) | Rejected on department |
| EMP-12 | − | Department Engineering with position Customer Support Agent | "belongs to a different department" |
| EMP-13 | − | Department Engineering with section Tier 1 Support | Rejected on section |
| EMP-14 | − | Choose company Apex Horizon Ventures (Inactive) or shift Night Shift (Inactive). For other units, deactivate one first (e.g. Marketing) | "This ... is inactive" |
| EMP-15 | + | Deactivate department Marketing, then edit EMP032 Emily Wong (Digital Marketing Specialist) changing only a phone number | Saves (existing assignment may stay) |
| EMP-16 | − | (a) Edit EMP005 Daniel Carter and open the *Reporting manager* list. (b) Excel import, Update only: row with employee code `EMP005` and manager code `EMP005` | (a) Daniel Carter is **not in the list** (the form hides the employee themselves). (b) Rejected: "An employee cannot be their own manager." / "Choose a different manager." on the manager field |
| EMP-17 | − | Excel import row with manager code `EMP999` | "manager does not exist" |
| EMP-18 | − | Delete EMP001 Thura Aung (CEO) | Conflict: "Thura Aung manages 6 employee(s)" (EMP002, 003, 004, 007, 011, 012). Reassign them first |
| EMP-19 | + | Delete EMP060 Andrew Lim (no direct reports) | Deleted; documents and history removed |
| EMP-20 | G | After EMP-19, check Andrew Lim's attendance (60-day history), salary assignment and the September/October payslips | Records remain (orphans); attendance list shows "Employee #id" |
| EMP-21 | + | Edit EMP046 Hein Thant Zin: position Software Engineer (G3) → Senior Backend Engineer (G4) | History "Promotion" |
| EMP-22 | + | Edit EMP019 Wai Yan Soe: position Software Engineer (G3) → QA Engineer (G3) | History "Transfer" (no demotion type) |
| EMP-23 | + | Change only branch or department (keep position) | History "Transfer: Organization assignment changed". Branch, department and position must stay consistent, so this may be unreachable from the form: record what you find |
| EMP-24 | G | Change only the job grade (same position) | No history entry |
| EMP-25 | + | Edit EMP050 Rahul Sharma (Probation, joined 23 Oct 2026): status → Active | History "Status Change"; no automatic probation confirmation exists |
| EMP-26 | + | Edit EMP054 Zarni Tun: status Resigned, exit date `20/11/2026` | History "Exit"; no other record changes (see XMOD-06) |
| EMP-27 | + | Edit EMP047 Lwin Mar Htwe: Part-time → Full-time | History "Contract Change" |

| EMP-28 | + | Search by code/name/email/position, then filter. Status is under **More filters** with department, branch and type | As of 2026-11-04: Resigned 1 (EMP034), Terminated 1 (EMP044), Suspended 1 (EMP042), On Leave 1 (EMP035), Probation 5 (EMP050, 053, 057, 058, 060); type Contract 3 (038, 044, 054), Intern 2, Part-time 1; department Customer Support 6; joined 01/01/2014–31/12/2015 = 3. Filters combine with AND |
| EMP-29 | + | Add a document (file name only) and delete it | Listed on the profile Documents tab |
| EMP-30 | + | Open EMP001 Thura Aung › Salary | Full-month figures from the assignment effective 01/01/2026 (annual revision) |
| EMP-31 | − | Create a new employee and open its Salary tab (every seeded employee already has an assignment) | "has no salary assignment yet" |
| EMP-32 | + | Excel import of valid employees | Created with the same validation; each gets a "Joined" history row |
| EMP-33 | − | Excel import with one invalid row, in all-or-nothing mode | Nothing imported; problem details and validation report available |

## 2. Organization Management

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| ORG-01 | + | Create company, branch, department, section, position, job grade (valid chain) | Saved |
| ORG-02 | − | Duplicate code: company `AHT`, branch `YGN-HQ`, department `ENG`, section `HR-TA`, position `POS-001`, grade `G1` | "must be unique" |
| ORG-03 | − | Create a company named "Apex Horizon Technologies" | Rejected |
| ORG-04 | − | New department with branch Yangon Head Office but company Apex Horizon Logistics | Rejected on branch |
| ORG-05 | − | New position in department Engineering with section Tier 1 Support | Rejected on section |
| ORG-06 | − | Position set to report to itself | "cannot report to itself" |
| ORG-07 | B | Job grade with min 400: max 400 / max 300 | Accepted / Rejected |
| ORG-08 | − | Delete company AHT, branch YGN-HQ, department Engineering, section ENG-FE, grade G1 or position POS-001 | Conflict listing the usage counts, "Deactivate it instead" |
| ORG-09 | + | Deactivate department Marketing | Allowed; it can no longer be newly assigned to employees (EMP-14) |
| ORG-10 | + | Delete company Apex Horizon Ventures (Inactive, no branches or employees) | Deleted |
| ORG-11 | X | Change grade G1 band from 400–700 to 450–650 after salaries exist | Existing assignments unchanged; new assignments validated against the new band |
| ORG-12 | + | Open the Reporting Structure view | Renders (detail not covered by this document) |

## 3. Attendance Management

Reference shift: GEN 09:00–18:00, break 60, grace 15, Mon–Fri.

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| ATT-01 | + | Punch in/out as Su Myat Noe (GEN shift 09:00–18:00). **The Check-in screen uses the real clock**: for exact times use an adjustment (ADJ) or `attendance-calc.spec.ts`. Case: in 09:00, out 18:00 | Present; worked 480 (540 − 60 break); late 0; early 0; overtime 0 |
| ATT-02 | B | Check in 09:15 | Present (within grace), late 0 |
| ATT-03 | B | Check in 09:16 | Late; **late minutes = 16** (counted from start) |
| ATT-04 | + | Check in 09:30 | Late, 30 min, remark "Late arrival" |
| ATT-05 | + | Check in 09:00, out 12:30 | Half Day; worked 210; early leave 0 |
| ATT-06 | B | Net minutes exactly 240 vs 239 | 240 → not half day; 239 → Half Day |
| ATT-07 | + | Out 17:00 | Early leave 60 |
| ATT-08 | B | Out 18:30 / 18:31 | OT 0 / OT 31 (only above 30; all minutes counted) |
| ATT-09 | − | Check in twice as Su Myat Noe (reload first if today already has a record) | Conflict "Already checked in at ..." |
| ATT-10 | − | Check out without check in | Conflict "Check in before checking out" |
| ATT-11 | − | Check out twice | Conflict "Already checked out" |
| ATT-12 | − | Day shift: check out time ≤ check-in time | Validation error |
| ATT-13 | + | Night shift (e.g. 22:00–06:00): in 22:00, out 06:00 | Accepted; worked 8h minus break; no "end before start" error |
| ATT-14 | + | Punch on Sat/Sun, or on Fri 20/11/2026 (Company Foundation Day) / Mon 23/11/2026 (Full Moon Festival) | Status Present, remark "Worked on a ...", late/early 0, no overtime created |
| ATT-15 | − | Punch on a day marked On Leave | Conflict "You are on approved leave on this day" |
| ATT-16 | + | Employee with no shift punches | Uses the first shift in the list |
| ATT-17 | − | Create a day shift with end ≤ start (night shift off) | Rejected |
| ATT-18 | − | Create a shift with no working days; a duplicate code `GEN` | Rejected |
| ATT-19 | − | Delete shift GEN (assigned to most employees) | Conflict listing assigned employees |
| ATT-20 | X | Change EMP012 Zaw Lin Oo from WH-AM to WH-PM, then check old and new records | New punches use the new shift; existing records keep the old `shiftId`, not recalculated |
| ADJ-01 | + | Create a request for EMP017 Phyu Sin Thant on a day with a record (a Pending one already exists) | Pending; current times captured |
| ADJ-02 | − | Request for a future date | "cannot be in the future" |
| ADJ-03 | − | Request for a date with no attendance record | "no attendance record for this employee on that date" |
| ADJ-04 | − | Request with no time and no status | "Request at least one change" |
| ADJ-05 | − | Day shift: requested out ≤ in | Rejected |
| ADJ-06 | − | Create a second Pending request for EMP017 on the date of her seeded Pending request | Rejected |
| ADJ-07 | + | Approve the seeded request of EMP040 Nay Myo Aung (in 06:05, out 14:30, shift WH-AM) | Record re-derived: in 06:05 is within the 10-minute grace, so Present; source = Manual; remark "Adjusted (ADJ-nnnn)" |
| ADJ-08 | + | Approve the seeded request of EMP033 Zin Mar Oo (forced status Half Day) | Status forced to Half Day; derived minutes follow it |
| ADJ-09 | + | Reject the seeded request of EMP021 Naing Lin Tun | Record unchanged; request locked |
| ADJ-10 | − | Edit, delete or decide the Approved request of EMP016 or the Rejected request of EMP012 | Conflict |
| ADJ-11 | G | Requester approves their own adjustment | Allowed (no self-approval check) |
| OT-01 | + | Create overtime 18:00–20:00 | 120 minutes; Pending |
| OT-02 | B | Duration 14 min / 15 min / 12 h / 12 h 01 min | Rejected / Accepted / Accepted / Rejected |
| OT-03 | − | Future date | Rejected |
| OT-04 | − | Second entry for same employee and date | Rejected |
| OT-05 | G | Reject an overtime entry, then create a new one for the same date | Blocked (duplicate check ignores status) |
| OT-06 | + | Approve / reject | Status, decider and date recorded |
| OT-07 | G | Requester approves their own overtime | Allowed |

## 4. Leave Management

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| LV-01 | + | EMP026 Shwe Sin Oo: Annual Leave Mon `09/11/2026` – Wed `11/11/2026`, save as Draft | Number `LV-YYYY-NNNN`, status Draft, days = 3 |
| LV-02 | + | EMP056 Sai Htoo Aung: Fri `13/11/2026` – Mon `16/11/2026` | Days = 2 (weekend excluded) |
| LV-03 | + | Thu `19/11/2026` – Tue `24/11/2026` (Fri 20 Company Foundation Day and Mon 23 Full Moon are holidays) | Days = 2 (Thu 19 and Tue 24) |
| LV-04 | − | Sat `21/11/2026` – Mon `23/11/2026` (weekend + Full Moon Festival) | "no working days" |
| LV-05 | − | End before start | Rejected |
| LV-06 | + | EMP056: half day on Wed `11/11/2026` | Days = 0.5 |
| LV-07 | − | Half day with different start/end | "single date" error |
| LV-08 | − | EMP050 Rahul Sharma (joined 23/10/2026): start `20/10/2026` | Rejected |
| LV-09 | B | EMP056, Casual Leave: Mon 09 – Wed 11 Nov (3 days) / Mon 09 – Thu 12 Nov (4 days) | Accepted / "limited to 3 consecutive days" |
| LV-10 | − | Maternity for EMP001 Thura Aung (male) / Paternity for EMP002 Su Myat Noe (female) | "not available to this employee" |
| LV-11 | − | Deactivate Compassionate Leave, then apply with it | "Choose an active leave type" |
| LV-12 | − | EMP017 Phyu Sin Thant: dates overlapping her Submitted Annual leave (starts in 3 days, 3 calendar days) | "overlap LV-..." |
| LV-13 | + | EMP049 Yoon Wadi Kyaw: overlap her Draft (starts in 20 days); or EMP038 Bo Bo Han: overlap his Rejected leave (20 days ago) | Allowed |
| LV-14 | + | Edit or delete a Draft | Allowed |
| LV-15 | − | Edit or delete a Submitted/Approved request | Conflict |
| LV-16 | + | EMP056, Annual Leave 3 working days (entitled 15), submit | Status Submitted; days now counted as pending |
| LV-17 | − | EMP050 Rahul Sharma (under 3 months' service, so Annual entitlement 0): submit 1 day of Annual Leave | "Insufficient ... balance: 1 requested, 0 available" |
| LV-18 | − | EMP056: Medical Leave 1 day with no document, submit | "needs a supporting document" |
| LV-19 | − | Submit a non-Draft | Conflict |
| LV-20 | + | As Su Myat Noe approve the seeded Submitted Annual leave of EMP026 Shwe Sin Oo (3 working days, starts in 8 days) | Approved; balance moves pending → taken; remarks default "Approved." |
| LV-21 | − | Create and submit leave for EMP002 Su Myat Noe, then approve or reject it | "You cannot decide your own leave request" |
| LV-22 | − | Reject with empty reason | "A reason is required" |
| LV-23 | + | Reject with reason | Rejected; days released |
| LV-24 | − | Decide a request that isn't Submitted | Conflict |
| LV-25 | + | Cancel the Submitted leave of EMP017 Phyu Sin Thant | Cancelled; days returned |
| LV-26 | + | After LV-20, cancel EMP026's Approved leave (starts in 8 days) | Cancelled |
| LV-27 | − | Cancel the Approved leave of EMP031 Linn Htet Maung (started today) or EMP020 Yamin Aye (started yesterday) | "already started cannot be cancelled" |
| LV-28 | X | Create Annual Leave for EMP056 covering past weekdays that have Present/Late records (within 60 days), submit, approve | Those records become On Leave, times cleared, remark = leave number |
| LV-29 | X | Same approval, with Weekend/Holiday records in range and future days | Weekend/Holiday untouched; no records created for future days |
| LV-30 | G | Cancel the approved leave from LV-28 (if allowed) | Attendance stays On Leave (not reverted) |
| LV-31 | G | Create leave for an employee who has Resigned/passed exit date | Allowed (only joining date validated) |
| LV-32 | G | Leave dated next year, submitted this year | Balance check uses the **current** year and today's date |
| LV-33 | + | Leave calendar for a month, with and without "include pending" | Approved always shown; Submitted only when included; weekends/holidays flagged |
| LV-34 | − | Delete Annual Leave (used by applications and policies) | Conflict |
| LV-35 | − | Leave type carry-forward max > yearly days; max consecutive < 1; duplicate code | Rejected |
| LV-36 | − | Create a second active Annual Leave policy with no grade or employment type (the standard policy already exists) | "An active policy already exists ..." |
| BAL-01 | + | EMP050 Rahul Sharma (service under 3 months), Annual Leave | Entitled 0 |
| BAL-02 | + | EMP056 Sai Htoo Aung (joined 12/01/2026), Annual Leave | Entitled 15 |
| BAL-03 | + | EMP005 Daniel Carter (Engineering Manager, G6) | Entitled 18 (grade policy beats default) |
| BAL-04 | + | EMP058 Chan Myae Aung (Intern), Annual Leave | Intern policy (6 days, monthly accrual) applies, not the standard 15 |
| BAL-05 | + | Casual leave (6/yr monthly) in November | 5.5 |
| BAL-06 | + | Leave type with no policy | Type default days |
| BAL-07 | + | Balances: entitled 15, taken 3, pending 2 | Available 10; never below 0 |
| BAL-08 | G | Carry-forward flag on Annual Leave, check next-year balance | Unused days are **not** carried over |
| BAL-09 | + | EMP054 Zarni Tun or EMP038 Bo Bo Han (Contract), Annual Leave | Contract policy: entitled 10 |
| BAL-10 | + | EMP047 Lwin Mar Htwe (Part-time), Annual Leave | Standard policy: entitled 15 (no part-time rule) |

## 5. Payroll Management

### 5.1 Setup

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| PAY-S01 | − | Component "Meal allowance" value 0 / −5 | Rejected |
| PAY-S02 | − | Edit TAX (percent of gross): value 120 | Rejected |
| PAY-S03 | − | New component with code `HOUSING` | Rejected |
| PAY-S04 | − | Delete HOUSING (used by every structure). Compare with PENSION (inactive, unused), which can be deleted | Conflict |
| PAY-S05 | − | Structure with no components / only TAX and SOCIAL | "Choose at least one component" / "needs at least one earning" |
| PAY-S06 | − | Delete structure STAFF (assigned to G1–G5 staff) | Conflict |
| PAY-S07 | B | EMP053 Ingyin Thwe (G1, band 400–700): basic 400 / 700 / 399 / 701 | Accepted / Accepted / Rejected / Rejected |
| PAY-S08 | − | Assignment effective before the joining date | Rejected |
| PAY-S09 | − | EMP001: add an assignment effective `01/01/2026` (one already exists) | Rejected |
| PAY-S10 | − | Deactivate structure MGMT, then assign it | "Choose an active structure" |
| PAY-S11 | − | Delete the only assignment of EMP048 Aung Myo Oo | "needs at least one salary assignment" |
| PAY-S12 | + | EMP005: add an assignment effective `01/11/2026` (raise) | Used for periods whose end date is on/after the effective date |

### 5.2 Period and workflow

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| PAY-P01 | + | Create period April 2026 (seeded periods are May–Oct 2026 Paid and Nov 2026 Draft) | Draft; name, dates derived; pay date defaults to month end |
| PAY-P02 | − | Create period December 2026 | "cannot be in the future" |
| PAY-P03 | − | Create period November 2026 | "already exists" |
| PAY-P04 | − | Pay date before the period start | Rejected |
| PAY-P05 | + | Process November 2026 (Draft) | 58 payslips (60 employees minus EMP034 and EMP044); status Pending Approval; totals filled |
| PAY-P06 | − | Create April 2026 (stays Draft), then process November 2026 | "Finish <period> before processing ..." |
| PAY-P07 | G | Process while the earlier period is Approved (not Paid) | Allowed |
| PAY-P08 | + | Create a new employee in this session (no assignment), then process November 2026 | Warning "... skipped"; others still processed |
| PAY-P09 | − | Create a period before any employee joined (e.g. February 2014), then process | "no employees to pay in this period" (verify the month is accepted) |
| PAY-P10 | + | Process November 2026 and look for EMP034 (exit 2026-08-31), EMP044 (exit 2026-06-15) and EMP053 Ingyin Thwe (joined 02/11/2026) | EMP034 and EMP044 absent; EMP053 present with proration 29/30 |
| PAY-P11 | + | Approve a Pending Approval period | Approved; approver name and date stored |
| PAY-P12 | − | Approve a Draft or an Approved period; process a non-Draft; mark paid before approval | Conflict with current status in the message |
| PAY-P13 | + | Mark an Approved period as paid | Paid; `paidOn` set |
| PAY-P14 | + | Return to draft from Pending Approval or Approved | Payslips removed, totals reset; reason logged |
| PAY-P15 | − | Try Return to draft / edit / delete on October 2026 (Paid) | Conflict (Paid is final) |
| PAY-P16 | − | Edit or delete a non-Draft period (e.g. Pending Approval November 2026) | Conflict |
| PAY-P17 | G | The same user processes, approves and marks paid | Allowed (no segregation of duties) |
| PAY-P18 | + | Re-process after Return to Draft | Payslips regenerated with current data |

### 5.3 Calculation (use the unit tests too)

| ID | Type | Scenario / Inputs | Expected result |
|---|---|---|---|
| PAY-C01 | + | Basic 3000, 22 working days, housing 10% of basic, transport 50, tax 5% of gross, 2 unpaid days, 2 OT hours | Housing 300; OT 51.14; gross 3,401.14; tax 170.06; unpaid 272.73; **net 2,958.35** |
| PAY-C02 | + | Full month, no leave or overtime | Basic as assigned; net = gross − component deductions |
| PAY-C03 | + | Joined on the 11th of a 30-day month | Proration 20/30; basic 2000 from 3000; fixed components also prorated |
| PAY-C04 | + | Exit date on the 20th | Proration 20/30 (exit day included) |
| PAY-C05 | + | Joined and left inside the same period | Days employed = exit − join + 1 |
| PAY-C06 | − | Employed zero days in the period | No payslip (calculator returns null) |
| PAY-C07 | + | OT 90 minutes | 1.5 h × hourly × 1.5; hourly = **full** basic ÷ (working days × 8) |
| PAY-C08 | + | Only **Approved** OT in the period counts | Pending/Rejected OT and OT dated outside the period ignored |
| PAY-C09 | + | Unpaid leave across the period boundary | Only days inside the period, weekends/holidays excluded |
| PAY-C10 | + | Half-day unpaid leave | 0.5 day |
| PAY-C11 | + | Paid leave types (Annual, Medical, ...) | No deduction |
| PAY-C12 | G | Unpaid leave with a percent-of-gross deduction | Gross is **not** reduced by unpaid leave, so the percent deduction uses the full gross |
| PAY-C13 | G | Mid-month joiner with unpaid leave or OT | Daily/hourly rate uses the **full** basic, not the prorated basic |
| PAY-C14 | + | Salary revised mid-month | Assignment in force at the **period end date** applies to the whole month |
| PAY-C15 | + | Inactive component in the structure | Ignored in the calculation |
| PAY-C16 | + | Late arrivals and Absent days in attendance | No effect on pay |
| PAY-C17 | + | Payslip is a snapshot: change a component or department afterwards | Existing payslip unchanged until the period is reprocessed |
| PAY-C18 | + | Payslip view / print and payroll history chart | Figures match the run totals |

## 6. Settings / Users / Roles (supporting)

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| SET-01 | − | Rename or delete the built-in roles HR Administrator or Employee | Rejected |
| SET-02 | − | Deactivate role Department Manager (4 users) | Conflict with user count |
| SET-03 | − | Edit HR Administrator permissions | "always has full access and cannot be changed" |
| SET-04 | − | Grant Create/Edit without View | "View is required" |
| SET-05 | − | Create a user for EMP001 (already has one) / EMP034 Soe Moe Kyaw (exit date set) | Rejected |
| SET-06 | − | Change the linked employee of an existing user | Rejected |
| SET-07 | − | As Su Myat Noe: change own role/status or delete own account | Conflict |
| SET-08 | − | Deactivate/demote/delete the only HR Administrator (Su Myat Noe, EMP002) | "There must always be one active HR Administrator". The own-account rule (SET-07) fires first, so create a second administrator to reach this guard |
| SET-09 | + | Unlock EMP012 Zaw Lin Oo (Locked) | Status Active |
| SET-10 | G | Log in conceptually as a role without payroll access and open Payroll | Still accessible (permissions not enforced) |
| SET-11 | − | General settings: bad email, unlisted timezone/date format, currency not 3 capital letters, month outside 1–12 | Field errors |

## 7. Cross-module (end-to-end) scenarios

| ID | Type | Scenario | Expected result |
|---|---|---|---|
| XMOD-01 | X | New employee (EMP061, joined 02/11/2026) → salary assignment → punch → process November 2026 | One payslip; basic prorated from joining date |
| XMOD-02 | X | New employee **without** salary assignment, then process | Warning "no salary assignment ... skipped"; no payslip |
| XMOD-03 | X | Unpaid Leave (UL) for EMP056, 2 working days in November, approve → process November 2026 | "Unpaid leave" deduction = days × basic ÷ working days |
| XMOD-04 | X | **Leave to attendance:** annual leave approved over days that already have attendance | Records show On Leave; punch on those days is refused |
| XMOD-05 | X | **OT to payroll:** create → approve OT → process | "Overtime pay" line; punch-derived OT alone adds nothing |
| XMOD-06 | X | Resign EMP054 Zarni Tun with exit date 20/11/2026; check pending leave, user account, subordinates, shift | Payroll prorated; **leave, user, reports, shift unchanged** (document as current behavior) |
| XMOD-07 | X | Move EMP019 Wai Yan Soe from Engineering to Sales (Account Executive) | History "Transfer"; manager, shift, salary unchanged; next payslip shows new department |
| XMOD-08 | X | **Late approval after processing:** process a period, then approve leave/OT for it | Payslips unchanged (stale). Return to Draft → reprocess → updated |
| XMOD-09 | X | Same as XMOD-08 after the period is **Paid** | No way to recompute or adjust (Known gap) |
| XMOD-10 | X | **Attendance edit after payroll:** approve an adjustment for a processed month | No effect on payslips; no lock |
| XMOD-11 | X | Promotion: change position to a higher grade, then try keeping the old basic salary | Promotion logged; existing assignment is not re-validated against the new band (Known gap) |
| XMOD-12 | X | Deactivate department Marketing (EMP011 Sophia Bennett, EMP032 Emily Wong and EMP051 Khaing Zar Chi still in it) | Allowed; existing employees stay, no new assignments possible |
| XMOD-13 | X | Reload the page after a series of changes | All data returns to seed values |
| XMOD-14 | X | Recruitment: candidate marked Selected | No employee is created (manual registration required) |


## 8. Date Entry and Filters (UI)

Applies to every form built on the shared form field (employee form, leave, overtime, recruitment, CRUD dialogs) and to the **Joined from / Joined to** filters on the employee list. Format shown and typed: `dd/mm/yyyy`. Stored value: ISO `yyyy-MM-dd`.

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| DT-01 | + | Click inside a date box | Calendar opens |
| DT-02 | + | Click the calendar icon; press Esc | Opens; Esc closes it and the value is unchanged |
| DT-03 | + | Type `04/11/2026`, click away | Accepted; reopening the calendar highlights 4 Nov 2026 |
| DT-04 | + | Type `2026-11-04`, `4-11-2026`, `04 Nov 2026` | All accepted as 4 Nov 2026 |
| DT-05 | − | Type `31/02/2026`, `13/13/2026`, `abc`, `04/11/26` | "Enter a valid date."; nothing saved |
| DT-06 | B | `29/02/2028` / `29/02/2027` | Accepted / "Enter a valid date." |
| DT-07 | − | Clear a required date and leave the field | "<Label> is required." |
| DT-08 | + | Open an empty Date of birth calendar | Opens on the multi-year view at the year 18 years ago; two clicks (year, month/day) set a date |
| DT-09 | + | Pick a date in the calendar, then edit the text by hand | Text and calendar stay in sync |
| DT-10 | + | Keyboard only: Tab to the calendar icon, Enter, arrow keys, Page Up/Down, Enter | Opens, moves by day/month, selects; focus returns to the field |
| DT-11 | + | Open the calendar | Week starts on Monday |
| DT-12 | + | Edit an existing record | Stored date shows as `dd/mm/yyyy`; saved value is unchanged if untouched |
| DT-13 | X | Set the computer time zone to UTC−10, then UTC+13; save and reopen a date | The same calendar day (no one-day shift) |
| DT-14 | + | Employee list: type a valid *Joined from* date | List filters as soon as the date is valid (no need to click away) |
| DT-15 | + | Clear *Joined from* | Filter removed; all rows return |
| DT-16 | B | Set *Joined to*, open the *Joined from* calendar | Days after *Joined to* are disabled; same for *Joined to* before *Joined from* |
| DT-17 | + | Employee list: Status, Department, Branch, Employment type | Status sits in **More filters** with the others; the badge counts all four |
| DT-18 | + | Apply several filters, remove one chip, then **Clear filters** | Chip removal drops only that filter; Clear resets all including dates |
| DT-19 | + | Combine Status + Department + date range | Rows must match **all** filters; empty result shows "No employees found" |
| DT-20 | X | Phone width (360 px): open a calendar | Fits the screen; no horizontal page scroll |
| DT-21 | G | Open "from / to" filters on other lists (attendance, CRUD pages) and the Excel export dialog | Still the browser's native date input (different look, `mm/dd/yyyy` in some locales) |

## 9. Recruitment

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| REC-01 | − | Job opening with closing date before posted date; posted date in the future | Rejected on closing date / "cannot be in the future" |
| REC-02 | B | Vacancies 0 / 1 / 50 / 51 | Rejected / Accepted / Accepted / Rejected |
| REC-03 | − | Hiring manager EMP034 Soe Moe Kyaw (Resigned) or EMP044 Tun Tun Naing (Terminated) | "Choose a current employee as hiring manager." |
| REC-04 | + | DevOps Engineer: Draft → Open → On Hold → Open → Closed | Allowed |
| REC-05 | − | DevOps Engineer: Draft → On Hold; QA Engineer (Filled) → Open | "cannot be set to ..." (Filled has no exits) |
| REC-06 | − | Reopen Financial Analyst (Closed, closing date 20 days ago) | "The closing date has passed. Edit the opening ..." |
| REC-07 | − | Reopen when selected candidates already equal vacancies | "All vacancies already have a selected candidate." |
| REC-08 | − | Lower vacancies below the number already selected | "n candidate(s) are already selected for this job." |
| REC-09 | − | Delete an opening with applications (e.g. Senior Angular Developer; confirm it has applicants first) | Conflict with the application count |
| REC-10 | − | Candidate with experience −1 / 51 years, bad email, bad phone | Field errors |
| REC-11 | − | Same candidate applies twice to the same job | "already applied for this job" |
| REC-12 | + | Move Applied → Screening → Interview → Selected | Allowed in order |
| REC-13 | − | Skip a stage (Applied → Interview), or move out of Rejected | "cannot move to ..." |
| REC-14 | − | Reject with an empty reason | "A reason is required." |
| REC-15 | − | Select a candidate with no completed interview recommending Hire / Strong hire | "Selecting a candidate needs a completed interview ..." |
| REC-16 | − | Select more candidates than vacancies | "All n vacancies ... already have a selected candidate." |
| REC-17 | + | Reject an application whose job is Closed | Allowed (only forward moves are blocked) |
| REC-18 | − | Move forward when the job is Closed/Draft/Filled | "... so applications cannot move forward." |
| REC-19 | − | Delete a Selected application | "Reject it first." |
| REC-20 | B | Interview duration 14 / 15 / 240 / 241 minutes | Rejected / Accepted / Accepted / Rejected |
| REC-21 | − | Schedule an interview for an application at Applied or Selected | "Interviews can be scheduled for ... Screening or Interview" |
| REC-22 | − | Schedule in the past; mark a future interview Completed | Rejected both |
| REC-23 | − | Interviewer EMP034 Soe Moe Kyaw (Resigned); same interviewer with overlapping time on one day | "Choose a current employee as interviewer." / "The interviewer already has an interview at ..." |
| REC-24 | − | Complete an interview without score (1–5) or recommendation; give a score to a non-completed one | Rejected |
| REC-25 | − | Delete a Completed interview; edit a Cancelled one | Conflict |
| REC-26 | G | Candidate Selected | No employee created (see XMOD-14) |

## 10. Excel Import / Export (Employees)

Limits: 5 MB, 5,000 rows, 5 worksheets, 50,000 export rows. Ready-made files and their expected results are in [test-data/](test-data/README.md); or build your own from the downloaded template.

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| XLS-01 | + | Download the template | Instructions sheet, header-only data sheet, field guide, drop-downs, reference sheets |
| XLS-02 | + | Import a valid file (Insert only) | Preview shows ready rows; result counts match; history entry and report available |
| XLS-03 | − | Rename a `.txt`/`.png` to `.xlsx`; upload a `.csv`/`.xls` | Refused (file type / signature) |
| XLS-04 | B | File of 5 MB / just over; 5,000 / 5,001 rows | Accepted / refused; accepted / refused |
| XLS-05 | − | Empty workbook; header row only; duplicate header | Clear file-level error |
| XLS-06 | + | Headers in another order, different case, or an alias | Matched automatically, no mapping needed |
| XLS-07 | + | Missing required header / unknown column / two headers to one field | Mapping step shows each problem and lets you fix it |
| XLS-08 | − | Two rows with employee code `EMP061` | Both flagged as duplicates |
| XLS-09 | − | Insert only with existing code `EMP005`; Update only with unknown code `EMP999` | Rows rejected with the reason |
| XLS-10 | + | Update with a blank optional cell | Current value kept |
| XLS-11 | − | Formula cell (`=1+1`) | Reported as an error; never evaluated |
| XLS-12 | − | All-or-nothing with one bad row | Option unavailable until fixed; nothing imported |
| XLS-13 | + | Import valid rows only with one bad row | Valid rows imported; failures listed in the report |
| XLS-14 | − | Same business rules as the form: under 18, duplicate email, branch from another company | Row errors identical to the form |
| XLS-15 | + | Dates as `yyyy-MM-dd`, `dd MMM yyyy`, and real Excel dates | All accepted |
| XLS-16 | − | Date typed as text `04/11/2026` (file 2, row 14) | Rejected: "Joining Date is not a valid date." Only `yyyy-MM-dd`, `dd MMM yyyy` and real Excel dates are accepted, although the app itself shows `dd/mm/yyyy` |
| XLS-17 | − | Click confirm twice / go back and re-run an executed batch | A batch runs once |
| XLS-18 | + | Export with filters applied (more than one page of results) | All matching rows, not only the visible page |
| XLS-19 | + | Export with defaults | Personal and financial columns off until ticked |
| XLS-20 | − | Export with no column selected | "at least one column" |
| XLS-21 | + | Cell text starting with `=`, `+`, `-`, `@` | Exported with a leading apostrophe; ordinary numbers and phones untouched |
| XLS-22 | + | Open the exported file | Frozen header, filters, typed cells, Department/Branch summary sheets |

## 11. UI, Accessibility and Responsive

| ID | Type | Scenario / Steps | Expected result |
|---|---|---|---|
| UI-01 | + | Visit every sidebar screen at 1440 px and 360 px | No horizontal page scroll; tables scroll inside their container |
| UI-02 | + | Keyboard only: Tab through a form, open and close a dialog | Logical order, visible focus, Esc closes, focus returns to the opener |
| UI-03 | + | Screen reader / accessible names | Every button, link and field has a name; one `h1` per page |
| UI-04 | + | Dark mode toggle on lists, forms and charts | Text and error colours stay readable |
| UI-05 | + | Browser zoom 200% and text-only zoom | No clipped controls or overlapped text |
| UI-06 | + | Leave a form with unsaved changes | Warning appears; staying keeps the entries |
| UI-07 | + | Every delete | Confirmation dialog names the record; Cancel changes nothing |
| UI-08 | + | Slow or failed load | Loading state, then "Try again" on error |
| UI-09 | + | Collapse the sidebar; open flyout menus; use the mobile drawer | Works and closes after navigation |
| UI-10 | + | Unknown URL | "Page not found" page |
| UI-11 | + | Chrome, Edge, Firefox, Safari: type and pick a date | Same format and result |
| UI-12 | − | Submit a form with several errors | Summary "Please correct n fields"; each field shows its own message |

## 12. Smoke Suite (about 30 minutes)

Run these after every build, then the full module tables before release.

| Area | IDs |
|---|---|
| Employees | EMP-02, EMP-03, EMP-05, EMP-06, EMP-28, EMP-18 |
| Dates and filters | DT-01, DT-03, DT-05, DT-14, DT-17, DT-18 |
| Organization | ORG-01, ORG-08 |
| Attendance | ATT-01, ATT-03, ATT-09, ADJ-07 |
| Leave | LV-01, LV-17, LV-20, LV-21, LV-28 |
| Payroll | PAY-P05, PAY-P06, PAY-P11, PAY-P13, PAY-C01 |
| Recruitment | REC-04, REC-12, REC-15 |
| Excel | XLS-02, XLS-03, XLS-18 |
| Settings | SET-08 |
| Cross-module | XMOD-01, XMOD-03 |

## 13. Automation Coverage

| Module | Playwright (`e2e/`) | Unit (`src/app/**`) |
|---|---|---|
| Employees, dates | `employees.spec.ts` | `employee-data.spec.ts`, `employee-screens.spec.ts`, `hrms-validators.spec.ts`, `date.util.spec.ts` |
| Organization | `organization.spec.ts` | `organization-pages.spec.ts` |
| Attendance | `attendance.spec.ts` | `attendance-calc.spec.ts`, `attendance-data.spec.ts`, `attendance-screens.spec.ts` |
| Leave | `leave.spec.ts` | `leave-balance.spec.ts`, `leave-data.spec.ts`, `leave-screens.spec.ts` |
| Payroll | `payroll.spec.ts` | `payroll-calc.spec.ts`, `payroll-data.spec.ts` |
| Recruitment | `recruitment.spec.ts` | `recruitment-data.spec.ts` |
| Excel | `excel.spec.ts` | `excel-engine.spec.ts`, `excel-coercion.spec.ts`, `employee-excel.spec.ts`, `excel-import-wizard.component.spec.ts` |
| Settings | `settings.spec.ts` | `settings-data.spec.ts` |
| UI quality | `quality.spec.ts`, `shell.spec.ts`, `dashboard.spec.ts` | `dashboard.component.spec.ts` |

Not automated yet: the calendar popup itself (DT-01 to DT-13, DT-16, DT-20), time-zone checks, browser matrix, and Recruitment/Payroll/Settings screen components. Existing e2e tests only fill dates by typing ISO text.


---

<a id="myanmar"></a>

# မြန်မာဘာသာ (Myanmar Version)

# Module အလိုက် Test Scenario များ

အကောင်အထည်ပေါ်ပြီးသော rule များအပေါ်အခြေခံသည် ([HR-BUSINESS-ANALYSIS.md](HR-BUSINESS-ANALYSIS.md) ကိုကြည့်ပါ)။ "မျှော်မှန်းရလဒ်" သည် **လက်ရှိ code လုပ်ဆောင်သည့်အတိုင်း** ဖြစ်သည်။

- **Type:** `+` ကောင်းသောလမ်းကြောင်း၊ `−` ငြင်းပယ်ရမည့်အခြေအနေ၊ `B` အနားသတ်တန်ဖိုး (boundary)၊ `X` module ကျော်၊ `G` **Known gap** (လက်ရှိ ပြုမူပုံ အငြင်းပွားစရာရှိသည်။ နောက်ပိုင်း ပြောင်းလဲသွားလျှင် သိနိုင်ရန် test ဖြင့် မှတ်ထားသည်)။
- **ပတ်ဝန်းကျင်:** front-end သာဖြစ်သည်။ Reload လုပ်ရင် data ပြန်ကျသဖြင့် app အတွင်း navigate လုပ်ပါ (`page.goto` မသုံးပါနှင့်)။ ပိတ်ရက် = စနေ/တနင်္ဂနွေ။ Login ဝင်ထားသူက Su Myat Noe (employee ID 2, HR Administrator) တစ်ယောက်တည်းသာ။
- **ရက်စွဲ:** ဥပမာများတွင် ယနေ့ = ၂၀၂၆-၁၁-၀၄ (ဗုဒ္ဓဟူး) ဟု ယူထားသည်။ တခြားရက်ဆိုလျှင် ချိန်ညှိပါ။
- **Automation:** မည်သည့် suite က မည်သည့် module ကိုဖုံးသည်ကို ၁၃ တွင်ကြည့်ပါ။ Calculator case များ (ATT-0x, BAL-xx, PAY-Cxx) သည် `attendance-calc.spec.ts`, `leave-balance.spec.ts`, `payroll-calc.spec.ts` နှင့်သက်ဆိုင်သည်။
- **ရက်စွဲရိုက်ပုံ:** App တွင် `dd/mm/yyyy` (ဥပမာ 04/11/2026) ဖြင့်ရိုက်ပါ။ Excel ဖိုင်တွင် `yyyy-MM-dd` သို့မဟုတ် `dd MMM yyyy` သုံးပါ။
- **စတင်ရန်:** Build တိုင်းပြီးနောက် **smoke suite (၁၂)** ကို run ပါ။ Release မတိုင်မီ module table အပြည့်ကို run ပါ။
- **Test data:** Seed company, shift, holiday, policy များနှင့် scenario တစ်ခုချင်းသုံးသော employee များ (EMP001, EMP050, ...) အတွက် **၀ အပိုင်း** ကိုကြည့်ပါ။ အရေအတွက်များကို ၂၀၂၆-၁၁-၀၄ တွင်စစ်ထား။ Reload လုပ်လျှင် အားလုံးပြန် reset ဖြစ်သဖြင့် seed record ပေါ်တွင် destructive test လုပ်နိုင်သည်။
- **ရလဒ်မှတ်တမ်း:** Run တိုင်း build, browser, ရက်စွဲ, ID, Pass/Fail နှင့် မအောင်မြင်လျှင် screenshot မှတ်ပါ။ `G` row တစ်ခု ပြုမူပုံပြောင်းသွားလျှင် **အပြောင်းအလဲအဖြစ် report** လုပ်ပါ၊ bug ဟုတ်ချင်မှဟုတ်မည်။

---

## ၀။ Seed Data ကိုးကားစာရင်း

အောက်ပါ scenario များသည် seed record များကိုသုံးသည် (`src/app/mock-data`)။ **Fixed** data မပြောင်းပါ။ **Relative** data က ယနေ့ရက်စွဲနှင့်အတူရွေ့သဖြင့် တခြားနေ့တွင် run လျှင် အရေအတွက်များကို ပြန်စစ်ပါ။ ကိန်းဂဏန်းများကို **၂၀၂၆-၁၁-၀၄** အရ seed နှင့်တိုက်စစ်ထားသည်။

### ၀.၁ Fixed data

| အပိုင်း | Record များ |
|---|---|
| Company | AHT Apex Horizon Technologies (Active), AHL Apex Horizon Logistics (Active), AHV Apex Horizon Ventures (**Inactive**၊ branch/staff မရှိ) |
| Branch | AHT: YGN-HQ Yangon Head Office, MDY-BR Mandalay Branch, NPT-BR Nay Pyi Taw Office, SG-RO Singapore Regional Office; AHL: MDY-DC Mandalay Distribution Center, YGN-LH Yangon Logistics Hub |
| Department (၁၁) | Executive, Human Resources, Finance & Accounts, Engineering, Product Management, Sales (Mandalay), IT Infrastructure, Customer Support (Nay Pyi Taw), Warehouse Operations (MDY-DC), Fleet & Logistics (YGN-LH), Marketing (Singapore) |
| Job grade | G1 400–700, G2 600–1000, G3 900–1500, G4 1400–2200, G5 2000–3000, G6 2800–4200, G7 4000–6000, G8 5500–8500, G9 8000–14000 |
| Shift | GEN 09:00–18:00 break 60 grace 15 တနင်္လာ–သောကြာ; WH-AM 06:00–14:30 break 30 grace 10 တနင်္လာ–စနေ; WH-PM 14:00–22:30; SUP 08:00–17:00 break 60 grace 10 တနင်္လာ–စနေ; NIGHT 22:00–06:00 (**Inactive**) |
| ယနေ့နီးပါး holiday | သောကြာ ၂၀/၁၁/၂၀၂၆ Company Foundation Day (Company), တနင်္လာ ၂၃/၁၁/၂၀၂၆ Full Moon Festival, ၂၅/၁၂/၂၀၂၆ Christmas, ၃၁/၁၂/၂၀၂၆ Year-End |
| Leave type | AL 15 (တစ်ဆက်တည်းအများဆုံး 10), CL 6 (3), ML 30 (document), MAT 98 (အမျိုးသမီး, document), PAT 15 (အမျိုးသား, document), UL 30 (unpaid), CPL 5 |
| Leave policy | Annual: standard 15 (၃ လနောက်), G6 18, G7 20, G8 22, G9 25, Contract 10, Intern 6 monthly; Casual 6 monthly; Intern Medical 10 |
| Salary component | HOUSING 10% basic, TRANSPORT 50, MEAL 30, PERFORMANCE 5% basic, EXECUTIVE 300, TAX 5% gross, SOCIAL 2% gross, PENSION 3% basic (**Inactive**၊ မသုံး) |
| Salary structure | STAFF (G1–G5), MGMT (G6–G7), EXEC (G8–G9) |
| Role | HR Administrator နှင့် Employee က built-in; Auditor က Inactive; Department Manager တွင် user ၄ ဦး |
| Job opening | Senior Angular Developer (Open, 2), QA Engineer (Filled), Product Designer (Open), Account Executive (Open, 2), Customer Support Agent (Open, 3), HR Intern (On Hold), Financial Analyst (Closed၊ closing date ကျော်), DevOps Engineer (Draft) |

### ၀.၂ အသုံးဝင်သော employee များ

| ID | အမည် | အသုံးဝင်ပုံ |
|---|---|---|
| EMP001 | Thura Aung | CEO။ လူ ၆ ဦးကိုစီမံ (EMP002, 003, 004, 007, 011, 012)၊ ထို့ကြောင့် delete ငြင်း။ အမျိုးသား |
| EMP002 | Su Myat Noe | **Login ဝင်ထားသူ။** HR Director၊ တစ်ဦးတည်းသော HR Administrator၊ shift GEN။ အမျိုးသမီး |
| EMP005 | Daniel Carter | Engineering Manager (G6)။ Duplicate စမ်းရာတွင် code နှင့် email သုံး |
| EMP012 | Zaw Lin Oo | Warehouse Manager၊ shift WH-AM၊ user account **Locked** |
| EMP017, EMP026 | Phyu Sin Thant, Shwe Sin Oo | ဆုံးဖြတ်ချက်စောင့်နေသော Submitted Annual leave; EMP017 တွင် Pending attendance adjustment လည်းရှိ |
| EMP019 | Wai Yan Soe | Software Engineer (G3)၊ transfer နှင့် position ပြောင်းစမ်းရာတွင်သုံး |
| EMP020, EMP031 | Yamin Aye, Linn Htet Maung | စတင်ပြီးသော Approved ခွင့် (cancel မရ) |
| EMP034 | Soe Moe Kyaw | **Resigned**၊ exit 2026-08-31 (နိုဝင်ဘာ run တွင်မပါ) |
| EMP035 | Aye Chan Moe | On Leave (maternity ၉၈ ရက်) |
| EMP038, EMP054 | Bo Bo Han, Zarni Tun | Contract staff |
| EMP042 | Ko Ko Gyi | Suspended |
| EMP044 | Tun Tun Naing | **Terminated**၊ exit 2026-06-15 (နိုဝင်ဘာ run တွင်မပါ) |
| EMP047 | Lwin Mar Htwe | Part-time |
| EMP048 | Aung Myo Oo | Evening shift WH-PM; salary assignment **တစ်ခုတည်း** |
| EMP050 | Rahul Sharma | Relative: ၁၂ ရက်အကြာဝင်၊ Probation၊ ၃ လမပြည့်၊ annual entitlement 0 |
| EMP052 | Phone Myint Kyaw | အမျိုးသား၊ Submitted Paternity leave |
| EMP053, EMP058 | Ingyin Thwe, Chan Myae Aung | Intern (relative: ၂ နှင့် ၂၀ ရက်အကြာဝင်); EMP053 က G1 |
| EMP056 | Sai Htoo Aung | Seed ခွင့်မရှိ၊ 2026-01-12 ဝင်: ခွင့်စမ်းရန် သန့်ရှင်းသောသူ |
| EMP057, EMP060 | Nang Hseng Hom, Andrew Lim | ၃၅ နှင့် ၅၀ ရက်အကြာဝင်; EMP060 တွင် လက်အောက်ငယ်သားမရှိ၍ delete လို့ရ |

### ၀.၃ Relative data (ယနေ့ရက်စွဲနှင့်အတူရွေ့)

- Attendance: ၆၀ ရက် generated history; overtime ကို overtime တစ်နာရီနှင့်အထက်ရှိသော record မှထုတ်ယူ; adjustment များက ထို record များနှင့်ချိတ်။
- Leave application ရက်စွဲများက ယနေ့နှင့်ဆက်စပ်ထားသဖြင့် "၃ ရက်အကြာစ" သည် နေ့တိုင်းရွေ့သည်။
- Payroll: နောက်ဆုံးခြောက်လ **Paid** (၂၀၂၆ မေ–အောက်တိုဘာ)၊ **လက်ရှိလ Draft** (နိုဝင်ဘာ ၂၀၂၆)။
- Probation: joining date သုံးလအတွင်းရှိသူများ (ယနေ့ EMP050, 053, 057, 058, 060)။
- Seed employee အားလုံးတွင် salary assignment ရှိ။ ကြာရှည်ထမ်းများတွင် နှစ်ခု (ဝင်ရက်နှင့် ယခုနှစ် ၀၁/၀၁)။

### ၀.၄ Seed data ဖြင့် မစမ်းနိုင်သည်များ

- **Salary assignment မရှိသော employee** (PAY-P08, XMOD-02): employee အသစ်အရင်ဖန်တီးပါ။
- **Punch အချိန်အတိအကျ** (ATT-02 မှ ATT-08): Check-in screen က နာရီအစစ်သုံး။ Adjustment သို့မဟုတ် unit test သုံးပါ။
- **နောက်ဆုံး administrator guard** (SET-08): Login ဝင်သူက တစ်ဦးတည်းသော administrator ဖြစ်၍ ကိုယ်ပိုင် account rule က အရင်အလုပ်လုပ်။
- **Position မပြောင်းဘဲ branch/department ပြောင်း** (EMP-23): Organization ကွင်းဆက် validation က တားနိုင်။
- **Password နှင့် sign-in**: authentication မရှိ၍ role အလိုက်ဝင်ခွင့်ကို မစမ်းနိုင် (SET-10)။

## ၁။ Employee Management

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| EMP-01 | + | Employee အသစ်ထည့်သော form ဖွင့် | Default: Full-time, status Probation, probation ကုန်ရက် = ယနေ့ + ၃ လ |
| EMP-02 | + | Employee အသစ်ဖန်တီး (ဥပမာ Nilar Htay: company AHT, branch Yangon Head Office, department Engineering, position Software Engineer)၊ လိုအပ်သော field အားလုံးဖြည့်ပြီး save | **EMP061** အဖြစ်ဖန်တီး၊ history "Joined as Software Engineer" ကျ |
| EMP-03 | − | Employee code `EMP005` (`emp005` လည်း) ဖြင့် save | "Employee code ... already exists" ဖြင့် ငြင်း |
| EMP-04 | − | Work email `daniel.carter@apexhorizon.example` (`Daniel.Carter@apexhorizon.example` လည်း) ဖြင့် save | Work email ပေါ်တွင် ငြင်း |
| EMP-05 | − | မွေးနေ့ = မနက်ဖြန် | "cannot be in the future" |
| EMP-06 | B | မွေးနေ့ `04/11/2008` (၂၀၂၆-၁၁-၀၄ မှ ၁၈ နှစ်အတိ) / `05/11/2008` | လက်ခံ / ငြင်း ("at least 18") |
| EMP-07 | − | Joining date `02/11/2026`၊ probation ကုန်ရက် `01/10/2026` | Probation ကုန်ရက်ပေါ်တွင် ငြင်း |
| EMP-08 | − | Joining date `02/11/2026`၊ exit date `01/10/2026` | Exit date ပေါ်တွင် ငြင်း |
| EMP-09 | − | EMP054 Zarni Tun ကို edit: status Resigned၊ exit date ဗလာ | "An exit date is required ..." |
| EMP-10 | − | Company Apex Horizon Logistics နှင့် branch Yangon Head Office (AHT ၏ branch) | "branch does not belong to the chosen company" |
| EMP-11 | − | Branch Yangon Head Office နှင့် department Warehouse Operations (Mandalay Distribution Center တွင်ရှိ) | Department ပေါ်တွင် ငြင်း |
| EMP-12 | − | Department Engineering နှင့် position Customer Support Agent | "belongs to a different department" |
| EMP-13 | − | Department Engineering နှင့် section Tier 1 Support | Section ပေါ်တွင် ငြင်း |
| EMP-14 | − | Company Apex Horizon Ventures (Inactive) သို့မဟုတ် shift Night Shift (Inactive) ကိုရွေး။ တခြား unit အတွက် အရင်ဆုံး deactivate လုပ် (ဥပမာ Marketing) | "This ... is inactive" |
| EMP-15 | + | Department Marketing ကို deactivate ပြီး EMP032 Emily Wong (Digital Marketing Specialist) ကို ဖုန်းနံပါတ်သာပြောင်း၍ edit | Save ရသည် (ရှိပြီးသား assignment ကျန်နိုင်သည်) |
| EMP-16 | − | (က) EMP005 Daniel Carter ကို edit ပြီး *Reporting manager* စာရင်းကိုဖွင့်။ (ခ) Excel import, Update only: employee code `EMP005` နှင့် manager code `EMP005` ပါသော row | (က) Daniel Carter သည် စာရင်းထဲ**မပါ** (form က employee ကိုယ်တိုင်ကို ဖျောက်ထား)။ (ခ) ငြင်း: "An employee cannot be their own manager." / manager field တွင် "Choose a different manager." |
| EMP-17 | − | Manager code `EMP999` ပါသော Excel import row | "manager does not exist" |
| EMP-18 | − | EMP001 Thura Aung (CEO) ကို delete | Conflict: "Thura Aung manages 6 employee(s)" (EMP002, 003, 004, 007, 011, 012)။ အရင်ပြန်သတ်မှတ်ပါ |
| EMP-19 | + | EMP060 Andrew Lim (လက်အောက်ငယ်သားမရှိ) ကို delete | ဖျက်ပြီး document နှင့် history လည်း ပျက် |
| EMP-20 | G | EMP-19 ပြီးနောက် Andrew Lim ၏ attendance (၆၀ ရက်မှတ်တမ်း)၊ salary assignment နှင့် စက်တင်ဘာ/အောက်တိုဘာ payslip ကိုကြည့် | Record များ ကျန်နေသည် (orphan)၊ attendance list တွင် "Employee #id" ပြ |
| EMP-21 | + | EMP046 Hein Thant Zin ကို edit: position Software Engineer (G3) → Senior Backend Engineer (G4) | History "Promotion" |
| EMP-22 | + | EMP019 Wai Yan Soe ကို edit: position Software Engineer (G3) → QA Engineer (G3) | History "Transfer" (demotion type မရှိ) |
| EMP-23 | + | Position မပြောင်းဘဲ branch သို့မဟုတ် department သာပြောင်း | History "Transfer: Organization assignment changed"။ Branch, department, position တို့ ကိုက်ညီရမည်ဖြစ်၍ form မှ ဖြစ်နိုင်ချင်မှဖြစ်မည်၊ တွေ့ရှိချက်ကို မှတ်ပါ |
| EMP-24 | G | Position တူပြီး job grade သာ ပြောင်း | History မကျ |
| EMP-25 | + | EMP050 Rahul Sharma (Probation၊ ၂၀၂၆ အောက်တိုဘာ ၂၃ ဝင်) ကို edit: status → Active | History "Status Change" (probation အလိုအလျောက်အတည်ပြုခြင်း မရှိ) |
| EMP-26 | + | EMP054 Zarni Tun ကို edit: status Resigned၊ exit date `20/11/2026` | History "Exit"၊ တခြား record မပြောင်း (XMOD-06 ကိုကြည့်) |
| EMP-27 | + | EMP047 Lwin Mar Htwe ကို edit: Part-time → Full-time | History "Contract Change" |
| EMP-28 | + | Code/နာမည်/email/position ဖြင့်ရှာပြီး filter။ Status သည် department, branch, type နှင့်အတူ **More filters** ထဲတွင်ရှိ | ၂၀၂၆-၁၁-၀၄ အရ: Resigned ၁ (EMP034), Terminated ၁ (EMP044), Suspended ၁ (EMP042), On Leave ၁ (EMP035), Probation ၅ (EMP050, 053, 057, 058, 060); type Contract ၃ (038, 044, 054), Intern ၂, Part-time ၁; department Customer Support ၆; joined 01/01/2014–31/12/2015 = ၃။ Filter များ AND ဖြင့်ပေါင်း |
| EMP-29 | + | Document (ဖိုင်အမည်သာ) ထည့်ပြီး ဖျက် | Profile ရဲ့ Documents tab တွင် ပေါ်/ပျောက် |
| EMP-30 | + | EMP001 Thura Aung › Salary ကိုဖွင့် | ၀၁/၀၁/၂၀၂၆ effective ဖြစ်သော assignment (နှစ်စဉ်ပြင်ဆင်မှု) အရ တစ်လပြည့်ကိန်းဂဏန်းပြ |
| EMP-31 | − | Employee အသစ်ဖန်တီးပြီး Salary tab ကိုဖွင့် (seed employee အားလုံးတွင် assignment ရှိပြီးသား) | "has no salary assignment yet" |
| EMP-32 | + | မှန်ကန်သော employee များကို Excel import | တူညီသော validation ဖြင့် ဖန်တီး၊ တစ်ယောက်စီ "Joined" history ရ |
| EMP-33 | − | Row တစ်ခုမှားသော Excel import (all-or-nothing mode) | ဘာမှ import မဖြစ်၊ ပြဿနာအသေးစိတ်နှင့် validation report ရ |

## ၂။ Organization Management

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| ORG-01 | + | Company, branch, department, section, position, job grade ကို မှန်ကန်သောကွင်းဆက်ဖြင့် ဖန်တီး | Save ရ |
| ORG-02 | − | Code ထပ်: company `AHT`, branch `YGN-HQ`, department `ENG`, section `HR-TA`, position `POS-001`, grade `G1` | "must be unique" |
| ORG-03 | − | "Apex Horizon Technologies" အမည်ဖြင့် company ဖန်တီး | ငြင်း |
| ORG-04 | − | Department အသစ်ကို branch Yangon Head Office နှင့် company Apex Horizon Logistics ဖြင့် | Branch ပေါ်တွင် ငြင်း |
| ORG-05 | − | Department Engineering အတွက် position အသစ်ကို section Tier 1 Support ဖြင့် | Section ပေါ်တွင် ငြင်း |
| ORG-06 | − | Position ကို ကိုယ်တိုင်သို့ report လုပ်ခိုင်း | "cannot report to itself" |
| ORG-07 | B | Min 400 ရှိသော job grade: max 400 / max 300 | လက်ခံ / ငြင်း |
| ORG-08 | − | Company AHT, branch YGN-HQ, department Engineering, section ENG-FE, grade G1 သို့မဟုတ် position POS-001 ကို delete | Conflict (အသုံးပြုမှုအရေအတွက်နှင့် "Deactivate it instead") |
| ORG-09 | + | Department Marketing ကို deactivate | ရ၊ သို့သော် employee အသစ်ကို သတ်မှတ်မရတော့ (EMP-14) |
| ORG-10 | + | Company Apex Horizon Ventures (Inactive၊ branch/employee မရှိ) ကို delete | ဖျက်ရ |
| ORG-11 | X | လစာရှိနေပြီးနောက် grade G1 band ကို 400–700 မှ 450–650 သို့ပြောင်း | ရှိပြီးသား assignment မပြောင်း၊ assignment အသစ်ကိုသာ band အသစ်ဖြင့်စစ် |
| ORG-12 | + | Reporting Structure view ဖွင့် | ပေါ်သည် (အသေးစိတ်ကို ဤစာတမ်းမခြေရာခံပါ) |

## ၃။ Attendance Management

ကိုးကားသော shift: GEN 09:00–18:00, break 60, grace 15, တနင်္လာ–သောကြာ။

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| ATT-01 | + | Su Myat Noe (GEN shift 09:00–18:00) အဖြစ် punch ဝင်/ထွက်။ **Check-in screen သည် နာရီအစစ်ကိုသုံး**၊ အချိန်အတိအကျစမ်းလိုလျှင် adjustment (ADJ) သို့မဟုတ် `attendance-calc.spec.ts` ကိုသုံးပါ။ Case: ၀၉:၀၀ ဝင်၊ ၁၈:၀၀ ထွက် | Present; worked 480 (540 − break 60); late 0; early 0; overtime 0 |
| ATT-02 | B | ၀၉:၁၅ ဝင် | Present (grace အတွင်း)၊ late 0 |
| ATT-03 | B | ၀၉:၁၆ ဝင် | Late; **late = ၁၆ မိနစ်** (shift စတင်ချိန်မှ တွက်) |
| ATT-04 | + | ၀၉:၃၀ ဝင် | Late ၃၀ မိနစ်၊ remark "Late arrival" |
| ATT-05 | + | ၀၉:၀၀ ဝင်၊ ၁၂:၃၀ ထွက် | Half Day; worked 210; early leave 0 |
| ATT-06 | B | Break နုတ်ပြီး ၂၄၀ မိနစ်အတိ / ၂၃၉ မိနစ် | ၂၄၀ → half day မဟုတ်၊ ၂၃၉ → Half Day |
| ATT-07 | + | ၁၇:၀၀ ထွက် | Early leave ၆၀ |
| ATT-08 | B | ၁၈:၃၀ ထွက် / ၁၈:၃၁ ထွက် | OT 0 / OT 31 (၃၀ ကျော်မှ၊ မိနစ်အားလုံးတွက်) |
| ATT-09 | − | Su Myat Noe အဖြစ် check-in နှစ်ကြိမ် (ယနေ့ record ရှိပြီးသားဆိုလျှင် reload အရင်လုပ်) | Conflict "Already checked in at ..." |
| ATT-10 | − | Check-in မလုပ်ဘဲ check-out | Conflict "Check in before checking out" |
| ATT-11 | − | Check-out နှစ်ကြိမ် | Conflict "Already checked out" |
| ATT-12 | − | Day shift: check-out ≤ check-in | Validation error |
| ATT-13 | + | Night shift (၂၂:၀၀–၀၆:၀၀): ၂၂:၀၀ ဝင်၊ ၀၆:၀၀ ထွက် | လက်ခံ၊ worked = ၈ နာရီ − break |
| ATT-14 | + | စနေ/တနင်္ဂနွေ၊ သို့မဟုတ် သောကြာ `20/11/2026` (Company Foundation Day) / တနင်္လာ `23/11/2026` (Full Moon Festival) တွင် punch | Status Present၊ remark "Worked on a ..."၊ late/early 0၊ overtime မဖန်တီး |
| ATT-15 | − | On Leave ဖြစ်သောနေ့တွင် punch | Conflict "You are on approved leave on this day" |
| ATT-16 | + | Shift မသတ်မှတ်ထားသော employee punch | စာရင်းထဲက ပထမ shift ကိုသုံး |
| ATT-17 | − | Day shift ဖန်တီး (end ≤ start၊ night shift မဟုတ်) | ငြင်း |
| ATT-18 | − | Work day မရှိသော shift; code `GEN` ထပ်သော shift ဖန်တီး | ငြင်း |
| ATT-19 | − | Shift GEN (employee အများစုတာဝန်ပေးထား) ကို delete | Conflict (တာဝန်ရသူအရေအတွက်ပြ) |
| ATT-20 | X | EMP012 Zaw Lin Oo ကို WH-AM မှ WH-PM သို့ပြောင်းပြီး record အဟောင်း/အသစ်ကြည့် | Punch အသစ်များက shift အသစ်သုံး၊ ရှိပြီးသား record များက `shiftId` အဟောင်းကျန်ပြီး ပြန်မတွက် |
| ADJ-01 | + | Record ရှိသောရက်အတွက် EMP017 Phyu Sin Thant ၏ request ဖန်တီး (Pending တစ်ခုရှိပြီးသား) | Pending; လက်ရှိအချိန်များမှတ်သားထား |
| ADJ-02 | − | အနာဂတ်ရက်အတွက်တောင်းဆို | "cannot be in the future" |
| ADJ-03 | − | Attendance record မရှိသောရက်အတွက် | "no attendance record for this employee on that date" |
| ADJ-04 | − | အချိန်လည်းမပါ၊ status လည်းမပါ | "Request at least one change" |
| ADJ-05 | − | Day shift: တောင်းဆိုသော out ≤ in | ငြင်း |
| ADJ-06 | − | EMP017 ၏ seed Pending request ရက်စွဲတွင် ဒုတိယ Pending request ဖန်တီး | ငြင်း |
| ADJ-07 | + | EMP040 Nay Myo Aung ၏ seed request (ဝင် ၀၆:၀၅၊ ထွက် ၁၄:၃၀၊ shift WH-AM) ကို approve | Record ပြန်တွက်: ၀၆:၀၅ ဝင်ချိန်က grace ၁၀ မိနစ်အတွင်းဖြစ်၍ Present၊ source = Manual၊ remark "Adjusted (ADJ-nnnn)" |
| ADJ-08 | + | EMP033 Zin Mar Oo ၏ seed request (forced status Half Day) ကို approve | Status ကို Half Day အဖြစ်အတင်းသတ်မှတ်၊ derived minutes က ၎င်းအတိုင်း |
| ADJ-09 | + | EMP021 Naing Lin Tun ၏ seed request ကို reject | Record မပြောင်း၊ request လော့ခ်ကျ |
| ADJ-10 | − | EMP016 ၏ Approved request သို့မဟုတ် EMP012 ၏ Rejected request ကို edit/delete/decide | Conflict |
| ADJ-11 | G | တောင်းဆိုသူကိုယ်တိုင် ၎င်း adjustment ကို approve | ရသည် (self-approval စစ်ဆေးမှု မရှိ) |
| OT-01 | + | Overtime ၁၈:၀၀–၂၀:၀၀ ဖန်တီး | ၁၂၀ မိနစ်၊ Pending |
| OT-02 | B | ၁၄ မိနစ် / ၁၅ မိနစ် / ၁၂ နာရီ / ၁၂ နာရီ ၁ မိနစ် | ငြင်း / လက်ခံ / လက်ခံ / ငြင်း |
| OT-03 | − | အနာဂတ်ရက် | ငြင်း |
| OT-04 | − | တူညီသော employee နှင့်ရက်အတွက် ဒုတိယတစ်ခု | ငြင်း |
| OT-05 | G | Overtime ကို reject လုပ်ပြီး ထိုရက်အတွက် အသစ်ပြန်ဖန်တီး | တားမြစ် (duplicate စစ်ဆေးမှုက status မခွဲ) |
| OT-06 | + | Approve / reject | Status, ဆုံးဖြတ်သူ, ရက်စွဲ မှတ်သားထား |
| OT-07 | G | တောင်းဆိုသူကိုယ်တိုင် overtime ကို approve | ရသည် |

## ၄။ Leave Management

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| LV-01 | + | EMP026 Shwe Sin Oo: Annual Leave တနင်္လာ `09/11/2026` – ဗုဒ္ဓဟူး `11/11/2026`၊ Draft အဖြစ်သိမ်း | နံပါတ် `LV-YYYY-NNNN`၊ status Draft၊ days = 3 |
| LV-02 | + | EMP056 Sai Htoo Aung: သောကြာ `13/11/2026` – တနင်္လာ `16/11/2026` | days = 2 (ပိတ်ရက်ချန်) |
| LV-03 | + | ကြာသပတေး `19/11/2026` – အင်္ဂါ `24/11/2026` (၂၀ ရက် Company Foundation Day နှင့် ၂၃ ရက် Full Moon တို့မှာ holiday) | days = 2 (၁၉ နှင့် ၂၄) |
| LV-04 | − | စနေ `21/11/2026` – တနင်္လာ `23/11/2026` (ပိတ်ရက် + Full Moon Festival) | "no working days" |
| LV-05 | − | End date သည် start date မတိုင်မီ | ငြင်း |
| LV-06 | + | EMP056: ဗုဒ္ဓဟူး `11/11/2026` တစ်ရက်တည်း half day | days = 0.5 |
| LV-07 | − | Half day ဖြင့် start နှင့် end ကွဲ | "single date" error |
| LV-08 | − | EMP050 Rahul Sharma (၂၃/၁၀/၂၀၂၆ ဝင်): `20/10/2026` မှစ | ငြင်း |
| LV-09 | B | EMP056၊ Casual Leave: တနင်္လာ ၉ – ဗုဒ္ဓဟူး ၁၁ နိုဝင်ဘာ (၃ ရက်) / တနင်္လာ ၉ – ကြာသပတေး ၁၂ (၄ ရက်) | လက်ခံ / "limited to 3 consecutive days" |
| LV-10 | − | EMP001 Thura Aung (အမျိုးသား) က Maternity / EMP002 Su Myat Noe (အမျိုးသမီး) က Paternity | "not available to this employee" |
| LV-11 | − | Compassionate Leave ကို deactivate ပြီးထိုအမျိုးအစားဖြင့်လျှောက် | "Choose an active leave type" |
| LV-12 | − | EMP017 Phyu Sin Thant: သူမ၏ Submitted Annual leave (၃ ရက်အကြာစ၊ calendar ၃ ရက်) နှင့်ထပ်သောရက်စွဲ | "overlap LV-..." |
| LV-13 | + | EMP049 Yoon Wadi Kyaw: သူမ၏ Draft (၂၀ ရက်အကြာစ) နှင့်ထပ်၊ သို့မဟုတ် EMP038 Bo Bo Han: ၎င်း၏ Rejected ခွင့် (၂၀ ရက်အရင်) နှင့်ထပ် | ရ |
| LV-14 | + | Draft ကို edit/delete | ရ |
| LV-15 | − | Submitted/Approved ကို edit/delete | Conflict |
| LV-16 | + | EMP056၊ Annual Leave အလုပ်ရက် ၃ ရက် (entitled 15)၊ submit | Status Submitted; ရက်များကို pending အဖြစ်ရေတွက် |
| LV-17 | − | EMP050 Rahul Sharma (service ၃ လမပြည့်၍ Annual entitlement 0): Annual Leave ၁ ရက် submit | "Insufficient ... balance: 1 requested, 0 available" |
| LV-18 | − | EMP056: Medical Leave ၁ ရက်ကို document မပါဘဲ submit | "needs a supporting document" |
| LV-19 | − | Draft မဟုတ်သည်ကို Submit | Conflict |
| LV-20 | + | Su Myat Noe အဖြစ် EMP026 Shwe Sin Oo ၏ seed Submitted Annual leave (အလုပ်ရက် ၃၊ ၈ ရက်အကြာစ) ကို approve | Approved; balance က pending → taken; remarks default "Approved." |
| LV-21 | − | EMP002 Su Myat Noe အတွက်ခွင့်ဖန်တီး၊ submit ပြီး approve သို့မဟုတ် reject | "You cannot decide your own leave request" |
| LV-22 | − | အကြောင်းပြချက်မထည့်ဘဲ Reject | "A reason is required" |
| LV-23 | + | အကြောင်းပြချက်ဖြင့် Reject | Rejected; ရက်များပြန်လွှတ် |
| LV-24 | − | Submitted မဟုတ်သည်ကို decide | Conflict |
| LV-25 | + | EMP017 Phyu Sin Thant ၏ Submitted ခွင့်ကို cancel | Cancelled; ရက်များပြန်ရ |
| LV-26 | + | LV-20 ပြီးနောက် EMP026 ၏ Approved ခွင့် (၈ ရက်အကြာစ) ကို cancel | Cancelled |
| LV-27 | − | EMP031 Linn Htet Maung ၏ Approved ခွင့် (ယနေ့စ) သို့မဟုတ် EMP020 Yamin Aye ၏ (မနေ့စ) ကို cancel | "already started cannot be cancelled" |
| LV-28 | X | EMP056 အတွက် ရက် ၆၀ အတွင်း Present/Late record ရှိပြီးသား ပြီးခဲ့သောအလုပ်ရက်များကိုဖုံးသော Annual Leave ဖန်တီး၊ submit၊ approve | ထို record များ On Leave ဖြစ်၊ အချိန်ဖျက်၊ remark = ခွင့်နံပါတ် |
| LV-29 | X | အထက်အတိုင်းဖြစ်ပြီး Weekend/Holiday record နှင့် အနာဂတ်ရက်ပါ | Weekend/Holiday မထိ၊ အနာဂတ်ရက်အတွက် record မဖန်တီး |
| LV-30 | G | LV-28 ရဲ့ approved ခွင့်ကို cancel (ခွင့်ပြုလျှင်) | Attendance က On Leave အတိုင်းကျန် (ပြန်မပြင်) |
| LV-31 | G | နုတ်ထွက်ပြီး/exit date ကျော်သော employee အတွက် ခွင့်ဖန်တီး | ရသည် (joining date သာ စစ်) |
| LV-32 | G | နောက်နှစ်ရက်စွဲခွင့်ကို ဒီနှစ်မှာ submit | Balance စစ်ရာတွင် **လက်ရှိနှစ်** နှင့် ယနေ့ရက်ကိုသာ သုံး |
| LV-33 | + | လတစ်လ၏ leave calendar ကို pending ပါ/မပါ | Approved အမြဲပြ၊ Submitted က include လုပ်မှသာ၊ weekend/holiday အမှတ်အသားပါ |
| LV-34 | − | Annual Leave (application နှင့် policy သုံးနေ) ကို delete | Conflict |
| LV-35 | − | Carry-forward max > yearly days; max consecutive < 1; code ထပ် | ငြင်း |
| LV-36 | − | Grade/employment type မပါသော ဒုတိယ active Annual Leave policy ဖန်တီး (standard policy ရှိပြီးသား) | "An active policy already exists ..." |
| BAL-01 | + | EMP050 Rahul Sharma (service ၃ လမပြည့်)၊ Annual Leave | Entitled 0 |
| BAL-02 | + | EMP056 Sai Htoo Aung (၁၂/၀၁/၂၀၂၆ ဝင်)၊ Annual Leave | Entitled 15 |
| BAL-03 | + | EMP005 Daniel Carter (Engineering Manager, G6) | Entitled 18 (grade policy က default ကိုနိုင်) |
| BAL-04 | + | EMP058 Chan Myae Aung (Intern)၊ Annual Leave | Intern policy (၆ ရက်၊ monthly accrual) က standard 15 ကိုနိုင် |
| BAL-05 | + | Casual leave (၆/နှစ်၊ monthly) နိုဝင်ဘာလ | 5.5 |
| BAL-06 | + | Policy မရှိသော leave type | Type ၏ default ရက် |
| BAL-07 | + | Entitled 15, taken 3, pending 2 | Available 10; ၀ အောက်မကျ |
| BAL-08 | G | Annual Leave ၏ carry-forward flag ဖွင့်ပြီး နောက်နှစ် balance ကြည့် | မသုံးရသေးသောရက်များ **ကူးမလာ** |
| BAL-09 | + | EMP054 Zarni Tun သို့မဟုတ် EMP038 Bo Bo Han (Contract)၊ Annual Leave | Contract policy: entitled 10 |
| BAL-10 | + | EMP047 Lwin Mar Htwe (Part-time)၊ Annual Leave | Standard policy: entitled 15 (part-time rule မရှိ) |

## ၅။ Payroll Management

### ၅.၁ Setup

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| PAY-S01 | − | Component "Meal allowance" value 0 / −5 | ငြင်း |
| PAY-S02 | − | TAX (percent of gross) ကို edit: value 120 | ငြင်း |
| PAY-S03 | − | Code `HOUSING` ဖြင့် component အသစ် | ငြင်း |
| PAY-S04 | − | HOUSING (structure အားလုံးသုံး) ကို delete။ PENSION (inactive၊ မသုံး) ကိုတော့ ဖျက်လို့ရ | Conflict |
| PAY-S05 | − | Component မပါသော structure / TAX နှင့် SOCIAL သာပါသော structure | "Choose at least one component" / "needs at least one earning" |
| PAY-S06 | − | Structure STAFF (G1–G5 staff အတွက် assign) ကို delete | Conflict |
| PAY-S07 | B | EMP053 Ingyin Thwe (G1၊ band 400–700): basic 400 / 700 / 399 / 701 | လက်ခံ / လက်ခံ / ငြင်း / ငြင်း |
| PAY-S08 | − | Joining date မတိုင်မီ effective ဖြစ်သော assignment | ငြင်း |
| PAY-S09 | − | EMP001: effective `01/01/2026` assignment တစ်ခုထပ်ထည့် (ရှိပြီးသား) | ငြင်း |
| PAY-S10 | − | Structure MGMT ကို deactivate ပြီး assign | "Choose an active structure" |
| PAY-S11 | − | EMP048 Aung Myo Oo ၏ တစ်ခုတည်းသော assignment ကို delete | "needs at least one salary assignment" |
| PAY-S12 | + | EMP005: effective `01/11/2026` assignment ထပ်ထည့် (လစာတိုး) | Period ကုန်ရက်သည် effective date နောက်ဆိုလျှင် သုံး |

### ၅.၂ Period နှင့် workflow

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| PAY-P01 | + | ၂၀၂၆ ဧပြီ period ဖန်တီး (seed period များ: မေ–အောက်တိုဘာ Paid၊ နိုဝင်ဘာ Draft) | Draft; နာမည်နှင့်ရက်စွဲ ကိုယ်တိုင်ထွက်၊ pay date default = လကုန်ရက် |
| PAY-P02 | − | ၂၀၂၆ ဒီဇင်ဘာ period ဖန်တီး | "cannot be in the future" |
| PAY-P03 | − | ၂၀၂၆ နိုဝင်ဘာ period ဖန်တီး | "already exists" |
| PAY-P04 | − | Pay date သည် period မစမီ | ငြင်း |
| PAY-P05 | + | နိုဝင်ဘာ ၂၀၂၆ (Draft) ကို Process | Payslip ၅၈ ခု (၆၀ ထဲမှ EMP034 နှင့် EMP044 ကိုနုတ်)၊ status Pending Approval၊ စုစုပေါင်းများဖြည့် |
| PAY-P06 | − | ၂၀၂၆ ဧပြီ (Draft အဖြစ်ကျန်) ကိုဖန်တီးပြီး နိုဝင်ဘာ ၂၀၂၆ ကို process | "Finish <period> before processing ..." |
| PAY-P07 | G | ယခင်လ Approved (Paid မဟုတ်သေး) ဖြစ်နေစဉ် process | ရသည် |
| PAY-P08 | + | ဤ session တွင် employee အသစ် (assignment မရှိ) ဖန်တီးပြီး နိုဝင်ဘာ ၂၀၂၆ ကို process | Warning "... skipped"; တခြားသူတွေ ဆက်ထွက် |
| PAY-P09 | − | Employee တစ်ယောက်မှမဝင်ရသေးသောလ (ဥပမာ ၂၀၁၄ ဖေဖော်ဝါရီ) အတွက် period ဖန်တီးပြီး process | "no employees to pay in this period" (ထိုလကို လက်ခံမခံ အတည်ပြုပါ) |
| PAY-P10 | + | နိုဝင်ဘာ ၂၀၂၆ ကို process ပြီး EMP034 (exit 2026-08-31), EMP044 (exit 2026-06-15), EMP053 Ingyin Thwe (၀၂/၁၁/၂၀၂၆ ဝင်) ကိုရှာ | EMP034 နှင့် EMP044 မပါ၊ EMP053 ပါပြီး proration 29/30 |
| PAY-P11 | + | Pending Approval ကို Approve | Approved; approver အမည်နှင့်ရက်စွဲ သိမ်း |
| PAY-P12 | − | Draft သို့မဟုတ် Approved ကို Approve; non-Draft ကို Process; Approve မလုပ်ဘဲ mark paid | Conflict (လက်ရှိ status ပါသော message) |
| PAY-P13 | + | Approved period ကို Mark as paid | Paid; `paidOn` သတ်မှတ် |
| PAY-P14 | + | Pending Approval သို့မဟုတ် Approved မှ Return to draft | Payslip ဖျက်၊ စုစုပေါင်း reset၊ အကြောင်းပြချက် log |
| PAY-P15 | − | အောက်တိုဘာ ၂၀၂၆ (Paid) ကို Return to draft / edit / delete ကြိုးစား | Conflict (Paid က နောက်ဆုံး) |
| PAY-P16 | − | non-Draft period (ဥပမာ Pending Approval နိုဝင်ဘာ ၂၀၂၆) ကို edit/delete | Conflict |
| PAY-P17 | G | လူတစ်ယောက်တည်းက process, approve, mark paid အားလုံးလုပ် | ရသည် (တာဝန်ခွဲဝေမှုမရှိ) |
| PAY-P18 | + | Return to Draft ပြီး ပြန် process | လက်ရှိ data ဖြင့် payslip ပြန်ထွက် |

### ၅.၃ တွက်ချက်မှု (unit test များနှင့်လည်း တွဲကြည့်ပါ)

| ID | Type | Scenario / Input | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| PAY-C01 | + | Basic 3000, အလုပ်ရက် 22, housing 10% of basic, transport 50, tax 5% of gross, unpaid 2 ရက်, OT 2 နာရီ | Housing 300; OT 51.14; gross 3,401.14; tax 170.06; unpaid 272.73; **net 2,958.35** |
| PAY-C02 | + | တစ်လပြည့်၊ ခွင့်နှင့် OT မပါ | Basic ကိုက်; net = gross − component deduction များ |
| PAY-C03 | + | ၃၀ ရက်လတွင် ၁၁ ရက်နေ့ဝင် | Proration 20/30; basic 3000 → 2000; fixed component များပါ prorate |
| PAY-C04 | + | Exit date = ၂၀ ရက်နေ့ | Proration 20/30 (exit ရက်ပါ) |
| PAY-C05 | + | Period အတွင်း ဝင်လည်းဝင်၊ ထွက်လည်းထွက် | Days employed = exit − join + 1 |
| PAY-C06 | − | Period တစ်ရက်မှ မအလုပ်လုပ်ခဲ့ | Payslip မထွက် (calculator က null ပြန်) |
| PAY-C07 | + | OT ၉၀ မိနစ် | 1.5 နာရီ × hourly × 1.5; hourly = **basic အပြည့်** ÷ (အလုပ်ရက် × 8) |
| PAY-C08 | + | Period အတွင်း **Approved** OT သာတွက် | Pending/Rejected OT နှင့် period ပြင်ပရက်စွဲ OT ကို လျစ်လျူ |
| PAY-C09 | + | Period နယ်စပ်ကျော်သော unpaid leave | Period အတွင်းရက်သာ၊ ပိတ်ရက်/holiday ချန် |
| PAY-C10 | + | Half-day unpaid leave | 0.5 ရက် |
| PAY-C11 | + | Paid leave type များ (Annual, Medical စသည်) | Deduction မရှိ |
| PAY-C12 | G | Unpaid leave ရှိပြီး % of gross deduction ရှိ | Gross ကို unpaid leave က မလျှော့၊ ထို့ကြောင့် % deduction က gross အပြည့်ပေါ်တွက် |
| PAY-C13 | G | လအလယ်ဝင်သူ၌ unpaid leave သို့မဟုတ် OT | နေ့စား/နာရီနှုန်းက **basic အပြည့်** ကိုသုံး (prorate မလုပ်) |
| PAY-C14 | + | လအလယ်မှာ လစာပြင် | **Period ကုန်ရက်** မှာ အသက်ဝင်သော assignment ကို တစ်လလုံးသုံး |
| PAY-C15 | + | Structure ထဲက inactive component | တွက်ချက်မှုမှ ချန် |
| PAY-C16 | + | Attendance မှ Late နှင့် Absent ရက်များ | လစာကို မသက်ရောက် |
| PAY-C17 | + | Payslip က snapshot: နောက်မှ component သို့မဟုတ် department ပြောင်း | ရှိပြီးသား payslip မပြောင်း၊ period ကို ပြန် process မှသာ ပြောင်း |
| PAY-C18 | + | Payslip ကြည့်/print နှင့် payroll history chart | ကိန်းဂဏန်းများက run စုစုပေါင်းနှင့် ကိုက် |

## ၆။ Settings / Users / Roles (ထောက်ကူ)

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| SET-01 | − | Built-in role HR Administrator သို့မဟုတ် Employee ကို နာမည်ပြောင်း/delete | ငြင်း |
| SET-02 | − | Role Department Manager (user ၄ ဦး) ကို deactivate | Conflict (user အရေအတွက်ပါ) |
| SET-03 | − | HR Administrator ၏ permission ကို ပြင် | "always has full access and cannot be changed" |
| SET-04 | − | View မပါဘဲ Create/Edit ပေး | "View is required" |
| SET-05 | − | EMP001 (user ရှိပြီးသား) / EMP034 Soe Moe Kyaw (exit date ရှိ) အတွက် user ဖန်တီး | ငြင်း |
| SET-06 | − | ရှိပြီးသား user ၏ ချိတ်ထားသော employee ကို ပြောင်း | ငြင်း |
| SET-07 | − | Su Myat Noe အဖြစ် ကိုယ်ပိုင် role/status ပြောင်း သို့မဟုတ် ကိုယ့် account ဖျက် | Conflict |
| SET-08 | − | တစ်ဦးတည်းသော HR Administrator (Su Myat Noe, EMP002) ကို deactivate/demote/delete | "There must always be one active HR Administrator"။ ကိုယ်ပိုင် account rule (SET-07) က အရင်အလုပ်လုပ်သဖြင့် ဤ guard သို့ရောက်ရန် ဒုတိယ administrator တစ်ဦးဖန်တီးပါ |
| SET-09 | + | EMP012 Zaw Lin Oo (Locked) ကို unlock | Status Active |
| SET-10 | G | Payroll ဝင်ခွင့်မရှိသော role အဖြစ် သဘောထားပြီး Payroll ဖွင့် | ဝင်လို့ရနေသေး (permission ကို မပြဋ္ဌာန်း) |
| SET-11 | − | General settings: email မှား, မစာရင်းပါသော timezone/date format, ငွေကြေး ၃ လုံးအကြီးမဟုတ်, လ ၁–၁၂ ပြင်ပ | Field error များ |

## ၇။ Module ကျော် (အစမှအဆုံး) Scenario များ

| ID | Type | Scenario | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| XMOD-01 | X | Employee အသစ် (EMP061၊ ၀၂/၁၁/၂၀၂၆ ဝင်) → salary assignment → punch → နိုဝင်ဘာ ၂၀၂၆ process | Payslip တစ်ခု; basic ကို ဝင်ရောက်ရက်မှ prorate |
| XMOD-02 | X | Salary assignment **မရှိ** သော employee အသစ်ကို process | Warning "no salary assignment ... skipped"; payslip မထွက် |
| XMOD-03 | X | EMP056 အတွက် နိုဝင်ဘာတွင် အလုပ်ရက် ၂ ရက် Unpaid Leave (UL) approve → နိုဝင်ဘာ ၂၀၂၆ process | "Unpaid leave" deduction = ရက် × basic ÷ အလုပ်ရက် |
| XMOD-04 | X | **ခွင့်မှ attendance:** attendance ရှိပြီးသားရက်များကို annual leave approve | Record များ On Leave ပြ; ထိုရက်များ punch ကို ငြင်း |
| XMOD-05 | X | **OT မှ payroll:** ဖန်တီး → approve → process | "Overtime pay" line ပေါ်; punch မှထွက်သော OT တစ်ခုတည်းဆိုလျှင် ဘာမှမထပ်တိုး |
| XMOD-06 | X | EMP054 Zarni Tun ကို exit date 20/11/2026 ဖြင့် Resigned; pending leave, user account, လက်အောက်ငယ်သား, shift ကိုစစ် | Payroll ကို prorate; **ခွင့်, user, လက်အောက်ငယ်သား, shift မပြောင်း** (လက်ရှိ ပြုမူပုံအဖြစ် မှတ်ပါ) |
| XMOD-07 | X | EMP019 Wai Yan Soe ကို Engineering မှ Sales (Account Executive) သို့ပြောင်း | History "Transfer"; manager, shift, salary မပြောင်း; နောက် payslip တွင် department အသစ် |
| XMOD-08 | X | **Process ပြီးမှ approve:** period ကို process ပြီး ထိုလအတွက် ခွင့်/OT approve | Payslip မပြောင်း (stale)။ Return to Draft → ပြန် process လျှင် update |
| XMOD-09 | X | XMOD-08 ကို period **Paid** ဖြစ်ပြီးမှ လုပ် | ပြန်တွက်ရန်/ပြင်ရန်နည်းလမ်း မရှိ (Known gap) |
| XMOD-10 | X | **Payroll ပြီးမှ attendance ပြင်:** process လုပ်ပြီးလအတွက် adjustment approve | Payslip အပေါ် သက်ရောက်မှုမရှိ; lock မရှိ |
| XMOD-11 | X | Promotion: position ကို grade မြင့်သည့်ဆီပြောင်းပြီး basic အဟောင်းကိုထား | Promotion log ကျ; ရှိပြီးသား assignment ကို band အသစ်နှင့် ပြန်မစစ် (Known gap) |
| XMOD-12 | X | Department Marketing ကို deactivate (EMP011 Sophia Bennett, EMP032 Emily Wong, EMP051 Khaing Zar Chi ကျန်ရှိ) | ရသည်; ရှိပြီးသား employee များ ကျန်; assignment အသစ်မရ |
| XMOD-13 | X | အပြောင်းအလဲများလုပ်ပြီး page ကို reload | Data အားလုံး seed တန်ဖိုးသို့ ပြန်ကျ |
| XMOD-14 | X | Recruitment: candidate ကို Selected သတ်မှတ် | Employee မဖန်တီးပါ (လက်ဖြင့် register လုပ်ရမည်) |

## ၈။ ရက်စွဲထည့်ခြင်းနှင့် Filter (UI)

Shared form field သုံးသော form အားလုံး (employee form, leave, overtime, recruitment, CRUD dialog) နှင့် employee list ၏ **Joined from / Joined to** filter များတွင် သက်ရောက်သည်။ ပြသ/ရိုက်ထည့်ပုံစံ `dd/mm/yyyy`၊ သိမ်းသောတန်ဖိုး ISO `yyyy-MM-dd`။

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| DT-01 | + | ရက်စွဲ box ထဲကို click | Calendar ပွင့် |
| DT-02 | + | Calendar icon ကို click၊ Esc နှိပ် | ပွင့်၊ Esc ဖြင့်ပိတ်ပြီး တန်ဖိုးမပြောင်း |
| DT-03 | + | `04/11/2026` ရိုက်ပြီး အပြင်ကို click | လက်ခံ၊ calendar ပြန်ဖွင့်လျှင် ၂၀၂၆ နိုဝင်ဘာ ၄ ကို အမှတ်အသားပြ |
| DT-04 | + | `2026-11-04`, `4-11-2026`, `04 Nov 2026` ရိုက် | အားလုံး ၂၀၂၆-၁၁-၀၄ အဖြစ်လက်ခံ |
| DT-05 | − | `31/02/2026`, `13/13/2026`, `abc`, `04/11/26` ရိုက် | "Enter a valid date."၊ မသိမ်းပါ |
| DT-06 | B | `29/02/2028` / `29/02/2027` | လက်ခံ / "Enter a valid date." |
| DT-07 | − | လိုအပ်သောရက်စွဲကို ဖျက်ပြီး field မှထွက် | "<Label> is required." |
| DT-08 | + | ဗလာ Date of birth ၏ calendar ဖွင့် | ၁၈ နှစ်အရင်ကာလ၏ multi-year view ဖြင့်ပွင့်၊ click ၂ ချက် (နှစ်၊ လ/ရက်) ဖြင့်ရွေးနိုင် |
| DT-09 | + | Calendar မှရွေးပြီး စာကို လက်ဖြင့်ပြင် | စာနှင့် calendar တူညီနေ |
| DT-10 | + | Keyboard သာ: calendar icon သို့ Tab၊ Enter၊ မြား key, Page Up/Down၊ Enter | ပွင့်၊ ရက်/လ ရွှေ့၊ ရွေးနိုင်၊ focus က field သို့ပြန် |
| DT-11 | + | Calendar ဖွင့် | အပတ်စတင်ရက် = တနင်္လာ |
| DT-12 | + | ရှိပြီးသား record ကို edit | သိမ်းထားသောရက်ကို `dd/mm/yyyy` ဖြင့်ပြ၊ မထိလျှင် တန်ဖိုးမပြောင်း |
| DT-13 | X | ကွန်ပျူတာ time zone ကို UTC−10 နှင့် UTC+13 သို့ပြောင်း၊ ရက်စွဲသိမ်းပြီးပြန်ဖွင့် | ရက်တူညီ (တစ်ရက်လွဲမသွား) |
| DT-14 | + | Employee list: *Joined from* တွင် မှန်ကန်သောရက်ရိုက် | ရက်မှန်ကန်သည်နှင့် list filter ဖြစ် (အပြင်click မလို) |
| DT-15 | + | *Joined from* ကိုဖျက် | Filter ပြေ၊ row အားလုံးပြန်ပေါ် |
| DT-16 | B | *Joined to* သတ်မှတ်ပြီး *Joined from* calendar ဖွင့် | *Joined to* ထက်နောက်ကရက်များ ပိတ်ထား၊ *Joined to* ကိုလည်း *Joined from* မတိုင်မီ ပိတ်ထား |
| DT-17 | + | Employee list: Status, Department, Branch, Employment type | Status သည် **More filters** ထဲတွင် ကျန်သုံးခုနှင့်အတူရှိ၊ badge က လေးခုလုံးကိုရေတွက် |
| DT-18 | + | Filter များစွာသုံး၊ chip တစ်ခုဖယ်၊ ပြီးမှ **Clear filters** | Chip ဖယ်လျှင် ထို filter သာပြေ၊ Clear က ရက်စွဲအပါအဝင် အားလုံး reset |
| DT-19 | + | Status + Department + ရက်အပိုင်းအခြား ပေါင်းသုံး | Filter **အားလုံး**နှင့်ကိုက်သော row သာ၊ မတွေ့လျှင် "No employees found" |
| DT-20 | X | ဖုန်းအကျယ် (360 px): calendar ဖွင့် | Screen ထဲဝင်၊ page ဘေးတိုက် scroll မဖြစ် |
| DT-21 | G | တခြား list များ (attendance, CRUD page) ၏ "from / to" filter နှင့် Excel export dialog ကိုဖွင့် | Browser ၏ native date input အတိုင်းကျန် (ပုံစံကွဲ၊ အချို့ locale တွင် `mm/dd/yyyy`) |

## ၉။ Recruitment

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| REC-01 | − | Closing date က posted date မတိုင်မီ၊ posted date က အနာဂတ် | Closing date ပေါ်ငြင်း / "cannot be in the future" |
| REC-02 | B | Vacancies 0 / 1 / 50 / 51 | ငြင်း / လက်ခံ / လက်ခံ / ငြင်း |
| REC-03 | − | Hiring manager EMP034 Soe Moe Kyaw (Resigned) သို့မဟုတ် EMP044 Tun Tun Naing (Terminated) | "Choose a current employee as hiring manager." |
| REC-04 | + | DevOps Engineer: Draft → Open → On Hold → Open → Closed | ရ |
| REC-05 | − | DevOps Engineer: Draft → On Hold; QA Engineer (Filled) → Open | "cannot be set to ..." (Filled မှ ထွက်လမ်းမရှိ) |
| REC-06 | − | Financial Analyst (Closed၊ closing date ၂၀ ရက်အရင်) ကို reopen | "The closing date has passed. Edit the opening ..." |
| REC-07 | − | Selected လူအရေအတွက်က vacancies နှင့်ညီပြီး reopen | "All vacancies already have a selected candidate." |
| REC-08 | − | Vacancies ကို selected လူအရေအတွက်ထက် လျှော့ | "n candidate(s) are already selected for this job." |
| REC-09 | − | Application ရှိသော opening (ဥပမာ Senior Angular Developer၊ applicant ရှိမရှိ အရင်အတည်ပြု) ကို delete | Conflict (application အရေအတွက်ပါ) |
| REC-10 | − | အတွေ့အကြုံ −1 / 51 နှစ်၊ email မှား၊ phone မှား | Field error များ |
| REC-11 | − | Candidate တစ်ဦးက job တူကို နှစ်ကြိမ်လျှောက် | "already applied for this job" |
| REC-12 | + | Applied → Screening → Interview → Selected | အစဉ်လိုက်ရ |
| REC-13 | − | Stage ကျော် (Applied → Interview) သို့မဟုတ် Rejected မှထွက် | "cannot move to ..." |
| REC-14 | − | အကြောင်းပြချက်မထည့်ဘဲ Reject | "A reason is required." |
| REC-15 | − | Hire / Strong hire အကြံပြုသော completed interview မရှိဘဲ Select | "Selecting a candidate needs a completed interview ..." |
| REC-16 | − | Vacancies ထက်ပိုများသောသူကို Select | "All n vacancies ... already have a selected candidate." |
| REC-17 | + | Job က Closed ဖြစ်နေသော application ကို Reject | ရ (ရှေ့သို့ရွှေ့ခြင်းသာ တားမြစ်) |
| REC-18 | − | Job က Closed/Draft/Filled ဖြစ်စဉ် ရှေ့သို့ရွှေ့ | "... so applications cannot move forward." |
| REC-19 | − | Selected application ကို delete | "Reject it first." |
| REC-20 | B | Interview ကြာချိန် 14 / 15 / 240 / 241 မိနစ် | ငြင်း / လက်ခံ / လက်ခံ / ငြင်း |
| REC-21 | − | Applied သို့မဟုတ် Selected ရှိ application အတွက် interview ချိန်း | "Interviews can be scheduled for ... Screening or Interview" |
| REC-22 | − | အတိတ်ရက်တွင်ချိန်း၊ အနာဂတ် interview ကို Completed သတ်မှတ် | နှစ်ခုလုံးငြင်း |
| REC-23 | − | Interviewer EMP034 Soe Moe Kyaw (Resigned); interviewer တူ၊ တစ်ရက်တည်းအချိန်ထပ် | "Choose a current employee as interviewer." / "The interviewer already has an interview at ..." |
| REC-24 | − | Score (1–5) သို့မဟုတ် recommendation မပါဘဲ Complete၊ Completed မဟုတ်သည့်အရာကို score ပေး | ငြင်း |
| REC-25 | − | Completed interview ကို delete၊ Cancelled ကို edit | Conflict |
| REC-26 | G | Candidate ကို Selected သတ်မှတ် | Employee မဖန်တီး (XMOD-14 ကိုကြည့်) |

## ၁၀။ Excel Import / Export (Employees)

ကန့်သတ်ချက်: 5 MB, 5,000 row, worksheet 5 ခု, export 50,000 row။ အသင့်ပြင်ဆင်ထားသောဖိုင်များနှင့် မျှော်မှန်းရလဒ်များကို [test-data/](test-data/README.md) တွင်ကြည့်ပါ၊ သို့မဟုတ် download လုပ်ထားသော template ဖြင့် ကိုယ်တိုင်ပြုလုပ်ပါ။

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| XLS-01 | + | Template download | Instructions sheet, header သာပါသော data sheet, field guide, drop-down, reference sheet များပါ |
| XLS-02 | + | မှန်ကန်သောဖိုင်ကို Import (Insert only) | Preview တွင် ready row များ၊ result အရေအတွက်ကိုက်၊ history နှင့် report ရ |
| XLS-03 | − | `.txt`/`.png` ကို `.xlsx` ဟုနာမည်ပြောင်း၊ `.csv`/`.xls` တင် | ငြင်း (file type / signature) |
| XLS-04 | B | ဖိုင် 5 MB / အနည်းငယ်ကျော်၊ 5,000 / 5,001 row | လက်ခံ / ငြင်း၊ လက်ခံ / ငြင်း |
| XLS-05 | − | Workbook ဗလာ၊ header row သာ၊ header ထပ် | ဖိုင်အဆင့် error ရှင်းရှင်းလင်းလင်း |
| XLS-06 | + | Header အစီအစဉ်ကွဲ၊ စာလုံးအကြီးအသေးကွဲ၊ alias သုံး | အလိုအလျောက်ကိုက်၊ mapping မလို |
| XLS-07 | − | လိုအပ်သော header မပါ / မသိသော column / header နှစ်ခုက field တစ်ခုသို့ | Mapping အဆင့်တွင် ပြဿနာတစ်ခုချင်းပြပြီး ပြင်ခွင့်ပေး |
| XLS-08 | − | Employee code `EMP061` တူသော row နှစ်ခု | နှစ်ခုလုံး duplicate အဖြစ်အမှတ်အသား |
| XLS-09 | − | Insert only နှင့် code ရှိပြီးသား `EMP005`; Update only နှင့် မသိသော code `EMP999` | Row များကို အကြောင်းပြချက်ဖြင့်ငြင်း |
| XLS-10 | + | Update တွင် optional cell ဗလာ | လက်ရှိတန်ဖိုးကျန် |
| XLS-11 | − | Formula cell (`=1+1`) | Error အဖြစ်သတင်းပို့၊ မည်သည့်အခါမှ မတွက် |
| XLS-12 | − | All-or-nothing ဖြင့် မှားသော row တစ်ခုပါ | မပြင်မချင်း option မရ၊ ဘာမှ import မဖြစ် |
| XLS-13 | + | မှားသော row တစ်ခုပါပြီး valid rows only import | Valid row များ import ဖြစ်၊ မအောင်မြင်သည်များ report တွင်ပါ |
| XLS-14 | − | Form နှင့်တူ rule များ: ၁၈ နှစ်မပြည့်, email ထပ်, တခြား company ၏ branch | Form နှင့်တူ row error များ |
| XLS-15 | + | ရက်စွဲကို `yyyy-MM-dd`, `dd MMM yyyy`, Excel ရက်စွဲအစစ် | အားလုံးလက်ခံ |
| XLS-16 | − | ရက်စွဲကို စာသား `04/11/2026` အဖြစ်ရေး (file 2, row 14) | ငြင်း: "Joining Date is not a valid date." `yyyy-MM-dd`, `dd MMM yyyy` နှင့် Excel ရက်စွဲအစစ်သာလက်ခံ၊ App ကိုယ်တိုင်က `dd/mm/yyyy` ပြသော်လည်း |
| XLS-17 | − | Confirm နှစ်ကြိမ်နှိပ် / execute ပြီးသော batch ကိုပြန်သွားပြီး ပြန်run | Batch တစ်ကြိမ်သာ run နိုင် |
| XLS-18 | + | Filter သုံးပြီး Export (ရလဒ်တစ်မျက်နှာထက်ပိုများ) | ကိုက်ညီသော row အားလုံး၊ မြင်ရသည့်စာမျက်နှာသာမဟုတ် |
| XLS-19 | + | Default ဖြင့် Export | ကိုယ်ရေး/ငွေကြေး column များ မရွေးမချင်းပိတ်ထား |
| XLS-20 | − | Column တစ်ခုမှမရွေးဘဲ Export | "at least one column" |
| XLS-21 | + | Cell စာသားက `=`, `+`, `-`, `@` ဖြင့်စ | ရှေ့တွင် apostrophe ထည့်ပြီး export၊ သာမန်ကိန်းနှင့် ဖုန်းနံပါတ်မထိ |
| XLS-22 | + | Export ဖိုင်ကိုဖွင့် | Header ရပ်ထား၊ filter, type မှန်သော cell, Department/Branch summary sheet များပါ |

## ၁၁။ UI, Accessibility နှင့် Responsive

| ID | Type | Scenario / အဆင့်များ | မျှော်မှန်းရလဒ် |
|---|---|---|---|
| UI-01 | + | Sidebar screen အားလုံးကို 1440 px နှင့် 360 px တွင်ကြည့် | Page ဘေးတိုက် scroll မရှိ၊ table များက ကိုယ်ပိုင် container အတွင်း scroll |
| UI-02 | + | Keyboard သာ: form ကို Tab ဖြင့်ကျော်၊ dialog ဖွင့်/ပိတ် | အစဉ်မှန်၊ focus မြင်ရ၊ Esc ပိတ်၊ focus က ဖွင့်ခဲ့သည့်နေရာသို့ပြန် |
| UI-03 | + | Screen reader / accessible name | Button, link, field တိုင်းနာမည်ရှိ၊ page တစ်ခုလျှင် `h1` တစ်ခု |
| UI-04 | + | List, form, chart များတွင် dark mode ပြောင်း | စာနှင့် error အရောင်များ ဖတ်ရဆဲ |
| UI-05 | + | Browser zoom 200% နှင့် text-only zoom | Control မြတ်မနေ၊ စာမထပ် |
| UI-06 | + | မသိမ်းရသေးသော form မှထွက် | သတိပေးချက်ပေါ်၊ ဆက်နေလျှင် ထည့်ထားသည်များကျန် |
| UI-07 | + | Delete တိုင်း | Record အမည်ပါသော confirmation dialog၊ Cancel လျှင်ဘာမှမပြောင်း |
| UI-08 | + | နှေးသော/မအောင်မြင်သော load | Loading ပြပြီး error တွင် "Try again" |
| UI-09 | + | Sidebar ကို collapse၊ flyout menu ဖွင့်၊ mobile drawer သုံး | အလုပ်လုပ်ပြီး navigate ပြီးနောက်ပိတ် |
| UI-10 | + | မရှိသော URL | "Page not found" စာမျက်နှာ |
| UI-11 | + | Chrome, Edge, Firefox, Safari: ရက်စွဲရိုက်/ရွေး | ပုံစံနှင့်ရလဒ်တူ |
| UI-12 | − | Error များစွာဖြင့် form submit | "Please correct n fields" အနှစ်ချုပ်၊ field တိုင်းတွင် ကိုယ်ပိုင်စာ |

## ၁၂။ Smoke Suite (မိနစ် ၃၀ ခန့်)

Build တိုင်းပြီးနောက် ဤအရာများကို run ပါ။ Release မတိုင်မီ module table အပြည့်အစုံကို run ပါ။

| အပိုင်း | ID များ |
|---|---|
| Employees | EMP-02, EMP-03, EMP-05, EMP-06, EMP-28, EMP-18 |
| ရက်စွဲနှင့် filter | DT-01, DT-03, DT-05, DT-14, DT-17, DT-18 |
| Organization | ORG-01, ORG-08 |
| Attendance | ATT-01, ATT-03, ATT-09, ADJ-07 |
| Leave | LV-01, LV-17, LV-20, LV-21, LV-28 |
| Payroll | PAY-P05, PAY-P06, PAY-P11, PAY-P13, PAY-C01 |
| Recruitment | REC-04, REC-12, REC-15 |
| Excel | XLS-02, XLS-03, XLS-18 |
| Settings | SET-08 |
| Module ကျော် | XMOD-01, XMOD-03 |

## ၁၃။ Automation ဖုံးလွှမ်းမှု

အင်္ဂလိပ်ဘာသာပိုင်း ၁၃ ကိုကြည့်ပါ (ဖိုင်အမည်များ တူညီသည်)။ မ automate ရသေးသည်များ: calendar popup (DT-01 မှ DT-13, DT-16, DT-20), time-zone စစ်ဆေးမှု, browser matrix, Recruitment/Payroll/Settings screen component များ။ ရှိပြီးသား e2e က ISO စာသားကို ရိုက်ထည့်ခြင်းသာလုပ်သည်။
