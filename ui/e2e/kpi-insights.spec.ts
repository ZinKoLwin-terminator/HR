import { Locator, Page } from '@playwright/test';
import { closeFilters, openFilters } from './filters';
import {
  dialogFocused,
  expect,
  pickEmployeeOption,
  searchEmployeesInApp,
  signedOutTest,
  test,
} from './fixtures';

/**
 * KPI dashboards and reports (section 17 of docs/E2E-TEST-SCENARIOS.md, rows KPI-38 to KPI-44).
 * Mock `e2e` build: sample data, reset on every page load. The browser clock is fixed (and keeps
 * running) so the seeded periods are 2026 Q3 (Closed, HR review) and 2026 Q4 (Active).
 *
 * Sample scorecards: 2026 Q4 holds EMP046 Submitted, EMP017 Draft, EMP019 Planning, EMP006 Under
 * Review, EMP014 Returned, EMP056 Draft and EMP002 Submitted (29 KPIs); 2026 Q3 holds EMP046, EMP017,
 * EMP014 and EMP006 Finalized and EMP056 Rejected. Daniel Carter (EMP005) manages EMP046, EMP017,
 * EMP014 and EMP019. People without a sample account get a session written with their role's
 * permission keys, as `/api/auth/me` would give them. Every name and figure is fictional.
 */

const TODAY = new Date('2026-11-17T09:00:00');

const EMPLOYEE_KEYS = [
  'dashboard:View',
  'attendance:View',
  'attendance:Create',
  'leave:View',
  'leave:Create',
  'kpi:View',
  'kpi:Create',
];

const session = (
  id: number,
  employeeId: number,
  displayName: string,
  roles: string[],
  permissions: string[],
) => ({
  id,
  employeeId,
  displayName,
  email: `${displayName.toLowerCase().replace(/\s+/g, '.')}@apexhorizon.example`,
  jobTitle: '',
  roles,
  permissions,
});

/** Daniel Carter, a Department Manager: kpi:Approve without employees:Edit (no HR tier). */
const MANAGER_SESSION = session(
  11,
  5,
  'Daniel Carter',
  ['Department Manager'],
  [
    'dashboard:View',
    'employees:View',
    'organization:View',
    'kpi:View',
    'kpi:Create',
    'kpi:Approve',
  ],
);
/** Hein Thant Zin (EMP046), the starter Employee role. */
const EMPLOYEE_46_SESSION = session(41, 46, 'Hein Thant Zin', ['Employee'], EMPLOYEE_KEYS);
/** Sai Htoo Aung (EMP056), the starter Employee role; his 2026 Q4 scorecard is still a Draft. */
const EMPLOYEE_56_SESSION = session(42, 56, 'Sai Htoo Aung', ['Employee'], EMPLOYEE_KEYS);

const nav = (page: Page) => page.getByRole('navigation', { name: 'Main navigation' });
const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const stat = (page: Page, label: string) =>
  page.locator('app-stat-card').filter({ hasText: label }).locator('.stat__value');
/** A titled card (a section labelled by its heading). */
const panel = (page: Page, title: string) => page.getByRole('region', { name: title, exact: true });

async function start(page: Page, user?: object) {
  await page.clock.install({ time: TODAY });
  if (user) {
    await page.addInitScript((s) => {
      if (!sessionStorage.getItem('hrms.session')) {
        sessionStorage.setItem('hrms.session', JSON.stringify(s));
      }
    }, user);
  }
}

/** Opens a Material select by its arrow end (the label can sit over the centre). */
async function openSelect(select: Locator) {
  const box = await select.boundingBox();
  if (!box) throw new Error('Select is not visible');
  await select.click({ position: { x: box.width - 22, y: box.height / 2 } });
}

async function choose(page: Page, select: Locator, option: string | RegExp) {
  await openSelect(select);
  await page.getByRole('option', { name: option, exact: typeof option === 'string' }).click();
  await expect(page.getByRole('listbox')).toHaveCount(0);
}

