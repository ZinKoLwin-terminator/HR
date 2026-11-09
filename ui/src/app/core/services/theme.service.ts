import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type ColorScheme = 'light' | 'dark';

export interface AccentTheme {
  id: string;
  label: string;
  /** Swatch preview only; the real colors come from the Material palette in styles.scss. */
  swatch: string;
}

export const ACCENT_THEMES: readonly AccentTheme[] = [
  { id: 'azure', label: 'Azure', swatch: '#1a73e8' },
  { id: 'violet', label: 'Violet', swatch: '#7c4dff' },
  { id: 'emerald', label: 'Emerald', swatch: '#1b9a5a' },
  { id: 'rose', label: 'Rose', swatch: '#e11d63' },
  { id: 'amber', label: 'Amber', swatch: '#f08a00' },
  { id: 'cyan', label: 'Cyan', swatch: '#0e9bb5' },
];

const ACCENT_KEY = 'hrms.accent';
const SCHEME_KEY = 'hrms.scheme';

/**
 * Light / dark mode plus an accent color theme. The M3 theme uses `color-scheme`, so one
 * property switches light/dark; the accent is a `theme-<id>` class on <html>. Both choices
 * are remembered in localStorage (a per-browser convenience; works without it).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  readonly accents = ACCENT_THEMES;
  readonly scheme = signal<ColorScheme>(this.initialScheme());
  readonly accent = signal<string>(this.initialAccent());

  constructor() {
    effect(() => {
      const root = this.document.documentElement;
      root.style.colorScheme = this.scheme();
      root.classList.toggle('dark-theme', this.scheme() === 'dark');
      this.store(SCHEME_KEY, this.scheme());
    });
    effect(() => {
      const root = this.document.documentElement;
      for (const a of ACCENT_THEMES) root.classList.remove(`theme-${a.id}`);
      root.classList.add(`theme-${this.accent()}`);
      this.store(ACCENT_KEY, this.accent());
    });
  }

  toggle(): void {
    this.scheme.update((s) => (s === 'dark' ? 'light' : 'dark'));
  }

  setAccent(id: string): void {
    if (ACCENT_THEMES.some((a) => a.id === id)) this.accent.set(id);
  }

  private initialScheme(): ColorScheme {
    const saved = this.read(SCHEME_KEY);
    if (saved === 'light' || saved === 'dark') return saved;
    return this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light';
  }

  private initialAccent(): string {
    const saved = this.read(ACCENT_KEY);
    return ACCENT_THEMES.some((a) => a.id === saved) ? saved! : 'azure';
  }

  private read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private store(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Storage unavailable: the choice lasts for this visit only.
    }
  }
}
