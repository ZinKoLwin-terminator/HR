import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { SERVER_SESSION } from '../../../core/services/auth.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { MyGrievanceDataService } from '../../../core/services/data/grievance-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { RelativeTimePipe } from '../../../shared/pipes/relative-time.pipe';
import { GrievanceStatusBadgeComponent } from '../shared/grievance-status-badge.component';

/**
 * The employee's grievance overview: counts of their own cases, how many have an update they have
 * not opened, the cases updated most recently, and a "New grievance" button (with
 * `grievance:Create`). Figures come from the server; nothing is computed here.
 */
@Component({
  selector: 'app-grievance-overview',
  imports: [
    EmptyStateComponent,
    GrievanceStatusBadgeComponent,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    RelativeTimePipe,
    RouterLink,
    StatCardComponent,
  ],
  template: `
    <app-page-header
      title="Grievances"
      description="Raise a workplace concern with HR, follow its progress and answer HR's questions."
    >
      @if (canCreate()) {
        <a pageActions mat-flat-button routerLink="/grievances/my/new">
          <mat-icon>add</mat-icon>New grievance
        </a>
      }
    </app-page-header>

    @if (!apiMode) {
      <p class="notice" role="note">
        <mat-icon aria-hidden="true">info</mat-icon>
        Simulation only — this demo stores grievances in your browser and enforces nothing,
        including anonymity.
      </p>
    }

    @if (!linked()) {
      <app-empty-state
        icon="person_off"
        title="No employee record is linked to your account"
        message="Grievances are raised by employees. Ask HR to link your user account to your employee record."
      />
    } @else if (dashboard.error() && !dashboard.hasValue()) {
      <app-empty-state
        icon="error"
        title="Your grievances could not be loaded"
        [message]="errorText()"
      >
        <button mat-flat-button type="button" (click)="dashboard.reload()">Try again</button>
      </app-empty-state>
    } @else if (dashboard.hasValue() ? dashboard.value() : undefined; as d) {
      <section class="cards" aria-label="My grievance counts">
        <app-stat-card
          label="Updates to read"
          [value]="d.unreadUpdates"
          icon="mark_email_unread"
          [tone]="d.unreadUpdates > 0 ? 'warning' : 'neutral'"
          link="/grievances/my"
        />
        <app-stat-card
          label="Waiting for your answer"
          [value]="d.awaitingMyResponse"
          icon="help"
          [tone]="d.awaitingMyResponse > 0 ? 'danger' : 'neutral'"
        />
        <app-stat-card label="Open" [value]="d.open" icon="pending_actions" />
        <app-stat-card label="Drafts" [value]="d.drafts" icon="edit_note" tone="neutral" />
        <app-stat-card label="Resolved" [value]="d.resolved" icon="task_alt" tone="success" />
        <app-stat-card
          label="Closed or rejected"
          [value]="d.closed + d.rejected"
          icon="inventory_2"
          tone="neutral"
        />
      </section>

      <section class="recent" aria-labelledby="recent-title">
        <div class="recent__head">
          <h2 id="recent-title">Recently updated</h2>
          <a mat-button routerLink="/grievances/my">See all my grievances</a>
        </div>
        @if (d.recent.length === 0) {
          <app-empty-state
            icon="forum"
            title="You have not raised a grievance"
            message="If something at work is not right, you can tell HR here, with your name or anonymously where the category allows it."
          />
        } @else {
          <ul class="list">
            @for (g of d.recent; track g.id) {
              <li>
                <a class="item" [routerLink]="['/grievances/my', g.id]">
                  <span class="item__main">
                    <span class="item__subject">
                      @if (g.hasUnreadUpdate) {
                        <span class="unread" title="New update"></span>
                        <span class="cdk-visually-hidden">New update:</span>
                      }
                      {{ g.subject }}
                    </span>
                    <span class="item__meta">
                      {{ g.caseNo ?? 'Draft' }} · {{ g.categoryName }}
                      @if (g.isAnonymous) {
                        · Anonymous
                      }
                      · updated {{ g.lastUpdateAtUtc ?? g.createdAtUtc | relativeTime }}
                    </span>
                  </span>
                  <app-grievance-status-badge [status]="g.status" />
                </a>
              </li>
            }
          </ul>
        }
      </section>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading your grievances" />
    }
  `,
  styles: `
    .notice {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
      font: var(--mat-sys-body-medium);
    }
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 24px;
    }
    .recent__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .recent__head h2 {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }
    .list {
      margin: 8px 0 0;
      padding: 0;
      list-style: none;
    }
    .item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 10px 12px;
      border-radius: var(--app-radius-md);
      color: inherit;
      text-decoration: none;
    }
    .item:hover,
    .item:focus-visible {
      background: var(--mat-sys-surface-container);
    }
    .item__main {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .item__subject {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .item__meta {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .unread {
      flex: none;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--mat-sys-error);
    }
  `,
})
export class GrievanceOverviewComponent {
  private readonly grievances = inject(MyGrievanceDataService);
  private readonly user = inject(CurrentUserService);
  private readonly permissions = inject(PermissionService);
  protected readonly apiMode = inject(SERVER_SESSION);

  protected readonly linked = computed(() => {
    const id = this.user.current().employeeId;
    return id !== null && id > 0;
  });
  protected readonly canCreate = computed(
    () => this.linked() && this.permissions.can('grievance:Create'),
  );

  protected readonly dashboard = rxResource({
    params: () => (this.linked() ? {} : undefined),
    stream: () => this.grievances.dashboard(),
  });
  protected readonly errorText = computed(() =>
    this.dashboard.error() ? errorMessage(this.dashboard.error()) : '',
  );
}
