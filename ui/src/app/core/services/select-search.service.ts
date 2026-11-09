import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Panels with fewer options than this don't get a search box. */
const MIN_OPTIONS_FOR_SEARCH = 8;

const SEARCH_ICON =
  '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path fill="currentColor" d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14"/></svg>';

/**
 * Adds a "search options" box to every long `mat-select` dropdown. Panels render in the
 * overlay outside the app root, so one observer covers all selects without touching each template.
 */
@Injectable({ providedIn: 'root' })
export class SelectSearchService {
  private readonly document = inject(DOCUMENT);

  constructor() {
    if (typeof MutationObserver === 'undefined') return;
    new MutationObserver((records) => {
      for (const record of records) {
        record.addedNodes.forEach((node) => {
          if (!(node instanceof HTMLElement)) return;
          const panels = node.matches('.mat-mdc-select-panel')
            ? [node]
            : Array.from(node.querySelectorAll<HTMLElement>('.mat-mdc-select-panel'));
          panels.forEach((panel) => requestAnimationFrame(() => this.attach(panel)));
        });
      }
    }).observe(this.document.body, { childList: true, subtree: true });
  }

  private attach(panel: HTMLElement): void {
    if (panel.querySelector('.app-select-search')) return;
    const options = () => Array.from(panel.querySelectorAll<HTMLElement>('.mat-mdc-option'));
    if (options().length < MIN_OPTIONS_FOR_SEARCH) return;

    const wrapper = this.document.createElement('div');
    wrapper.className = 'app-select-search';
    wrapper.innerHTML = SEARCH_ICON;
    const input = this.document.createElement('input');
    input.type = 'search';
    input.placeholder = 'Search…';
    input.autocomplete = 'off';
    input.setAttribute('aria-label', 'Search options');
    wrapper.appendChild(input);

    const empty = this.document.createElement('div');
    empty.className = 'app-select-search__empty';
    empty.textContent = 'No matches found';
    empty.hidden = true;

    const visible = () => options().filter((o) => o.style.display !== 'none');

    input.addEventListener('input', () => {
      const term = input.value.trim().toLowerCase();
      for (const option of options()) {
        const match = term === '' || (option.textContent ?? '').toLowerCase().includes(term);
        option.style.display = match ? '' : 'none';
      }
      empty.hidden = visible().length > 0;
    });

    // Keep typing, Space and arrows from reaching the select's own key handling.
    input.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' || event.key === 'Tab') return;
      event.stopPropagation();
      if (event.key === 'Enter') {
        event.preventDefault();
        visible()[0]?.click();
      } else if (event.key === 'ArrowDown') {
        event.preventDefault();
        visible()[0]?.focus();
      }
    });

    panel.prepend(wrapper);
    wrapper.after(empty);
    input.focus({ preventScroll: true });
  }
}
