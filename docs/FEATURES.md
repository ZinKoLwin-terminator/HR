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

## Performance (KPI and employee performance)

Branch `feature/api-kpi` (API and screens) with the database branch `feature/schema-kpi`. The API is the authority
for every rule and score below; the demo build's mock services repeat the rules for display only (see "Demo build"
at the end of this section). Full contract: `api/README.md`, the "HTTP contract (KPI ...)" sections. Customer guide:
[modules/performance.md](modules/performance.md).

**Screens** (sidebar group **Performance**, routes under `/performance`; each route carries its own permission):

| Screen | Route | Permission | Purpose |
| --- | --- | --- | --- |
| Dashboard | `/performance/dashboard` | `kpi:View` | My performance; My team (`kpi:Approve`); HR overview (`kpi:Approve` with `employees:Edit`). `/performance` opens the first Performance screen the role may see |
| My KPIs | `/performance/my-kpis`, `/:id` | `kpi:View` | The caller's own scorecards (never one in Planning); enter results, attach evidence, submit, read the outcome |
| Reviews | `/performance/reviews`, `/:id` | `kpi:Approve` | The review queue (oldest submission first) and the review screen |
| Reports | `/performance/reports` | `kpi:View` | Summary by period, employee history, department, ranking, KPI achievement, target vs actual; Excel export and print |
| Assignments | `/performance/assignments`, `/:id` | `kpi-setup:View` | Assign KPIs to one employee or in bulk, edit the plan, finalize it |
| Templates, Categories, Evaluation Periods, Rating Bands | `/performance/templates` etc. | `kpi-setup:View` | HR setup; Create, Edit and Delete need the matching `kpi-setup` action |

**Roles** (starter grants of the database post-deployment script, mirrored in the mock):

| Role | Grants | In practice |
| --- | --- | --- |
| HR Administrator | everything | HR tier (has `employees:Edit` and `kpi:Approve`) |
| HR Officer | `kpi-setup` View, Create, Edit, Delete; `kpi` View, Create, Approve | HR tier: setup, every scorecard, the HR dashboard and the HR final review |
| Department Manager | `kpi` View, Create, Approve | Team tier: own, direct reports' and the scorecards naming them reviewer |
| Employee, Payroll Officer, Recruiter | `kpi` View, Create | Own tier: own results and submission |
| Auditor (inactive) | `kpi-setup` View; `kpi` View | Read only |

The HR tier is `kpi:Approve` **together with** `employees:Edit`, as for leave and attendance: granting both to a role
also grants the KPI HR rights. Give `employees:Edit` only to HR staff.

**Setup.**

- **Categories** (code, name) and **rating bands** (label, minimum score; a score gets the label of the highest band
  it reaches). Default bands: Outstanding 90, Exceeds Expectations 75, Meets Expectations 60, Needs Improvement 40,
  Unsatisfactory 0.
- **Templates**: measurement type (Number, Percentage, Currency, Hours, Rating, Boolean), direction, scoring method,
  default target and weight, optional range, rating scale and achievement cap, frequency, optional department. A
  template with assigned KPIs cannot be deleted; deactivate it instead.
- **Evaluation periods**: per company, Monthly, Quarterly, Half-Yearly, Yearly or Custom. Draft → Active → Completed →
  Closed (final), each by command. Two periods of the same type in one company cannot share a day. Active: only the
  name and the submission due date can change; Completed and Closed: no edit. A period cannot be closed while a
  scorecard is in review or returned. "Requires HR final review" decides whether approval finalizes at once.

**Assignment and plan (HR).** A scorecard is one employee's KPI plan and result for one period (one per employee and
period). Assigning copies the template's code, name, rules, target and weight into the scorecard, so a later template
edit never changes it; the employee's company, branch, department and position are copied too, so reports keep the
department the employee was in during the period. Bulk assignment takes employee ids or department, branch, position
and job-grade filters (at most 500 employees) and lists who was assigned and who was skipped with the reason.
Finalizing the plan needs weights of exactly 100% and sets the reviewer to the employee's **current** manager (none
when the employee has no manager: then only the HR tier can review). **Nobody plans their own scorecard**: the API
refuses it (409 "You cannot plan your own scorecard.") and bulk assignment skips that employee; another HR user plans
the HR person's scorecard.

