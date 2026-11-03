import { Routes } from '@angular/router';

export interface PlaceholderPage {
  path: string;
  label: string;
  description: string;
}

/** Builds "coming soon" child routes for screens planned in a later phase. */
export function placeholderRoutes(phase: number, pages: PlaceholderPage[]): Routes {
  return pages.map((page) => ({
    path: page.path,
    title: page.label,
    data: { breadcrumb: page.label, description: page.description, phase },
    loadComponent: () =>
      import('../components/coming-soon/coming-soon.component').then((m) => m.ComingSoonComponent),
  }));
}
