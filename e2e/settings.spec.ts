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
async function chooseIn(
  scope: Locator,
  page: Page,
  name: string,
  option: string | RegExp,
  exact = true,
) {
  await openSelect(scope.getByRole('combobox', { name }));
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
/** Moves around inside the app: `page.goto` would reload it and reset the in-memory data. */
async function openMenuLink(page: Page, group: string, link: string) {
  const nav = page.getByRole('navigation', { name: 'Main navigation' });
  const target = nav.getByRole('link', { name: link, exact: true });
  if (!(await target.isVisible())) await nav.getByRole('button', { name: group }).click();
  await target.click();
}

test.describe('settings', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('saves general settings, validating the currency code', async ({ page }) => {
    await page.goto('/settings/general');
    await expect(page.getByLabel('Organization name')).toHaveValue('Apex Horizon Group');
    await expect(page.getByRole('button', { name: 'Save settings' })).toBeDisabled();
    await page.getByLabel('Currency code').fill('us');
    await page.getByRole('button', { name: 'Save settings' }).click();
    await expect(page.getByText('Use three capital letters, e.g. USD.')).toBeVisible();
    await page.getByLabel('Currency code').fill('EUR');
    await page.getByRole('button', { name: 'Save settings' }).click();
    await expect(page.getByText('General settings saved (simulation).')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Save settings' })).toBeDisabled();
  });

  test('asks before leaving general settings with unsaved changes', async ({ page }) => {
    await page.goto('/settings/general');
    await page.getByLabel('Organization name').fill('Changed Name');
    await openMenuLink(page, 'Settings', 'Users');
    await expect(alertDialog(page)).toContainText('Discard unsaved changes?');
    await alertDialog(page).getByRole('button', { name: 'Keep editing' }).click();
    await expect(page.getByLabel('Organization name')).toHaveValue('Changed Name');
  });

  test('manages users: unique employee, own account protected, unlock', async ({ page }) => {
    await page.goto('/settings/users');
    await page.getByLabel('Search by name or email').fill('Su Myat');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('HR Administrator');

    // Your own account cannot be deleted
    await rows(page)
      .first()
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
    await page.keyboard.press('Escape');
    await page.getByLabel('Search by name or email').fill('');

    await page.getByRole('button', { name: 'Add user' }).click();
    await dialog(page).getByRole('button', { name: 'Create user' }).click();
    await expect(
      dialog(page)
        .getByText(/required/i)
        .first(),
    ).toBeVisible();
    await chooseIn(dialog(page), page, 'Employee', /EMP020/, false);
    await chooseIn(dialog(page), page, 'Role', 'Recruiter');
    await dialog(page).getByRole('button', { name: 'Create user' }).click();
    await expect(page.getByText(/created successfully/)).toBeVisible();

    await chooseIn(page.locator('body'), page, 'Status', 'Locked');
    await expect(rows(page)).toHaveCount(1);
    await rowMenu(page, 'Locked', 'Unlock account');
    await expect(page.getByText(/unlocked\./)).toBeVisible();
    await expect(rows(page)).toHaveCount(0);
  });
  test('manages roles and edits permissions with the View rule', async ({ page }) => {
    await page.goto('/settings/roles');
    await expect(rows(page).filter({ hasText: 'HR Administrator' })).toContainText('Built-in');

    await page.getByRole('button', { name: 'Add role' }).click();
    await dialog(page).getByLabel('Role name').fill('Trainer');
    await dialog(page).getByRole('button', { name: 'Create role' }).click();
    await expect(page.getByText('Role "Trainer" created successfully.')).toBeVisible();

    // Built-in roles and roles with users cannot be deleted
    await rows(page)
      .filter({ hasText: 'HR Administrator' })
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
    await page.keyboard.press('Escape');

    await rowMenu(page, 'Trainer', 'Edit permissions');
    await expect(page).toHaveURL(/\/settings\/permissions\?role=\d+/);
    await expect(page.getByText(/Simulation only/)).toBeVisible();
    await page.getByRole('checkbox', { name: 'Leave Approve' }).check();
    await expect(page.getByRole('checkbox', { name: 'Leave View' })).toBeChecked(); // View follows
    await page.getByRole('checkbox', { name: 'Leave View' }).uncheck();
    await expect(page.getByRole('checkbox', { name: 'Leave Approve' })).not.toBeChecked();
    await page.getByRole('checkbox', { name: 'Employees View' }).check();
    await page.getByRole('button', { name: 'Save permissions' }).click();
    await expect(page.getByText('Permissions of Trainer saved (simulation).')).toBeVisible();
    await expect(page.getByRole('checkbox', { name: 'Employees View' })).toBeChecked();

    // The administrator role is read-only
    await chooseIn(page.locator('body'), page, 'Role', 'HR Administrator');
    await expect(page.getByText(/always has full access/)).toBeVisible();
    await expect(page.getByRole('checkbox', { name: 'Payroll Approve' })).toBeDisabled();
  });
});

test.describe('settings on mobile', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('permission matrix scrolls inside its own container', async ({ page }) => {
    await page.goto('/settings/permissions');
    await expect(page.getByRole('heading', { name: 'Permissions' })).toBeVisible();
    await expect(page.getByRole('checkbox', { name: 'Dashboard View' })).toBeVisible();
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
    expect(overflow).toBe(false);
  });
});
