import { Page } from '@playwright/test';
import { dialogFocused, expect, signedOutTest as test } from './fixtures';
import {
  CASE_NO,
  DANIEL,
  SU_MYAT,
  YE_YINT,
  action,
  alertDialog,
  card,
  caseStatus,
  choose,
  dialog,
  myStatus,
  openGrievances,
  openTab,
  rows,
  signIn,
  signInAs,
  toast,
} from './grievance-helpers';

/**
 * The grievance lifecycle across roles (GRV-E2E-10 to GRV-E2E-22): Daniel Carter (Employee) submits;
 * Su Myat Noe (HR Administrator) acknowledges and assigns Ye Yint Naing (HR Officer) as Lead; he
 * investigates, records a note and asks for information; Daniel answers; Ye Yint proposes a
 * resolution; Su Myat Noe, who neither proposed nor investigates, approves it as reviewer and shares
 * it; she closes the case; Daniel reads the resolution; she reopens it with a reason. Mock `e2e`
 * build: one page load, people switch by signing out and in inside the app. Fictional texts only.
 */

test.skip(({ isMobile }) => isMobile, 'Write flows run on desktop');

/** An ISO date `days` from today (the browser and the test share the machine clock). */
function isoInDays(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** The same date as the date fields display it (dd/MM/yyyy). */
const shown = (iso: string) => iso.split('-').reverse().join('/');

const SUBJECT = 'Fictional meeting room heating';
const NOTE = 'Fictional note: facilities confirmed the heater was switched off.';
const QUESTION = 'Fictional question: which meeting rooms are affected?';
const ANSWER = 'Fictional answer: rooms 3 and 4 on the second floor.';
const SUMMARY = 'Fictional resolution: the heaters are repaired and checked weekly.';
const REOPEN_REASON = 'Fictional reason: the heater failed again.';

const history = (page: Page) => page.getByRole('list', { name: 'Case history' });
const timeline = (page: Page) => page.getByRole('list', { name: 'Grievance timeline' });

/** Opens a case from a case-team list by its number (search, then the row link). */
async function openCase(page: Page, link: string, heading: string, caseNo: string) {
  await openGrievances(page, link);
  await expect(page.getByRole('heading', { name: heading, level: 1 })).toBeVisible();
  await page.getByRole('searchbox', { name: /Search by case number/ }).fill(caseNo);
  await expect(rows(page)).toHaveCount(1);
  await page.getByRole('link', { name: `Open case ${caseNo}` }).click();
  await expect(page.getByRole('heading', { name: caseNo, level: 1 })).toBeVisible();
}

/** Opens Daniel's grievance from My Grievances. */
async function openMine(page: Page) {
  await openGrievances(page, 'My Grievances');
  await page.getByRole('link', { name: `Open grievance ${SUBJECT}` }).click();
  await expect(page.getByRole('heading', { name: SUBJECT, level: 1 })).toBeVisible();
}

test('GRV-E2E-10 to 22: submit, acknowledge, assign, investigate, request info, respond, propose, approve, close, reopen', async ({
  page,
}) => {
  test.setTimeout(240_000);

  // ---- Daniel submits a named grievance ----------------------------------------------------
  await signIn(page, DANIEL);
  await openGrievances(page, 'My Grievances');
  await page.getByRole('link', { name: 'New grievance' }).click();
  await choose(page, page, 'Category', 'Working Conditions');
  await page.getByLabel('Subject', { exact: true }).fill(SUBJECT);
  await page
    .getByLabel('Description')
    .fill('Fictional: the heating in the meeting rooms has not worked for a week.');
  await page.getByRole('checkbox', { name: /I confirm that what I have written is true/ }).check();
  await page.getByRole('button', { name: 'Submit', exact: true }).click();
  await dialog(page).getByRole('button', { name: 'Submit' }).click();
  await expect(toast(page, /submitted\./)).toBeVisible();
  await expect(myStatus(page)).toHaveText('Submitted');
  const caseNo = ((await card(page, 'Your grievance').textContent()) ?? '').match(CASE_NO)?.[0];
  expect(caseNo, 'the submitted grievance has a case number').toBeTruthy();
  const no = caseNo!;

  // ---- GRV-E2E-10: HR acknowledges with a priority and a target date -------------------------
  await signInAs(page, SU_MYAT);
  await openCase(page, 'Case Management', 'Case management', no);
  await expect(caseStatus(page)).toHaveText('Submitted');
  await expect(card(page, 'Submitter')).toContainText('Daniel Carter (EMP005)');

  // GRV-E2E-11: rejecting needs a reason (required, not blank); cancelling changes nothing.
  await action(page, 'Reject').click();
  await expect(alertDialog(page).getByRole('heading', { name: `Reject ${no}?` })).toBeVisible();
  await dialogFocused(page);
  await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
  await expect(alertDialog(page)).toContainText('Reason (shown to the employee) is required.');
  await alertDialog(page).getByLabel('Reason (shown to the employee)').fill('   ');
  await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
  await expect(alertDialog(page)).toContainText('Reason (shown to the employee) cannot be blank.');
  await alertDialog(page).getByRole('button', { name: 'Cancel' }).click();
  await expect(alertDialog(page)).toHaveCount(0);
  await expect(caseStatus(page)).toHaveText('Submitted');

  await action(page, 'Acknowledge').click();
  await expect(dialog(page).getByRole('heading', { name: `Acknowledge ${no}?` })).toBeVisible();
  await dialogFocused(page);
  // The category's default target (Working Conditions: 30 days) is proposed.
  await expect(dialog(page).getByLabel('Target resolution date')).toHaveValue(shown(isoInDays(30)));
  await expect(dialog(page)).toContainText('The category’s default: 30 days from today.');
  // A target in the past is refused.
  await dialog(page).getByLabel('Target resolution date').fill(isoInDays(-1));
  await dialog(page).getByRole('button', { name: 'Acknowledge' }).click();
  await expect(dialog(page).locator('mat-error')).toContainText(
    /Target date is earlier than allowed\.|The target date cannot be in the past\./,
  );
  await choose(dialog(page), page, 'Priority', 'High');
  await dialog(page).getByLabel('Target resolution date').fill(isoInDays(14));
  await dialog(page).getByRole('button', { name: 'Acknowledge' }).click();
  await expect(dialog(page)).toHaveCount(0);
  await expect(toast(page, 'Acknowledge: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Acknowledged');
  await expect(page.getByLabel('Case summary')).toContainText('High');

  // ---- GRV-E2E-12: HR assigns Ye Yint Naing as the Lead investigator ------------------------
  await action(page, 'Assign investigator').click();
  await expect(dialog(page).getByRole('heading', { name: 'Assign an investigator' })).toBeVisible();
  await dialogFocused(page);
  // The investigator is required.
  await dialog(page).getByRole('button', { name: 'Assign' }).click();
  await expect(dialog(page)).toContainText('Investigator is required.');
  await choose(dialog(page), page, 'Investigator', /^Ye Yint Naing \(EMP009\)/);
  await expect(dialog(page).getByRole('radio', { name: 'Lead investigator' })).toBeChecked();
  await dialog(page).getByRole('button', { name: 'Assign' }).click();
  await expect(dialog(page)).toHaveCount(0);
  await expect(toast(page, 'Investigator assigned.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Assigned');
  await expect(card(page, 'Investigators')).toContainText('Ye Yint Naing · Lead');
  await expect(card(page, 'Facts')).toContainText('Ye Yint Naing');

  // ---- GRV-E2E-13: the investigator starts, records a note and asks for information ---------
  await signInAs(page, YE_YINT);
  await openCase(page, 'Investigations', 'My investigations', no);
  await expect(caseStatus(page)).toHaveText('Assigned');
  // No management command in the investigator's workspace.
  for (const name of ['Acknowledge', 'Assign investigator', 'Close', 'Reject']) {
    await expect(action(page, name)).toHaveCount(0);
  }
  await action(page, 'Start investigation').click();
  await dialog(page).getByRole('button', { name: 'Start investigation' }).click();
  await expect(toast(page, 'Start investigation: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Under Investigation');

  await openTab(page, 'Investigation notes');
  const notes = page.getByRole('tabpanel');
  await notes.getByRole('button', { name: 'Add note' }).click();
  await expect(notes).toContainText('Text is required.');
  await notes.getByLabel('Text', { exact: true }).fill(NOTE);
  await notes.getByRole('button', { name: 'Add note' }).click();
  await expect(toast(page, 'Added to the investigation record.')).toBeVisible();
  await expect(notes.locator('li.note')).toContainText([NOTE]);

  await action(page, 'Request information').click();
  await dialogFocused(page);
  await dialog(page).getByRole('button', { name: 'Send request' }).click();
  await expect(dialog(page)).toContainText('Question for the employee is required.');
  await dialog(page).getByLabel('Question for the employee').fill(QUESTION);
  await dialog(page).getByRole('button', { name: 'Send request' }).click();
  await expect(toast(page, 'Request information: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Awaiting Employee Response');

  // ---- GRV-E2E-14: the employee is asked, answers, and never sees the investigation note ----
  await signInAs(page, DANIEL);
  await openMine(page);
  await expect(myStatus(page)).toHaveText('Awaiting Employee Response');
  const messages = card(page, 'Messages with HR');
  await expect(messages).toContainText(QUESTION);
  await expect(messages).toContainText('HR has asked you for more information.');
  await expect(page.locator('body')).not.toContainText(NOTE);
  const respond = messages.getByRole('button', { name: "Respond to HR's request" });
  await expect(respond).toBeDisabled();
  await messages.getByLabel("Your answer to HR's request").fill(ANSWER);
  await respond.click();
  await expect(toast(page, 'Your answer was sent to HR.')).toBeVisible();
  await expect(myStatus(page)).toHaveText('Under Investigation');
  await expect(messages).toContainText(ANSWER);
  await expect(messages.getByRole('button', { name: "Respond to HR's request" })).toHaveCount(0);
  await expect(timeline(page)).toContainText('Employee responded');

  // ---- GRV-E2E-15: the Lead proposes a resolution; he cannot decide on it -------------------
  await signInAs(page, YE_YINT);
  await openCase(page, 'Investigations', 'My investigations', no);
  await expect(caseStatus(page)).toHaveText('Under Investigation');
  await expect(page.getByRole('tabpanel')).toContainText(ANSWER);
  await action(page, 'Propose resolution').click();
  await dialogFocused(page);
  await dialog(page).getByLabel('Resolution summary').fill(SUMMARY);
  await dialog(page).getByRole('button', { name: 'Propose' }).click();
  await expect(toast(page, 'Propose resolution: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Resolution Proposed');
  await expect(action(page, 'Approve resolution')).toHaveCount(0);
  await expect(action(page, 'Send proposal back')).toHaveCount(0);

  // ---- GRV-E2E-16/17: the reviewer (not the proposer, not an investigator) decides ----------
  await signInAs(page, SU_MYAT);
  await openGrievances(page, 'Reviews');
  await expect(page.getByRole('heading', { name: 'Reviews', level: 1 })).toBeVisible();
  const queued = rows(page).filter({ hasText: no });
  await expect(queued).toHaveCount(1);
  await expect(queued).toContainText('Resolution Proposed');
  await queued.getByRole('link', { name: `Open case ${no}` }).click();
  await expect(page).toHaveURL(/\/grievances\/reviews\/\d+$/);
  await expect(page.getByRole('tab', { name: 'Messages' })).toHaveCount(0);
  await openTab(page, 'Proposal and escalations');
  await expect(page.getByRole('tabpanel')).toContainText(SUMMARY);
  await expect(page.getByRole('tabpanel')).toContainText('Proposed by Ye Yint Naing');

  // Sending the proposal back needs a reason.
  await action(page, 'Send proposal back').click();
  await dialogFocused(page);
  await dialog(page).getByRole('button', { name: 'Send back' }).click();
  await expect(dialog(page)).toContainText('What needs to change is required.');
  await dialog(page).getByRole('button', { name: 'Cancel' }).click();
  await expect(caseStatus(page)).toHaveText('Resolution Proposed');

  await action(page, 'Approve resolution').click();
  const share = dialog(page).getByRole('switch', {
    name: 'Share the resolution summary with the employee',
  });
  await expect(share).toBeChecked();
  await dialog(page).getByRole('button', { name: 'Approve' }).click();
  await expect(toast(page, 'Approve resolution: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Resolved');

  // ---- GRV-E2E-18: HR closes; the history shows every step ---------------------------------
  await openCase(page, 'Case Management', 'Case management', no);
  await expect(page.getByText('Shared with the employee.')).toBeVisible();
  await action(page, 'Close').click();
  await expect(dialog(page)).toContainText('Closing is final');
  await dialog(page).getByRole('button', { name: 'Close case' }).click();
  await expect(toast(page, 'Close: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Closed');
  await openTab(page, 'History');
  for (const step of [
    'Submitted',
    'Acknowledged',
    'Investigator assigned',
    'Investigation started',
    'Information requested',
    'Employee responded',
    'Resolution proposed',
    'Resolution approved',
    'Closed',
  ]) {
    await expect(history(page)).toContainText(step);
  }
  await expect(history(page)).toContainText('Daniel Carter');
  await expect(history(page)).toContainText(QUESTION);

  // ---- GRV-E2E-19/20: the employee sees the resolution; internal steps stay internal --------
  await signInAs(page, DANIEL);
  await openMine(page);
  await expect(myStatus(page)).toHaveText('Closed');
  await expect(card(page, 'Resolution')).toContainText(SUMMARY);
  await expect(timeline(page)).toContainText('Resolution approved');
  await expect(timeline(page)).not.toContainText('Resolution proposed');
  await expect(timeline(page)).not.toContainText('Ye Yint Naing');
  await expect(card(page, 'Messages with HR').getByRole('textbox')).toHaveCount(0);

  // ---- GRV-E2E-21/22: HR reopens with a reason (required) -----------------------------------
  await signInAs(page, SU_MYAT);
  await openCase(page, 'Case Management', 'Case management', no);
  await action(page, 'Reopen').click();
  await dialogFocused(page);
  await dialog(page).getByRole('button', { name: 'Reopen' }).click();
  await expect(dialog(page)).toContainText('Reason (shown to the employee) is required.');
  await dialog(page).getByLabel('Reason (shown to the employee)').fill(REOPEN_REASON);
  await dialog(page).getByRole('button', { name: 'Reopen' }).click();
  await expect(toast(page, 'Reopen: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Reopened');
  await openTab(page, 'History');
  await expect(history(page)).toContainText('Reopened');
  await expect(history(page)).toContainText(REOPEN_REASON);

  await signInAs(page, DANIEL);
  await openMine(page);
  await expect(myStatus(page)).toHaveText('Reopened');
  await expect(timeline(page)).toContainText(REOPEN_REASON);
});
