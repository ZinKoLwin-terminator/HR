import { Page } from '@playwright/test';
import { expect, test } from './fixtures';
import { clearAllFilters, closeFilters, openFilters } from './filters';

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
const confirmDialog = (page: Page) => page.getByRole('alertdialog');

async function choose(page: Page, name: string, option: string | RegExp, exact = true) {
  // Material's label receives the click (and opens the select); Playwright's hit-test would refuse it.
  const inFilters = await openFilters(page);
  await page.getByRole('combobox', { name }).click({ force: true });
  await page.getByRole('option', { name: option, exact }).click();
  if (inFilters) await closeFilters(page);
}

/** Same, but for a dropdown inside the open dialog (page filters can share the label). */
async function chooseInDialog(page: Page, name: string, option: string | RegExp, exact = true) {
  await expect(dialog(page)).toBeVisible();
  await dialog(page).getByRole('combobox', { name }).click();
  await page.getByRole('option', { name: option, exact }).click();
}
async function rowMenu(page: Page, rowText: string | RegExp, action: string) {
  await rows(page)
    .filter({ hasText: rowText })
    .first()
    .getByRole('button', { name: /^Actions for/ })
    .click();
  await page.getByRole('menuitem', { name: action }).click();
}

/** Total from the paginator, e.g. "1 – 10 of 37" → 37. */
async function total(page: Page): Promise<number> {
  const text = await page.locator('mat-paginator .mat-mdc-paginator-range-label').innerText();
  return Number(/of (\d+)/.exec(text)?.[1] ?? 0);
}

const localIso = (daysAgo: number) => {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
};

test.describe('attendance dashboard', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('shows the day summary, charts and exception lists, and navigates between days', async ({
    page,
  }) => {
    await page.goto('/attendance/overview');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Attendance dashboard' }),
    ).toBeVisible();

    const summary = page.getByRole('region', { name: 'Attendance summary' });
    await expect(summary.getByText('Scheduled')).toBeVisible();
    await expect(summary.getByText('Checked in')).toBeVisible();
    await expect(summary.getByText('Absent')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Attendance by department' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Attendance trend' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Late arrivals' })).toBeVisible();

    // Chart table view
    await page.getByRole('radio', { name: 'Table view' }).first().click();
    await expect(
      page.locator('app-department-attendance-chart table tbody tr').first(),
    ).toBeVisible();

    // Previous day → banner and Today button enabled
    await page.getByRole('button', { name: 'Previous day' }).click();
    await expect(page.getByRole('button', { name: 'Today' })).toBeEnabled();
    await expect(page.getByRole('note')).toContainText('Showing');

    // Filter on the most recent weekday before today: "Previous day" lands on a weekend when
    // the test runs on a Monday, and weekends have too few scheduled people to compare.
    const day = new Date();
    do {
      day.setDate(day.getDate() - 1);
    } while (day.getDay() === 0 || day.getDay() === 6);
    const iso = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
    const longDate = day.toLocaleDateString('en-US', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
    const dateInput = page.getByLabel('Date', { exact: true });
    await dateInput.fill(iso);
    await dateInput.dispatchEvent('change');
    await expect(page.getByRole('note')).toContainText(longDate);

    // Branch filter narrows the headcount
    const before = Number(
      (await summary.locator('app-stat-card').first().innerText()).match(/\d+/)?.[0],
    );
    expect(before).toBeGreaterThan(0);
    await choose(page, 'Branch', 'Mandalay Distribution Center');
    await expect
      .poll(async () =>
        Number((await summary.locator('app-stat-card').first().innerText()).match(/\d+/)?.[0]),
      )
      .toBeLessThan(before);
  });
});

