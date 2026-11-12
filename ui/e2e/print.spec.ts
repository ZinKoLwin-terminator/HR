import { Page } from '@playwright/test';
import { expect, test } from './fixtures';

test.skip(({ isMobile }) => isMobile, 'Printing is exercised on desktop');

/** Replaces the browser print dialog with a counter, so the test can see that it was requested. */
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    const w = window as unknown as { __printCalls: number; print: () => void };
    w.__printCalls = 0;
    w.print = () => {
      w.__printCalls++;
    };
  });
});

const printCalls = (page: Page) =>
  page.evaluate(() => (window as unknown as { __printCalls: number }).__printCalls);

test('a list prints its rows through a print-only table', async ({ page }) => {
  await page.goto('/organization/departments');
  const screenRows = page.locator('tr.mat-mdc-row');
  await expect(screenRows.first()).toBeVisible();
  const count = await screenRows.count();
  await page.getByRole('button', { name: /^Print / }).click();
  await page.getByRole('menuitem', { name: /Print current view/ }).click();
  await expect.poll(() => printCalls(page)).toBe(1);
  // Laid out for paper (hidden on screen): one row per row on the page, no action column.
  await expect(page.locator('.print-only table.print-table tbody tr')).toHaveCount(count);
  await expect(page.locator('.print-only table.print-table')).not.toContainText('Actions');
});

test('a payslip has a Print payslip button', async ({ page }) => {
  await page.goto('/payroll/payslips');
  await page.locator('tr.mat-mdc-row').first().click();
  await expect(page).toHaveURL(/\/payroll\/payslips\/\d+/);
  await page.getByRole('button', { name: /^Print payslip for / }).click();
  await expect.poll(() => printCalls(page)).toBe(1);
});

test('an employee profile prints, and opens the letters screen', async ({ page }) => {
  await page.goto('/employees/1');
  await page.getByRole('button', { name: /^Print profile of / }).click();
  await expect.poll(() => printCalls(page)).toBe(1);
  await expect(page.locator('app-profile-print')).toContainText('Employment');

  await page.getByRole('link', { name: /^Letters and certificates for / }).click();
  await expect(page).toHaveURL(/\/employees\/1\/letters/);
  const print = page.getByRole('button', { name: /^Print letter for / });
  await expect(print).toBeDisabled();
  await page.getByLabel('Reference number').fill('HR/2026/001');
  await page.getByLabel('Signatory name').fill('Fictional Signatory');
  await expect(page.getByRole('article', { name: 'Letter preview' })).toContainText(
    'Authorised signatory',
  );
  await expect(print).toBeEnabled();
  await print.click();
  await expect.poll(() => printCalls(page)).toBe(2);
});
