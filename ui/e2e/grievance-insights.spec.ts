import { Page } from '@playwright/test';
import { closeFilters, openFilters } from './filters';
import { dialogFocused, expect, test } from './fixtures';
import {
  alertDialog,
  card,
  choose,
  dialog,
  openGrievances,
  rows,
  toast,
} from './grievance-helpers';

/**
 * Grievance dashboard, reports, categories and the access log (GRV-E2E-40 to GRV-E2E-47), as Su Myat
 * Noe (HR Administrator, the fixture's session). Sample data: 16 submitted cases, five of them
 * anonymous (#4, #6, #9, #13, #17); Workplace Safety holds #7 (named), #12 and #17 (anonymous).
 * Mock `e2e` build; every name and figure is fictional.
 */

test.skip(({ isMobile }) => isMobile, 'Insight screens are exercised on desktop');

const stat = (page: Page, label: string) =>
  page.locator('app-stat-card').filter({ hasText: label }).locator('.stat__value');

/** The total of the paginator ("1 – 3 of 3"). */
async function total(page: Page): Promise<number> {
  const text = await page.locator('mat-paginator .mat-mdc-paginator-range-label').innerText();
  return Number(/of (\d+)/.exec(text)?.[1] ?? 0);
}

async function filterBy(page: Page, name: string, option: string) {
  await openFilters(page);
  await choose(page, page, name, option);
  await closeFilters(page);
}

test('GRV-E2E-40/41: the dashboard shows its figures and charts; anonymous departments are not disclosed', async ({
  page,
}) => {
  await page.goto('/grievances/dashboard');
  await expect(page.getByRole('heading', { name: 'Grievance dashboard', level: 1 })).toBeVisible();
  const figures = page.getByRole('region', { name: 'Key figures' });
  for (const label of [
    'Total',
    'New',
    'Under review',
    'Under investigation',
    'Awaiting the employee',
    'Pending resolution',
    'Resolved',
    'Closed',
    'Overdue',
    'Escalated',
    'Resolution rate',
    'Average resolution',
  ]) {
    await expect(figures.locator('app-stat-card').filter({ hasText: label }).first()).toBeVisible();
  }
  // Drafts are never counted: 16 submitted sample cases.
  await expect(stat(page, 'Total')).toHaveText('16');
  await expect(stat(page, 'Resolution rate')).toHaveText(/%$/);

  for (const title of [
    'Monthly trend',
    'By category',
    'By department',
    'By priority',
    'By status',
  ]) {
    const panel = card(page, title);
    await expect(panel).toBeVisible();
    await expect(panel.locator('canvas[role="img"]')).toHaveAttribute('aria-label', /\S/);
  }

  // GRV-E2E-41: anonymous cases are grouped as "Not disclosed", never under a department.
  const departments = card(page, 'By department');
  await expect(departments.locator('canvas')).toHaveAttribute('aria-label', /Not disclosed \d+/);
  await departments.getByRole('radio', { name: 'Table view' }).click();
  const notDisclosed = departments.getByRole('row').filter({ hasText: 'Not disclosed' });
  await expect(notDisclosed).toHaveCount(1);
  await expect(notDisclosed.getByRole('cell')).toHaveText('5');

  // The recent steps name cases by number only and open them.
  const recent = card(page, 'Recent activity');
  await expect(recent.getByRole('link').first()).toHaveText(/^GRV-\d{4}-\d{5}$/);
});