const periodPicker = (page: Page) => page.getByRole('combobox', { name: 'Evaluation period' });
const reportPicker = (page: Page) => page.getByRole('combobox', { name: 'Report' });

/** Sets one filter of the report's filter popup. */
async function filter(page: Page, name: string, option: string) {
  await openFilters(page);
  await choose(
    page,
    page.getByRole('dialog', { name: 'Filters' }).getByRole('combobox', { name, exact: true }),
    option,
  );
  await closeFilters(page);
}

/** Opens a Performance screen through the sidebar (keeps the in-memory data). */
async function openPerformance(page: Page, link: string) {
  const group = nav(page).getByRole('button', { name: 'Performance' });
  if ((await group.getAttribute('aria-expanded')) !== 'true') await group.click();
  await nav(page).getByRole('link', { name: link, exact: true }).click();
}

test.describe('KPI dashboards', () => {
  test.skip(({ isMobile }) => isMobile, 'Dashboard flows run on desktop (390 px is checked below)');

  test('KPI-38: HR overview of a period, with Finalized-only averages', async ({ page }) => {
    await start(page);
    await page.goto('/performance');
    await expect(page).toHaveURL(/\/performance\/dashboard$/);
    await expect(page.getByRole('heading', { name: 'KPI dashboard', level: 1 })).toBeVisible();
    await expect(page.getByRole('tab')).toHaveText(['My performance', 'My team', 'HR overview']);
    await expect(page.getByRole('tab', { name: 'HR overview' })).toHaveAttribute(
      'aria-selected',
      'true',
    );

    // The server's default period: the Active one.
    await expect(periodPicker(page)).toContainText('2026 Q4');
    await expect(stat(page, 'Employees with KPIs')).toHaveText('7');
    await expect(stat(page, 'KPI assignments')).toHaveText('29');
    await expect(stat(page, 'Completed evaluations')).toHaveText('0');
    await expect(stat(page, 'Pending submissions')).toHaveText('3');
    await expect(stat(page, 'Pending approvals')).toHaveText('3');
    await expect(stat(page, 'Average score')).toHaveText('—'); // nothing Finalized: no average
    await expect(stat(page, 'Completion rate')).toHaveText('0.00%');

    await choose(page, periodPicker(page), '2026 Q3 (Closed)');
    await expect(stat(page, 'Completed evaluations')).toHaveText('4');
    await expect(stat(page, 'Completion rate')).toHaveText('80.00%'); // 4 of 5 past Planning
    const average = (await stat(page, 'Average score').innerText()).trim();
    expect(average).toMatch(/^\d+\.\d{2}$/);
    const top = panel(page, 'Top performers');
    await expect(top.locator('tbody tr')).toHaveCount(4);
    await expect(top).not.toContainText('EMP056'); // Rejected: never ranked
    await expect(page.getByTestId('attention-threshold')).toContainText('60.00');
    // Each chart has a text alternative (and a table view).
    await expect(
      page.locator(
        'canvas[role="img"][aria-label^="Bar chart of the average score per department"]',
      ),
    ).toHaveCount(1);

    // The same average as the period summary report (figures match the scorecards).
    await openPerformance(page, 'Reports');
    await expect(page.getByRole('heading', { name: 'KPI reports', level: 1 })).toBeVisible();
    const q3 = rows(page).filter({ hasText: '2026 Q3' });
    await expect(q3).toContainText(average);
    await expect(q3).toContainText('80.00');
  });
});

