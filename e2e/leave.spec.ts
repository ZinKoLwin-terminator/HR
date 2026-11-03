import { Locator, Page } from '@playwright/test';
import { expect, test } from './fixtures';

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
const alertDialog = (page: Page) => page.getByRole('alertdialog');

/**
 * Opens a Material select by clicking its arrow end. The field's label (which Material makes
 * clickable) can sit over the centre, so Playwright's hit-test refuses a centred click.
 */
async function openSelect(select: Locator) {
  const box = await select.boundingBox();
  if (!box) throw new Error('Select is not visible');
  await select.click({ position: { x: box.width - 22, y: box.height / 2 } });
}
async function choose(page: Page, name: string, option: string | RegExp, exact = true) {
  await openSelect(page.getByRole('combobox', { name }));
  await page.getByRole('option', { name: option, exact }).click();
  await expect(page.getByRole('listbox')).toHaveCount(0); // let the panel close before the next action
}

/** Same, for a dropdown inside the open dialog (page filters can share the label). */
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

async function total(page: Page): Promise<number> {
  const text = await page.locator('mat-paginator .mat-mdc-paginator-range-label').innerText();
  return Number(/of (\d+)/.exec(text)?.[1] ?? 0);
}

test.describe('leave types', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('validates, creates, edits and deletes; protects types in use', async ({ page }) => {
    await page.goto('/leave/types');
    await expect(rows(page)).toHaveCount(7);
    await expect(rows(page).filter({ hasText: 'Annual Leave' })).toContainText('Up to 5 days');
    await expect(rows(page).filter({ hasText: 'Maternity Leave' })).toContainText('Women only');

    await page.getByRole('button', { name: 'Add leave type' }).click();
    await dialog(page).getByRole('button', { name: 'Create leave type' }).click();
    await expect(dialog(page).getByText('Code is required.')).toBeVisible();
    await dialog(page).getByLabel('Code').fill('SAB');
    await dialog(page).getByLabel('Leave type name').fill('Sabbatical');
    await dialog(page).getByLabel('Default days per year').fill('20');

    // Carry-forward cannot exceed the yearly entitlement (data rule)
    await dialog(page)
      .getByRole('switch', { name: /carry forward/ })
      .click();
    await dialog(page).getByLabel('Maximum carry-forward days').fill('40');
    await dialog(page).getByRole('button', { name: 'Create leave type' }).click();
    await expect(
      dialog(page)
        .getByText(/cannot exceed the yearly entitlement/)
        .first(),
    ).toBeVisible();
    await dialog(page).getByLabel('Maximum carry-forward days').fill('5');
    await dialog(page).getByRole('button', { name: 'Create leave type' }).click();
    await expect(page.getByText('Leave type "Sabbatical" created successfully.')).toBeVisible();
    await expect(rows(page)).toHaveCount(8);
    await expect(rows(page).filter({ hasText: 'Sabbatical' })).toContainText('Up to 5 days');

    await rowMenu(page, 'Sabbatical', 'Edit');
    await expect(dialog(page).getByLabel('Code')).toBeDisabled();
    await dialog(page).getByLabel('Default days per year').fill('25');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Leave type "Sabbatical" updated successfully.')).toBeVisible();

    await rowMenu(page, 'Annual Leave', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/cannot be deleted because it is used by/)).toBeVisible();

    await rowMenu(page, 'Sabbatical', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Leave type "Sabbatical" deleted.')).toBeVisible();
    await expect(rows(page)).toHaveCount(7);
  });
});

test.describe('leave policies', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('lists policies and refuses a clashing active policy', async ({ page }) => {
    await page.goto('/leave/policies');
    await expect(rows(page)).toHaveCount(9);
    await expect(rows(page).filter({ hasText: 'standard (after 3 months)' })).toContainText('3 mo');
    await expect(rows(page).filter({ hasText: 'monthly accrual' })).toContainText('Monthly');

    await page.getByRole('button', { name: 'Add leave policy' }).click();
    await dialog(page).getByLabel('Policy name').fill('Annual leave — executives (copy)');
    await chooseInDialog(page, 'Leave type', 'Annual Leave');
    await dialog(page).getByLabel('Days per year').fill('30');
    await chooseInDialog(page, 'Job grade', 'G9 · Executive');
    await dialog(page).getByRole('button', { name: 'Create leave policy' }).click();
    await expect(
      dialog(page)
        .getByText(/active policy already exists/)
        .first(),
    ).toBeVisible();

    // Targeting another employment type makes it a different group
    await chooseInDialog(page, 'Employment type', 'Part-time');
    await dialog(page).getByRole('button', { name: 'Create leave policy' }).click();
    await expect(page.getByText(/Leave policy ".*" created successfully\./)).toBeVisible();
    await expect(rows(page)).toHaveCount(10);

    await rowMenu(page, 'executives (copy)', 'Delete');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/deleted\./)).toBeVisible();
    await expect(rows(page)).toHaveCount(9);
  });
});

