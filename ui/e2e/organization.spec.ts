import { Page } from '@playwright/test';
import { expect, test } from './fixtures';
import { closeFilters, openFilters } from './filters';

test.skip(({ isMobile }) => isMobile, 'Organization CRUD flows are exercised on desktop');

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
/** Destructive confirmations use role="alertdialog". */
const confirmDialog = (page: Page) => page.getByRole('alertdialog');

async function rowMenu(page: Page, name: string, action: string) {
  await rows(page)
    .filter({ hasText: name })
    .getByRole('button', { name: /^Actions for/ })
    .click();
  await page.getByRole('menuitem', { name: action }).click();
}

async function choose(page: Page, label: string, option: string) {
  await dialog(page).getByRole('combobox', { name: label }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

test.describe('companies', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/organization/companies');
    await expect(rows(page)).toHaveCount(3);
  });

  test('searches and filters', async ({ page }) => {
    await page.getByRole('searchbox', { name: /Search by code/ }).fill('logistics');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('APEX Logistics');

    await page.getByRole('searchbox', { name: /Search by code/ }).fill('');
    await expect(rows(page)).toHaveCount(3);

    await openFilters(page);
    await page.getByRole('combobox', { name: 'Status' }).click();
    await page.getByRole('option', { name: 'Inactive' }).click();
    await closeFilters(page);
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('APEX Ventures');
  });

  test('validates, creates, edits, deactivates and deletes a company', async ({ page }) => {
    await page.getByRole('button', { name: 'Add company' }).click();
    await expect(dialog(page).getByRole('heading', { name: 'Add company' })).toBeVisible();

    // Required-field and format validation
    await dialog(page).getByRole('button', { name: 'Create company' }).click();
    await expect(dialog(page).getByText('Company code is required.')).toBeVisible();
    await expect(dialog(page).getByText('Please correct the highlighted fields.')).toBeVisible();
    await dialog(page).getByLabel('Company code').fill('bad code');
    await dialog(page).getByLabel('Company name').click();
    await expect(
      dialog(page).getByText('Use upper-case letters, numbers and dashes only.'),
    ).toBeVisible();

    // Server-side (mock) uniqueness rule maps to the field
    await dialog(page).getByLabel('Company code').fill('AHT');
    await dialog(page).getByLabel('Company name').fill('Brand New Co');
    await dialog(page).getByLabel('Legal name').fill('Brand New Co Ltd');
    await dialog(page).getByLabel('Industry').fill('Retail');
    await dialog(page).getByLabel('Founded date').fill('2020-05-01');
    await dialog(page).getByLabel('Email').fill('hello@brandnew.example');
    await dialog(page).getByLabel('Phone').fill('+95 1 555 0999');
    await dialog(page).getByLabel('City').fill('Yangon');
    await choose(page, 'Status', 'Active');
    await dialog(page).getByRole('button', { name: 'Create company' }).click();
    await expect(dialog(page).getByText('Company code "AHT" already exists.')).toBeVisible();

    // Fix and save
    await dialog(page).getByLabel('Company code').fill('BNC');
    await dialog(page).getByRole('button', { name: 'Create company' }).click();
    await expect(dialog(page)).toBeHidden();
    await expect(page.getByText('Company "Brand New Co" created successfully.')).toBeVisible();
    await expect(rows(page)).toHaveCount(4);

    // Edit (code is locked after creation)
    await rowMenu(page, 'Brand New Co', 'Edit');
    await expect(dialog(page).getByLabel('Company code')).toBeDisabled();
    await dialog(page).getByLabel('Industry').fill('E-commerce');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Company "Brand New Co" updated successfully.')).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Brand New Co' })).toContainText('E-commerce');

    // Deactivate
    await rowMenu(page, 'Brand New Co', 'Deactivate');
    await expect(rows(page).filter({ hasText: 'Brand New Co' })).toContainText('Inactive');

    // Delete with confirmation (cancel first, then confirm)
    await rowMenu(page, 'Brand New Co', 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Cancel' }).click();
    await expect(rows(page)).toHaveCount(4);
    await rowMenu(page, 'Brand New Co', 'Delete');
    await expect(confirmDialog(page)).toContainText('"Brand New Co" will be permanently removed');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Company "Brand New Co" deleted.')).toBeVisible();
    await expect(rows(page)).toHaveCount(3);
  });

  test('refuses to delete a company that is still in use', async ({ page }) => {
    await rowMenu(page, 'APEX Technologies', 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/cannot be deleted because it is used by/)).toBeVisible();
    await expect(rows(page)).toHaveCount(3);
  });

  test('shows record details and can switch to edit', async ({ page }) => {
    await rows(page).filter({ hasText: 'APEX Logistics' }).click();
    await expect(dialog(page)).toContainText('Company details');
    await expect(dialog(page)).toContainText('REG-2018-004410');
    await dialog(page).getByRole('button', { name: 'Edit' }).click();
    await expect(dialog(page).getByRole('heading', { name: 'Edit company' })).toBeVisible();
  });
});