signedOutTest.describe('KPI dashboards by role', () => {
  signedOutTest.skip(({ isMobile }) => isMobile, 'Dashboard flows run on desktop');

  signedOutTest(
    'KPI-39: a manager sees his team only: pending reviews, approved, average, comparison',
    async ({ page }) => {
      await start(page, MANAGER_SESSION);
      await page.goto('/performance/dashboard');
      await expect(page.getByRole('tab')).toHaveText(['My performance', 'My team']);
      await expect(page.getByRole('tab', { name: 'My team' })).toHaveAttribute(
        'aria-selected',
        'true',
      );
      await expect(stat(page, 'Team size')).toHaveText('4');
      await expect(stat(page, 'Pending reviews')).toHaveText('1');
      await expect(stat(page, 'Approved or finalized')).toHaveText('0');
      const team = panel(page, 'Team scorecards');
      await expect(team.locator('tbody tr')).toHaveCount(4);
      await expect(team).toContainText('EMP046');
      await expect(team).not.toContainText('EMP056');
      await expect(team).not.toContainText('EMP006');

      await choose(page, periodPicker(page), '2026 Q3 (Closed)');
      await expect(stat(page, 'Team size')).toHaveText('3');
      await expect(stat(page, 'Approved or finalized')).toHaveText('3');
      await expect(stat(page, 'Team average')).toHaveText(/^\d+\.\d{2}$/);

      // The comparison chart has a text alternative and a table view with every value.
      const comparison = panel(page, 'Employee comparison');
      await expect(comparison.locator('canvas')).toHaveAttribute('aria-label', /overall scores/);
      await comparison.getByRole('radio', { name: 'Table view' }).click();
      await expect(comparison.locator('tbody tr')).toHaveCount(3);
      await expect(comparison).toContainText('EMP014');
    },
  );

  signedOutTest(
    'KPI-40: an employee sees his KPIs, score, feedback and history (no tabs)',
    async ({ page }) => {
      await start(page, EMPLOYEE_46_SESSION);
      await page.goto('/performance/dashboard');
      await expect(page.getByRole('heading', { name: 'KPI dashboard', level: 1 })).toBeVisible();
      await expect(page.getByRole('tab')).toHaveCount(0);
      await expect(periodPicker(page)).toContainText('2026 Q4');
      await expect(page.locator('app-status-badge').first()).toContainText('Submitted');
      const kpis = panel(page, 'My KPIs');
      await expect(kpis.locator('tbody tr')).toHaveCount(5);
      await expect(kpis).toContainText('ENG-REVIEW-HOURS');
      await expect(kpis.locator('thead')).toContainText('Achievement %');
      await expect(stat(page, 'Overall score')).toHaveText(/^\d+\.\d{2}$/);
      await expect(page.getByText('No feedback yet.')).toBeVisible();
      const history = panel(page, 'Earlier scorecards');
      await expect(history).toContainText('2026 Q3');
      await expect(history).toContainText('Finalized');

      await choose(page, periodPicker(page), '2026 Q3 (Closed)');
      await expect(page.getByTestId('manager-rating')).toContainText('4 of 5');
      await expect(page.getByText('Sample feedback: reliable and quick reviews.')).toBeVisible();
    },
  );

  signedOutTest(
    'KPI-40: a Draft scorecard shows the pending-submission banner',
    async ({ page }) => {
      await start(page, EMPLOYEE_56_SESSION);
      await page.goto('/performance/dashboard');
      const banner = page.getByTestId('pending-banner');
      await expect(banner).toContainText('Submission pending');
      await expect(banner).toContainText('2026 Q4');
      await banner.getByRole('link', { name: 'Open my scorecard' }).click();
      await expect(page).toHaveURL(/\/performance\/my-kpis\/\d+$/);
    },
  );
});