test.describe('leave applications', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('shows calculated days and eligible types, then runs a draft through submit and cancel', async ({
    page,
  }) => {
    await page.goto('/leave/applications');
    await expect(rows(page)).toHaveCount(10);

    await page.getByRole('button', { name: 'Add leave application' }).click();
    await chooseInDialog(page, 'Employee', 'Hnin Ei Phyu (EMP006)');
    // Leave types follow the employee: women see maternity but not paternity leave
    await openSelect(dialog(page).getByRole('combobox', { name: 'Leave type' }));
    await expect(page.getByRole('option', { name: 'Maternity Leave' })).toBeVisible();
    await expect(page.getByRole('option', { name: 'Paternity Leave' })).toHaveCount(0);
    await page.getByRole('option', { name: 'Medical Leave', exact: true }).click();

    // Number of days is calculated and read-only; weekends are excluded
    const days = dialog(page).getByLabel('Number of days');
    await expect(days).toBeDisabled();
    await dialog(page).getByLabel('Start date').fill('2027-03-01');
    await dialog(page).getByLabel('End date').fill('2027-03-05');
    await expect(days).toHaveValue('5');
    await dialog(page).getByLabel('End date').fill('2027-03-09');
    await expect(days).toHaveValue('7');
    await dialog(page).getByRole('switch', { name: 'Half day' }).click();
    await expect(days).toHaveValue('0.5');
    await dialog(page).getByRole('switch', { name: 'Half day' }).click();
    await dialog(page).getByLabel('End date').fill('2027-03-02');
    await expect(days).toHaveValue('2');

    // Cross-field validation
    await dialog(page).getByLabel('End date').fill('2027-02-20');
    await dialog(page).getByLabel('Reason').click();
    await expect(
      dialog(page).getByText('End date must be on or after the start date.'),
    ).toBeVisible();
    await dialog(page).getByLabel('End date').fill('2027-03-02');

    // Weekend-only dates have no working days
    await dialog(page).getByLabel('Start date').fill('2027-03-06');
    await dialog(page).getByLabel('End date').fill('2027-03-07');
    await dialog(page).getByLabel('Reason').fill('Doctor visit');
    await dialog(page).getByRole('button', { name: 'Create leave application' }).click();
    await expect(
      dialog(page)
        .getByText(/no working days/)
        .first(),
    ).toBeVisible();

    await dialog(page).getByLabel('Start date').fill('2027-03-01');
    await dialog(page).getByLabel('End date').fill('2027-03-02');
    await dialog(page).getByRole('button', { name: 'Create leave application' }).click();
    await expect(
      page.getByText(
        /Leave application "LV-\d{4}-\d{4} · Hnin Ei Phyu \(EMP006\)" created successfully\./,
      ),
    ).toBeVisible();

    // The new draft appears with status Draft
    await page.getByRole('searchbox', { name: /Search by application/ }).fill('doctor visit');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('Draft');
    await expect(rows(page).first()).toContainText('01 Mar – 02 Mar');

    // Medical leave needs a document before it can be submitted
    await rowMenu(page, 'Draft', 'Submit for approval');
    await dialog(page).getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByText(/needs a supporting document/)).toBeVisible();

    await rowMenu(page, 'Draft', 'Edit');
    await dialog(page)
      .locator('input[type=file]')
      .setInputFiles({
        name: 'certificate.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('x'),
      });
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText(/updated successfully/)).toBeVisible();
    await rowMenu(page, 'Draft', 'Submit for approval');
    await dialog(page).getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByText(/submitted for approval\./)).toBeVisible();
    await expect(rows(page).first()).toContainText('Submitted');

    // Submitted applications are locked: no Edit / Delete
    await rows(page)
      .first()
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await expect(page.getByRole('menuitem', { name: 'Edit' })).toHaveCount(0);
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
    await page.keyboard.press('Escape');

    // Overlapping dates for the same employee are refused
    await page.getByRole('button', { name: 'Add leave application' }).click();
    await chooseInDialog(page, 'Employee', 'Hnin Ei Phyu (EMP006)');
    await openSelect(dialog(page).getByRole('combobox', { name: 'Leave type' }));
    await page.getByRole('option', { name: 'Casual Leave', exact: true }).click();
    await dialog(page).getByLabel('Start date').fill('2027-03-02');
    await dialog(page).getByLabel('End date').fill('2027-03-02');
    await dialog(page).getByLabel('Reason').fill('Overlap attempt');
    await dialog(page).getByRole('button', { name: 'Create leave application' }).click();
    await expect(
      dialog(page)
        .getByText(/overlap LV-/)
        .first(),
    ).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();

    // Cancel the submitted request with a reason
    await rowMenu(page, 'Submitted', 'Cancel leave');
    await alertDialog(page).getByLabel('Reason (optional)').fill('Plans changed');
    await alertDialog(page).getByRole('button', { name: 'Cancel leave' }).click();
    await expect(page.getByText(/LV-\d{4}-\d{4} cancelled\./)).toBeVisible();
    await expect(rows(page).first()).toContainText('Cancelled');
  });

  test('deletes a draft after confirmation', async ({ page }) => {
    await page.goto('/leave/applications');
    await expect(rows(page).first()).toBeVisible();
    const drafts = (await rows(page).filter({ hasText: 'Draft' }).count()) > 0;
    expect(drafts).toBe(true); // seeded drafts exist
    const before = await total(page);
    await rowMenu(page, 'Draft', 'Delete');
    await expect(alertDialog(page)).toContainText('permanently removed');
    await alertDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/deleted\./)).toBeVisible();
    await expect.poll(() => total(page)).toBe(before - 1);
  });
});

