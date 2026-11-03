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
/** Moves around inside the app: `page.goto` would reload it and reset the in-memory data. */
async function openMenuLink(page: Page, group: string, link: string) {
  const nav = page.getByRole('navigation', { name: 'Main navigation' });
  const target = nav.getByRole('link', { name: link, exact: true });
  if (!(await target.isVisible())) await nav.getByRole('button', { name: group }).click();
  await target.click();
}
const column = (page: Page, stage: string) =>
  page.getByRole('region', { name: new RegExp(`^${stage}`) });

test.describe('recruitment', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('creates a job opening as a draft, publishes it and protects openings with applicants', async ({
    page,
  }) => {
    await page.goto('/recruitment/job-openings');
    await expect(page.getByRole('heading', { name: 'Job openings' })).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Senior Angular Developer' })).toContainText('Open');

    await page.getByRole('button', { name: 'Add job opening' }).click();
    await dialog(page).getByRole('button', { name: 'Create job opening' }).click();
    await expect(dialog(page).getByText('Job title is required.')).toBeVisible();
    await dialog(page).getByLabel('Job title').fill('Data Analyst');
    await chooseInDialog(page, 'Department', 'Finance & Accounts');
    await chooseInDialog(page, 'Hiring manager', /EMP004/, false);
    await dialog(page).getByLabel('Closing date').fill('2099-01-01');
    await dialog(page).getByRole('button', { name: 'Create job opening' }).click();
    await expect(page.getByText('Job opening "Data Analyst" created successfully.')).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Data Analyst' })).toContainText('Draft');

    await rowMenu(page, 'Data Analyst', 'Publish');
    await expect(page.getByText('"Data Analyst" is now open.')).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Data Analyst' })).toContainText('Open');

    await rowMenu(page, 'Data Analyst', 'Close opening');
    await alertDialog(page).getByRole('button', { name: 'Close opening' }).click();
    await expect(rows(page).filter({ hasText: 'Data Analyst' })).toContainText('Closed');

    // Openings with applicants cannot be deleted
    await rows(page)
      .filter({ hasText: 'Senior Angular Developer' })
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
  });

  test('manages candidates with validation', async ({ page }) => {
    await page.goto('/recruitment/candidates');
    await expect(rows(page).first()).toBeVisible();
    await page.getByRole('button', { name: 'Add candidate' }).click();
    await dialog(page).getByRole('button', { name: 'Create candidate' }).click();
    await expect(dialog(page).getByText('First name is required.')).toBeVisible();
    await dialog(page).getByLabel('First name').fill('Zara');
    await dialog(page).getByLabel('Last name').fill('Quinn');
    await dialog(page).getByLabel('Email').fill('mia.hartley@example.com'); // already used
    await dialog(page).getByLabel('Phone').fill('+95 9 123 45678');
    await dialog(page).getByRole('button', { name: 'Create candidate' }).click();
    await expect(
      dialog(page)
        .getByText(/already exists/)
        .first(),
    ).toBeVisible();
    await dialog(page).getByLabel('Email').fill('zara.quinn@example.com');
    await dialog(page).getByRole('button', { name: 'Create candidate' }).click();
    await expect(page.getByText('Candidate "Zara Quinn" created successfully.')).toBeVisible();
  });

  test('moves candidates through the pipeline, rejecting with a reason', async ({ page }) => {
    await page.goto('/recruitment/pipeline');
    await expect(page.getByRole('heading', { name: 'Recruitment pipeline' })).toBeVisible();
    for (const stage of ['Applied', 'Screening', 'Interview', 'Selected', 'Rejected']) {
      await expect(column(page, stage)).toBeVisible();
    }
    await expect(column(page, 'Selected')).toContainText('Daniel Ruiz');

    // Lena Vogel is at Applied → Screening
    const card = column(page, 'Applied').getByRole('listitem').filter({ hasText: 'Lena Vogel' });
    await card.getByRole('button', { name: 'Actions for Lena Vogel' }).click();
    await page.getByRole('menuitem', { name: 'Move to Screening' }).click();
    await expect(page.getByText('Lena Vogel moved to Screening.')).toBeVisible();
    await expect(column(page, 'Screening')).toContainText('Lena Vogel');

    // Reject needs a reason
    await column(page, 'Screening').getByRole('button', { name: 'Actions for Lena Vogel' }).click();
    await page.getByRole('menuitem', { name: /Reject/ }).click();
    await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(alertDialog(page)).toBeVisible(); // still open: reason required
    await alertDialog(page).getByLabel('Reason').fill('Salary expectations too high');
    await alertDialog(page).getByRole('button', { name: 'Reject' }).click();
    await expect(page.getByText('Lena Vogel rejected.')).toBeVisible();
    await expect(column(page, 'Rejected')).toContainText('Salary expectations too high');

    // A candidate who has not had a "Hire" interview cannot be selected
    await column(page, 'Screening')
      .getByRole('button', { name: 'Actions for Noah Okafor' })
      .click();
    await page.getByRole('menuitem', { name: 'Move to Interview' }).click();
    await column(page, 'Interview')
      .getByRole('button', { name: 'Actions for Noah Okafor' })
      .click();
    await page.getByRole('menuitem', { name: 'Move to Selected' }).click();
    await expect(page.getByText(/needs a completed interview/)).toBeVisible();
  });

  test('adds an application from the pipeline and schedules and cancels an interview', async ({
    page,
  }) => {
    await page.goto('/recruitment/pipeline');
    await expect(column(page, 'Applied')).toBeVisible();
    await page.getByRole('button', { name: 'Add application' }).click();
    await chooseInDialog(page, 'Job opening', /Customer Support Agent/, false);
    await chooseInDialog(page, 'Candidate', /Zoe|Kai Tanaka/, false);
    await dialog(page).getByRole('button', { name: 'Create application' }).click();
    await expect(page.getByText('Application added.')).toBeVisible();
    await expect(column(page, 'Applied')).toContainText('Kai Tanaka');

    await openMenuLink(page, 'Recruitment', 'Interviews');
    await expect(page.getByRole('heading', { name: 'Interviews' })).toBeVisible();
    await expect(rows(page).filter({ hasText: 'Mia Hartley' }).first()).toContainText(
      /Completed|Scheduled/,
    );

    await rows(page)
      .filter({ hasText: 'Scheduled' })
      .first()
      .getByRole('button', { name: /^Actions for/ })
      .click();
    await page.getByRole('menuitem', { name: 'Cancel interview' }).click();
    await alertDialog(page).getByRole('button', { name: 'Cancel interview' }).click();
    await expect(page.getByText('Interview cancelled.')).toBeVisible();
  });
});

test.describe('recruitment on mobile', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('pipeline board does not widen the page', async ({ page }) => {
    await page.goto('/recruitment/pipeline');
    await expect(page.getByRole('heading', { name: 'Recruitment pipeline' })).toBeVisible();
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
    expect(overflow).toBe(false);
  });
});
