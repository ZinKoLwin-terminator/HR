import { Locator, Page } from '@playwright/test';
import {
  ADMIN_SESSION,
  dialogFocused,
  expect,
  pickEmployeeOption,
  signedOutTest,
  test,
} from './fixtures';
import { closeFilters, openFilters } from './filters';

/**
 * KPI setup and planning (section 17 of docs/E2E-TEST-SCENARIOS.md, rows KPI-01 to KPI-19).
 * Mock `e2e` build: the in-memory data resets on every page load, so a flow that needs its own
 * records navigates inside the app. The browser clock starts at a fixed date (and keeps running,
 * so debounced searches still fire) so the seeded periods are always 2026 Q3 (Closed), 2026 Q4
 * (Active) and 2027 H1 (Draft, Half-Yearly).
 */

const TODAY = new Date('2026-11-17T09:00:00');

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
/** Destructive confirmations use role="alertdialog". */
const alertDialog = (page: Page) => page.getByRole('alertdialog');
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main navigation' });

/** Opens a Material select by its arrow end (its floating label can cover the centre). */
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

async function rowMenu(page: Page, rowText: string | RegExp, action: string) {
  await rows(page)
    .filter({ hasText: rowText })
    .first()
    .getByRole('button', { name: /^Actions for/ })
    .click();
  await page.getByRole('menuitem', { name: action, exact: true }).click();
}

/** Opens a Performance screen through the sidebar (keeps the in-memory data). */
async function openPerformance(page: Page, link: string) {
  const group = nav(page).getByRole('button', { name: 'Performance' });
  if ((await group.getAttribute('aria-expanded')) !== 'true') {
    await group.click();
  }
  await nav(page).getByRole('link', { name: link, exact: true }).click();
}

async function search(page: Page, label: RegExp, term: string) {
  await page.getByRole('searchbox', { name: label }).fill(term);
}

/** Creates a period through the form of the Evaluation Periods screen. */
async function addPeriod(
  page: Page,
  p: { name: string; type?: string; start: string; end: string; company?: string },
) {
  await page.getByRole('button', { name: 'Add evaluation period' }).click();
  await dialogFocused(page);
  const form = dialog(page);
  await chooseIn(form, page, 'Company', p.company ?? 'APEX Technologies');
  await form.getByLabel('Period name').fill(p.name);
  if (p.type) await chooseIn(form, page, 'Period type', p.type);
  await form.getByLabel('Start date').fill(p.start);
  await form.getByLabel('End date').fill(p.end);
  await form.getByRole('button', { name: 'Create evaluation period' }).click();
}

/** Picks an employee in the searchable picker of an assign dialog. */
async function pickEmployee(page: Page, code: string) {
  await pickEmployeeOption(page, dialog(page).getByLabel('Employee', { exact: true }), code);
}

/** Adds a KPI row from the "Add a KPI" picker of an assign dialog. */
async function addKpi(page: Page, code: string, weight?: string) {
  await chooseIn(dialog(page), page, 'Add a KPI', new RegExp(`^${code} ·`));
  if (weight !== undefined) {
    await dialog(page).getByLabel(`Weight for ${code}`).fill(weight);
  }
}