test.describe('KPI reports', () => {
  test.skip(({ isMobile }) => isMobile, 'Report flows run on desktop (390 px is checked below)');

  test('KPI-41: department, ranking and target vs actual rows follow the filters', async ({
    page,
  }) => {
    await start(page);
    await page.goto('/performance/reports');
    await expect(page.getByRole('heading', { name: 'KPI reports', level: 1 })).toBeVisible();

    // Department performance: the department's scorecards of one period (the default: 2026 Q4).
    await choose(page, reportPicker(page), 'Department performance');
    await expect(page.getByText('Choose a department')).toBeVisible();
    await choose(page, page.getByRole('combobox', { name: 'Department' }), 'Engineering');
    await expect(page.locator('dl.facts')).toContainText('2026 Q4');
    await expect(rows(page)).toHaveCount(4); // EMP014, EMP017, EMP019, EMP046
    await filter(page, 'Period', '2026 Q3 (Closed)');
    await expect(page.locator('dl.facts')).toContainText('2026 Q3');
    await expect(rows(page)).toHaveCount(3);
    await expect(rows(page).first()).toContainText('EMP014'); // by employee code

    // Ranking: Finalized only, standard competition ranks, no status filter.
    await choose(page, reportPicker(page), 'Ranking');
    await expect(rows(page)).toHaveCount(4);
    await expect(rows(page).first().locator('td').first()).toHaveText('1');
    await expect(page.getByRole('table', { name: 'Ranking' })).not.toContainText('EMP056');
    await openFilters(page);
    await expect(
      page.getByRole('dialog', { name: 'Filters' }).getByRole('combobox', { name: 'Status' }),
    ).toHaveCount(0);
    await closeFilters(page);

    // Target vs actual: every status; the Rejected filter leaves EMP056's four KPIs.
    await choose(page, reportPicker(page), 'Target vs actual');
    await filter(page, 'Status', 'Rejected');
    await expect(rows(page)).toHaveCount(4);
    for (const row of await rows(page).all()) await expect(row).toContainText('EMP056');
  });

  test('KPI-43: the ranking exports the filtered rows; the employee report prints', async ({
    page,
  }) => {
    await page.addInitScript(() => {
      const w = window as unknown as { __printCalls: number; print: () => void };
      w.__printCalls = 0;
      w.print = () => {
        w.__printCalls++;
      };
    });
    await start(page);
    await page.goto('/performance/reports');
    await choose(page, reportPicker(page), 'Ranking');
    await filter(page, 'Period', '2026 Q3 (Closed)');
    await expect(rows(page)).toHaveCount(4);

    await page.getByRole('button', { name: 'Export' }).click();
    const dialog = page.getByRole('dialog');
    await expect(
      dialog.getByRole('heading', { name: 'Export KPI ranking to Excel' }),
    ).toBeVisible();
    await expect(dialog).toContainText('Period: 2026 Q3');
    const downloadPromise = page.waitForEvent('download');
    await dialog.getByRole('button', { name: 'Export to Excel' }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/^kpi-ranking-export-\d{8}-\d{4}\.xlsx$/);
    const module = await import('exceljs');
    const ExcelJS = ((module as { default?: typeof module }).default ?? module) as typeof module;
    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.readFile(await download.path());
    const sheet = workbook.getWorksheet('KPI ranking')!;
    expect(sheet.getRow(1).values).toEqual(
      expect.arrayContaining(['Rank', 'Employee Code', 'Overall Score', 'Rating']),
    );
    expect(sheet.actualRowCount).toBe(5); // header + the 4 filtered rows
    expect(sheet.getRow(2).getCell(1).value).toBe(1);

    // Employee performance history of EMP046, printed (the browser's Save as PDF).
    await choose(page, reportPicker(page), 'Employee performance history');
    await pickEmployeeOption(page, page.getByLabel('Employee', { exact: true }), 'EMP046');
    await expect(rows(page)).toHaveCount(2);
    await page.getByRole('button', { name: 'Print Employee performance history' }).click();
    await expect
      .poll(() => page.evaluate(() => (window as unknown as { __printCalls: number }).__printCalls))
      .toBe(1);
    const printed = page.getByTestId('report-print');
    await expect(printed.locator('table.print-table').first().locator('tbody tr')).toHaveCount(2);
    await expect(printed).toContainText('Employee: EMP046');
  });

  test('KPI-42: after a transfer, a finalized period still lists him under his old department', async ({
    page,
  }) => {
    await start(page);
    // Hein Thant Zin (EMP046) moves from Engineering to IT Infrastructure in the employee form. His
    // 2026 Q3 scorecard (period Closed, scorecard Finalized) copied Engineering when it was planned.
    await page.goto('/employees/46/edit');
    await expect(page.getByLabel('Employee code')).toHaveValue('EMP046');
    await choose(page, page.getByRole('combobox', { name: 'Department' }), 'IT Infrastructure');
    await choose(page, page.getByRole('combobox', { name: 'Position' }), 'Network Engineer');
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Employee "Hein Thant Zin" updated successfully.')).toBeVisible();
    await expect(page).toHaveURL(/\/employees\/46$/);

    // The employee list (inside the app, so the change stays) shows the new department.
    await searchEmployeesInApp(page, 'EMP046');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('IT Infrastructure');

    // The 2026 Q3 Engineering report still lists him, beside EMP014 and EMP017.
    await openPerformance(page, 'Reports');
    await choose(page, reportPicker(page), 'Department performance');
    const department = page.getByRole('combobox', { name: 'Department' });
    await choose(page, department, 'Engineering');
    await filter(page, 'Period', '2026 Q3 (Closed)');
    const facts = page.locator('dl.facts');
    await expect(facts).toContainText('Engineering');
    await expect(facts).toContainText('2026 Q3');
    await expect(rows(page)).toHaveCount(3);
    await expect(rows(page).filter({ hasText: 'EMP046' })).toHaveCount(1);

    // His new department's report for that period does not: it holds EMP056 (Rejected) only.
    await choose(page, department, 'IT Infrastructure');
    await expect(facts).toContainText('IT Infrastructure');
    await expect(facts).toContainText('2026 Q3');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('EMP056');
  });
});

