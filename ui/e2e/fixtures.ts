import { test as base, expect, Locator, Page } from '@playwright/test';

/**
 * Opens the employee list through the sidebar and searches it. The app has no global search,
 * and navigating inside the app (not page.goto) keeps the in-memory sample data.
 */
export async function searchEmployeesInApp(page: Page, term: string) {
  const nav = page.getByRole('navigation', { name: 'Main navigation' });
  const group = nav.getByRole('button', { name: 'Employees' });
  if ((await group.getAttribute('aria-expanded')) !== 'true') {
    await group.click();
  }
  await expect(group).toHaveAttribute('aria-expanded', 'true');
  await nav.getByRole('link', { name: 'Employee List' }).click();
  await expect(page).toHaveURL(/\/employees\/list/);
  await page.getByRole('searchbox', { name: /Search by employee code/ }).fill(term);
}

/**
 * Waits until the newest Material dialog has taken its initial focus. MatDialog moves focus into a
 * dialog only after its open animation (a 150 ms timer plus a render, longer on a loaded machine).
 * Typing before that races with the move: `fill` focuses its field and then sends the text, so a
 * focus move in between sends the text to the dialog's first field instead. Call it after opening
 * a dialog and before typing into it.
 */
export async function dialogFocused(page: Page) {
  await expect
    .poll(
      () =>
        page.evaluate(() => {
          const dialogs = document.querySelectorAll('mat-dialog-container');
          return dialogs[dialogs.length - 1]?.matches(':focus-within') ?? false;
        }),
      { message: 'the dialog takes its initial focus' },
    )
    .toBe(true);
}

/**
 * Types an employee code into an employee picker (`app-employee-picker`) and chooses the match.
 * The picker opens on focus with its previous list (the first 20 employees by first name) and
 * replaces it once the debounced search answers (150 ms plus the mock latency). An option of the
 * old list can be clicked while that list is swapped out, and the click then misses: nothing is
 * chosen. So wait until the list holds only the match, then click it. For a picker that keeps its
 * choice (not `clearOnPick`): the field then shows the chosen employee.
 */
export async function pickEmployeeOption(page: Page, input: Locator, code: string) {
  await input.fill(code);
  const options = page.getByRole('option');
  await expect(options).toHaveText([new RegExp(`^\\s*${code} ·`)]);
  await options.first().click();
  await expect(input).toHaveValue(new RegExp(`^${code} ·`));
}

/** Mirrors AuthService's stored session (frontend-only demo auth). */
export const ADMIN_SESSION = {
  id: 1,
  employeeId: 2,
  displayName: 'Su Myat Noe',
  email: 'sumyat.noe@apexhorizon.example',
  jobTitle: 'Director of Human Resources',
  roles: ['HR Administrator'],
};

/**
 * Test isolation from the public internet: the app loads web fonts from Google
 * Fonts, which can stall page loads on restricted networks. Tests do not depend on them.
 *
 * Pages sit behind the login guard, so every test starts signed in as the HR
 * administrator. Specs that exercise sign-in use `signedOutTest`.
 */
export const test = base.extend({
  page: async ({ page }, use) => {
    await page.route(/fonts\.(googleapis|gstatic)\.com/, (route) => route.abort());
    await page.addInitScript((session) => {
      if (!sessionStorage.getItem('hrms.session')) {
        sessionStorage.setItem('hrms.session', JSON.stringify(session));
      }
    }, ADMIN_SESSION);
    await use(page);
  },
});

export const signedOutTest = base.extend({
  page: async ({ page }, use) => {
    await page.route(/fonts\.(googleapis|gstatic)\.com/, (route) => route.abort());
    await use(page);
  },
});

export { expect };
