import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { HistoryEventType } from '../../../../core/models/employee-extras.models';
import { EmploymentHistoryDataService } from '../../../../core/services/data/employee-extras-data.services';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

const ICONS: Record<HistoryEventType, string> = {
  Joined: 'login',
  Confirmation: 'verified',
  Promotion: 'trending_up',
  Transfer: 'swap_horiz',
  'Salary Revision': 'payments',
  'Contract Change': 'description',
  'Status Change': 'sync_alt',
  Exit: 'logout',
};

@Component({
  selector: 'app-profile-history-tab',
  imports: [DatePipe, MatIconModule, MatProgressBarModule, EmptyStateComponent],
  template: `
    <section class="card" aria-labelledby="hist-heading">
      <div class="card__header">
        <div>
          <h2 id="hist-heading">Employment history</h2>
          <p>Newest first. Changes made in the app are added automatically.</p>
        </div>
      </div>
      @if (history.isLoading() && !entries().length) {
        <mat-progress-bar mode="indeterminate" aria-label="Loading history" />
      } @else if (entries().length) {
        <ol class="timeline">
          @for (entry of entries(); track entry.id) {
            <li>
              <span class="timeline__icon" aria-hidden="true"
                ><mat-icon>{{ icon(entry.type) }}</mat-icon></span
              >
              <div class="timeline__body">
                <div class="timeline__head">
                  <span class="timeline__title">{{ entry.title }}</span>
                  <span class="timeline__type">{{ entry.type }}</span>
                </div>
                <p>{{ entry.details }}</p>
                <span class="muted"
                  >{{ entry.date | date }} · recorded by {{ entry.recordedBy }}</span
                >
              </div>
            </li>
          }
        </ol>
      } @else {
        <app-empty-state icon="history" title="No history yet" />
      }
    </section>
  `,
  styles: `
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .timeline li {
      position: relative;
      display: flex;
      gap: 16px;
      padding-bottom: 20px;
    }
    .timeline li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 19px;
      top: 40px;
      bottom: 0;
      width: 2px;
      background: var(--mat-sys-outline-variant);
    }
    .timeline__icon {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      flex: none;
      border-radius: 50%;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .timeline__body {
      min-width: 0;
    }
    .timeline__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .timeline__title {
      font: var(--mat-sys-title-small);
    }
    .timeline__type {
      padding: 1px 8px;
      border-radius: 999px;
      font: var(--mat-sys-label-small);
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    p {
      margin: 2px 0 4px;
    }
  `,
})
export class ProfileHistoryTabComponent {
  readonly employeeId = input.required<number>();
  private readonly historyApi = inject(EmploymentHistoryDataService);

  protected readonly history = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      this.historyApi.query({
        pageIndex: 0,
        pageSize: 200,
        filters: { employeeId: params.id },
        sort: { active: 'date', direction: 'desc' },
      }),
  });
  protected readonly entries = computed(() => this.history.value()?.items ?? []);

  protected icon(type: HistoryEventType): string {
    return ICONS[type];
  }
}