test.describe('departments', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/organization/departments');
    await expect(rows(page)).toHaveCount(10);
  });

  test('paginates and sorts', async ({ page }) => {
    await expect(page.getByText('1 – 10 of 11')).toBeVisible();
    await page.getByRole('button', { name: 'Next page' }).click();
    await expect(rows(page)).toHaveCount(1);

    await page.getByRole('button', { name: 'Previous page' }).click();
    await page.getByRole('columnheader', { name: 'Code' }).click();
    await expect(rows(page).first()).toContainText('CS');
    await page.getByRole('columnheader', { name: 'Code' }).click();
    await expect(rows(page).first()).toContainText('SAL');
  });

  test('branch dropdown depends on the selected company', async ({ page }) => {
    await page.getByRole('button', { name: 'Add department' }).click();
    await choose(page, 'Company', 'APEX Logistics');
    await dialog(page).getByRole('combobox', { name: 'Branch' }).click();
    // Branch is required, so there is no "None" entry; only this company's branches are offered.
    await expect(page.getByRole('option')).toHaveText([
      'Mandalay Distribution Center',
      'Yangon Logistics Hub',
    ]);
    await page.getByRole('option', { name: 'Yangon Logistics Hub' }).click();

    // Changing the company clears the now-invalid branch
    await choose(page, 'Company', 'APEX Technologies');
    await expect(dialog(page).getByRole('combobox', { name: 'Branch' })).not.toContainText(
      'Yangon Logistics Hub',
    );
  });

  test('filters by company', async ({ page }) => {
    await openFilters(page);
    await page.getByRole('combobox', { name: 'Company' }).click();
    await page.getByRole('option', { name: 'APEX Logistics' }).click();
    await closeFilters(page);
    await expect(rows(page)).toHaveCount(2);
  });
});

test.describe('job grades', () => {
  test('cross-field rule: maximum salary must not be below minimum', async ({ page }) => {
    await page.goto('/organization/job-grades');
    await expect(rows(page)).toHaveCount(9);
    await page.getByRole('button', { name: 'Add job grade' }).click();
    await dialog(page).getByLabel('Grade code').fill('G10');
    await dialog(page).getByLabel('Grade name').fill('Fellow');
    await dialog(page)
      .getByLabel(/Minimum monthly salary/)
      .fill('9000');
    await dialog(page)
      .getByLabel(/Maximum monthly salary/)
      .fill('5000');
    await dialog(page).getByLabel('Grade name').click();
    await expect(
      dialog(page).getByText('Maximum must be greater than or equal to minimum.'),
    ).toBeVisible();
    await dialog(page)
      .getByLabel(/Maximum monthly salary/)
      .fill('12000');
    await choose(page, 'Status', 'Active');
    await dialog(page).getByRole('button', { name: 'Create job grade' }).click();
    await expect(page.getByText('Job grade "G10 Fellow" created successfully.')).toBeVisible();
  });
});

test.describe('reporting structure', () => {
  test('shows the chart, expands, collapses and searches', async ({ page }) => {
    await page.goto('/organization/reporting-structure');
    // "Su Myat Noe" is also the signed-in user in the header, so scope to the chart.
    const chart = page.getByRole('region', { name: 'Organization chart' });
    await expect(chart.getByText('Thura Aung')).toBeVisible();
    await expect(page.getByText('People in structure')).toBeVisible();

    await page.getByRole('button', { name: 'Collapse all' }).click();
    await expect(chart.getByText('Su Myat Noe')).toBeHidden();
    await page.getByRole('button', { name: 'Expand all' }).click();
    await expect(chart.getByText('Su Myat Noe')).toBeVisible();

    await page.getByRole('searchbox', { name: 'Find a person' }).fill('Ingyin');
    await expect(chart.getByText('Ingyin Thwe')).toBeVisible();
    await expect(chart.getByText('Zaw Lin Oo')).toBeHidden();
  });
});
