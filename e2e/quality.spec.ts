import { NAVIGATION, NavItem } from '../src/app/core/constants/navigation';
import { expect, test } from './fixtures';

const routes = (items: NavItem[]): string[] =>
  items.flatMap((i) => [...(i.route ? [i.route] : []), ...routes(i.children ?? [])]);

/** Every sidebar screen. */
const ROUTES = routes(NAVIGATION);

/**
 * Cross-cutting quality sweep over every screen: no runtime errors, one h1, every control has an
 * accessible name, every form field is labelled and the page never scrolls sideways.
 * (Automated checks only — not a substitute for a manual accessibility review.)
 */
for (const route of ROUTES) {
  test(`quality: ${route}`, async ({ page }) => {
    const problems: string[] = [];
    page.on('pageerror', (e) => problems.push(`pageerror: ${e.message}`));
    page.on('console', (m) => {
      if (m.type() === 'error' && !/fonts\.(googleapis|gstatic)|net::ERR_FAILED/.test(m.text())) {
        problems.push(`console: ${m.text()}`);
      }
    });

    await page.goto(route);
    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible();
    await expect(page.getByRole('progressbar')).toHaveCount(0, { timeout: 10_000 });

    const report = await page.evaluate(() => {
      const visible = (el: Element) => {
        const r = (el as HTMLElement).getBoundingClientRect();
        const s = getComputedStyle(el);
        return r.width > 0 && r.height > 0 && s.visibility !== 'hidden';
      };
      /** Text of an element ignoring icon ligatures. */
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

    expect(report.h1, 'exactly one h1').toBe(1);
    expect(report.unnamedControls, 'controls without an accessible name').toEqual([]);
    expect(report.unlabelledFields, 'form fields without a label').toEqual([]);
    expect(report.unlabelledImages, 'images without alt').toEqual([]);
    expect(report.overflow, 'page scrolls sideways').toBe(false);
    expect(report.lang).not.toBe('');
    expect(problems).toEqual([]);
  });
}
