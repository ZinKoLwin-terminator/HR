import { Page } from '@playwright/test';

/** The filter icon button that opens the filter popup (absent on pages without filters). */
const toggle = (page: Page) => page.getByRole('button', { name: 'Toggle filters' });

/** Opens the filter popup if the page has one and it is closed. Returns whether a popup exists. */
export async function openFilters(page: Page): Promise<boolean> {
  const button = toggle(page);
  if ((await button.count()) === 0) return false;
  if ((await button.first().getAttribute('aria-expanded')) !== 'true') await button.first().click();
  return true;
}

/** Closes the filter popup (so it does not cover the table) if it is open. */
export async function closeFilters(page: Page): Promise<void> {
  const button = toggle(page);
  if ((await button.count()) === 0) return;
  if ((await button.first().getAttribute('aria-expanded')) === 'true') {
    const dialog = page.getByRole('dialog', { name: 'Filters' });
    // Use the popup's own button: Escape is swallowed when focus is in a closed select/datepicker.
    await dialog.getByRole('button', { name: 'Done' }).click();
    await dialog.waitFor({ state: 'detached' });
  }
}

/** Resets the search term and every filter through the popup's "Clear all" button. */
export async function clearAllFilters(page: Page): Promise<void> {
  await openFilters(page);
  const clear = page.getByRole('button', { name: 'Clear all' });
  if (await clear.isEnabled()) await clear.click();
  await closeFilters(page);
}
