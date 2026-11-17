import { Locator, Page } from '@playwright/test';
import { expect, signedOutTest, test } from './fixtures';

/**
 * KPI submission and review (section 17 of docs/E2E-TEST-SCENARIOS.md, rows KPI-21 to KPI-36).
 * Mock `e2e` build: the in-memory data resets on every page load, so a flow that needs several
 * people signs out and in INSIDE the app (the demo password works for every sample account) and
 * never reloads. The browser clock is fixed (and keeps running) so the seeded periods are always
 * 2026 Q3 (Closed, HR review) and 2026 Q4 (Active, no HR review).
 *
 * People: Su Myat Noe (EMP002, HR Administrator; the fixture's session), Thura Aung (EMP001,
 * Department Manager, the reviewer of her scorecard 7), Ye Yint Naing (EMP009, HR Officer) and
 * Sai Htoo Aung (EMP056, the starter Employee role; not a sample account, so the test writes the
 * session with the role's permission keys, as `/api/auth/me` would give them). Every file is
 * generated here: a few fictional bytes, never a real document.
 */

const TODAY = new Date('2026-11-17T09:00:00');
const PASSWORD = 'Welcome@123';
const THURA = 'thura.aung@apexhorizon.example';
const SU_MYAT = 'sumyat.noe@apexhorizon.example';
const YE_YINT = 'yeyint.naing@apexhorizon.example';

/** The starter Employee role (settings seed): no kpi:Approve and no kpi-setup. */
const EMPLOYEE_56_SESSION = {
  id: 42,
  employeeId: 56,
  displayName: 'Sai Htoo Aung',
  email: 'saihtoo.aung@apexhorizon.example',
  jobTitle: '',
  roles: ['Employee'],
  permissions: [
    'dashboard:View',
    'attendance:View',
    'attendance:Create',
    'leave:View',
    'leave:Create',
    'kpi:View',
    'kpi:Create',
  ],
};

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
const alertDialog = (page: Page) => page.getByRole('alertdialog');
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main navigation' });
const kpi = (page: Page, code: string) => page.locator(`li[data-kpi="${code}"]`);
const status = (page: Page) => page.getByTestId('scorecard-status');
const toast = (page: Page, text: string | RegExp) =>
  page.locator('.app-toast').filter({ hasText: text });

/** A tiny PDF-looking file (the content check reads the `%PDF-` signature). */
function pdf(name: string, size: number) {
  const head = Buffer.from('%PDF-1.4\n% fictional evidence\n');
  return {
    name,
    mimeType: 'application/pdf',
    buffer: Buffer.concat([head, Buffer.alloc(Math.max(0, size - head.length), 0x20)]),
  };
}

async function openSelect(select: Locator) {
  const box = await select.boundingBox();
  if (!box) throw new Error('Select is not visible');
  await select.click({ position: { x: box.width - 22, y: box.height / 2 } });
}

async function chooseIn(scope: Locator, page: Page, name: string, option: string | RegExp) {
  await openSelect(scope.getByRole('combobox', { name }));
  await page.getByRole('option', { name: option, exact: typeof option === 'string' }).click();
  await expect(page.getByRole('listbox')).toHaveCount(0);
}

/** Opens a Performance screen through the sidebar (keeps the in-memory data). */
async function openPerformance(page: Page, link: string) {
  const group = nav(page).getByRole('button', { name: 'Performance' });
  if ((await group.getAttribute('aria-expanded')) !== 'true') {
    await group.click();
  }
  await nav(page).getByRole('link', { name: link, exact: true }).click();
}

