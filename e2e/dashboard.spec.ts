import { expect, test } from './fixtures';

test.describe('dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.getByRole('region', { name: 'Key figures' })).toBeVisible();
  });

  test('shows KPI cards with sample figures', async ({ page }) => {
    const kpis = page.getByRole('region', { name: 'Key figures' });
    await expect(kpis.getByText('Total employees')).toBeVisible();
    await expect(kpis.getByText('58', { exact: true })).toBeVisible();
    await expect(kpis.getByText('Present today')).toBeVisible();
  });

  test('renders charts with accessible summaries and a table view', async ({ page }) => {
    await expect(
      page.getByRole('img', { name: /Doughnut chart of today's attendance/ }),
    ).toBeVisible();
    await expect(page.getByRole('img', { name: /headcount by department/i })).toBeVisible();
    await page.getByRole('radio', { name: 'Table view' }).first().click();
    await expect(page.locator('app-attendance-today-donut table tbody tr')).toHaveCount(4);
  });

  test('lists pending leave approvals and upcoming events', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Pending leave approvals' })).toBeVisible();
    await expect(page.getByText('7 requests awaiting a decision')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Upcoming events' })).toBeVisible();
  });

  test('KPI card links navigate', async ({ page }) => {
    await page.getByRole('link', { name: /Total employees/ }).click();
    await expect(page).toHaveURL(/\/employees\/list$/);
  });
});