test.describe('KPI activity', () => {
  test.skip(({ isMobile }) => isMobile, 'Write flows run on desktop');

  test('KPI-44: KPI actions appear in Recent activities by employee code, never with comments', async ({
    page,
  }) => {
    test.setTimeout(90_000);
    const dialog = page.getByRole('dialog');
    const toast = (text: string) => page.locator('.app-toast').filter({ hasText: text });
    await start(page);

    // KPI-19: complete EMP019's 2026 Q4 plan (60%) with one KPI, then finalize it.
    await page.goto('/performance/assignments/3');
    await expect(page.getByTestId('plan-total')).toContainText('60%');
    await page.getByRole('button', { name: 'Add KPIs' }).click();
    await expect(dialog.getByRole('heading', { name: 'Add KPIs to the plan' })).toBeVisible();
    await dialogFocused(page);
    await choose(page, dialog.getByRole('combobox', { name: 'Add a KPI' }), /^DEFECT-RATE ·/);
    await dialog.getByLabel('Weight for DEFECT-RATE').fill('40');
    await expect(dialog.getByTestId('weight-total')).toContainText('Total weight: 100%');
    await dialog.getByRole('button', { name: 'Add KPIs' }).click();
    await expect(toast('KPIs added to the scorecard of EMP019 (2026 Q4).')).toBeVisible();
    await page.getByRole('button', { name: 'Finalize plan' }).click();
    await dialog.getByRole('button', { name: 'Finalize plan' }).click();
    await expect(toast('Plan finalized for EMP019.')).toBeVisible();

    // KPI-31: return EMP006's scorecard (Under Review) with a reason.
    await openPerformance(page, 'Reviews');
    await page.getByRole('link', { name: 'Review the scorecard of EMP006 for 2026 Q4' }).click();
    await page.getByRole('button', { name: 'Return for revision' }).click();
    await dialogFocused(page);
    await dialog.getByLabel('Reason').fill('Fictional KPI-44 return reason');
    await dialog.getByRole('button', { name: 'Return', exact: true }).click();
    await expect(toast('Scorecard of EMP006 returned for revision.')).toBeVisible();

    // KPI-29: approve EMP046's submitted scorecard with feedback and a comment (no HR review).
    await openPerformance(page, 'Reviews');
    await page.getByRole('link', { name: 'Review the scorecard of EMP046 for 2026 Q4' }).click();
    await page.getByLabel('Overall feedback').fill('Fictional KPI-44 feedback');
    await page.getByRole('button', { name: 'Approve', exact: true }).click();
    await dialogFocused(page);
    await dialog.getByLabel('Comment (optional)').fill('Fictional KPI-44 approval note');
    await dialog.getByRole('button', { name: 'Approve and finalize' }).click();
    await expect(toast('Scorecard of EMP046 approved and finalized.')).toBeVisible();

    // The dashboard's activity card, newest first. Each line names the scorecard by employee code
    // and period, shows the performance icon and the actor, and never a comment or a name. A
    // review decision reads "was reviewed" whatever it was (approve, return), as in the API.
    // The main dashboard (Performance has a Dashboard link too).
    await nav(page).locator('a[href="/dashboard"]').click();
    await expect(page).toHaveURL(/\/dashboard$/);
    const card = page.getByRole('region', { name: 'Recent activities' });
    const entries = card.getByRole('listitem');
    const expected = [
      'Scorecard of EMP046 for "2026 Q4" was reviewed',
      'Review notes saved on the scorecard of EMP046 for "2026 Q4"',
      'Scorecard of EMP006 for "2026 Q4" was reviewed',
      'KPI plan of EMP019 for "2026 Q4" finalized',
      '1 KPI(s) assigned to EMP019 for "2026 Q4"',
    ];
    for (const [i, message] of expected.entries()) {
      const entry = entries.nth(i);
      await expect(entry.locator('.list__title')).toHaveText(message);
      await expect(entry.locator('mat-icon')).toHaveText('trending_up'); // category performance
      await expect(entry).toContainText('Su Myat Noe');
      for (const name of ['Hein Thant Zin', 'Hnin Ei Phyu']) {
        await expect(entry).not.toContainText(name);
      }
    }
    await expect(card).not.toContainText('Fictional');
  });
});

