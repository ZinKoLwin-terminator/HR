import { expect, test } from './fixtures';

test.describe('application shell', () => {
  test('starts on the dashboard with title and breadcrumb', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page).toHaveTitle(/Dashboard · APEX HRMS/);
    await expect(page.getByRole('navigation', { name: 'Breadcrumb' })).toContainText('Dashboard');
  });

  test('shows not found for unknown URLs', async ({ page }) => {
    await page.goto('/nope');
    await expect(page.getByText('Page not found')).toBeVisible();
  });

  test('toggles dark mode', async ({ page }) => {
    await page.goto('/dashboard');
    const html = page.locator('html');
    const before = await html.getAttribute('class');
    await page.getByRole('button', { name: /Switch to (dark|light) mode/ }).click();
    await expect(html).not.toHaveAttribute('class', before ?? '');
  });
});

test.describe('desktop navigation', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop only');

  test('navigates through sidebar groups', async ({ page }) => {
    await page.goto('/dashboard');
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await nav.getByRole('button', { name: 'Leave Management' }).click();
    await nav.getByRole('link', { name: 'Leave Calendar' }).click();
    await expect(page).toHaveURL(/\/leave\/calendar$/);
    await expect(nav.getByRole('link', { name: 'Leave Calendar' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    await expect(page.getByRole('navigation', { name: 'Breadcrumb' })).toContainText(
      'Leave Management',
    );
  });

  test('collapses the sidebar to an icon rail with flyout menus', async ({ page }) => {
    await page.goto('/dashboard');
    await page.getByRole('button', { name: 'Collapse sidebar' }).click();
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await nav.getByRole('button', { name: 'Payroll' }).click();
    await page.getByRole('menuitem', { name: 'Payroll History' }).click();
    await expect(page).toHaveURL(/\/payroll\/history$/);
    await page.getByRole('button', { name: 'Expand sidebar' }).click();
    await expect(nav.getByText('Payroll History')).toBeVisible();
  });
});

test.describe('mobile navigation', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('opens the drawer and closes it after navigating', async ({ page }) => {
    await page.goto('/dashboard');
    await page.getByRole('button', { name: 'Open navigation menu' }).click();
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await nav.getByRole('button', { name: 'Employees' }).click();
    await nav.getByRole('link', { name: 'Employee List' }).click();
    await expect(page).toHaveURL(/\/employees\/list$/);
    await expect(nav).toBeHidden();
  });
});