test.describe('attendance records', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('filters, sorts, pages and shows record details', async ({ page }) => {
    await page.goto('/attendance/records');
    await expect(rows(page)).toHaveCount(10);
    const all = await total(page);
    expect(all).toBeGreaterThan(1000);

    await choose(page, 'Department', 'Engineering');
    await expect(rows(page).first()).toContainText('Engineering');
    expect(await total(page)).toBeLessThan(all);

    await choose(page, 'Status', 'Late');
    await expect(rows(page).first()).toBeVisible();
    await expect(rows(page).filter({ hasNotText: 'Late' })).toHaveCount(0);

    await openFilters(page);
    await page.getByRole('textbox', { name: 'From' }).fill(localIso(10));
    await page.getByRole('textbox', { name: 'To', exact: true }).fill(localIso(3));
    await closeFilters(page);
    await expect(rows(page).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Clear filters' })).toBeVisible();

    // Start the name search from a clean slate (Daniel may have no late days in that range).
    await page.getByRole('button', { name: 'Clear filters' }).click();
    await expect(rows(page)).toHaveCount(10);
    await page.getByRole('searchbox', { name: /Search by employee name/ }).fill('carter');
    await expect(rows(page).first()).toContainText('Daniel Carter');
    await expect(rows(page).filter({ hasNotText: 'Daniel Carter' })).toHaveCount(0);

    // Details dialog is read-only
    await rows(page).first().click();
    await expect(dialog(page)).toContainText('Daniel Carter');
    await expect(dialog(page)).toContainText('Check-in');
    await expect(dialog(page).getByRole('button', { name: 'Edit' })).toHaveCount(0);
    await dialog(page).getByRole('button', { name: 'Close', exact: true }).last().click();

    await page.getByRole('button', { name: 'Clear filters' }).click();
    await expect(rows(page)).toHaveCount(10);
    await page.getByRole('columnheader', { name: 'Date' }).click();
    // The sort click resets paging asynchronously, so retry the click until the page changes.
    await expect(async () => {
      await page.getByRole('button', { name: 'Next page' }).click();
      await expect(page.getByText(/11 – 20 of/)).toBeVisible({ timeout: 1500 });
    }).toPass();
  });

  test('lists late arrivals and early departures', async ({ page }) => {
    await page.goto('/attendance/exceptions');
    await expect(page.getByRole('tab', { name: 'Late arrivals', selected: true })).toBeVisible();
    await expect(rows(page)).toHaveCount(10);
    await expect(rows(page).filter({ hasNotText: 'Late' })).toHaveCount(0);

    await page.getByRole('tab', { name: 'Early departures' }).click();
    await expect(page.getByRole('columnheader', { name: 'Left early by' })).toBeVisible();
    await expect(rows(page).first()).toBeVisible();
  });
});

test.describe('check in / check out', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('shows the clock, shift and recent attendance, and records a punch when available', async ({
    page,
  }) => {
    await page.goto('/attendance/check-in');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Check in / check out' }),
    ).toBeVisible();
    await expect(page.locator('.clock')).toHaveText(/\d{2}:\d{2}:\d{2}/);
    await expect(page.getByText(/General Shift · 09:00 – 18:00/)).toBeVisible();
    await expect(
      page.getByRole('table', { name: /Recent attendance/ }).locator('tbody tr'),
    ).toHaveCount(8);

    // Whichever action applies today (depends on the day the tests run), perform it.
    const checkIn = page.getByRole('button', { name: /Check in/ });
    const checkOut = page.getByRole('button', { name: /Check out/ });
    if (await checkIn.isEnabled()) {
      await checkIn.click();
      await expect(page.getByText(/checked in at \d{2}:\d{2}/)).toBeVisible();
      await expect(checkIn).toBeDisabled();
      await expect(checkOut).toBeEnabled();
    } else if (await checkOut.isEnabled()) {
      await checkOut.click();
      await expect(page.getByText(/checked out at \d{2}:\d{2}/)).toBeVisible();
      await expect(checkOut).toBeDisabled();
    } else {
      await expect(page.locator('.state')).toHaveText(/Day complete|on approved leave|Today is a/);
    }
  });
});

