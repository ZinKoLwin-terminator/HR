import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

export interface Breadcrumb {
  label: string;
  url: string;
}

/** Builds breadcrumbs from `data.breadcrumb` on each matched route. */
export function buildBreadcrumbs(root: ActivatedRouteSnapshot): Breadcrumb[] {
  const crumbs: Breadcrumb[] = [];
  let url = '';
  let route: ActivatedRouteSnapshot | null = root;
  while (route) {
    const segment = route.url.map((s) => s.path).join('/');
    if (segment) {
      url += `/${segment}`;
    }
    const label = route.data['breadcrumb'] as string | undefined;
    if (label && crumbs.at(-1)?.label !== label) {
      crumbs.push({ label, url: url || '/' });
    }
    route = route.firstChild;
  }
  return crumbs;
}

@Component({
  selector: 'app-breadcrumbs',
  imports: [RouterLink, MatIconModule],
  template: `
    <nav aria-label="Breadcrumb" class="breadcrumbs">
      <ol>
        <li>
          <a routerLink="/dashboard" aria-label="Home"><mat-icon class="home">home</mat-icon></a>
        </li>
        @for (crumb of crumbs(); track crumb.url; let last = $last) {
          <li>
            <mat-icon class="sep" aria-hidden="true">chevron_right</mat-icon>
            @if (last) {
              <span aria-current="page">{{ crumb.label }}</span>
            } @else {
              <a [routerLink]="crumb.url">{{ crumb.label }}</a>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: `
    .breadcrumbs ol {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      list-style: none;
      margin: 0 0 12px;
      padding: 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    li {
      display: inline-flex;
      align-items: center;
    }
    a {
      display: inline-flex;
      align-items: center;
      color: inherit;
      text-decoration: none;
      border-radius: 4px;
    }
    a:hover {
      color: var(--mat-sys-primary);
      text-decoration: underline;
    }
    [aria-current='page'] {
      color: var(--mat-sys-on-surface);
      font-weight: 500;
    }
    .home {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .sep {
      font-size: 18px;
      width: 18px;
      height: 18px;
      margin: 0 2px;
      opacity: 0.6;
    }
  `,
})
export class BreadcrumbsComponent {
  private readonly router = inject(Router);

  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => buildBreadcrumbs(this.router.routerState.snapshot.root)),
    ),
    { initialValue: [] as Breadcrumb[] },
  );
}