**Workflow.**

```
Planning --finalize plan (HR)--> Draft --submit--> Submitted --start review--> Under Review
Submitted / Resubmitted / Under Review --approve--> Approved (only when the period requires an HR final review) --finalize (HR)--> Finalized
Submitted / Resubmitted / Under Review --approve, no HR review--> Finalized
Submitted / Resubmitted / Under Review / Approved --return (reason)--> Returned --submit--> Resubmitted
Submitted / Resubmitted / Under Review / Approved --reject (reason)--> Rejected (final)
```

- The employee enters results, changes evidence and submits while the period is Active. A Returned scorecard may also
  do all three while the period is Completed (a reviewer may return it then, and it must not be stuck); a Draft one
  needs Active. Every KPI needs a result to submit. Results are read-only after submitting.
- The reviewer (or the HR tier) can save ratings 1 to 5 and comments as a draft, then approve, return or reject.
  Return and reject need a reason. Returning clears the score and the whole review draft (the overall and per-KPI
  ratings and comments, and the HR comment), so the resubmitted results are reviewed afresh; the reason stays in the
  history. Review steps need an Active or Completed period, and the review queue lists only those periods.
- A period cannot be closed while a scorecard is Submitted, Resubmitted, Under Review, Approved or Returned:
  `Period "{name}" still has {N} scorecard(s) in review or returned to the employee. Finish them before closing.`
- Nobody reviews their own scorecard (409). The HR final review is HR tier only, and never by the person who
  recorded the manager-stage approval (409).
- Every step appends to the review history (actor, comment, score at the time). The history is append-only: the
  API's database account cannot update or delete it.
- One `rowVersion` guards the whole scorecard: two writes on the same version give one success and one 409.

**Calculation** (`KpiCalculator` in `api/src/Apex.Hrms.Domain/Performance`, the only authority for official scores;
the demo mirror is `ui/src/app/core/utilities/kpi-calculation.ts`):

| Rule | Achievement % |
| --- | --- |
| Higher Is Better (Direct or Capped Percentage) | actual ÷ target × 100 (0 for an actual of 0 or less) |
| Lower Is Better (needs a cap) | target ÷ actual × 100; an actual of 0 scores the cap; a negative actual is refused |
| Exact Target | 100 − (difference from the target ÷ target × 100) |
| Range Based (method and direction together) | 100 inside the range; below it actual ÷ minimum × 100; above it maximum ÷ actual × 100 |
| Rating Based (Rating measurement) | actual ÷ maximum rating × 100 (actual 0 to the maximum) |
| Boolean (Boolean measurement) | 0 → 0, 1 → 100 (nothing else accepted) |

- Then, in this order: never below 0, at most the template's cap (Capped Percentage; 1 to 500), at most the safety
  ceiling of **1000%** (so an uncapped Direct Percentage stays in range), rounded to **2 decimals, half away from
  zero**.
- Weighted score = achievement × weight ÷ 100, rounded the same way; overall score = the sum of the weighted scores,
  rounded. Weights are above 0, at most 100, at most 2 decimals, and add up to exactly 100.
- Decimal arithmetic only (no floating point on the server). The score is calculated at submit and again at approve,
  from the stored results, and stored with **Calculation Version 1**: changing a formula or the rounding means a new
  version, so a stored score stays explainable.
- Template rules (`KpiRule`, the same as the database CHECK constraints): Rating Based goes with the Rating
  measurement, Boolean with Boolean, Range Based method with the Range Based direction; Capped Percentage and **Lower
  Is Better require a cap**; Direct Percentage has none; the target is above 0 for Higher Is Better and Exact Target.
- Golden cases shared by the API tests and the Vitest unit tests: `docs/test-data/kpi-calculation-cases.json` (single
  achievements) and `docs/test-data/kpi-scorecard-cases.json` (whole scorecards, weights, rating labels and the
  dashboard figures; in the UI run by `core/utilities/kpi-scorecard-cases.spec.ts`).