test.describe('shift management', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('validates times, creates, edits and deletes a shift; protects assigned shifts', async ({
    page,
  }) => {
    await page.goto('/attendance/shifts');
    await expect(rows(page)).toHaveCount(5);
    await expect(rows(page).filter({ hasText: 'General Shift' })).toContainText('09:00 – 18:00');
    await expect(rows(page).filter({ hasText: 'General Shift' })).toContainText('Mon–Fri');

    await page.getByRole('button', { name: 'Add shift' }).click();
    await dialog(page).getByLabel('Shift code').fill('EVE');
    await dialog(page).getByLabel('Shift name').fill('Evening Support');
    await dialog(page).getByLabel('Start time').fill('18:00');
    await dialog(page).getByLabel('End time').fill('09:00');
    await dialog(page).getByLabel('Shift name').click();
    await expect(dialog(page).getByText(/End time must be after the start time/)).toBeVisible();

    // A night shift may end the next morning
    await dialog(page)
      .getByRole('switch', { name: /Night shift/ })
      .click();
    await expect(dialog(page).getByText(/End time must be after the start time/)).toBeHidden();
    await dialog(page)
      .getByRole('switch', { name: /Night shift/ })
      .click();
    await dialog(page).getByLabel('End time').fill('22:00');

    // Working days: remove Friday
    await dialog(page).getByRole('combobox', { name: 'Working days' }).click();
    await page.getByRole('option', { name: 'Friday' }).click();
    await page.keyboard.press('Escape');

    await dialog(page).getByRole('button', { name: 'Create shift' }).click();
    await expect(page.getByText('Shift "Evening Support" created successfully.')).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Evening Support' })).toContainText('Mon–Thu');
    await expect(rows(page)).toHaveCount(6);

    // Edit: the code is locked
    await rowMenu(page, 'Evening Support', 'Edit');
    await expect(dialog(page).getByLabel('Shift code')).toBeDisabled();
    await dialog(page).getByLabel('Late grace period').fill('20');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Shift "Evening Support" updated successfully.')).toBeVisible();

    // Duplicate code is refused by the data rules
    await page.getByRole('button', { name: 'Add shift' }).click();
    await dialog(page).getByLabel('Shift code').fill('GEN');
    await dialog(page).getByLabel('Shift name').fill('Copy');
    await dialog(page).getByRole('button', { name: 'Create shift' }).click();
    await expect(dialog(page).getByText('Shift code "GEN" already exists.').first()).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();

    // Assigned shifts cannot be deleted; unused ones can
    await rowMenu(page, 'General Shift', 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/assigned employee\(s\)/)).toBeVisible();
    await rowMenu(page, 'Evening Support', 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Shift "Evening Support" deleted.')).toBeVisible();
    await expect(rows(page)).toHaveCount(5);
  });
});

test.describe('shift assignment', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('assigns a shift to several employees at once', async ({ page }) => {
    await page.goto('/attendance/shift-assignment');
    await expect(rows(page).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Assign to selected' })).toBeDisabled();

    await choose(page, 'Department', 'Customer Support');
    await expect(rows(page).first()).toContainText('Customer Support');
    await expect(rows(page).filter({ hasNotText: 'Support Rotational' })).toHaveCount(0);

    await page.getByRole('checkbox', { name: 'Select all rows on this page' }).check();
    const count = await rows(page).count();
    await expect(page.getByText(`${count} employees selected`)).toBeVisible();

    await page.getByRole('combobox', { name: 'Assign shift' }).click();
    await page.getByRole('option', { name: /Warehouse Morning/ }).click();
    await page.getByRole('button', { name: 'Assign to selected' }).click();
    await expect(
      page.getByText(new RegExp(`Warehouse Morning assigned to ${count} employees`)),
    ).toBeVisible();
    await expect(page.getByText('0 employees selected')).toBeVisible();
    await expect(rows(page).filter({ hasNotText: 'Warehouse Morning' })).toHaveCount(0);

    // A single employee can be picked too
    await choose(page, 'Current shift', 'Warehouse Morning');
    await rows(page).first().getByRole('checkbox').check();
    await expect(page.getByText('1 employee selected')).toBeVisible();
    await page.getByRole('button', { name: 'Clear selection' }).click();
    await expect(page.getByText('0 employees selected')).toBeVisible();
  });
});

test.describe('work schedules', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('shows the weekly roster and moves between weeks', async ({ page }) => {
    await page.goto('/attendance/schedules');
    const grid = page.getByRole('region', { name: 'Weekly schedule' });
    await expect(grid.locator('tbody tr').first()).toBeVisible();
    expect(await grid.locator('tbody tr').count()).toBeGreaterThan(50);
    await expect(page.getByRole('button', { name: 'This week' })).toBeDisabled();
    await expect(grid.getByRole('columnheader')).toHaveCount(8);
    await expect(grid.locator('.chip[data-kind="Shift"]').first()).toBeVisible();
    await expect(grid.locator('.chip[data-kind="Off"]').first()).toBeVisible();

    const label = page.locator('.week-label');
    const initial = await label.innerText();
    await page.getByRole('button', { name: 'Next week' }).click();
    await expect(label).not.toHaveText(initial);
    await expect(page.getByRole('button', { name: 'This week' })).toBeEnabled();
    await page.getByRole('button', { name: 'This week' }).click();
    await expect(label).toHaveText(initial);

    const all = await grid.locator('tbody tr').count();
    await choose(page, 'Department', 'Human Resources');
    await expect.poll(async () => grid.locator('tbody tr').count()).toBeLessThan(all);
  });
});

