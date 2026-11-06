import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RecruitmentSummary } from '../../../core/models/dashboard.models';

/** Hiring snapshot: headline numbers plus a pipeline funnel. */
@Component({
  selector: 'app-recruitment-summary-card',
  imports: [RouterLink],
  template: `
    <section class="card" aria-labelledby="recruitment-heading">
      <div class="card__header">
        <div>
          <h2 id="recruitment-heading">Recruitment summary</h2>
          <p>Open roles and candidate pipeline</p>
        </div>
        <a routerLink="/recruitment/pipeline" class="link-button">View pipeline</a>
      </div>
      <dl class="facts">
        <div>
          <dt>Open positions</dt>
          <dd>{{ data().openPositions }}</dd>
        </div>
        <div>
          <dt>Active candidates</dt>
          <dd>{{ data().activeCandidates }}</dd>
        </div>
        <div>
          <dt>Interviews</dt>
          <dd>{{ data().upcomingInterviews }}</dd>
        </div>
      </dl>
      <ul class="funnel" aria-label="Applications by stage">
        @for (f of data().funnel; track f.label) {
          <li>
            <span class="funnel__label">{{ f.label }}</span>
            <span class="funnel__bar" aria-hidden="true"
              ><span class="funnel__fill" [style.width.%]="width(f.count)"></span
            ></span>
            <span class="funnel__count num">{{ f.count }}</span>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .card {
      height: 100%;
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 8px;
      margin: 0 0 16px;
    }
    .facts > div {
      padding: 10px 12px;
      border-radius: 12px;
      background: color-mix(in srgb, var(--app-color-primary) 6%, transparent);
    }
    dt {
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 2px 0 0;
      font-size: 20px;
      font-weight: 700;
      letter-spacing: -0.02em;
      font-variant-numeric: tabular-nums;
    }
    .funnel {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .funnel li {
      display: grid;
      grid-template-columns: 76px minmax(0, 1fr) 28px;
      align-items: center;
      gap: 10px;
    }
    .funnel__label {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .funnel__bar {
      height: 8px;
      border-radius: 999px;
      background: var(--mat-sys-surface-container-high);
      overflow: hidden;
    }
    .funnel__fill {
      display: block;
      height: 100%;
      border-radius: inherit;
      background: linear-gradient(90deg, var(--app-color-primary-lighter), var(--app-color-accent));
    }
    .funnel__count {
      text-align: right;
      font: var(--mat-sys-label-large);
    }
  `,
})
export class RecruitmentSummaryCardComponent {
  readonly recruitment = input.required<RecruitmentSummary>();
  protected readonly data = this.recruitment;

  private readonly max = computed(() =>
    Math.max(1, ...this.recruitment().funnel.map((f) => f.count)),
  );

  protected width(count: number): number {
    return Math.round((count / this.max()) * 100);
  }
}
