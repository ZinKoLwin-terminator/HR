import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { PageHeaderComponent } from '../page-header/page-header.component';

/**
 * Honest placeholder for screens scheduled in a later development phase.
 * Route data: `{ title, breadcrumb, phase, description }`.
 */
@Component({
  selector: 'app-coming-soon',
  imports: [PageHeaderComponent, MatIconModule, MatButtonModule, RouterLink],
  template: `
    @let info = data();
    <app-page-header [title]="info.title" [description]="info.description" />
    <section class="placeholder" aria-labelledby="placeholder-title">
      <span class="placeholder__icon" aria-hidden="true"><mat-icon>construction</mat-icon></span>
      <h2 id="placeholder-title">Not built yet</h2>
      <p>
        This screen is scheduled for <strong>Phase {{ info.phase }}</strong> of the frontend build.
        Navigation is in place, but the screen itself is not implemented yet.
      </p>
      <a mat-stroked-button routerLink="/dashboard"
        ><mat-icon>space_dashboard</mat-icon>Back to dashboard</a
      >
    </section>
  `,
  styles: `
    .placeholder {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 8px;
      padding: 56px 24px;
      border-radius: var(--app-radius-lg);
      border: 1px dashed var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface-container-lowest);
    }
    .placeholder__icon {
      display: grid;
      place-items: center;
      width: 64px;
      height: 64px;
      border-radius: 50%;
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    h2 {
      margin: 8px 0 0;
      font: var(--mat-sys-title-large);
    }
    p {
      margin: 0 0 12px;
      max-width: 520px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ComingSoonComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly data = toSignal(
    this.route.data.pipe(
      map((d) => ({
        title: String(d['breadcrumb'] ?? 'Coming soon'),
        description: String(d['description'] ?? ''),
        phase: Number(d['phase'] ?? 0),
      })),
    ),
    { initialValue: { title: '', description: '', phase: 0 } },
  );
}
