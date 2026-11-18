import { Page } from '@playwright/test';
import { dialogFocused, expect, signedOutTest, test } from './fixtures';
import {
  DANIEL,
  SU_MYAT,
  YE_YINT,
  action,
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
 * Grievance confidentiality in the case-team screens (GRV-E2E-30 to GRV-E2E-39). The demo enforces
 * nothing: these tests check what the mock SIMULATES (the API's redaction and tiers), never real
 * access control. People: Su Myat Noe (HR Administrator, the fixture's session), Ye Yint Naing (HR
 * Officer, investigator of the samples) and Daniel Carter (Employee, owner of the anonymous case
 * #4 "Overtime approvals withheld" and the named case #10). Sample case #5 names Ye Yint Naing as
 * its subject employee; #11 has a pending resolution proposed by him. Fictional data only.
 */

test.skip(({ isMobile }) => isMobile, 'Case-team flows run on desktop');

const ANON_SUBJECT = 'Fictional comments about accents';

const notNamed = async (page: Page) => {
  const main = page.getByRole('main');
  await expect(main).not.toContainText('Daniel Carter');
  await expect(main).not.toContainText('EMP005');
};

signedOutTest(
  'GRV-E2E-30/31: an anonymous grievance shows "Anonymous" in the list, the detail and the history',
  async ({ page }) => {
    signedOutTest.setTimeout(120_000);
    // Daniel submits anonymously; the confirmation says HR will not see his name.
    await signIn(page, DANIEL);
    await openGrievances(page, 'My Grievances');
    await page.getByRole('link', { name: 'New grievance' }).click();
    await choose(page, page, 'Category', 'Discrimination');
    await page.getByRole('switch', { name: 'Report anonymously' }).click();
    await page.getByLabel('Subject', { exact: true }).fill(ANON_SUBJECT);
    await page
      .getByLabel('Description')
      .fill('Fictional: remarks about accents are made in team meetings every week.');
    await page
      .getByRole('checkbox', { name: /I confirm that what I have written is true/ })
      .check();
    await page.getByRole('button', { name: 'Submit', exact: true }).click();
    await expect(dialog(page)).toContainText('HR will receive it without your name.');
    await dialog(page).getByRole('button', { name: 'Submit' }).click();
    await expect(toast(page, /submitted\./)).toBeVisible();
    await expect(card(page, 'Your grievance')).toContainText('Anonymously');
    await expect(myStatus(page)).toHaveText('Submitted');

    // HR's list: "Anonymous", department "Not disclosed", never the name.
    await signInAs(page, SU_MYAT);
    await openGrievances(page, 'Case Management');
    await page.getByRole('searchbox', { name: /Search by case number/ }).fill(ANON_SUBJECT);
    await expect(rows(page)).toHaveCount(1);
    const row = rows(page).first();
    await expect(row).toContainText('Anonymous');
    await expect(row).toContainText('Not disclosed');
    await notNamed(page);

    // The detail: the anonymous label, no department, no name or code anywhere.
    await row.getByRole('link', { name: /^Open case / }).click();
    await expect(page.getByLabel('Case summary')).toContainText('Anonymous');
    const submitter = card(page, 'Submitter');
    await expect(submitter).toContainText('Anonymous');
    await expect(submitter).toContainText(/Department not disclosed/i);
    await expect(submitter).toContainText('The employee reported this anonymously.');
    await expect(submitter.getByText('Position')).toHaveCount(0);
    await notNamed(page);

    // The history: the submission is signed "Anonymous submitter".
    await openTab(page, 'History');
    await expect(page.getByRole('list', { name: 'Case history' })).toContainText(
      'Anonymous submitter',
    );
    await notNamed(page);

    // The seeded anonymous case of Daniel (#4) is the same.
    await openGrievances(page, 'Case Management');
    await page.getByRole('searchbox', { name: /Search by case number/ }).fill('Overtime approvals');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('Anonymous');
    await notNamed(page);
  },
);

signedOutTest(
  'GRV-E2E-32: the employee never sees investigation notes or the case-team workspace',
  async ({ page }) => {
    await signIn(page, DANIEL);
    // His named case #10 waits for his answer: he sees HR's question, never the case team's record.
    await openGrievances(page, 'My Grievances');
    await page
      .getByRole('link', { name: 'Open grievance Allowance missing from two payslips' })
      .click();
    await expect(myStatus(page)).toHaveText('Awaiting Employee Response');
    await expect(card(page, 'Messages with HR')).toContainText(
      'Please tell us which two months are concerned.',
    );
    await expect(page.getByRole('tab')).toHaveCount(0);
    await expect(page.getByText('Investigation notes')).toHaveCount(0);
    await expect(page.getByRole('main')).not.toContainText('Ye Yint Naing');
    // The timeline names HR as a team, never the investigator.
    await expect(page.getByRole('list', { name: 'Grievance timeline' })).toContainText(
      'Information requested',
    );
  },
);

signedOutTest(
  'GRV-E2E-33: an investigator only sees the cases assigned to him',
  async ({ page }) => {
    await signIn(page, YE_YINT);
    await openGrievances(page, 'Investigations');
    await expect(page.getByRole('heading', { name: 'My investigations', level: 1 })).toBeVisible();
    await expect(rows(page).first()).toBeVisible();
    // Every listed case is led by him.
    for (const row of await rows(page).all()) {
      await expect(row).toContainText('Ye Yint Naing');
    }
    // Not assigned (#3, Submitted) and the case he is the subject of (#5) are not listed.
    const search = page.getByRole('searchbox', { name: /Search by case number/ });
    for (const subject of ['On-call rota', 'test environment access']) {
      await search.fill(subject);
      await expect(page.getByText('No case is assigned to you')).toBeVisible();
      await expect(rows(page)).toHaveCount(0);
    }
    await search.fill('Headsets');
    await expect(rows(page)).toHaveCount(1);
  },
);

signedOutTest(
  'GRV-E2E-39: a case outside your scope, opened by address, says it could not be opened',
  async ({ page }) => {
    // The mock answers "not found" for a case outside the caller's tiers (simulation). The screen
    // should show its error state, not keep loading.
    const errors: string[] = [];
    page.on('console', (m) => {
      if (m.type() === 'error' && !/fonts\.(googleapis|gstatic)|net::ERR_FAILED/.test(m.text())) {
        errors.push(m.text().split('\n')[0]);
      }
    });
    await signIn(page, YE_YINT);
    // #3 is not assigned to him; he is the subject employee of #5.
    for (const id of [3, 5]) {
      await page.goto(`/grievances/investigations/${id}`);
      await expect(page.getByText('This case could not be opened')).toBeVisible();
      await expect(page.getByText(`Grievance case #${id} was not found.`)).toBeVisible();
      await expect(page.getByRole('progressbar', { name: 'Loading the case' })).toHaveCount(0);
    }

    // Daniel Carter: his own case in the case-team workspace, and someone else's grievance.
    await signInAs(page, DANIEL);
    await page.goto('/grievances/cases/10');
    await expect(page.getByText('This case could not be opened')).toBeVisible();
    await expect(page.getByRole('main')).not.toContainText('Please tell us which two months');
    await page.goto('/grievances/my/3');
    await expect(page.getByText('This grievance could not be opened')).toBeVisible();
    expect(errors).toEqual([]);
  },
);

signedOutTest(
  'GRV-E2E-34: a role without the case-team keys gets the error state, not an empty list',
  async ({ page }) => {
    // Ye Yint Naing (HR Officer) holds grievance-investigation only. A mock session has no permission
    // list, so the sidebar shows every entry; the grievance mock refuses the management list and the
    // review queue (Forbidden). The screen should say so instead of showing an empty table.
    const errors: string[] = [];
    page.on('console', (m) => {
      if (m.type() === 'error' && !/fonts\.(googleapis|gstatic)|net::ERR_FAILED/.test(m.text())) {
        errors.push(m.text().split('\n')[0]);
      }
    });
    await signIn(page, YE_YINT);
    for (const [link, heading] of [
      ['Case Management', 'Case management'],
      ['Reviews', 'Reviews'],
    ]) {
      await openGrievances(page, link);
      await expect(page.getByRole('heading', { name: heading, level: 1 })).toBeVisible();
      await expect(page.getByText('The cases could not be loaded')).toBeVisible();
      await expect(page.getByText('You do not have permission to do this.')).toBeVisible();
    }
    expect(errors).toEqual([]);
  },
);

test('GRV-E2E-35: assigning the subject employee is refused as a conflict of interest', async ({
  page,
}) => {
  // Case #5 (Acknowledged) names Ye Yint Naing as its subject employee.
  await page.goto('/grievances/cases/5');
  await expect(caseStatus(page)).toHaveText('Acknowledged');
  await expect(card(page, 'Facts')).toContainText('Ye Yint Naing');
  await action(page, 'Assign investigator').click();
  await dialogFocused(page);
  await choose(dialog(page), page, 'Investigator', /^Ye Yint Naing \(EMP009\)/);
  await dialog(page).getByRole('button', { name: 'Assign' }).click();
  // One generic message on the field: it never says whether he is the subject or the submitter.
  await expect(dialog(page).locator('mat-error')).toHaveText(
    'This person cannot be assigned to this case (conflict of interest).',
  );
  await dialog(page).getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog(page)).toHaveCount(0);
  await expect(caseStatus(page)).toHaveText('Acknowledged');
  await expect(card(page, 'Investigators')).toContainText('Nobody is assigned yet.');
});

test('GRV-E2E-36: the reviewer workspace has no messages or files tab', async ({ page }) => {
  // Case #11 has a resolution proposed by Ye Yint Naing.
  await page.goto('/grievances/reviews');
  await expect(page.getByRole('heading', { name: 'Reviews', level: 1 })).toBeVisible();
  const row = rows(page).filter({ hasText: 'Headsets not replaced' });
  await expect(row).toContainText('Resolution Proposed');
  await row.getByRole('link', { name: /^Open case / }).click();
  await expect(page).toHaveURL(/\/grievances\/reviews\/11$/);
  // Findings, the proposal (opened first: it is what the reviewer decides on) and the history.
  await expect(page.getByRole('tab')).toHaveText([
    'Findings',
    'Proposal and escalations',
    'History',
  ]);
  await expect(page.getByRole('tabpanel')).toContainText('Proposed by Ye Yint Naing');
  for (const hidden of ['Messages', 'Files', 'Corrective actions', 'Access log']) {
    await expect(page.getByRole('tab', { name: hidden, exact: true })).toHaveCount(0);
  }
  await expect(page.getByRole('textbox', { name: 'Message to the employee' })).toHaveCount(0);
  // Findings and recommendations only, read-only for the reviewer.
  await openTab(page, 'Findings');
  await expect(page.getByRole('tabpanel')).toContainText('Sample finding');
  await expect(page.getByRole('button', { name: 'Add note' })).toHaveCount(0);
  // Only the decision commands.
  await expect(action(page, 'Approve resolution')).toBeVisible();
  await expect(action(page, 'Send proposal back')).toBeVisible();
  for (const name of ['Close', 'Reject', 'Assign investigator', 'Request information']) {
    await expect(action(page, name)).toHaveCount(0);
  }
});

test('GRV-E2E-37: the proposer of a resolution cannot decide on it', async ({ page }) => {
  // Case #6 is Under Review without a Lead: management proposes, then cannot approve as reviewer.
  await page.goto('/grievances/cases/6');
  await expect(caseStatus(page)).toHaveText('Under Review');
  await action(page, 'Propose resolution').click();
  await dialogFocused(page);
  await dialog(page).getByLabel('Resolution summary').fill('Fictional: publish training criteria.');
  await dialog(page).getByRole('button', { name: 'Propose' }).click();
  await expect(toast(page, 'Propose resolution: done.')).toBeVisible();
  await expect(caseStatus(page)).toHaveText('Resolution Proposed');
  await expect(
    page.getByText(
      'You proposed this resolution, so another reviewer has to approve or send it back.',
    ),
  ).toBeVisible();

  await openGrievances(page, 'Reviews');
  await rows(page)
    .filter({ hasText: 'Training places offered unequally' })
    .getByRole('link', { name: /^Open case / })
    .click();
  await expect(page).toHaveURL(/\/grievances\/reviews\/6$/);
  await expect(
    page.getByText(
      'You proposed this resolution, so another reviewer has to approve or send it back.',
    ),
  ).toBeVisible();
  await expect(action(page, 'Approve resolution')).toHaveCount(0);
  await expect(action(page, 'Send proposal back')).toHaveCount(0);
});

test('GRV-E2E-38: an active investigator of the case cannot decide its proposal', async ({
  page,
}) => {
  // Case #11 (Resolution Proposed): Su Myat Noe joins as a Member investigator, then reviews.
  await page.goto('/grievances/cases/11');
  await expect(caseStatus(page)).toHaveText('Resolution Proposed');
  await action(page, 'Assign investigator').click();
  await dialogFocused(page);
  await expect(dialog(page).getByRole('radio', { name: 'Member' })).toBeChecked();
  await choose(dialog(page), page, 'Investigator', /^Su Myat Noe \(EMP002\)/);
  await dialog(page).getByRole('button', { name: 'Assign' }).click();
  await expect(toast(page, 'Investigator assigned.')).toBeVisible();
  await expect(card(page, 'Investigators')).toContainText('Su Myat Noe · Member');

  await openGrievances(page, 'Reviews');
  await rows(page)
    .filter({ hasText: 'Headsets not replaced' })
    .getByRole('link', { name: /^Open case / })
    .click();
  await expect(page).toHaveURL(/\/grievances\/reviews\/11$/);
  await expect(caseStatus(page)).toHaveText('Resolution Proposed');
  await expect(action(page, 'Approve resolution')).toHaveCount(0);
  await expect(action(page, 'Send proposal back')).toHaveCount(0);
});
