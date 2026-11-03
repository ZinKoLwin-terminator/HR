import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type ColorScheme = 'light' | 'dark';

/** Light / dark mode. The M3 theme uses `color-scheme`, so one property switches all tokens. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  readonly scheme = signal<ColorScheme>(
    this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light',
  );

  constructor() {
    effect(() => {
      const root = this.document.documentElement;
      root.style.colorScheme = this.scheme();
      root.classList.toggle('dark-theme', this.scheme() === 'dark');
    });
  }

  toggle(): void {
    this.scheme.update((s) => (s === 'dark' ? 'light' : 'dark'));
  }
}