**Who sees what** (row scope, applied in the query: a scorecard outside it is 404 and absent from every list,
report, dashboard and export):

- **Own** (anyone else): their own scorecards, never one in Planning (HR's targets are still being prepared).
- **Team** (`kpi:Approve` without `employees:Edit`): own, direct reports' and every scorecard naming them reviewer.
  The reviewer is fixed when the plan is finalized, so it stays after a transfer; a new manager sees the scorecard
  but cannot review it.
- **All** (HR tier): every scorecard.
- **Review visibility for the scorecard's own employee:** the manager rating and comments (scorecard and KPI) only
  once the scorecard is Finalized or Rejected (not while Approved: HR can still return it); the HR comment only once
  Finalized; the reason of a Return or Reject always; the other history comments once Finalized or Rejected. The
  history entries themselves are never hidden. Reviewers, managers and HR read everything their scope allows.
- Activity entries (category `performance`) name the employee by code and the period only, never a comment, a
  result or a file name, and never tell a review decision apart.

**Evidence files.**

*Why evidence files?* A KPI result is a number the employee types. Evidence lets the reviewer verify a reported
actual result, for example a sales report, a training certificate or a signed delivery note, instead of trusting the
typed number. It also gives HR an audit trail if a result is disputed later. Evidence does not change the score and is
not a workflow step.

- Only the scorecard's own employee adds or removes files, while the scorecard is Draft or Returned and the period
  allows results. The employee, the reviewer and manager, and HR download them (always as a download, never shown
  inline).
- Allowed: PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), **decided by the file's content**, with an extension that
  agrees; at most **10 MB** per file and **5 files per KPI**; at most **10 uploads per minute per account**
  (`Kpi:EvidenceUploadsPerMinute`). The file name is cleaned and only displayed.
- The database keeps metadata only; the files live in the evidence folder (`Kpi:EvidenceFolder`, see
  [DEPLOYMENT.md](DEPLOYMENT.md)). **There is no virus or malware scan** yet.

**Dashboards and reports.** Read-only server figures over the caller's scope. Average score counts **Finalized**
scorecards only (Rejected and in-progress never count; "—" when there is none). Completion rate = Finalized ÷ every
scorecard past Planning. Ranking uses standard competition ranks (90, 90, 80 → 1, 1, 3). "Requiring attention" is a
Finalized score below the lowest active band of at least 60 (or 60). The manager dashboard lists at most 200 team
scorecards, by employee code; when there are more it says so and links to KPI Reports, and its figures still count
all of them. Excel export: three export-only modules (see
[EXCEL-IMPORT-EXPORT.md](EXCEL-IMPORT-EXPORT.md)); PDF is the browser's print of the report.

**Demo build (mock).** Sample data only, and nothing is security:

- HR, manager and own are decided from the **role names** of the simulated session (HR Administrator and HR Officer
  see everything, Department Manager sees the team), not from the `kpi:Approve` and `employees:Edit` keys; the mock
  services check no `kpi-setup` or `kpi` permission. The sidebar and route guards follow the session's permission
  keys, as in API mode.
- Evidence files are held **in memory** and lost on reload; no virus scan and no upload rate limit.
- Otherwise the mock repeats the API's rules and messages: nobody plans their own scorecard, the review visibility
  rule (`core/utilities/kpi-review-visibility.ts`), the "was reviewed" activity wording, the Completed-period rule for
  Returned scorecards, the close check, Return clearing the review draft and the manager dashboard's 200-row list.
- The figures use the same formulas and golden cases; with the API on, the server calculates them.

## Grievances (employee grievance cases)

Branches `feature/grievance` (screens), `feature/api-grievance` (API) and the database branch `feature/schema-grievance`
(schema `grv`); none is on `main`. The API is the authority for scope, redaction, the state machine, files and figures;
the demo build's mock services repeat the rules for display only and protect nothing (see "Demo build" at the end of this
section). Full contract: `api/README.md`, "HTTP contract (grievances)". Customer guide (Burmese):
[modules/grievance.md](modules/grievance.md).