test.describe('leave approvals', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('approves with remarks, rejects only with a reason, and records history', async ({
    page,
  }) => {
    await page.goto('/leave/approvals');
    await expect(rows(page)).toHaveCount(7);
    await expect(page.getByRole('button', { name: 'Add leave application' })).toHaveCount(0);

    // Approve
    await rowMenu(page, /LV-/, 'Approve');
    await expect(dialog(page)).toContainText('Approve leave?');
    await dialog(page).getByLabel('Remarks (optional)').fill('Enjoy the break');
    await dialog(page).getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText(/LV-\d{4}-\d{4} approved\./)).toBeVisible();
    await expect.poll(() => total(page)).toBe(6);

    // Reject needs a reason
    await rowMenu(page, /LV-/, 'Reject');
    await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(alertDialog(page).getByText('Reason for rejection is required.')).toBeVisible();
    await alertDialog(page).getByLabel('Reason for rejection').fill('Not enough team cover');
    await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(page.getByText(/LV-\d{4}-\d{4} rejected\./)).toBeVisible();
    await expect.poll(() => total(page)).toBe(5);

    // Both decisions are in the history
    await page.getByRole('tab', { name: 'Decision history' }).click();
    await expect(rows(page).filter({ hasText: 'Enjoy the break' })).toHaveCount(1);
    await expect(rows(page).filter({ hasText: 'Not enough team cover' })).toContainText('Rejected');
    await expect(rows(page).filter({ hasText: 'Su Myat Noe' }).first()).toBeVisible();
  });

  test('refuses to let the signed-in user decide their own request', async ({ page }) => {
    await page.goto('/leave/applications');
    await page.getByRole('button', { name: 'Add leave application' }).click();
    await chooseInDialog(page, 'Employee', 'Su Myat Noe (EMP002)');
    await openSelect(dialog(page).getByRole('combobox', { name: 'Leave type' }));
    await page.getByRole('option', { name: 'Annual Leave', exact: true }).click();
    await dialog(page).getByLabel('Start date').fill('2027-04-12');
    await dialog(page).getByLabel('End date').fill('2027-04-13');
    await dialog(page).getByLabel('Reason').fill('Own request');
    await dialog(page).getByRole('button', { name: 'Create leave application' }).click();
    await expect(page.getByText(/created successfully/)).toBeVisible();
    await page.getByRole('searchbox', { name: /Search by application/ }).fill('own request');
    await expect(rows(page)).toHaveCount(1);
    await rowMenu(page, 'Draft', 'Submit for approval');
    await dialog(page).getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByText(/submitted for approval/)).toBeVisible();

    // Navigate inside the app (a reload would reset the sample data)
    await page
      .getByRole('navigation', { name: 'Main navigation' })
      .getByRole('link', { name: 'Leave Approvals' })
      .click();
    await expect(rows(page).filter({ hasText: 'Su Myat Noe' })).toHaveCount(1);
    await rowMenu(page, 'Su Myat Noe', 'Approve');
    await dialog(page).getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText('You cannot decide your own leave request.')).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Su Myat Noe' })).toHaveCount(1);
  });
});

