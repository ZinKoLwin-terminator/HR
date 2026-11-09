import { Locator, Page } from '@playwright/test';
import { expect, test } from './fixtures';

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
const alertDialog = (page: Page) => page.getByRole('alertdialog');

/** Opens a Material select by its arrow end (the label can sit over the centre). */
async function openSelect(select: Locator) {
  const box = await select.boundingBox();
  if (!box) throw new Error('Select is not visible');
  await select.click({ position: { x: box.width - 22, y: box.height / 2 } });
}
async function chooseInDialog(page: Page, name: string, option: string | RegExp, exact = true) {
  await expect(dialog(page)).toBeVisible();
  await openSelect(dialog(page).getByRole('combobox', { name }));
  await page.getByRole('option', { name: option, exact }).click();
  await expect(page.getByRole('listbox')).toHaveCount(0);
}
async function rowMenu(page: Page, rowText: string | RegExp, action: string) {
  await rows(page)
    .filter({ hasText: rowText })
    .first()
    .getByRole('button', { name: /^Actions for/ })
    .click();
  await page.getByRole('menuitem', { name: action }).click();
}
/** Navigates inside the app: `page.goto` would reload it and reset the in-memory data. */
async function openMenuLink(page: Page, group: string, link: string) {
  const nav = page.getByRole('navigation', { name: 'Main navigation' });
  const target = nav.getByRole('link', { name: link, exact: true });
  if (!(await target.isVisible())) await nav.getByRole('button', { name: group }).click();
  await target.click();
}

test.describe('payroll setup', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('manages salary components and protects those used by a structure', async ({ page }) => {
    await page.goto('/payroll/salary-components');
    await expect(page.getByRole('heading', { name: 'Salary components' })).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Housing allowance' })).toContainText('10% of basic');

    await page.getByRole('button', { name: 'Add salary component' }).click();
    await dialog(page).getByRole('button', { name: 'Create salary component' }).click();
    await expect(dialog(page).getByText('Code is required.')).toBeVisible();
    await dialog(page).getByLabel('Code').fill('BONUS');
    await dialog(page).getByLabel('Component name').fill('Annual bonus');
    await chooseInDialog(page, 'Type', 'Earning (allowance)');
    await chooseInDialog(page, 'Calculation', '% of basic salary');
    await dialog(page).getByLabel('Amount or percentage').fill('150');
    await dialog(page).getByRole('button', { name: 'Create salary component' }).click();
    await expect(
      dialog(page)
        .getByText(/cannot exceed 100/)
        .first(),
    ).toBeVisible();
    await dialog(page).getByLabel('Amount or percentage').fill('8');
    await dialog(page).getByRole('button', { name: 'Create salary component' }).click();
    await expect(
      page.getByText('Salary component "Annual bonus" created successfully.'),
    ).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Annual bonus' })).toContainText('8% of basic');

    await rowMenu(page, 'Housing allowance', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/cannot be deleted because it is used by/)).toBeVisible();

    await rowMenu(page, 'Annual bonus', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Salary component "Annual bonus" deleted.')).toBeVisible();
  });

  test('lists salary structures and employee salaries with the sample label', async ({ page }) => {
    await page.goto('/payroll/salary-structures');
    await expect(rows(page)).toHaveCount(3);
    await expect(rows(page).filter({ hasText: 'Executive' })).toBeVisible();

    await openMenuLink(page, 'Payroll', 'Employee Salaries');
    await expect(page.getByRole('heading', { name: 'Employee salaries' })).toBeVisible();
    await expect(rows(page).first()).toContainText(/Current|Previous|Scheduled/);

    // A basic salary outside the employee's grade band is refused with the band in the message.
    await page.getByRole('button', { name: 'Add salary assignment' }).click();
    await chooseInDialog(page, 'Employee', /EMP015/);
    await chooseInDialog(page, 'Salary structure', /^Staff/, false);
    await dialog(page).getByLabel('Monthly basic salary (sample)').fill('99999');
    await dialog(page).getByLabel('Effective from').fill('2026-11-02');
    await dialog(page).getByRole('button', { name: 'Create salary assignment' }).click();
    await expect(dialog(page).getByText(/band/).first()).toBeVisible();
  });
});