test('GRV-E2E-42/43: report filters change the table; the export downloads the filtered rows', async ({
  page,
}) => {
  await page.goto('/grievances/reports');
  await expect(page.getByRole('heading', { name: 'Grievance reports', level: 1 })).toBeVisible();
  await expect(rows(page).first()).toBeVisible();
  const everything = await total(page);
  expect(everything).toBeGreaterThan(3);

  // GRV-E2E-42: category, then status; every row matches, departments of anonymous cases hidden.
  await filterBy(page, 'Category', 'Workplace Safety');
  await expect.poll(() => total(page)).toBe(3);
  for (const row of await rows(page).all()) {
    await expect(row).toContainText('Workplace Safety');
  }
  await expect(rows(page).filter({ hasText: 'Not disclosed' })).toHaveCount(1);
  // Case numbers only: no subject in the report.
  await expect(page.getByRole('main')).not.toContainText('Blocked fire exit');

  await filterBy(page, 'Status', 'Resolved');
  await expect.poll(() => total(page)).toBe(2);
  for (const row of await rows(page).all()) {
    await expect(row).toContainText('Resolved');
  }

  // GRV-E2E-43: the export takes the report's filters and never the subject or the submitter.
  await page.getByRole('button', { name: 'Export' }).click();
  await expect(
    dialog(page).getByRole('heading', { name: 'Export Grievance cases to Excel' }),
  ).toBeVisible();
  const downloadPromise = page.waitForEvent('download');
  await dialog(page).getByRole('button', { name: 'Export to Excel' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/^grievance-cases-export-\d{8}-\d{4}\.xlsx$/);
  const module = await import('exceljs');
  const ExcelJS = ((module as { default?: typeof module }).default ?? module) as typeof module;
  const workbook = new ExcelJS.Workbook();
  await workbook.xlsx.readFile(await download.path());
  const sheet = workbook.getWorksheet('Grievance cases')!;
  const headers = (sheet.getRow(1).values as unknown[]).slice(1);
  expect(headers).toEqual([
    'Case No',
    'Category',
    'Status',
    'Priority',
    'Department',
    'Submitted',
    'Target',
    'Resolved',
    'Days',
    'Overdue',
    'Lead Investigator',
    'Escalated',
  ]);
  expect(sheet.actualRowCount).toBe(3); // the header and the 2 filtered rows
  const cells: string[] = [];
  sheet.eachRow((row) => row.eachCell((cell) => cells.push(String(cell.value))));
  expect(cells.join(' ')).toContain('Not disclosed');
  for (const secret of ['Loose cabling', 'Forklift', 'Subject', 'Submitter', 'Description']) {
    expect(cells.join(' ')).not.toContain(secret);
  }
});

test('GRV-E2E-44: the report prints its filtered rows', async ({ page }) => {
  await page.addInitScript(() => {
    const w = window as unknown as { __printCalls: number; print: () => void };
    w.__printCalls = 0;
    w.print = () => {
      w.__printCalls++;
    };
  });
  await page.goto('/grievances/reports');
  await expect(rows(page).first()).toBeVisible();
  await filterBy(page, 'Category', 'Workplace Safety');
  await expect.poll(() => total(page)).toBe(3);
  await page.getByRole('button', { name: 'Print the grievance report' }).click();
  await expect
    .poll(() => page.evaluate(() => (window as unknown as { __printCalls: number }).__printCalls))
    .toBe(1);
  const printed = page.getByTestId('grievance-report-print');
  await expect(printed.locator('table.print-table tbody tr')).toHaveCount(3);
  await expect(printed).toContainText('Grievance case report');
  await expect(printed).toContainText('Workplace Safety');
});

test('GRV-E2E-45/46: categories are created and edited; one used by cases cannot be deleted', async ({
  page,
}) => {
  await page.goto('/grievances');
  await openGrievances(page, 'Categories');
  await expect(page.getByRole('heading', { name: 'Grievance categories', level: 1 })).toBeVisible();
  await expect(rows(page).first()).toBeVisible();
  await expect.poll(() => total(page)).toBe(10);
  await expect(rows(page).filter({ hasText: 'Salary and Compensation Issues' })).toContainText(
    'Named only',
  );

  // Create, with the code rules.
  await page.getByRole('button', { name: 'Add category' }).click();
  await dialogFocused(page);
  await dialog(page).getByRole('button', { name: 'Create category' }).click();
  await expect(dialog(page).getByText('Code is required.')).toBeVisible();
  await dialog(page).getByLabel('Code').fill('bad code');
  await dialog(page).getByLabel('Category name').fill('Fictional Remote Work');
  await expect(
    dialog(page).getByText('Use upper-case letters, numbers and dashes only.'),
  ).toBeVisible();
  await dialog(page).getByLabel('Code').fill('REMOTE-WORK');
  await dialog(page).getByRole('button', { name: 'Create category' }).click();
  await expect(dialog(page)).toHaveCount(0);
  await expect(toast(page, 'Category "Fictional Remote Work" created successfully.')).toBeVisible();
  await expect.poll(() => total(page)).toBe(11);
  // Sorted by order (99), it is on the second page: search for it.
  const search = page.getByRole('searchbox', { name: 'Search by code or name' });
  await search.fill('Fictional Remote');
  await expect(rows(page)).toHaveCount(1);

  // Edit: named only, a shorter target (1 to 365 days).
  const rowMenu = async (name: string, item: string) => {
    await rows(page)
      .filter({ hasText: name })
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await page.getByRole('menuitem', { name: item, exact: true }).click();
  };
  await rowMenu('Fictional Remote Work', 'Edit');
  await dialogFocused(page);
  await expect(dialog(page).getByLabel('Code')).toBeDisabled();
  await dialog(page).getByRole('switch', { name: 'Anonymous reports allowed' }).click();
  await dialog(page).getByLabel('Default target days').fill('366');
  await dialog(page).getByRole('button', { name: 'Save changes' }).click();
  await expect(dialog(page)).toContainText('Default target days must be between 1 and 365.');
  await dialog(page).getByLabel('Default target days').fill('10');
  await dialog(page).getByRole('button', { name: 'Save changes' }).click();
  await expect(toast(page, 'Category "Fictional Remote Work" updated successfully.')).toBeVisible();
  const edited = rows(page).filter({ hasText: 'Fictional Remote Work' });
  await expect(edited).toContainText('Named only');
  await expect(edited).toContainText('10');

  // GRV-E2E-46: a category used by cases is not deleted; deactivate it instead.
  await search.fill('Workplace Safety');
  await expect(rows(page)).toHaveCount(1);
  await rowMenu('Workplace Safety', 'Delete');
  await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
  await expect(
    page.getByText(
      '"Workplace Safety" cannot be deleted because it is used by 3 grievance case(s). Deactivate it instead.',
    ),
  ).toBeVisible();
  await expect(rows(page)).toHaveCount(1);

  // An unused one is deleted.
  await search.fill('Fictional Remote');
  await rowMenu('Fictional Remote Work', 'Delete');
  await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
  await expect(toast(page, 'Category "Fictional Remote Work" deleted.')).toBeVisible();
  await search.fill('');
  await expect.poll(() => total(page)).toBe(10);
});

test('GRV-E2E-47: the HR Administrator opens the access log from Reports; it records the export', async ({
  page,
}) => {
  await page.goto('/grievances/reports');
  await expect(rows(page).first()).toBeVisible();
  await page.getByRole('button', { name: 'Export' }).click();
  const downloadPromise = page.waitForEvent('download');
  await dialog(page).getByRole('button', { name: 'Export to Excel' }).click();
  await downloadPromise;

  await page.getByRole('link', { name: 'Access log' }).click();
  await expect(page).toHaveURL(/\/grievances\/access-log$/);
  await expect(page.getByRole('heading', { name: 'Grievance access log', level: 1 })).toBeVisible();
  await expect(rows(page).first()).toBeVisible();
  await choose(page, page, 'Action', 'Export');
  await expect(rows(page).first()).toContainText('Export');
  await expect(rows(page).first()).toContainText('Su Myat Noe');
  for (const row of await rows(page).all()) {
    await expect(row).toContainText('Export');
  }
});