test.describe('KPI setup', () => {
  test.skip(({ isMobile }) => isMobile, 'CRUD flows run on desktop, as the other setup specs');
  test.beforeEach(async ({ page }) => {
    await page.clock.install({ time: TODAY });
  });

  test('KPI-01/02/03: creates, refuses duplicates, edits, deactivates and deletes a category', async ({
    page,
  }) => {
    await page.goto('/performance/categories');
    await expect(page.getByRole('heading', { name: 'KPI categories', level: 1 })).toBeVisible();

    await page.getByRole('button', { name: 'Add category' }).click();
    await dialogFocused(page);
    await dialog(page).getByLabel('Code').fill('TSTQ');
    await dialog(page).getByLabel('Category name').fill('Test Quality');
    await dialog(page).getByRole('button', { name: 'Create category' }).click();
    await expect(page.getByText('Category "Test Quality" created successfully.')).toBeVisible();
    await search(page, /Search by code or name/, 'TSTQ');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('Active');

    // KPI-02: the code, then the name, must be unique (refused on that field).
    await page.getByRole('button', { name: 'Add category' }).click();
    await dialogFocused(page);
    await dialog(page).getByLabel('Code').fill('TSTQ');
    await dialog(page).getByLabel('Category name').fill('Test Quality two');
    await dialog(page).getByRole('button', { name: 'Create category' }).click();
    await expect(dialog(page).getByText('KPI category code must be unique.')).toBeVisible();
    await dialog(page).getByLabel('Code').fill('TSTQ-2');
    await dialog(page).getByLabel('Category name').fill('Test Quality');
    await dialog(page).getByRole('button', { name: 'Create category' }).click();
    await expect(dialog(page).getByText('KPI category name must be unique.')).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();

    await rowMenu(page, 'TSTQ', 'Edit');
    await expect(dialog(page).getByLabel('Code')).toBeDisabled();
    await dialogFocused(page);
    await dialog(page).getByLabel('Description').fill('Fictional test category.');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Category "Test Quality" updated successfully.')).toBeVisible();

    await rowMenu(page, 'TSTQ', 'Deactivate');
    await expect(page.getByText('Category "Test Quality" deactivated.')).toBeVisible();
    await expect(rows(page).first()).toContainText('Inactive');

    // KPI-03: a category that templates use cannot be deleted; the message counts them.
    await search(page, /Search by code or name/, 'Productivity');
    await rowMenu(page, 'Productivity', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(
      page.getByText(/"Productivity" cannot be deleted because it is used by \d+ KPI template/),
    ).toBeVisible();

    await search(page, /Search by code or name/, 'TSTQ');
    await rowMenu(page, 'TSTQ', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Category "Test Quality" deleted.')).toBeVisible();
    await expect(rows(page)).toHaveCount(0);
  });

  test('KPI-04/05: the template form adapts to the scoring method and refuses bad rules', async ({
    page,
  }) => {
    await page.goto('/performance/templates');
    await expect(page.getByRole('heading', { name: 'KPI templates', level: 1 })).toBeVisible();
    const form = dialog(page);
    const create = () => form.getByRole('button', { name: 'Create template' }).click();

    // TST-SALES: Higher Is Better, Capped Percentage, target 100, weight 40, cap 120.
    await page.getByRole('button', { name: 'Add template' }).click();
    await dialogFocused(page);
    await form.getByLabel('Code').fill('TST-SALES');
    await form.getByLabel('KPI name').fill('Test sales');
    await chooseIn(form, page, 'Category', 'Productivity');
    await expect(
      form.getByRole('combobox', { name: 'Department-specific (optional)' }),
    ).toBeVisible();
    await expect(form.getByLabel('Maximum achievement (%)')).toHaveCount(0);
    await chooseIn(form, page, 'Scoring method', 'Capped Percentage');
    await form.getByLabel('Default weight (%)').fill('40');
    // (a) Higher Is Better with target 0, and (d) Capped Percentage without a cap.
    await form.getByLabel('Default target').fill('0');
    await create();
    await expect(
      form.getByText('The target must be greater than 0 for this direction.'),
    ).toBeVisible();
    await expect(form.getByText('Capped Percentage needs a maximum achievement.')).toBeVisible();
    await form.getByLabel('Default target').fill('100');
    await form.getByLabel('Maximum achievement (%)').fill('120');
    await create();
    await expect(
      page.getByText('Template "TST-SALES Test sales" created successfully.'),
    ).toBeVisible();

    // TST-DEFECT: (f) Lower Is Better with Direct Percentage is refused on the cap, then fixed.
    await page.getByRole('button', { name: 'Add template' }).click();
    await dialogFocused(page);
    await form.getByLabel('Code').fill('TST-DEFECT');
    await form.getByLabel('KPI name').fill('Test defects');
    await chooseIn(form, page, 'Category', 'Quality');
    await chooseIn(form, page, 'Direction', 'Lower Is Better');
    await expect(form.getByText(/a result of 0 scores the cap/)).toBeVisible();
    await form.getByLabel('Default target').fill('5');
    await form.getByLabel('Default weight (%)').fill('35');
    await create();
    await expect(form.getByText(/Lower is better needs a maximum achievement/)).toBeVisible();
    await chooseIn(form, page, 'Scoring method', 'Capped Percentage');
    await form.getByLabel('Maximum achievement (%)').fill('100');
    await create();
    await expect(
      page.getByText('Template "TST-DEFECT Test defects" created successfully.'),
    ).toBeVisible();

    // TST-RATE: (e) Boolean forces its measurement type, (c) a reversed range is refused,
    // (b) Rating Based needs the maximum rating.
    await page.getByRole('button', { name: 'Add template' }).click();
    await dialogFocused(page);
    await form.getByLabel('Code').fill('TST-RATE');
    await form.getByLabel('KPI name').fill('Test rating');
    await chooseIn(form, page, 'Category', 'Quality');
    await chooseIn(form, page, 'Scoring method', 'Boolean');
    await expect(form.getByRole('combobox', { name: 'Measurement type' })).toBeDisabled();
    await expect(form.getByRole('combobox', { name: 'Measurement type' })).toContainText('Boolean');
    await expect(form.getByLabel('Default target')).toHaveValue('1');
    await chooseIn(form, page, 'Scoring method', 'Range Based');
    await expect(form.getByRole('combobox', { name: 'Direction' })).toBeDisabled();
    // Boolean is not a type for Range Based scoring, so the type is chosen again.
    await chooseIn(form, page, 'Measurement type', 'Number');
    await form.getByLabel('Range minimum').fill('105');
    await form.getByLabel('Range maximum').fill('95');
    await create();
    await expect(
      form.getByText('The range maximum must be greater than the range minimum.'),
    ).toBeVisible();
    await chooseIn(form, page, 'Scoring method', 'Rating Based');
    await expect(form.getByLabel('Range minimum')).toHaveCount(0);
    await expect(form.getByRole('combobox', { name: 'Measurement type' })).toContainText('Rating');
    // Leaving Range Based releases (and clears) the direction it had fixed.
    await chooseIn(form, page, 'Direction', 'Higher Is Better');
    await form.getByLabel('Default target').fill('4');
    await form.getByLabel('Default weight (%)').fill('25');
    await create();
    await expect(
      form.getByText('The top of the rating scale must be greater than 0.'),
    ).toBeVisible();
    await form.getByLabel('Maximum rating').fill('5');
    await create();
    await expect(
      page.getByText('Template "TST-RATE Test rating" created successfully.'),
    ).toBeVisible();

    // Listed with category, direction and scoring method; the filters narrow the list.
    await search(page, /Search by code or name/, 'TST-');
    await expect(rows(page)).toHaveCount(3);
    const sales = rows(page).filter({ hasText: 'TST-SALES' });
    await expect(sales).toContainText('Productivity');
    await expect(sales).toContainText('Higher Is Better');
    await expect(sales).toContainText('Capped Percentage');
    await expect(rows(page).filter({ hasText: 'TST-RATE' })).toContainText('Rating Based');
    await openFilters(page);
    await chooseIn(page.getByRole('dialog', { name: 'Filters' }), page, 'Category', 'Quality');
    await closeFilters(page);
    await expect(rows(page)).toHaveCount(2);

    // Clean up.
    for (const code of ['TST-DEFECT', 'TST-RATE']) {
      await rowMenu(page, code, 'Delete');
      await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
      await expect(page.getByText(new RegExp(`Template "${code} .*" deleted\\.`))).toBeVisible();
    }
    await openFilters(page);
    await page.getByRole('button', { name: 'Clear all' }).click();
    await closeFilters(page);
    await search(page, /Search by code or name/, 'TST-SALES');
    await rowMenu(page, 'TST-SALES', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(rows(page)).toHaveCount(0);
  });

  test('KPI-09/10/11: a period refuses bad dates and overlaps, then moves to Closed', async ({
    page,
  }) => {
    await page.goto('/performance/periods');
    await expect(page.getByRole('heading', { name: 'Evaluation periods', level: 1 })).toBeVisible();
    await expect(rows(page)).toHaveCount(3);

    // KPI-10: the end date must be after the start date.
    await addPeriod(page, { name: '2027 Q1', start: '2027-01-01', end: '2026-12-31' });
    await expect(
      dialog(page).getByText('The end date must be after the start date.'),
    ).toBeVisible();
    await dialog(page).getByLabel('End date').fill('2027-01-01');
    await dialog(page).getByRole('button', { name: 'Create evaluation period' }).click();
    await expect(
      dialog(page).getByText('The end date must be after the start date.'),
    ).toBeVisible();
    await dialog(page).getByLabel('End date').fill('2027-03-31');
    await dialog(page).getByRole('button', { name: 'Create evaluation period' }).click();
    await expect(page.getByText('Evaluation period "2027 Q1" created successfully.')).toBeVisible();
    await expect(rows(page).filter({ hasText: '2027 Q1' })).toContainText('Draft');

    // KPI-11: the same type cannot overlap; another type inside it is accepted.
    await addPeriod(page, { name: 'TST overlap', start: '2027-03-01', end: '2027-05-31' });
    await expect(dialog(page).locator('.form-banner')).toContainText('overlap period "2027 Q1"');
    await chooseIn(dialog(page), page, 'Period type', 'Monthly');
    await dialog(page).getByLabel('Period name').fill('TST 2027-03');
    await dialog(page).getByLabel('End date').fill('2027-03-31');
    await dialog(page).getByRole('button', { name: 'Create evaluation period' }).click();
    await expect(
      page.getByText('Evaluation period "TST 2027-03" created successfully.'),
    ).toBeVisible();
    await rowMenu(page, 'TST 2027-03', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Evaluation period "TST 2027-03" deleted.')).toBeVisible();

    // KPI-09: Draft → Active → Completed → Closed, each confirmed.
    await rowMenu(page, '2027 Q1', 'Activate');
    await expect(dialog(page)).toContainText('becomes Active');
    await dialog(page).getByRole('button', { name: 'Activate' }).click();
    await expect(page.getByText('Period "2027 Q1" activated.')).toBeVisible();
    await expect(rows(page).filter({ hasText: '2027 Q1' })).toContainText('Active');

    // Active: only the name and the due date can change.
    await rowMenu(page, '2027 Q1', 'Edit');
    await dialogFocused(page);
    await expect(dialog(page).getByRole('note')).toContainText(
      'only its name and submission due date',
    );
    await expect(dialog(page).getByLabel('Start date')).toBeDisabled();
    await expect(dialog(page).getByRole('combobox', { name: 'Company' })).toBeDisabled();
    await expect(dialog(page).getByLabel('Period name')).toBeEnabled();
    await dialog(page).getByLabel('Submission due date').fill('2027-04-14');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Evaluation period "2027 Q1" updated successfully.')).toBeVisible();
    // Wait for the re-read row: a command sends the row's rowVersion, and a stale one is a 409.
    await expect(rows(page).filter({ hasText: '2027 Q1' })).toContainText('2027-04-14');

    await rowMenu(page, '2027 Q1', 'Complete');
    await dialog(page).getByRole('button', { name: 'Complete' }).click();
    await expect(page.getByText('Period "2027 Q1" completed.')).toBeVisible();
    await expect(rows(page).filter({ hasText: '2027 Q1' })).toContainText('Completed');

    await rowMenu(page, '2027 Q1', 'Close');
    await expect(alertDialog(page)).toContainText('becomes final');
    await alertDialog(page).getByRole('button', { name: 'Close period' }).click();
    await expect(page.getByText('Period "2027 Q1" closed.')).toBeVisible();
    const closed = rows(page).filter({ hasText: '2027 Q1' });
    await expect(closed).toContainText('Closed');
    // Closed is final: no Edit, Delete or command is offered (a Closed period cannot be deleted,
    // so it stays until the next page load resets the in-memory data).
    await closed.getByRole('button', { name: /^Actions for/ }).click();
    await expect(page.getByRole('menuitem')).toHaveText([/View details/]);
    await page.keyboard.press('Escape');
  });
});

test.describe('KPI assignments', () => {
  test.skip(({ isMobile }) => isMobile, 'Planning flows run on desktop, as the other setup specs');
  test.beforeEach(async ({ page }) => {
    await page.clock.install({ time: TODAY });
  });

  test('KPI-07/13/14/17/19: assigns, refuses a duplicate, completes and finalizes a plan', async ({
    page,
  }) => {
    // A Draft period of our own, then on to Assignments inside the app.
    await page.goto('/performance/periods');
    await addPeriod(page, { name: '2027 Q1', start: '2027-01-01', end: '2027-03-31' });
    await expect(page.getByText('Evaluation period "2027 Q1" created successfully.')).toBeVisible();
    await openPerformance(page, 'Assignments');
    await expect(page.getByRole('heading', { name: 'KPI assignments', level: 1 })).toBeVisible();

    await page.getByRole('button', { name: 'Assign KPIs' }).click();
    await dialogFocused(page);
    await chooseIn(dialog(page), page, 'Evaluation period', '2027 Q1 (Draft)');

    // KPI-07: ENG-REVIEW-HOURS is for Engineering only: hidden for EMP056 (IT), offered for EMP046.
    await pickEmployee(page, 'EMP056');
    await openSelect(dialog(page).getByRole('combobox', { name: 'Add a KPI' }));
    await expect(page.getByRole('option', { name: /^OUTPUT-TASKS/ })).toBeVisible();
    await expect(page.getByRole('option', { name: /^ENG-REVIEW-HOURS/ })).toHaveCount(0);
    await page.keyboard.press('Escape');
    await pickEmployee(page, 'EMP046');
    await openSelect(dialog(page).getByRole('combobox', { name: 'Add a KPI' }));
    await expect(page.getByRole('option', { name: /^ENG-REVIEW-HOURS/ })).toBeVisible();
    await page.keyboard.press('Escape');

    // KPI-13: target and weight are prefilled from the template and editable; running total.
    await addKpi(page, 'OUTPUT-TASKS');
    await expect(dialog(page).getByLabel('Target for OUTPUT-TASKS')).toHaveValue('100');
    await expect(dialog(page).getByLabel('Weight for OUTPUT-TASKS')).toHaveValue('30');
    await dialog(page).getByLabel('Weight for OUTPUT-TASKS').fill('40');
    await addKpi(page, 'DEFECT-RATE', '35');
    const total = dialog(page).getByTestId('weight-total');
    await expect(total).toContainText('Total weight: 75%');
    await dialog(page).getByLabel('Weight for DEFECT-RATE').fill('65');
    await expect(total).toContainText('cannot add up to more than 100%');
    await dialog(page).getByLabel('Weight for DEFECT-RATE').fill('35');
    await dialog(page).getByRole('button', { name: 'Assign KPIs' }).click();
    await expect(
      page.getByText('Scorecard created for EMP046 (2027 Q1) with 2 KPIs.'),
    ).toBeVisible();

    // KPI-14: the same KPI again for the same employee and period is refused.
    await page.getByRole('button', { name: 'Assign KPIs' }).click();
    await dialogFocused(page);
    await chooseIn(dialog(page), page, 'Evaluation period', '2027 Q1 (Draft)');
    await pickEmployee(page, 'EMP046');
    await addKpi(page, 'OUTPUT-TASKS', '10');
    await dialog(page).getByRole('button', { name: 'Assign KPIs' }).click();
    await expect(dialog(page).getByRole('alert').first()).toContainText(
      'KPI OUTPUT-TASKS is already assigned to the scorecard of EMP046.',
    );
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();

    // The list shows the plan at 75% (highlighted); open it.
    await search(page, /Search by employee code or name/, 'EMP046');
    const row = rows(page).filter({ hasText: '2027 Q1' });
    await expect(row).toContainText('Planning');
    await expect(row).toContainText('not 100%');
    await row.getByRole('link', { name: 'Open the plan of EMP046 for 2027 Q1' }).click();
    await expect(page).toHaveURL(/\/performance\/assignments\/\d+$/);

    // KPI-17: below 100% the plan cannot be finalized.
    await expect(page.getByTestId('plan-total')).toContainText('75%');
    await expect(page.getByRole('button', { name: 'Finalize plan' })).toBeDisabled();
    await expect(page.getByText('They add up to 75% now.')).toBeVisible();

    // Add the last KPI from the plan: 200 means "KPIs added".
    await page.getByRole('button', { name: 'Add KPIs' }).click();
    await expect(dialog(page).getByRole('heading', { name: 'Add KPIs to the plan' })).toBeVisible();
    await dialogFocused(page);
    await addKpi(page, 'ATTENDANCE-RATE', '25');
    await expect(dialog(page).getByTestId('weight-total')).toContainText('Total weight: 100%');
    await dialog(page).getByRole('button', { name: 'Add KPIs' }).click();
    await expect(page.getByText('KPIs added to the scorecard of EMP046 (2027 Q1).')).toBeVisible();
    await expect(page.getByTestId('plan-total')).toContainText('100%');

    // Edit a KPI's target in the plan.
    await page.getByRole('button', { name: 'Edit OUTPUT-TASKS' }).click();
    await dialogFocused(page);
    await dialog(page).getByLabel('Target').fill('120');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('OUTPUT-TASKS updated.')).toBeVisible();

    // KPI-19: finalize; the confirmation names the reviewer, the employee's current manager.
    await page.getByRole('button', { name: 'Finalize plan' }).click();
    await expect(dialog(page)).toContainText('The reviewer will be Daniel Carter');
    await dialog(page).getByRole('button', { name: 'Finalize plan' }).click();
    await expect(
      page.getByText('Plan finalized for EMP046. The scorecard is now in Draft.'),
    ).toBeVisible();
    await expect(
      page.getByText('This scorecard is Draft, so its plan is read-only.'),
    ).toBeVisible();
    await expect(page.getByTestId('plan-reviewer')).toContainText('Daniel Carter');
    await expect(page.getByRole('button', { name: 'Finalize plan' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Edit OUTPUT-TASKS' })).toHaveCount(0);
    const history = page.getByRole('list', { name: 'Review history' });
    await expect(history).toContainText('Finalize Plan');
    await expect(history).toContainText('Su Myat Noe');
  });

  test('removes a KPI and deletes a scorecard in Planning, each confirmed', async ({ page }) => {
    // EMP019's seeded plan of the Active quarter (Planning, 60%).
    await page.goto('/performance/assignments');
    await search(page, /Search by employee code or name/, 'EMP019');
    await rows(page)
      .first()
      .getByRole('link', { name: /Open the plan of EMP019/ })
      .click();
    await expect(page.getByTestId('plan-total')).toContainText('60%');

    await page.getByRole('button', { name: 'Remove OUTPUT-TASKS' }).click();
    await expect(alertDialog(page)).toContainText('will be removed from the plan');
    await alertDialog(page).getByRole('button', { name: 'Remove' }).click();
    await expect(page.getByText('OUTPUT-TASKS removed from the plan.')).toBeVisible();
    await expect(page.getByTestId('plan-total')).toContainText('30%');

    await page.getByRole('button', { name: 'Delete scorecard' }).click();
    await expect(alertDialog(page)).toContainText('permanently removed');
    await alertDialog(page).getByRole('button', { name: 'Delete scorecard' }).click();
    await expect(page.getByText('Scorecard of EMP019 deleted.')).toBeVisible();
    await expect(page).toHaveURL(/\/performance\/assignments$/);
    await search(page, /Search by employee code or name/, 'EMP019');
    await expect(rows(page)).toHaveCount(0);
  });

  test('KPI-15: bulk assigns by department, lists the skipped, and assigns nobody twice', async ({
    page,
  }) => {
    await page.goto('/performance/assignments');
    const bulk = async () => {
      await page.getByRole('button', { name: 'Bulk assign' }).click();
      await dialogFocused(page);
      await chooseIn(dialog(page), page, 'Evaluation period', '2026 Q4 (Active)');
      await chooseIn(dialog(page), page, 'Department', 'Engineering');
      await addKpi(page, 'TRAINING-DONE', '10');
      await dialog(page).getByRole('button', { name: 'Assign KPIs' }).click();
    };

    await bulk();
    const summary = dialog(page).getByTestId('bulk-summary');
    await expect(summary).toContainText(/[1-9]\d* assigned/);
    const skipped = dialog(page).getByRole('region', { name: 'Skipped employees' });
    // EMP046's scorecard of this quarter is already Submitted.
    await expect(skipped.getByRole('row', { name: /EMP046/ })).toBeVisible();
    await expect(
      dialog(page)
        .getByRole('region', { name: 'Assigned employees' })
        .getByRole('row', {
          name: /EMP019 1/,
        }),
    ).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Done' }).click();

    // Running the same request again assigns nobody twice.
    await bulk();
    await expect(dialog(page).getByTestId('bulk-summary')).toContainText('0 assigned');
    await expect(dialog(page).getByText('Nobody was assigned.')).toBeVisible();
    await expect(
      dialog(page)
        .getByRole('region', { name: 'Skipped employees' })
        .getByRole('row', {
          name: /EMP019/,
        }),
    ).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Done' }).click();
  });
});

test.describe('KPI screens on a phone', () => {
  test.skip(({ isMobile }) => !isMobile, 'Phone layout only');
  test.use({ viewport: { width: 390, height: 844 } });

  test('the plan and the assign dialog fit 390 px without sideways scrolling', async ({ page }) => {
    await page.clock.install({ time: TODAY });
    const noSideways = async () =>
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth),
      ).toBeLessThanOrEqual(1);

    await page.goto('/performance/assignments/3');
    await expect(page.getByTestId('plan-total')).toContainText('60%');
    await noSideways();
    await page.getByRole('button', { name: 'Add KPIs' }).click();
    await dialogFocused(page);
    await addKpi(page, 'DEFECT-RATE', '40');
    await expect(dialog(page).getByTestId('weight-total')).toContainText('100%');
    await noSideways();
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();

    await page.goto('/performance/assignments');
    await page.getByRole('button', { name: 'Bulk assign' }).click();
    await expect(dialog(page).getByRole('combobox', { name: 'Choose employees by' })).toBeVisible();
    await noSideways();
  });
});

signedOutTest.describe('KPI setup permissions', () => {
  signedOutTest(
    'a role without kpi-setup sees only My KPIs in Performance and is sent to /forbidden',
    async ({ page, isMobile }) => {
      // A simulated session with an explicit permission list (the mock enforces nothing).
      await page.addInitScript(
        (session) => {
          sessionStorage.setItem('hrms.session', JSON.stringify(session));
        },
        {
          ...ADMIN_SESSION,
          roles: ['Employee'],
          permissions: ['dashboard:View', 'kpi:View', 'kpi:Create'],
        },
      );
      await page.goto('/dashboard');
      await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible();
      // The group holds the employee's own screen only (kpi:View), never the setup screens.
      if (isMobile) await page.getByRole('button', { name: 'Open navigation menu' }).click();
      await nav(page).getByRole('button', { name: 'Performance' }).click();
      await expect(nav(page).getByRole('link', { name: 'My KPIs', exact: true })).toBeVisible();
      for (const hidden of ['Assignments', 'Templates', 'Evaluation Periods', 'Reviews']) {
        await expect(nav(page).getByRole('link', { name: hidden, exact: true })).toHaveCount(0);
      }
      await page.goto('/performance/templates');
      await expect(page).toHaveURL(/\/forbidden$/);
    },
  );
});