**Screens** (sidebar group **Grievances**, routes under `/grievances`; each route carries its own permission, and
`/grievances` opens the first screen the role may see):

| Screen | Route | Permission | Purpose |
| --- | --- | --- | --- |
| Overview | `/grievances/overview` | `grievance:View` | Counts of the caller's own cases, unread updates, latest changes, "New grievance" |
| My Grievances | `/grievances/my`, `/my/:id` | `grievance:View` | Own cases including drafts; timeline, messages with HR, shared files, the resolution when shared |
| New grievance, Edit draft | `/grievances/my/new`, `/my/:id/edit` | `grievance:Create` | The submission form (unsaved-changes guard) |
| Dashboard | `/grievances/dashboard` | `grievance-management:View` | HR figures and charts |
| Case Management | `/grievances/cases`, `/:id` | `grievance-management:View` | Every submitted case; triage, assignment, corrective actions, every command |
| Record grievance | `/grievances/cases/new` | `grievance-management:Create` | HR files a grievance an employee raised in person, by phone, email or letter, in that employee's name ("Record grievance" on the case list; unsaved-changes guard) |
| Investigations | `/grievances/investigations`, `/:id` | `grievance-investigation:View` | Cases with the caller's active assignment |
| Reviews | `/grievances/reviews`, `/:id` | `grievance-review:View` | Escalated cases and proposals to decide |
| Review Summary | `/grievances/review-summary` | `grievance-review:View` | Review-queue figures and the summary (aggregates only, no case list, no names) |
| Reports | `/grievances/reports` | `grievance-management:View` | Case report, Excel export, print |
| Access log | `/grievances/access-log` | `grievance-management:View` and the HR Administrator role | Module-wide access log; linked from Reports, not in the sidebar |
| Categories | `/grievances/categories` | `grievance-management:View` (Create, Edit, Delete to change) | Code, name, anonymity allowed, default target days, sort order, status |

**Permissions** (four modules; starter grants of the post-deployment script, mirrored in the mock):

| Module | Actions | Scope | Starter grants |
| --- | --- | --- | --- |
| `grievance` | View, Create | The caller's own cases (drafts included), named or anonymous | Employee, HR Officer, Department Manager, Payroll Officer, Recruiter |
| `grievance-investigation` | View, Edit | Cases with an active assignment of the caller's employee; not drafts, not archived | HR Officer |
| `grievance-management` | View, Create, Edit, Delete | Every non-draft case (archived only with the Archived filter); categories; Delete archives | None (HR Administrator only) |
| `grievance-review` | View, Approve | Escalated open cases, pending proposals, and proposals the caller decided (read only); not archived | None (an HR Administrator grants it in Settings) |

- HR Administrator holds every grievance grant. The Auditor role has none.
- **Every case-team tier silently leaves out a case the caller submitted (named or anonymous) and a case whose subject
  employee is the caller**: it is not listed and opening it is `404`.
- The built-in administrator (no linked employee) cannot raise a grievance or be assigned (`403`).

**Workflow** (`GrievanceCaseWorkflow` in the API, `core/utilities/grievance-workflow.ts` in the UI; both run the golden
file `docs/test-data/grievance-transition-cases.json`):