/** Signs out and in as someone else, inside the app (no reload: the data stays). */
async function signInAs(page: Page, email: string) {
  await page.getByRole('button', { name: 'User menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login/);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/dashboard/);
}

/** Opens a scorecard from Performance › Reviews by employee code and period. */
async function openReview(page: Page, code: string, period: string) {
  await openPerformance(page, 'Reviews');
  await expect(page.getByRole('heading', { name: 'KPI reviews', level: 1 })).toBeVisible();
  await page.getByRole('link', { name: `Review the scorecard of ${code} for ${period}` }).click();
  await expect(page).toHaveURL(/\/performance\/reviews\/\d+$/);
}

async function setToggle(page: Page, code: string, label: 'Yes' | 'No') {
  await kpi(page, code).locator('mat-button-toggle', { hasText: label }).locator('button').click();
}

signedOutTest.describe('KPI employee results and evidence', () => {
  signedOutTest.skip(({ isMobile }) => isMobile, 'Write flows run on desktop');

  signedOutTest(
    'KPI-21 to KPI-26: an Employee-role user saves, is refused, attaches evidence and submits',
    async ({ page }) => {
      signedOutTest.setTimeout(90_000);
      await page.clock.install({ time: TODAY });
      await page.addInitScript((session) => {
        if (!sessionStorage.getItem('hrms.session')) {
          sessionStorage.setItem('hrms.session', JSON.stringify(session));
        }
      }, EMPLOYEE_56_SESSION);

      // The Performance group holds My KPIs only, and /performance never opens a setup screen.
      await page.goto('/performance');
      await expect(page).toHaveURL(/\/performance\/my-kpis$/);
      const group = nav(page).getByRole('button', { name: 'Performance' });
      if ((await group.getAttribute('aria-expanded')) !== 'true') await group.click();
      await expect(nav(page).getByRole('link', { name: 'My KPIs', exact: true })).toBeVisible();
      for (const hidden of ['Reviews', 'Assignments', 'Templates', 'Evaluation Periods']) {
        await expect(nav(page).getByRole('link', { name: hidden, exact: true })).toHaveCount(0);
      }

      // My KPIs: own scorecards only, newest period first.
      await expect(page.getByRole('heading', { name: 'My KPIs', level: 1 })).toBeVisible();
      await expect(rows(page)).toHaveCount(2);
      await expect(rows(page).first()).toContainText('2026 Q4');
      await expect(rows(page).nth(1)).toContainText('Rejected');
      await page.getByRole('link', { name: 'Open my scorecard for 2026 Q4' }).click();
      await expect(page.getByRole('heading', { name: 'My scorecard · 2026 Q4' })).toBeVisible();
      await expect(status(page)).toContainText('Draft');

      // KPI-24: each input fits its KPI. A rating above the scale is refused on its KPI; a
      // yes-or-no KPI only offers Yes and No.
      await expect(kpi(page, 'TRAINING-DONE').getByRole('spinbutton')).toHaveCount(0);
      await expect(kpi(page, 'TRAINING-DONE').locator('mat-button-toggle')).toHaveText([
        'Yes',
        'No',
      ]);
      await page.getByLabel('Actual for CUSTOMER-RATING').fill('6');
      await page.getByRole('button', { name: 'Save draft' }).click();
      await expect(kpi(page, 'CUSTOMER-RATING')).toContainText('Must be at most 5.');
      await expect(page.getByTestId('form-error')).toContainText(
        'Please correct the highlighted fields.',
      );
      await page.getByLabel('Actual for CUSTOMER-RATING').fill('');

      // KPI-21: a draft; the achievement shown is the server's.
      await page.getByLabel('Actual for OUTPUT-TASKS').fill('85');
      await expect(kpi(page, 'OUTPUT-TASKS').getByTestId('preview')).toContainText(
        'Preview: about 85.00%',
      );
      await page.getByLabel('Comment on OUTPUT-TASKS').fill('Fictional comment');
      await page.getByRole('button', { name: 'Save draft' }).click();
      await expect(toast(page, 'Draft saved. Nothing is submitted yet.')).toBeVisible();
      await expect(kpi(page, 'OUTPUT-TASKS').getByTestId('item-achievement')).toHaveText('85.00%');
      await expect(kpi(page, 'OUTPUT-TASKS').getByTestId('item-weighted')).toHaveText('34.00');
      await expect(status(page)).toContainText('Draft');

      // KPI-22: submit with missing results is refused on the missing KPIs; still Draft.
      await page.getByRole('button', { name: 'Submit', exact: true }).click();
      await expect(dialog(page)).toContainText('You cannot change results after submitting');
      await dialog(page).getByRole('button', { name: 'Submit' }).click();
      await expect(page.getByTestId('form-error')).toContainText(
        'Check: CUSTOMER-RATING, RESPONSE-HOURS, TRAINING-DONE.',
      );
      await expect(kpi(page, 'RESPONSE-HOURS')).toContainText('The actual result is missing.');
      await expect(kpi(page, 'OUTPUT-TASKS')).not.toContainText('The actual result is missing.');
      await expect(status(page)).toContainText('Draft');

      // KPI-25: a fictional 200 KB PDF on OUTPUT-TASKS, listed and downloadable.
      const chooser = page.waitForEvent('filechooser');
      await page.getByRole('button', { name: 'Add evidence to OUTPUT-TASKS' }).click();
      await (await chooser).setFiles(pdf('fictional-sales.pdf', 200 * 1024));
      await expect(toast(page, 'fictional-sales.pdf added to OUTPUT-TASKS.')).toBeVisible();
      const evidence = kpi(page, 'OUTPUT-TASKS').getByRole('group', {
        name: 'Evidence for OUTPUT-TASKS',
      });
      await expect(evidence).toContainText('fictional-sales.pdf');
      await expect(evidence).toContainText('200 KB');
      await expect(evidence).toContainText('(1 of 5)');
      const download = page.waitForEvent('download');
      await evidence.getByRole('button', { name: 'Download fictional-sales.pdf' }).click();
      expect((await download).suggestedFilename()).toBe('fictional-sales.pdf');

      // KPI-26: a type that is not allowed and a file over 10 MB are refused; nothing stored.
      const input = page.getByTestId('evidence-input-OUTPUT-TASKS');
      await input.setInputFiles({
        name: 'notes.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('just fictional text'),
      });
      await expect(evidence.getByRole('alert')).toContainText(
        'Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), up to 10 MB.',
      );
      await input.setInputFiles(pdf('too-big.pdf', 10 * 1024 * 1024 + 1));
      await expect(evidence.getByRole('alert')).toContainText('The file is larger than 10 MB.');
      await expect(evidence).toContainText('(1 of 5)');

      // Remove asks first; the file goes away.
      await evidence.getByRole('button', { name: 'Remove fictional-sales.pdf' }).click();
      await alertDialog(page).getByRole('button', { name: 'Remove' }).click();
      await expect(toast(page, 'fictional-sales.pdf removed.')).toBeVisible();
      await expect(evidence).toContainText('No evidence attached.');

      // KPI-23: complete the results and submit (unsaved changes are saved first).
      await page.getByLabel('Actual for CUSTOMER-RATING').fill('4');
      await page.getByLabel('Actual for RESPONSE-HOURS').fill('3');
      await setToggle(page, 'TRAINING-DONE', 'Yes');
      await page.getByRole('button', { name: 'Submit', exact: true }).click();
      await expect(dialog(page)).toContainText('Your unsaved changes are saved first.');
      await dialog(page).getByRole('button', { name: 'Submit' }).click();
      // 34.00 + 20.00 (rating 4 of 5, weight 25) + 20.00 (in range, weight 20) + 15.00 = 89.00.
      await expect(
        toast(page, 'Scorecard submitted. Overall score 89.00 (Exceeds Expectations).'),
      ).toBeVisible();
      await expect(status(page)).toContainText('Submitted');
      await expect(page.getByTestId('overall-score')).toContainText('89.00');
      await expect(page.getByRole('button', { name: 'Save draft' })).toHaveCount(0);
      await expect(page.getByLabel('Actual for OUTPUT-TASKS')).toHaveCount(0);
      await expect(page.getByRole('button', { name: /^Add evidence/ })).toHaveCount(0);
      await expect(page.getByRole('list', { name: 'Review history' })).toContainText('Submit');

      // An Employee-role user has no Reviews: the route says so.
      await page.goto('/performance/reviews');
      await expect(page).toHaveURL(/\/forbidden$/);
    },
  );
});

test.describe('KPI review and approval', () => {
  test.skip(({ isMobile }) => isMobile, 'Write flows run on desktop');
  test.beforeEach(async ({ page }) => {
    await page.clock.install({ time: TODAY });
  });

  test('KPI-28/29/30/31/33: return with a reason, resubmit, then approve and finalize', async ({
    page,
  }) => {
    test.setTimeout(120_000);
    // KPI-33: Su Myat Noe never reviews her own scorecard 7.
    await page.goto('/performance/reviews');
    await expect(page.getByRole('heading', { name: 'KPI reviews', level: 1 })).toBeVisible();
    await expect(rows(page).first()).toBeVisible();
    await expect(rows(page).filter({ hasText: 'EMP002' })).toHaveCount(0);
    await page.goto('/performance/reviews/7');
    await expect(page.getByTestId('review-note')).toContainText('your own scorecard');
    for (const name of ['Approve', 'Reject', 'Return for revision', 'Start review']) {
      await expect(page.getByRole('button', { name, exact: true })).toHaveCount(0);
    }

    // KPI-28: Thura Aung's queue lists the scorecard naming her reviewer, not EMP056's.
    await signInAs(page, THURA);
    await openPerformance(page, 'Reviews');
    const mine = rows(page).filter({ hasText: 'EMP002' });
    await expect(mine).toHaveCount(1);
    await expect(mine).toContainText('Manager review');
    await expect(rows(page).filter({ hasText: 'EMP056' })).toHaveCount(0);
    await mine.getByRole('link', { name: 'Review the scorecard of EMP002 for 2026 Q4' }).click();
    await expect(page.getByRole('heading', { name: /Review · EMP002/ })).toBeVisible();

    await page.getByRole('button', { name: 'Start review' }).click();
    await expect(toast(page, 'Review of EMP002 started.')).toBeVisible();
    await expect(status(page)).toContainText('Under Review');

    // KPI-30: return and reject need a reason.
    await page.getByRole('button', { name: 'Return for revision' }).click();
    await dialog(page).getByRole('button', { name: 'Return', exact: true }).click();
    await expect(dialog(page)).toContainText('A reason is required.');
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();
    await page.getByRole('button', { name: 'Reject', exact: true }).click();
    await alertDialog(page).getByLabel('Reason').fill('   ');
    await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(alertDialog(page)).toContainText('A reason is required.');
    await alertDialog(page).getByRole('button', { name: 'Cancel' }).click();
    await expect(status(page)).toContainText('Under Review');

    // KPI-31: return with a reason.
    await page.getByRole('button', { name: 'Return for revision' }).click();
    await dialog(page).getByLabel('Reason').fill('Fictional reason');
    await dialog(page).getByRole('button', { name: 'Return', exact: true }).click();
    await expect(toast(page, 'Scorecard of EMP002 returned for revision.')).toBeVisible();
    await expect(status(page)).toContainText('Returned');

    // The employee sees the reason, changes a result and resubmits.
    await signInAs(page, SU_MYAT);
    await openPerformance(page, 'My KPIs');
    await page.getByRole('link', { name: 'Open my scorecard for 2026 Q4' }).click();
    await expect(page.getByTestId('return-reason')).toHaveText('Fictional reason');
    await expect(
      page.getByRole('heading', { name: /Returned for revision by Thura Aung/ }),
    ).toBeVisible();
    await page.getByLabel('Actual for ATTENDANCE-RATE').fill('99');
    await page.getByRole('button', { name: 'Submit', exact: true }).click();
    await dialog(page).getByRole('button', { name: 'Submit' }).click();
    await expect(toast(page, /Scorecard resubmitted\. Overall score/)).toBeVisible();
    await expect(status(page)).toContainText('Resubmitted');

    // KPI-29: Thura rates, then approves; the period has no HR review, so it is finalized.
    await signInAs(page, THURA);
    await openReview(page, 'EMP002', '2026 Q4');
    await expect(kpi(page, 'ATTENDANCE-RATE').getByTestId('item-actual')).toContainText('99');
    await page.getByLabel('Rating for ATTENDANCE-RATE').fill('4');
    await page.getByLabel('Overall rating').fill('4');
    await page.getByLabel('Overall feedback').fill('Fictional feedback');
    await page.getByRole('button', { name: 'Approve', exact: true }).click();
    await expect(dialog(page)).toContainText('approving finalizes the scorecard');
    await dialog(page).getByLabel('Comment (optional)').fill('Fictional approval note');
    await dialog(page).getByRole('button', { name: 'Approve and finalize' }).click();
    await expect(toast(page, 'Scorecard of EMP002 approved and finalized.')).toBeVisible();
    await expect(status(page)).toContainText('Finalized');
    const history = page.getByRole('list', { name: 'Review history' });
    for (const step of ['Start Review', 'Return', 'Resubmit', 'Approve']) {
      await expect(history).toContainText(step);
    }
    await expect(history).toContainText('Fictional reason');
    await expect(page.getByRole('button', { name: 'Approve', exact: true })).toHaveCount(0);

    // The employee sees the feedback and the final score, read-only.
    await signInAs(page, SU_MYAT);
    await openPerformance(page, 'My KPIs');
    await expect(rows(page).first()).toContainText('Finalized');
    await page.getByRole('link', { name: 'Open my scorecard for 2026 Q4' }).click();
    await expect(page.getByRole('region', { name: 'Review feedback' })).toContainText(
      'Fictional feedback',
    );
    await expect(page.getByTestId('overall-score')).toContainText(/\d+\.\d\d/);
    await expect(page.getByRole('button', { name: 'Save draft' })).toHaveCount(0);
  });

  test('KPI-35: with an HR review the approver cannot finalize; another HR person does', async ({
    page,
  }) => {
    test.setTimeout(150_000);
    // A period of our own that asks for an HR final review, with a plan for Su Myat Noe.
    await page.goto('/performance/periods');
    await page.getByRole('button', { name: 'Add evaluation period' }).click();
    const form = dialog(page);
    await chooseIn(form, page, 'Company', 'APEX Technologies');
    await form.getByLabel('Period name').fill('2027 Q1');
    await form.getByLabel('Start date').fill('2027-01-01');
    await form.getByLabel('End date').fill('2027-03-31');
    await form.getByLabel('Requires HR final review').click();
    await expect(form.getByLabel('Requires HR final review')).toBeChecked();
    await form.getByRole('button', { name: 'Create evaluation period' }).click();
    await expect(toast(page, 'Evaluation period "2027 Q1" created successfully.')).toBeVisible();
    await rows(page)
      .filter({ hasText: '2027 Q1' })
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await page.getByRole('menuitem', { name: 'Activate', exact: true }).click();
    await dialog(page).getByRole('button', { name: 'Activate' }).click();
    await expect(toast(page, 'Period "2027 Q1" activated.')).toBeVisible();

    await openPerformance(page, 'Assignments');
    await page.getByRole('button', { name: 'Assign KPIs' }).click();
    await chooseIn(dialog(page), page, 'Evaluation period', '2027 Q1 (Active)');
    await dialog(page).getByLabel('Employee', { exact: true }).fill('EMP002');
    await page.getByRole('option', { name: /^EMP002 ·/ }).click();
    for (const code of ['ATTENDANCE-RATE', 'TRAINING-DONE']) {
      await chooseIn(dialog(page), page, 'Add a KPI', new RegExp(`^${code} ·`));
      await dialog(page).getByLabel(`Weight for ${code}`).fill('50');
    }
    await dialog(page).getByRole('button', { name: 'Assign KPIs' }).click();
    await expect(toast(page, 'Scorecard created for EMP002 (2027 Q1) with 2 KPIs.')).toBeVisible();
    await page.getByRole('searchbox', { name: /Search by employee code or name/ }).fill('EMP002');
    await page.getByRole('link', { name: 'Open the plan of EMP002 for 2027 Q1' }).click();
    // The plan names the employee's manager once it has been read: Thura Aung reviews.
    await expect(page.getByTestId('plan-reviewer')).toContainText('Thura Aung');
    await page.getByRole('button', { name: 'Finalize plan' }).click();
    await expect(dialog(page)).toContainText('The reviewer will be Thura Aung');
    await dialog(page).getByRole('button', { name: 'Finalize plan' }).click();
    await expect(toast(page, 'Plan finalized for EMP002.')).toBeVisible();

    // Su Myat Noe enters her results and submits.
    await openPerformance(page, 'My KPIs');
    await page.getByRole('link', { name: 'Open my scorecard for 2027 Q1' }).click();
    await page.getByLabel('Actual for ATTENDANCE-RATE').fill('96');
    await setToggle(page, 'TRAINING-DONE', 'Yes');
    await page.getByRole('button', { name: 'Submit', exact: true }).click();
    await dialog(page).getByRole('button', { name: 'Submit' }).click();
    await expect(toast(page, /Scorecard submitted\./)).toBeVisible();

    // Thura approves: Approved (not Finalized), and she is offered no final review.
    await signInAs(page, THURA);
    await openReview(page, 'EMP002', '2027 Q1');
    await page.getByRole('button', { name: 'Approve', exact: true }).click();
    await expect(dialog(page)).toContainText('goes to HR for the final review');
    await dialog(page).getByRole('button', { name: 'Approve and send to HR' }).click();
    await expect(
      toast(page, 'Scorecard of EMP002 approved. It now waits for the HR final review.'),
    ).toBeVisible();
    await expect(status(page)).toContainText('Approved');
    await expect(page.getByRole('button', { name: 'Finalize', exact: true })).toHaveCount(0);
    await expect(page.getByTestId('review-note')).toContainText('another HR person');

    // Ye Yint Naing (HR Officer) gives the final review.
    await signInAs(page, YE_YINT);
    await openPerformance(page, 'Reviews');
    const waiting = rows(page).filter({ hasText: '2027 Q1' }).filter({ hasText: 'EMP002' });
    await expect(waiting).toContainText('HR final review');
    await waiting.getByRole('link', { name: 'Review the scorecard of EMP002 for 2027 Q1' }).click();
    await page.getByRole('button', { name: 'Finalize', exact: true }).click();
    await dialog(page).getByLabel('HR comment (optional)').fill('Fictional HR comment');
    await dialog(page).getByRole('button', { name: 'Finalize', exact: true }).click();
    await expect(toast(page, 'Scorecard of EMP002 finalized.')).toBeVisible();
    await expect(status(page)).toContainText('Finalized');
    await expect(page.getByText('HR comment: Fictional HR comment')).toBeVisible();
  });

  test('KPI-32/36: reject is final; a Draft or Finalized scorecard offers no review step', async ({
    page,
  }) => {
    // Su Myat Noe reviews EMP006's scorecard (she is the reviewer): reject with a reason.
    await page.goto('/performance/reviews/4');
    await expect(status(page)).toContainText('Under Review');
    await page.getByRole('button', { name: 'Reject', exact: true }).click();
    await expect(alertDialog(page)).toContainText('Rejecting is final');
    await alertDialog(page).getByLabel('Reason').fill('Fictional reason');
    await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(toast(page, 'Scorecard of EMP006 rejected.')).toBeVisible();
    await expect(status(page)).toContainText('Rejected');
    await expect(page.getByTestId('review-note')).toContainText('nothing to review');
    for (const name of ['Approve', 'Reject', 'Return for revision', 'Start review']) {
      await expect(page.getByRole('button', { name, exact: true })).toHaveCount(0);
    }

    // KPI-36: nothing to approve on a Draft or a Finalized scorecard.
    await page.goto('/performance/reviews/6');
    await expect(status(page)).toContainText('Draft');
    await expect(page.getByRole('button', { name: 'Approve', exact: true })).toHaveCount(0);
    await page.goto('/performance/reviews/8');
    await expect(status(page)).toContainText('Finalized');
    await expect(page.getByTestId('review-note')).toContainText('nothing to review');
  });
});

test.describe('KPI employee and reviewer screens on a phone', () => {
  test.skip(({ isMobile }) => !isMobile, 'Phone layout only');
  test.use({ viewport: { width: 390, height: 844 } });

  test('My scorecard and a review fit 390 px without sideways scrolling', async ({ page }) => {
    await page.clock.install({ time: TODAY });
    const noSideways = async () =>
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth),
      ).toBeLessThanOrEqual(1);

    await page.goto('/performance/my-kpis');
    await expect(rows(page).first()).toBeVisible();
    await noSideways();
    await page.goto('/performance/my-kpis/7');
    await expect(status(page)).toContainText('Submitted');
    await noSideways();
    await page.goto('/performance/reviews/4');
    await expect(page.getByLabel('Overall rating')).toBeVisible();
    await noSideways();
    await page.getByRole('button', { name: 'Return for revision' }).click();
    await expect(dialog(page).getByLabel('Reason')).toBeVisible();
    await noSideways();
  });
});
