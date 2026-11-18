import { Locator, Page } from '@playwright/test';
import { expect } from './fixtures';

/**
 * Shared helpers of the grievance specs (`grievance-*.spec.ts`). Mock `e2e` build: the in-memory data
 * resets on every page load, so a flow that needs several people signs out and in INSIDE the app (the
 * demo password works for every sample account) and never reloads.
 *
 * People (sample accounts, fictional): Su Myat Noe (EMP002, HR Administrator: every grievance key,
 * so management and review), Ye Yint Naing (EMP009, HR Officer: grievance-investigation View and
 * Edit, the investigator of the samples) and Daniel Carter (EMP005, the Employee role: grievance View
 * and Create; he owns cases #1 and #2 (drafts, #2 anonymous), #4 (anonymous, Submitted) and #10
 * (Awaiting Employee Response)). No role but the HR Administrator holds grievance-review, so the
 * reviewer is Su Myat Noe: she never proposes and is never assigned in these flows, because the
 * proposer may not decide and an active investigator may not decide.
 */

export const PASSWORD = 'Welcome@123';
export const SU_MYAT = 'sumyat.noe@apexhorizon.example';
export const YE_YINT = 'yeyint.naing@apexhorizon.example';
export const DANIEL = 'daniel.carter@apexhorizon.example';

/** A case number as the module writes it. */
export const CASE_NO = /GRV-\d{4}-\d{5}/;

export const nav = (page: Page) => page.getByRole('navigation', { name: 'Main navigation' });
export const rows = (page: Page) => page.locator('tr.mat-mdc-row');
export const dialog = (page: Page) => page.getByRole('dialog');
export const alertDialog = (page: Page) => page.getByRole('alertdialog');
export const toast = (page: Page, text: string | RegExp) =>
  page.locator('.app-toast').filter({ hasText: text });
/** A titled card (a section labelled by its heading). */
export const card = (page: Page, title: string) =>
  page.getByRole('region', { name: title, exact: true });

/** Signs in from the login page (a signed-out test). */
export async function signIn(page: Page, email: string) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/dashboard/);
}