test.describe('payroll processing workflow', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('processes, reviews, approves and pays the current period, then shows payslips', async ({
    page,
  }) => {
    await page.goto('/payroll/processing');
    const draft = rows(page).filter({ hasText: 'Draft' });
    await expect(draft).toHaveCount(1);
    await expect(rows(page).filter({ hasText: 'Paid' })).toHaveCount(6);

    await rowMenu(page, 'Draft', 'Process payroll');
    await dialog(page).getByRole('button', { name: 'Process', exact: true }).click();
    await expect(page.getByText(/processed\. Review the payslips/)).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Pending Approval' })).toHaveCount(1);

    await rowMenu(page, 'Pending Approval', 'Review');
    await expect(page).toHaveURL(/\/payroll\/processing\/\d+$/);
    await expect(page.getByText(/Sample figures/).first()).toBeVisible();
    await expect(page.getByRole('region', { name: /Payroll totals/ })).toBeVisible();
    await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();

    await page.getByRole('button', { name: 'Approve' }).first().click();
    await dialog(page).getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText(/approved\./)).toBeVisible();

    await page
      .getByRole('button', { name: /Mark as paid/ })
      .first()
      .click();
    await dialog(page)
      .getByRole('button', { name: /Mark as paid/ })
      .click();
    await expect(page.getByText(/marked as paid\./)).toBeVisible();

    // Payslip of a person in the period just paid
    await openMenuLink(page, 'Payroll', 'Payslips');
    await expect(page.getByRole('heading', { name: 'Payslips' })).toBeVisible();
    await page
      .getByRole('button', { name: /^Actions for/ })
      .first()
      .click();
    await page.getByRole('menuitem', { name: 'View payslip' }).click();
    await expect(page).toHaveURL(/\/payroll\/payslips\/\d+$/);
    await expect(page.getByText(/SAMPLE PAYSLIP/)).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Payslip');
    await expect(page.getByRole('button', { name: 'Print' })).toBeVisible();
  });

  test('returns a processed period to draft and enforces period order', async ({ page }) => {
    await page.goto('/payroll/processing');
    await rowMenu(page, 'Draft', 'Process payroll');
    await dialog(page).getByRole('button', { name: 'Process', exact: true }).click();
    await expect(rows(page).filter({ hasText: 'Pending Approval' })).toHaveCount(1);

    await rowMenu(page, 'Pending Approval', 'Return to draft');
    await alertDialog(page).getByLabel('Reason (optional)').fill('Allowance changed');
    await alertDialog(page).getByRole('button', { name: 'Return to draft' }).click();
    await expect(page.getByText(/returned to draft\./)).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Draft' })).toHaveCount(1);

    // Paid periods are final
    await rows(page)
      .filter({ hasText: 'Paid' })
      .first()
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await expect(page.getByRole('menuitem', { name: 'Return to draft' })).toHaveCount(0);
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
  });

  test('shows payroll history with a table alternative to the chart', async ({ page }) => {
    await page.goto('/payroll/history');
    await expect(page.getByRole('heading', { name: 'Payroll history' })).toBeVisible();
    await expect(page.getByRole('region', { name: /Summary/ })).toBeVisible();
    await expect(page.getByText(/Sample/).first()).toBeVisible();
  });

  test('shows the salary summary on an employee profile', async ({ page }) => {
    await page.goto('/employees');
    await rows(page).first().getByRole('link').first().click();
    await page.getByRole('tab', { name: 'Salary' }).click();
    await expect(page.getByText(/not real tax/i)).toBeVisible();
  });
});

test.describe('payroll on mobile', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('payslips list is usable without horizontal page scroll', async ({ page }) => {
    await page.goto('/payroll/payslips');
    await expect(page.getByRole('heading', { name: 'Payslips' })).toBeVisible();
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
    expect(overflow).toBe(false);
  });
});