test.describe('overtime', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('approves, rejects, adds and validates overtime', async ({ page }) => {
    await page.goto('/attendance/overtime');
    await expect(rows(page).first()).toBeVisible();
    await choose(page, 'Status', 'Pending');
    await expect(rows(page).filter({ hasNotText: 'Pending' })).toHaveCount(0);
    const pending = await total(page);
    expect(pending).toBeGreaterThan(2);

    await rowMenu(page, 'Pending', 'Approve');
    await expect(dialog(page)).toContainText('Approve');
    await dialog(page).getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText(/Overtime for .* approved\./)).toBeVisible();
    await expect.poll(() => total(page)).toBe(pending - 1);

    await rowMenu(page, 'Pending', 'Reject');
    await confirmDialog(page).getByLabel('Reason for rejection').fill('Not pre-approved');
    await confirmDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(page.getByText(/Overtime for .* rejected\./)).toBeVisible();
    await expect.poll(() => total(page)).toBe(pending - 2);

    // Decided entries can only be viewed
    await clearAllFilters(page);
    await choose(page, 'Status', 'Approved');
    await rows(page)
      .first()
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await expect(page.getByRole('menuitem', { name: 'Approve' })).toHaveCount(0);
    await expect(page.getByRole('menuitem', { name: 'Edit' })).toHaveCount(0);
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
    await page.keyboard.press('Escape');

    // New entry
    await page.getByRole('button', { name: 'Add overtime entry' }).click();
    await dialog(page).getByRole('button', { name: 'Create overtime entry' }).click();
    await expect(dialog(page).getByText('Employee is required.')).toBeVisible();
    await chooseInDialog(page, 'Employee', 'Su Myat Noe (EMP002)');
    await dialog(page).getByLabel('Date').fill('2026-01-05');
    await dialog(page).getByLabel('Reason').fill('Year-end audit');
    await dialog(page).getByLabel('Overtime end').fill('18:05');
    await dialog(page).getByRole('button', { name: 'Create overtime entry' }).click();
    await expect(
      dialog(page)
        .getByText(/between 15 minutes and 12 hours/)
        .first(),
    ).toBeVisible();
    await dialog(page).getByLabel('Overtime end').fill('20:30');
    await dialog(page).getByRole('button', { name: 'Create overtime entry' }).click();
    await expect(page.getByText(/Overtime entry ".*" created successfully\./)).toBeVisible();

    // A second entry for the same day is refused
    await page.getByRole('button', { name: 'Add overtime entry' }).click();
    await chooseInDialog(page, 'Employee', 'Su Myat Noe (EMP002)');
    await dialog(page).getByLabel('Date').fill('2026-01-05');
    await dialog(page).getByLabel('Reason').fill('Again');
    await dialog(page).getByRole('button', { name: 'Create overtime entry' }).click();
    await expect(
      dialog(page)
        .getByText(/already recorded/)
        .first(),
    ).toBeVisible();
  });
});

