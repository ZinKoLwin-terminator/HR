import { test as base, expect } from '@playwright/test';

/**
 * Test isolation from the public internet: the app loads web fonts from Google
 * Fonts, which can stall page loads on restricted networks. Tests do not depend on them.
 */
export const test = base.extend({
  page: async ({ page }, use) => {
    await page.route(/fonts\.(googleapis|gstatic)\.com/, (route) => route.abort());
    await use(page);
  },
});

export { expect };