| From | Action | To | Who |
| --- | --- | --- | --- |
| Draft | Submit (declaration required; anonymous only if the category allows it) | Submitted (number `GRV-yyyy-nnnnn`, UTC year) | Owner, `grievance:Create` |
| Submitted | Acknowledge (priority and target date; Critical escalates at once and alerts the reviewers) | Acknowledged | Management Edit |
| Acknowledged, Reopened | Start review | Under Review | Management Edit |
| Acknowledged, Under Review, Reopened | Assign a Lead | Assigned | Management Edit |
| Assigned, Under Investigation, Awaiting Employee Response | Replace the Lead (reason required) | unchanged | Management Edit |
| Any open status after Submitted | Add a Member, remove a Member (the Lead is only replaced) | unchanged | Management Edit |
| Assigned | Start investigation | Under Investigation | Assigned investigator Edit or Management Edit |
| Under Review, Assigned, Under Investigation | Request information (stores the return status) | Awaiting Employee Response | Management Edit or assigned investigator Edit |
| Awaiting Employee Response | Respond (the submitter's message) | The stored return status | Owner |
| Under Investigation | Propose resolution | Resolution Proposed | Lead investigator Edit |
| Under Review | Propose resolution | Resolution Proposed | Management Edit |
| Resolution Proposed | Approve (choose whether the employee sees the summary) | Resolved | `grievance-review:Approve`; not the proposer, not an active investigator of the case |
| Resolution Proposed | Send back (reason) | Under Investigation, or Under Review without a Lead | As approve |
| Submitted, Acknowledged, Under Review | Reject (reason) | Rejected | Management Edit |
| Resolved | Close | Closed | Management Edit |
| Resolved, Rejected | Reopen (reason) | Reopened | Management Edit |
| Closed | Reopen (reason) | Reopened | Management Edit **and** `grievance-review:Approve` |
| Any open status | Escalate (reason; once, never cleared) | unchanged | Management Edit or assigned investigator Edit |
| Closed, Rejected, not on legal hold | Archive | unchanged, archive date set | Management Delete |

- Every step appends to `grv.StatusHistory`. Errors: malformed `rowVersion` `400`, outside the caller's scope `404`,
  wrong actor `403`, wrong status or stale `rowVersion` `409`. One `rowVersion` guards the case.
- Overdue = open and the target date before today (business time zone), computed when read. The target date keeps
  running while the case awaits the employee. There is no employee withdraw in v1: only a draft can be deleted.
- The submitter's timeline shows every status change except the proposal loop (propose, send back); assign, unassign,
  escalate and archive stay internal.
- Messages: the submitter and the case team may write on any submitted case that is not Closed or archived (Rejected and
  Resolved included). While the case awaits the employee, the submitter's message is the response.
- Submit validates the draft before a case number is taken from the sequence. Acknowledging as Critical escalates the
  case once; it is never escalated twice.
- **Reopen ends every active assignment** (hidden "Case reopened" unassign entries, not on the submitter's timeline);
  HR assigns a Lead again, and may choose the same person.

**Submission form.** A draft needs an active category and a subject; submitting also needs a subject of 5 to 200
characters, a description of 20 to 4000, the declaration, and asks for confirmation. Incident date not after today;
location and involved person at most 200 characters; involved department optional. At most **5 open drafts** per
employee. **Named or anonymous is fixed when the draft is first saved** (`PUT` with another `isAnonymous` is a `400`: start
a new draft); a category change must still allow the draft's anonymity. Submitting copies a named submitter's company,
branch, department and position; an anonymous case keeps no placement.

**Recording a grievance for an employee (HR intake).** `POST /api/grievances/cases` (201, `grievance-management:Create`,
body `GrievanceIntakeRequest`, answer `GrievanceCaseDetail`; contract `GrievanceCaseDataService.recordForEmployee`).
HR records a grievance an employee raised outside the system. The result is a **Submitted, named** case (never
anonymous) whose submitter is that employee: it appears in their My Grievances (unread) and in Case Management. Case
number, submission and declaration time are set now, and the placement is copied from the employee as a named submit
does. One history row: `Submit`, Draft to Submitted, by the HR user (role `Management`), reason `Recorded by HR on
behalf of the employee (received in person | by phone | by email | by letter | another way).`, visible to the
submitter (their timeline shows "HR team"). Access log `StatusChange` with detail `intake:Draft>Submitted`.
Validation (`400` with field errors): `employeeId` a current employee ("Choose an active employee.") and not the
caller's own ("Use My Grievances to raise your own grievance."); an active category ("Choose an active category.");
subject 5 to 200 and description 20 to 4000 after trimming; incident date not after today; location and involved
person at most 200; involved department, when given, active ("Choose an active department."); `receivedVia` one of
`InPerson`, `Phone`, `Email`, `Letter`, `Other` ("Choose how the grievance was received."); `declarationAccepted` true
("Confirm that the employee gave this account and agreed that HR records it."). The form loads its categories and
departments from `GET /options` (`grievance:View` **or** `grievance-management:Create`) and its employee picker from
`GET /employees` (management Edit **or** Create). No files on the form: they are added from the case afterwards.

**Anonymity (keyed pseudonym).** An anonymous case stores `HMACSHA256(Grievance:PseudonymKey,
"apex-hrms:grievance-submitter:<employee id>")` in `grv.CaseSubmitter.SubmitterPseudonym`, never the employee id; the
key is not in the database. "My grievances" matches the caller's employee id or their pseudonym. The category
**Salary and Compensation Issues** (`COMPENSATION`) does not allow anonymity; every category has the switch. There is no
legal-disclosure route in v1.

- The case team sees `submitter: null`, the label "Anonymous", department "Not disclosed", and "Anonymous submitter" in
  history and messages, with no ids. A submitter (named or anonymous) is never stored with a user or employee id in
  history, messages, files or the access log; on a named case the case team sees the name resolved from
  `grv.CaseSubmitter`.
- Anonymous submitters get no notification (they see an unread badge from `LastSubmitterUpdateAtUtc` against
  `SubmitterSeenAtUtc`, which lives in `grv.CaseSubmitter` so reading never changes the case row).
- Files uploaded by an anonymous submitter lose their identifying metadata before storage: DOCX/XLSX document
  properties, JPEG EXIF, XMP, IPTC and comments, PNG text, EXIF and time chunks. A file that cannot be followed is
  refused. **PDFs are stored as sent**; the form warns about this.
- Assigning the subject, or the submitter of a **named** case, as investigator is a `400` on `investigatorEmployeeId`
  with one generic conflict-of-interest message (logged as AccessRefused). The submitter of an anonymous case is never
  checked (a refusal would reveal them); they simply get `404` when opening it.
- **Residual risks** (stated in the form and the in-app guide): timing of submissions and replies (and the database-wide
  `RowVersion` counter), writing style, PDF metadata, file names, the facts in a small department, count differencing
  (totals are the same for everyone, but lists, the case report and recent activity leave out the caller's subject
  cases), database-level linkability through parameter-capturing traces, and loss or change of the key (anonymous cases
  are then cut off from their submitters for good).

**Who sees what in a case.** Management: everything. Investigator: no corrective actions, no Restricted files.
Reviewer-only caller: Finding and Recommendation notes, Shared files and the proposal only; no messages, corrective
actions, assignment or escalation reasons, or information-request text in the history. The submitter: their timeline,
messages, Shared files, the rejection reason, and the resolution summary only when Resolved or Closed and the reviewer
shared it; never notes, corrective actions, priority, assignments, case-team files or a case-team name ("HR team").

**Files.** PDF, PNG, JPEG, DOCX and XLSX, decided by content; at most **10 MB** per file; **10 submitter files** and
**20 case-team files** per case (separate counts); **100 MB per submitter account per day**
(`Grievance:SubmitterDailyUploadBytes`); **10 uploads per minute per account** (`Grievance:FileUploadsPerMinute`).
Visibility: Shared (submitter and case team), CaseTeam (management and assigned investigators), Restricted (management
only). Submitter uploads are always Shared and never change the case row. The submitter adds files to a draft or an
open case; the case team to an open case only (never Resolved, Rejected, Closed or archived). **There is no virus or
malware scan.**

**Access log and notifications.**

- `grv.AuditLog` (append-only) records case-team views, history reads, downloads, uploads, exports, assignment changes,
  status changes, triage, notes, messages, corrective actions and refused access, with codes and ids only. A submitter's
  own reads and anything done to a draft are never logged; a submitter's actions on a submitted case are logged without
  a user id. The case log and the module-wide log need `grievance-management:View` **and** the HR Administrator role.
- No `app.ActivityLog` entries: the company-wide activity feed never mentions grievances.
- Notifications are in-app only (no email): title `Grievance GRV-…`, text `Grievance GRV-…: <event>.`, never a category,
  subject or name. The mock now sends exactly the same ones.

  | Event | Recipients |
  | --- | --- |
  | "a new grievance was submitted" | Holders of `grievance-management:View` |
  | "a grievance was recorded by HR" | Holders of `grievance-management:View`, except the HR user who recorded it |
  | "HR recorded a grievance for you" | The employee HR recorded it for (links `/grievances/my/{id}`) |
  | "the employee responded" | Holders of `grievance-management:View`, and the case's active investigators (link `/grievances/investigations/{id}`) |
  | "you were assigned to investigate it" | The assigned investigator |
  | "a resolution was proposed"; "it was escalated as urgent" (immediate escalations only) | Holders of `grievance-review:Approve` |
  | "there is an update on your grievance" | A **named** submitter, on any history step they can see, an HR message or a Shared file |

  Never the anonymous submitter (unread badge only), never the subject employee; an event caused by the case team
  leaves out the actor.

**Retention.** Close and reject set `RetentionUntil` to that date plus `Grievance:RetentionYears` (default 7); reopening
clears it. Archive is a soft delete of a Closed or Rejected case; an archived case is read-only. A case on legal hold
(set in triage) cannot be archived. There is no purge in v1.

**Triage** (management Edit): category (an anonymous case only to a category that allows anonymity), priority, target
date (not before today), subject employee (a picker of at most 20 current employees; an employee who is an active
assignee of the case is refused), involved department and legal hold (required in the request). On a case that is no
longer open only the legal hold may change. Raising an open case to Critical escalates it.

**Dashboards and reports.** Aggregates count every non-draft case, archived included, and are **the same for every
viewer** (they do not leave out the caller's own or subject cases). Resolution rate = (Resolved + Closed) ÷ (counted −
Rejected) × 100, 2 decimals; average resolution days over cases now Resolved or Closed, 1 decimal. Departments are those
copied at submission; anonymous is "Not disclosed"; departments with fewer than 3 cases merge into "Other /
suppressed". Monthly trend: the last 12 months. Recent activity: the 10 latest steps, case numbers only, without the
caller's subject cases. The case report filters submission dates, department, category, status, priority,
investigator, days to resolve and overdue; its rows leave out the caller's subject cases. The summary (management or
review View) has no department filter. Excel: export-only `grievance-cases` (see
[EXCEL-IMPORT-EXPORT.md](EXCEL-IMPORT-EXPORT.md)); PDF is the browser's print of the loaded report page.

**Demo build (mock).** Sample data only, and nothing is security:

- The tiers come from the simulated session's role grants in Settings > Permissions; nothing outside the grievance
  mocks honours them. The pseudonym is a fixed-key FNV-1a hash anyone can recompute from the bundle.
- Files are held in memory and lost on reload; no metadata removal, no upload rate limit (the daily quota is simulated
  in memory), no virus scan.
- Otherwise the mock repeats the API's checks in the API's order, the redaction, the state machine (same golden file),
  the access log and the same per-recipient notifications. See [MOCK-DATA-AND-SERVICES.md](MOCK-DATA-AND-SERVICES.md).

## Settings (UI simulation)

- **General settings**: organization name, contact email, time zone, date format, currency, fiscal
  year start, notification toggles. Saved in memory; other screens do not read them.
- **Users**: linked to one employee (name/email copied from the employee); statuses Active,
  Inactive, Locked ("Unlock account"); you cannot delete or demote your own account.
- **Roles**: built-in roles cannot be renamed or deleted; roles with users cannot be deleted; a role
  with active users cannot be deactivated.
- **Permissions**: module × action matrix per role (granting anything grants View; removing View
  clears the module). HR Administrator is read-only. **Nothing is enforced anywhere.**

## Data Exchange (Excel import and export)

Import wizard (template → upload → columns → review → confirm → result), import history with problem
details and a downloadable validation report, and an Export dialog on lists. **Implemented for**
employees, companies, branches, departments, job grades, positions, leave balances, shift assignments
and salary assignments, in the demo build and in the API. The KPI reports offer three export-only modules (`kpi-scorecards`,
`kpi-ranking`, `kpi-target-vs-actual`), and the grievance report one (`grievance-cases`), in the demo build too. See [EXCEL-IMPORT-EXPORT.md](EXCEL-IMPORT-EXPORT.md) for the
rules (strategies, all-or-nothing vs partial, formula safety, limits).