/** Signs out and in as someone else, inside the app (no reload: the data stays). */
export async function signInAs(page: Page, email: string) {
  await page.getByRole('button', { name: 'User menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login/);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/dashboard/);
}

/** The links of the expanded Grievances group (not the flyout, not another group's "Reports"). */
export const grievanceLinks = (page: Page) => page.locator('#nav-group-grievances');

/** Opens a Grievances screen through the sidebar (keeps the in-memory data). */
export async function openGrievances(page: Page, link: string) {
  const group = nav(page).getByRole('button', { name: 'Grievances' });
  if ((await group.getAttribute('aria-expanded')) !== 'true') {
    await group.click();
  }
  await expect(group).toHaveAttribute('aria-expanded', 'true');
  await grievanceLinks(page).getByRole('link', { name: link, exact: true }).click();
}

/** Clicks a Material select at its arrow end (the floating label can sit over the centre). */
export async function openSelect(select: Locator) {
  const box = await select.boundingBox();
  if (!box) throw new Error('Select is not visible');
  await select.click({ position: { x: box.width - 22, y: box.height / 2 } });
}

/** Chooses an option of a labelled select, and waits for the list to close. */
export async function choose(
  scope: Locator | Page,
  page: Page,
  name: string,
  option: string | RegExp,
) {
  await openSelect(scope.getByRole('combobox', { name }));
  await page.getByRole('option', { name: option, exact: typeof option === 'string' }).click();
  await expect(page.getByRole('listbox')).toHaveCount(0);
}

/** A tiny PDF-looking file (the content check reads the `%PDF-` signature). Fictional bytes. */
export function pdf(name: string, size = 2048) {
  const head = Buffer.from('%PDF-1.4\n% fictional grievance evidence\n');
  return {
    name,
    mimeType: 'application/pdf',
    buffer: Buffer.concat([head, Buffer.alloc(Math.max(0, size - head.length), 0x20)]),
  };
}

/** The (hidden) file input of an attachment list. */
export const fileInput = (scope: Locator | Page) => scope.getByTestId('grievance-file-input');

/** The status pill of the summary bar (case team) or of "Your grievance" (the employee). */
export const caseStatus = (page: Page) =>
  page.getByLabel('Case summary').locator('app-status-badge');
export const myStatus = (page: Page) => card(page, 'Your grievance').locator('app-status-badge');

/** A command of the case actions bar. */
export const action = (page: Page, name: string) =>
  page.getByRole('group', { name: 'Case actions' }).getByRole('button', { name, exact: true });

/** Selects a tab of the case detail. */
export async function openTab(page: Page, name: string) {
  await page.getByRole('tab', { name, exact: true }).click();
  await expect(page.getByRole('tab', { name, exact: true })).toHaveAttribute(
    'aria-selected',
    'true',
  );
}

/**
 * Sessions written straight into storage (no sign-in screen), as the sample accounts get them in the
 * mock build: no permission list, so the grievance mock reads the role's grants. Fictional people.
 */
export const DANIEL_SESSION = {
  id: 11,
  employeeId: 5,
  displayName: 'Daniel Carter',
  email: DANIEL,
  jobTitle: '',
  roles: ['Employee'],
};
export const YE_YINT_SESSION = {
  id: 6,
  employeeId: 9,
  displayName: 'Ye Yint Naing',
  email: YE_YINT,
  jobTitle: '',
  roles: ['HR Officer'],
};

/** What the automated accessibility audit of a screen found (empty lists: nothing). */
export interface ScreenAudit {
  h1: number;
  unnamedControls: string[];
  unlabelledFields: string[];
  unlabelledImages: string[];
  overflow: boolean;
  lang: string;
}

/**
 * The checks of the quality sweep (`quality.spec.ts`) on the current screen: one h1, every visible
 * button and link named, every visible field labelled, images with alt text, no sideways scroll and
 * a page language. Automated and partial: not a substitute for a manual review.
 */
export function auditScreen(page: Page): Promise<ScreenAudit> {
  return page.evaluate(() => {
    const visible = (el: Element) => {
      const r = (el as HTMLElement).getBoundingClientRect();
      const s = getComputedStyle(el);
      return r.width > 0 && r.height > 0 && s.visibility !== 'hidden';
    };
    const textOf = (el: Element) => {
      const clone = el.cloneNode(true) as Element;
      clone
        .querySelectorAll('mat-icon, .mat-icon, [aria-hidden="true"]')
        .forEach((n) => n.remove());
      return (clone.textContent ?? '').trim();
    };
    const named = (el: Element) =>
      !!(
        el.getAttribute('aria-label') ||
        el.getAttribute('aria-labelledby') ||
        el.getAttribute('title') ||
        textOf(el)
      );
    const unnamedControls = [...document.querySelectorAll('button, a[href], [role="button"]')]
      .filter(visible)
      .filter((el) => !named(el))
      .map((el) => el.outerHTML.slice(0, 120));
    const unlabelledFields = [
      ...document.querySelectorAll('input:not([type=hidden]), select, textarea, [role=combobox]'),
    ]
      .filter(visible)
      .filter((el) => {
        const id = el.id;
        return !(
          el.getAttribute('aria-label') ||
          el.getAttribute('aria-labelledby') ||
          (id && document.querySelector(`label[for="${CSS.escape(id)}"]`)) ||
          el.closest('label')
        );
      })
      .map((el) => el.outerHTML.slice(0, 120));
    const unlabelledImages = [...document.querySelectorAll('img:not([alt])')].map((el) =>
      el.outerHTML.slice(0, 120),
    );
    return {
      h1: document.querySelectorAll('h1').length,
      unnamedControls,
      unlabelledFields,
      unlabelledImages,
      overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
      lang: document.documentElement.lang,
    };
  });
}
