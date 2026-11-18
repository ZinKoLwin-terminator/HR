import { Page } from '@playwright/test';
import { ADMIN_SESSION, expect, signedOutTest as test } from './fixtures';
import {
  DANIEL_SESSION,
  YE_YINT_SESSION,
  action,
  auditScreen,
  choose,
  dialog,
} from './grievance-helpers';

/**
 * Accessibility sweep of the grievance screens that are NOT in the sidebar (the sidebar ones are in
 * `quality.spec.ts`): forms, details in each workspace with every tab opened, and a command dialog.
 * The same automated checks as the quality sweep (one h1, named controls, labelled fields, alt text,
 * no sideways scroll, page language, no console or runtime errors), on desktop and on a phone. The
 * suite has no axe-core: this is automated and partial, not a substitute for a manual review with a
 * screen reader and contrast tooling. Mock `e2e` build, sample data, fictional people.
 */

type Who = 'employee' | 'investigator' | 'hr';
const SESSIONS: Record<Who, object> = {
  employee: DANIEL_SESSION,
  investigator: YE_YINT_SESSION,
  hr: ADMIN_SESSION,
};

interface Screen {
  who: Who;
  route: string;
  /** Tabs to open one by one (the details); each is audited. */
  tabs?: string[];
  /** Extra state to audit (a switch turned on, a dialog opened). */
  then?: (page: Page) => Promise<void>;
}

const SCREENS: Screen[] = [
  {
    who: 'employee',
    route: '/grievances/my/new',
    then: async (page) => {
      // The anonymous variant of the form (another layout: no name fields, a file warning).
      await choose(page, page, 'Category', 'Workplace Harassment');
      await page.getByRole('switch', { name: 'Report anonymously' }).click();
      await expect(page.getByText('Files can identify you.', { exact: false })).toBeVisible();
    },
  },
  { who: 'employee', route: '/grievances/my/2/edit' },
  { who: 'employee', route: '/grievances/my/10' },
  { who: 'employee', route: '/grievances/my/4' },
  {
    who: 'hr',
    route: '/grievances/cases/9',
    tabs: [
      'Messages',
      'Investigation notes',
      'Proposal and escalations',
      'Files',
      'Corrective actions',
      'History',
      'Access log',
    ],
  },
  {
    who: 'hr',
    route: '/grievances/cases/3',
    then: async (page) => {
      await action(page, 'Acknowledge').click();
      await expect(dialog(page).getByLabel('Target resolution date')).toBeVisible();
    },
  },
  {
    who: 'hr',
    route: '/grievances/reviews/11',
    tabs: ['Findings', 'Proposal and escalations', 'History'],
  },
  { who: 'hr', route: '/grievances/access-log' },
  {
    who: 'investigator',
    route: '/grievances/investigations/10',
    tabs: ['Messages', 'Investigation notes', 'Proposal and escalations', 'Files', 'History'],
  },
];

async function expectClean(page: Page, where: string) {
  const report = await auditScreen(page);
  expect(report.h1, `${where}: exactly one h1`).toBe(1);
  expect(report.unnamedControls, `${where}: controls without an accessible name`).toEqual([]);
  expect(report.unlabelledFields, `${where}: form fields without a label`).toEqual([]);
  expect(report.unlabelledImages, `${where}: images without alt`).toEqual([]);
  expect(report.overflow, `${where}: page scrolls sideways`).toBe(false);
  expect(report.lang).not.toBe('');
}

for (const screen of SCREENS) {
  test(`grievance quality: ${screen.route} (${screen.who})`, async ({ page }) => {
    const problems: string[] = [];
    page.on('pageerror', (e) => problems.push(`pageerror: ${e.message}`));
    page.on('console', (m) => {
      if (m.type() === 'error' && !/fonts\.(googleapis|gstatic)|net::ERR_FAILED/.test(m.text())) {
        problems.push(`console: ${m.text().split('\n')[0]}`);
      }
    });
    await page.route(/fonts\.(googleapis|gstatic)\.com/, (route) => route.abort());
    await page.addInitScript((session) => {
      if (!sessionStorage.getItem('hrms.session')) {
        sessionStorage.setItem('hrms.session', JSON.stringify(session));
      }
    }, SESSIONS[screen.who]);

    await page.goto(screen.route);
    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible();
    await expect(page.getByRole('progressbar')).toHaveCount(0, { timeout: 10_000 });
    await expectClean(page, screen.route);

    for (const tab of screen.tabs ?? []) {
      await page.getByRole('tab', { name: tab, exact: true }).click();
      await expect(page.getByRole('tab', { name: tab, exact: true })).toHaveAttribute(
        'aria-selected',
        'true',
      );
      await expect(page.getByRole('progressbar')).toHaveCount(0, { timeout: 10_000 });
      await expectClean(page, `${screen.route} › ${tab}`);
    }

    if (screen.then) {
      await screen.then(page);
      await expectClean(page, `${screen.route} (after interaction)`);
    }
    expect(problems).toEqual([]);
  });
}