signedOutTest.describe('KPI reports by role', () => {
  signedOutTest.skip(({ isMobile }) => isMobile, 'Report flows run on desktop');

  signedOutTest('KPI-41: a manager’s ranking holds his team only', async ({ page }) => {
    await start(page, MANAGER_SESSION);
    await page.goto('/performance/reports');
    await choose(page, reportPicker(page), 'Ranking');
    await expect(rows(page)).toHaveCount(3); // EMP046, EMP017, EMP014 (2026 Q3)
    const table = page.getByRole('table', { name: 'Ranking' });
    await expect(table).not.toContainText('EMP006');
    await expect(table).not.toContainText('EMP056');
  });
});

test.describe('KPI dashboards and reports at 390 px', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  const scrollsSideways = (page: Page) =>
    page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );

  test('the HR overview and the reports fit without sideways scrolling', async ({ page }) => {
    await start(page);
    await page.goto('/performance/dashboard');
    await expect(stat(page, 'Employees with KPIs')).toHaveText('7');
    expect(await scrollsSideways(page)).toBe(false);
    await page.goto('/performance/reports');
    await expect(page.getByRole('heading', { name: 'KPI reports', level: 1 })).toBeVisible();
    await expect(rows(page).first()).toBeVisible();
    expect(await scrollsSideways(page)).toBe(false);
  });
});

signedOutTest.describe('KPI employee dashboard at 390 px', () => {
  signedOutTest.use({ viewport: { width: 390, height: 844 } });

  signedOutTest('the employee dashboard fits without sideways scrolling', async ({ page }) => {
    await start(page, EMPLOYEE_46_SESSION);
    await page.goto('/performance/dashboard');
    await expect(panel(page, 'My KPIs').locator('tbody tr')).toHaveCount(5);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
      ),
    ).toBe(false);
  });
});