test.describe('leave balances', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('shows policy-based entitlements, years and per-employee details', async ({ page }) => {
    await page.goto('/leave/balances');
    await expect(rows(page)).toHaveCount(10);
    await expect(rows(page).first()).toContainText('Thura Aung');
    await expect(rows(page).first()).toContainText('/ 25');

    // A previous year has no leave taken
    await choose(page, 'Year', String(new Date().getFullYear() - 1));
    await expect(rows(page).first()).toContainText('25 / 25');
    await choose(page, 'Year', String(new Date().getFullYear()));

    await page.getByRole('searchbox', { name: /Search by employee name/ }).fill('aye chan');
    await expect(rows(page)).toHaveCount(1);
    await rows(page).first().click();
    await expect(dialog(page)).toContainText('Aye Chan Moe');
    await expect(dialog(page).getByRole('row', { name: /Maternity Leave/ })).toBeVisible();
    await expect(dialog(page).getByRole('row', { name: /Paternity Leave/ })).toHaveCount(0);
    await dialog(page).getByRole('button', { name: 'Close' }).click();
    await expect(dialog(page)).toHaveCount(0);

    await page.getByRole('searchbox', { name: /Search by employee name/ }).fill('');
    await choose(page, 'Department', 'Human Resources');
    await expect(rows(page).first()).toContainText('Human Resources');
    await expect(rows(page).filter({ hasNotText: 'Human Resources' })).toHaveCount(0);
  });

  test('the employee profile uses the same entitlements', async ({ page }) => {
    await page.goto('/employees/1?tab=leave');
    await expect(page.getByRole('heading', { name: /Leave balances/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Annual Leave/ })).toContainText('25');
  });
});

test.describe('leave calendar', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('shows leave by day, filters, month navigation and day details', async ({ page }) => {
    await page.goto('/leave/calendar');
    const grid = page.getByRole('table', { name: 'Leave calendar' });
    await expect(grid.locator('thead th')).toHaveCount(7);
    await expect(grid.locator('.chip').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Today' })).toBeDisabled();

    // Pending requests are drawn dashed and can be hidden
    await expect(grid.locator('.chip.pending').first()).toBeVisible();
    await page.getByRole('switch', { name: 'Show pending requests' }).click();
    await expect(grid.locator('.chip.pending')).toHaveCount(0);
    await expect(grid.locator('.chip').first()).toBeVisible();
    await page.getByRole('switch', { name: 'Show pending requests' }).click();

    // Day details
    await grid
      .locator('button.day', { has: page.locator('.chip') })
      .first()
      .click();
    await expect(dialog(page).getByRole('listitem').first()).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Close' }).click();
    await expect(dialog(page)).toHaveCount(0);

    // Filters
    const all = await grid.locator('.chip').count();
    await choose(page, 'Leave type', 'Casual Leave');
    await expect.poll(async () => grid.locator('.chip').count()).toBeLessThan(all);
    await choose(page, 'Leave type', 'All');

    // Month navigation
    const label = page.locator('.month-label');
    const current = await label.innerText();
    await page.getByRole('button', { name: 'Next month' }).click();
    await expect(label).not.toHaveText(current);
    await expect(page.getByRole('button', { name: 'Today' })).toBeEnabled();
    await page.getByRole('button', { name: 'Previous month' }).click();
    await expect(label).toHaveText(current);
    await page.getByRole('button', { name: 'Previous month' }).click();
    await page.getByRole('button', { name: 'Today' }).click();
    await expect(label).toHaveText(current);
  });
});

test.describe('leave on mobile', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('the calendar becomes a list and applications stay usable', async ({ page }) => {
    await page.goto('/leave/calendar');
    await expect(page.getByRole('table', { name: 'Leave calendar' })).toBeHidden();
    await expect(page.locator('.agenda__day').first()).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1),
    ).toBe(false);

    await page.goto('/leave/applications');
    await expect(rows(page).first()).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Employee' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Status' })).toBeVisible();
  });
});