test.describe('attendance adjustments', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('approves and rejects requests; creates, edits and deletes a pending one', async ({
    page,
  }) => {
    await page.goto('/attendance/adjustments');
    await expect(rows(page).first()).toBeVisible();
    await choose(page, 'Status', 'Pending');
    await expect(rows(page).filter({ hasNotText: 'Pending' })).toHaveCount(0);
    const pending = await total(page);
    expect(pending).toBeGreaterThanOrEqual(3);

    await rowMenu(page, 'Pending', 'Approve and apply');
    await expect(dialog(page)).toContainText('Apply the requested times');
    await dialog(page).getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText(/ADJ-\d{4} approved and applied/)).toBeVisible();
    await expect.poll(() => total(page)).toBe(pending - 1);

    await rowMenu(page, 'Pending', 'Reject');
    await confirmDialog(page).getByLabel('Reason for rejection').fill('Times do not match the log');
    await confirmDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(page.getByText(/ADJ-\d{4} rejected/)).toBeVisible();
    await expect.poll(() => total(page)).toBe(pending - 2);

    // New request: needs a change, an existing record and no duplicates
    const date = localIso(3);
    await page.getByRole('button', { name: 'Add adjustment request' }).click();
    await chooseInDialog(page, 'Employee', 'Su Myat Noe (EMP002)');
    await dialog(page).getByLabel('Date to correct').fill(date);
    await dialog(page).getByLabel('Reason').fill('Forgot to punch');
    await dialog(page).getByRole('button', { name: 'Create adjustment request' }).click();
    await expect(
      dialog(page)
        .getByText(/at least one change/)
        .first(),
    ).toBeVisible();

    await dialog(page).getByLabel('Correct check-in').fill('08:50');
    await dialog(page).getByLabel('Correct check-out').fill('08:00');
    await dialog(page).getByRole('button', { name: 'Create adjustment request' }).click();
    await expect(
      dialog(page)
        .getByText(/check-out time must be after the check-in time/i)
        .first(),
    ).toBeVisible();

    await dialog(page).getByLabel('Correct check-out').fill('18:20');
    await dialog(page).getByRole('button', { name: 'Create adjustment request' }).click();
    await expect(
      page.getByText(/Adjustment request "ADJ-\d{4}" created successfully\./),
    ).toBeVisible();

    await clearAllFilters(page);
    await page.getByRole('searchbox', { name: 'Search by reason' }).fill('forgot to punch');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('08:50 – 18:20');

    // Duplicate pending request for the same day is refused
    await page.getByRole('button', { name: 'Add adjustment request' }).click();
    await chooseInDialog(page, 'Employee', 'Su Myat Noe (EMP002)');
    await dialog(page).getByLabel('Date to correct').fill(date);
    await dialog(page).getByLabel('Correct check-in').fill('08:40');
    await dialog(page).getByLabel('Reason').fill('Second try');
    await dialog(page).getByRole('button', { name: 'Create adjustment request' }).click();
    await expect(
      dialog(page)
        .getByText(/pending request already exists/)
        .first(),
    ).toBeVisible();
    await dialog(page).getByRole('button', { name: 'Cancel' }).click();

    // Edit then delete the pending request
    await rowMenu(page, /./, 'Edit');
    await dialog(page).getByLabel('Reason').fill('Forgot to punch (edited)');
    await dialog(page).getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText(/updated successfully/)).toBeVisible();
    await rowMenu(page, /./, 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/ADJ-\d{4}" deleted\./)).toBeVisible();
    await expect(rows(page)).toHaveCount(0);
  });

  test('approving an adjustment updates the attendance record', async ({ page }) => {
    await page.goto('/attendance/adjustments');
    await page.getByRole('button', { name: 'Add adjustment request' }).click();
    const date = localIso(4);
    await chooseInDialog(page, 'Employee', 'Daniel Carter (EMP005)');
    await dialog(page).getByLabel('Date to correct').fill(date);
    await dialog(page).getByLabel('Correct check-in').fill('07:11');
    await dialog(page).getByLabel('Correct check-out').fill('19:44');
    await dialog(page).getByLabel('Reason').fill('Test correction');
    await dialog(page).getByRole('button', { name: 'Create adjustment request' }).click();
    await expect(page.getByText(/created successfully/)).toBeVisible();

    await page.getByRole('searchbox', { name: 'Search by reason' }).fill('test correction');
    // Wait for the search to narrow the list to our request before acting on a row.
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('07:11 – 19:44');
    await rowMenu(page, 'Pending', 'Approve and apply');
    await dialog(page).getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText(/approved and applied/)).toBeVisible();

    // Navigate inside the app (a full reload would reset the sample data)
    await page
      .getByRole('navigation', { name: 'Main navigation' })
      .getByRole('link', { name: 'Attendance Records' })
      .click();
    await page.getByRole('searchbox', { name: /Search by employee name/ }).fill('daniel carter');
    await openFilters(page);
    await page.getByRole('textbox', { name: 'From' }).fill(date);
    await page.getByRole('textbox', { name: 'To', exact: true }).fill(date);
    await closeFilters(page);
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('07:11');
    await expect(rows(page).first()).toContainText('19:44');
    await expect(rows(page).first()).toContainText('Manual');
  });
});

test.describe('attendance on mobile', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('dashboard and records stay usable on a phone', async ({ page }) => {
    await page.goto('/attendance/overview');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Attendance dashboard' }),
    ).toBeVisible();
    await expect(
      page.getByRole('region', { name: 'Attendance summary' }).locator('app-stat-card').first(),
    ).toBeVisible();
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth + 1,
    );
    expect(overflow).toBe(false);

    await page.goto('/attendance/records');
    await expect(rows(page).first()).toBeVisible();
    // Secondary columns are hidden, but employee and status remain.
    await expect(page.getByRole('columnheader', { name: 'Employee' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Status' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Source' })).toBeHidden();
  });
});
