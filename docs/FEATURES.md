# Features and business rules

All rules below are enforced by the **mock services** (the contract-level behaviour a real API should
reproduce) and, where noted, by the UI. Everything is sample data.

## Dashboard

KPI cards, charts (headcount growth, department split, attendance trend), pending leave requests,
upcoming events, the latest processed payroll summary and a recent-activity feed.

## Organization

Companies → Branches → Departments → Sections → Positions, plus Job Grades (with salary bands) and a
Reporting Structure view. Hierarchy is validated (a department belongs to a branch of its company,
etc.). Records that are referenced cannot be deleted — deactivate them instead.

## Employees

- **List** with search, filters, sorting and paging; **form** for add/edit with validation (phone,
  age, dates, uniqueness); **profile** with tabs (overview, employment, history, documents, salary
  summary, attendance, leave); **documents** library (file name only is stored).
- The profile **Salary** tab is calculated from the employee's current salary assignment.

## Attendance

Overview dashboard, check in/out (simulated), records, late & early exceptions, shifts, shift
assignment, work schedules (weekly grid), overtime requests and attendance adjustments with an
approval workflow. Daily status is derived from shift rules, holidays and approved leave.

## Leave

Leave types, policies (the most specific active policy for an employee's job grade, with minimum service and monthly accrual), balances, applications with
submit → approve/reject workflow, approvals queue and a team calendar. Balances are computed from
policy, accrual and approved leave; working days exclude weekends and holidays.

## Payroll (sample figures only)

- **Salary components** (earning/deduction; fixed, % of basic or % of gross), **salary structures**
  (sets of components) and **employee salaries** (basic salary + structure from an effective date;
  basic must stay inside the job-grade band; a later row revises a salary).
- **Payroll processing**: create a period (year + month; not in the future; one per month), then
  **Process → Approve → Mark as paid (sample)**. A processed or approved period can be **returned
  to draft** (payslips are removed); paid periods are final. Earlier periods must be finished first.
  Employees without a salary assignment are skipped with a warning.
- **Calculation** (`core/utilities/payroll-calc.ts`): proration by calendar days for joiners and
  leavers; allowances; overtime at 1.5× the hourly rate (basic ÷ (working days × 8)); unpaid-leave
  deduction at the daily rate; deductions on basic or gross; net = gross − deductions.
- **Payslips** are snapshots (later setup changes do not alter them), listed per period and
  viewable/printable. **History** shows gross vs net per period with a table alternative.
- **Not included:** real tax, social-security or statutory rules, payment files, bank integration.

## Recruitment

- **Job openings**: created as Draft; actions Publish, Put on hold, Resume, Close, Reopen. Closed and
  Filled openings are not editable; an opening with applications cannot be deleted. An opening
  becomes **Filled** automatically when enough candidates are selected and reopens if one drops out.
- **Candidates**: unique email; cannot be deleted while they have applications.
- **Pipeline board**: Applied → Screening → Interview → Selected / Rejected. Rejecting requires a
  reason and cancels scheduled interviews. **Selected** needs a completed interview with a "Hire"
  or "Strong hire" recommendation and a free vacancy. Applications only open for **Open** openings,
  one per candidate per opening.
- **Interviews**: scheduling moves a screened candidate to Interview; an interviewer cannot be
  double-booked; completing needs a score (1–5) and a recommendation; completed interviews cannot be
  deleted; cancelled ones are locked. Record feedback by editing an interview.

## Settings (UI simulation)

- **General settings**: organization name, contact email, time zone, date format, currency, fiscal
  year start, notification toggles. Saved in memory; other screens do not read them.
- **Users**: linked to one employee (name/email copied from the employee); statuses Active,
  Inactive, Locked ("Unlock account"); you cannot delete or demote your own account.
- **Roles**: built-in roles cannot be renamed or deleted; roles with users cannot be deleted; a role
  with active users cannot be deactivated.
- **Permissions**: module × action matrix per role (granting anything grants View; removing View
  clears the module). HR Administrator is read-only. **Nothing is enforced anywhere.**
