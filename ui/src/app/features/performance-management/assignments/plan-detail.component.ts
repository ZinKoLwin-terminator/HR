import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, filter, finalize, map, of, switchMap, tap } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import { fullName } from '../../../core/models/employee.models';
import {
  AssignKpiResult,
  ScorecardDetail,
  ScorecardItem,
  ScorecardItemUpdate,
} from '../../../core/models/kpi.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { KPI_WEIGHT_TOTAL, sumKpiWeights } from '../../../core/utilities/kpi-calculation';
import {
  EntityFormDialogData,
  FieldConfig,
  FormValues,
} from '../../../shared/components/crud/crud.models';
import { EntityFormDialogComponent } from '../../../shared/components/crud/entity-form-dialog.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { assignmentMessage } from '../shared/assignment-rows';
import { ReviewHistoryComponent } from '../shared/review-history.component';
import { ScorecardItemsTableComponent } from '../shared/scorecard-items-table.component';
import { AssignKpisDialogComponent, AssignKpisDialogData } from './assign-kpis-dialog.component';
import { safeValue } from '../../../shared/utilities/resource-value';

/** The plan can be finalized only when the weights add up to exactly 100%. */
export const canFinalizePlan = (items: readonly Pick<ScorecardItem, 'weight'>[]): boolean =>
  items.length > 0 && sumKpiWeights(items.map((i) => i.weight)) === KPI_WEIGHT_TOTAL;

/** A plan can still be changed (KPIs edited, added, finalized) in a Draft or Active period. */
export const periodAcceptsPlanChanges = (card: Pick<ScorecardDetail, 'periodStatus'>): boolean =>
  card.periodStatus === 'Draft' || card.periodStatus === 'Active';

/** What an item edit sends, from the form values and the item's own `rowVersion`. */
export function itemUpdateFrom(values: FormValues, item: ScorecardItem): ScorecardItemUpdate {
  const sortOrder = values['sortOrder'];
  return {
    target: Number(values['target']),
    weight: Number(values['weight']),
    startDate: String(values['startDate']),
    endDate: String(values['endDate']),
    sortOrder:
      sortOrder === null || sortOrder === undefined || sortOrder === ''
        ? undefined
        : Number(sortOrder),
    rowVersion: item.rowVersion,
  };
}

/** The item edit form. `others` is the weight of the other KPIs of the plan. */
export function itemFields(item: ScorecardItem, others: number): FieldConfig[] {
  const room = Math.max(0, sumKpiWeights([KPI_WEIGHT_TOTAL, -others]));
  return [
    {
      key: 'target',
      label: 'Target',
      type: 'number',
      required: true,
      min: 0,
      hint: item.unit ? `In ${item.unit}` : undefined,
    },
    {
      key: 'weight',
      label: 'Weight (%)',
      type: 'number',
      required: true,
      min: 0.01,
      max: KPI_WEIGHT_TOTAL,
      hint: `At most ${room}%: the other KPIs add up to ${others}%`,
      rule: (v) => {
        const weight = Number(v['weight']);
        if (v['weight'] === null || v['weight'] === '' || !Number.isFinite(weight)) return null;
        const total = sumKpiWeights([others, weight]);
        return total > KPI_WEIGHT_TOTAL
          ? `The KPI weights would add up to ${total}%; they cannot exceed 100%.`
          : null;
      },
    },
    { key: 'startDate', label: 'Start date', type: 'date', required: true },
    {
      key: 'endDate',
      label: 'End date',
      type: 'date',
      required: true,
      hint: 'Within the evaluation period',
      rule: (v) =>
        v['startDate'] && v['endDate'] && String(v['endDate']) < String(v['startDate'])
          ? 'Must be on or after the start date.'
          : null,
    },
    {
      key: 'sortOrder',
      label: 'Sort order',
      type: 'number',
      min: 0,
      max: 32767,
      hint: 'Lower numbers come first',
    },
  ];
}

/**
 * One scorecard's plan: header, KPIs and the review history. While the scorecard is in Planning HR
 * can edit or remove a KPI, add KPIs, finalize the plan (only at exactly 100% weight) or delete the
 * scorecard. After Planning the page is read-only. Every write sends the `rowVersion` of the latest
 * answer (the actions wait while a write or a reload runs); a conflict shows the server's message
 * with a Reload button.
 */
@Component({
  selector: 'app-plan-detail',
  imports: [
    AppDatePipe,
    DecimalPipe,
    EmptyStateComponent,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    ReviewHistoryComponent,
    RouterLink,
    ScorecardItemsTableComponent,
    StatusBadgeComponent,
  ],
  template: `
    @if (notFound()) {
      <app-empty-state
        icon="event_busy"
        title="Scorecard not found"
        message="It may have been deleted, or it is not one you can see."
      >
        <a mat-flat-button routerLink="/performance/assignments">Back to assignments</a>
      </app-empty-state>
    } @else if (detail.error() && !card()) {
      <app-empty-state icon="error" title="The plan could not be loaded" [message]="loadError()">
        <button mat-flat-button type="button" (click)="reload()">Try again</button>
      </app-empty-state>
    } @else if (card(); as c) {
      <app-page-header
        [title]="'KPI plan · ' + c.employeeCode + ' ' + c.employeeName"
        [description]="c.periodName + ' · ' + c.departmentName"
      >
        <ng-container pageActions>
          <a mat-stroked-button routerLink="/performance/assignments">
            <mat-icon>arrow_back</mat-icon>All assignments
          </a>
          @if (editable()) {
            @if (canCreate() && planOpen()) {
              <button mat-stroked-button type="button" [disabled]="busy()" (click)="addKpis(c)">
                <mat-icon>add</mat-icon>Add KPIs
              </button>
            }
            @if (canDelete()) {
              <button
                mat-stroked-button
                type="button"
                class="danger-outline"
                [disabled]="busy()"
                (click)="deleteCard(c)"
              >
                <mat-icon>delete</mat-icon>Delete scorecard
              </button>
            }
            @if (canEdit() && planOpen()) {
              <button
                mat-flat-button
                type="button"
                [disabled]="busy() || !finalizable()"
                [attr.aria-describedby]="finalizable() ? null : 'finalize-hint'"
                (click)="finalize(c)"
              >
                <mat-icon>task_alt</mat-icon>Finalize plan
              </button>
            }
          }
        </ng-container>
      </app-page-header>

      @if (detail.isLoading() || busy()) {
        <mat-progress-bar mode="indeterminate" aria-label="Updating the plan" />
      }

      @if (conflict()) {
        <div class="banner banner--error" role="alert">
          <mat-icon aria-hidden="true">sync_problem</mat-icon>
          <span>{{ conflict() }}</span>
          <button mat-stroked-button type="button" (click)="reload()">Reload</button>
        </div>
      }

      @if (!editable()) {
        <div class="banner" role="status">
          <mat-icon aria-hidden="true">lock</mat-icon>
          <span>
            This scorecard is {{ c.status }}, so its plan is read-only. KPIs can only be changed
            while the scorecard is in Planning.
          </span>
        </div>
      } @else if (!planOpen()) {
        <div class="banner" role="status">
          <mat-icon aria-hidden="true">lock</mat-icon>
          <span>
            Period {{ c.periodName }} is {{ c.periodStatus }}: its KPIs can no longer be changed and
            the plan cannot be finalized. The scorecard can still be deleted.
          </span>
        </div>
      } @else if (!finalizable() && canEdit()) {
        <p id="finalize-hint" class="hint">
          <mat-icon aria-hidden="true">info</mat-icon>
          Finalize plan is available when the KPI weights add up to exactly 100%. They add up to
          {{ total() | number: '1.0-2' }}% now.
        </p>
      }

      <section class="card" aria-label="Scorecard summary">
        <dl class="facts">
          <div>
            <dt>Employee</dt>
            <dd>{{ c.employeeCode }} · {{ c.employeeName }}</dd>
          </div>
          <div>
            <dt>Period</dt>
            <dd>{{ c.periodName }} <app-status-badge [status]="c.periodStatus" /></dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd><app-status-badge [status]="c.status" /></dd>
          </div>
          <div>
            <dt>Reviewer</dt>
            <dd data-testid="plan-reviewer">
              @if (c.reviewerName) {
                {{ c.reviewerName }}
              } @else if (!editable()) {
                <span class="muted">None: only HR can review</span>
              } @else if (managerName(); as manager) {
                {{ manager }}
                <span class="muted">(current manager; fixed when the plan is finalized)</span>
              } @else {
                <span class="muted">Set when the plan is finalized</span>
              }
            </dd>
          </div>
          <div>
            <dt>Plan finalized</dt>
            <dd>{{ c.planFinalizedOn ? (c.planFinalizedOn | appDate) : '—' }}</dd>
          </div>
          <div>
            <dt>Total weight</dt>
            <dd [class.off]="!finalizable()" data-testid="plan-total">
              {{ total() | number: '1.0-2' }}%
              @if (!finalizable()) {
                <span class="muted">(must be 100% to finalize)</span>
              }
            </dd>
          </div>
          @if (c.overallScore !== null) {
            <div>
              <dt>Overall score</dt>
              <dd>
                {{ c.overallScore | number: '1.2-2' }}
                @if (c.ratingLabel) {
                  <span class="muted">{{ c.ratingLabel }}</span>
                }
              </dd>
            </div>
          }
        </dl>
      </section>

      <h2 class="section">KPIs</h2>
      <app-scorecard-items-table
        caption="KPIs on the plan"
        [items]="c.items"
        [editable]="editable() && planOpen() && canEdit()"
        [disabled]="busy()"
        [showResults]="!editable()"
        (edit)="editItem(c, $event)"
        (remove)="removeItem($event)"
      />

      <h2 class="section">Review history</h2>
      <app-review-history [history]="c.history" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the plan" />
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .danger-outline {
      color: var(--mat-sys-error);
    }
    .banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      margin-bottom: 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .banner--error {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .banner span {
      flex: 1 1 220px;
    }
    .hint {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 16px;
      color: var(--mat-sys-on-surface-variant);
    }
    .card {
      margin-bottom: 8px;
      padding: 16px 20px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 14px 24px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
    dd {
      margin: 2px 0 0;
      font: var(--mat-sys-body-large);
    }
    dd.off {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      font-weight: 400;
    }
    .section {
      margin: 24px 0 10px;
      font: var(--mat-sys-title-medium);
    }
  `,
})
export class PlanDetailComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();

  private readonly scorecards = inject(ScorecardDataService);
  private readonly employees = inject(EmployeeDataService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);

  /** UX only (the server enforces): add needs Create, edit/remove/finalize Edit, delete Delete. */
  protected readonly canCreate = computed(() => this.permissions.can('kpi-setup:Create'));
  protected readonly canEdit = computed(() => this.permissions.can('kpi-setup:Edit'));
  protected readonly canDelete = computed(() => this.permissions.can('kpi-setup:Delete'));

  /** Set when a write answers 409 (a stale `rowVersion` or a status that moved on). */
  protected readonly conflict = signal('');
  /** A write is running: the actions wait, so the next write uses the version it answers with. */
  private readonly writing = signal(false);

  protected readonly detail = rxResource({
    params: () => Number(this.id()),
    stream: ({ params }) => this.scorecards.getById(params),
  });
  protected readonly card = computed<ScorecardDetail | null>(() => safeValue(this.detail) ?? null);
  protected readonly busy = computed(() => this.writing() || this.detail.isLoading());
  protected readonly notFound = computed(() => {
    const error = this.detail.error();
    const cause = error instanceof Error ? error.cause : error;
    return isAppError(cause) && cause.code === 'NotFound';
  });
  protected readonly loadError = computed(() =>
    this.detail.error() ? errorMessage(this.detail.error()) : '',
  );

  /** The plan can be changed only while the scorecard is in Planning. */
  protected readonly editable = computed(() => this.card()?.status === 'Planning');
  /** ... and, for everything but deleting it, while the period is Draft or Active. */
  protected readonly planOpen = computed(() => {
    const card = this.card();
    return !!card && periodAcceptsPlanChanges(card);
  });
  protected readonly total = computed(() =>
    sumKpiWeights(this.card()?.items.map((i) => i.weight) ?? []),
  );
  protected readonly finalizable = computed(() => canFinalizePlan(this.card()?.items ?? []));

  /**
   * The employee's current manager: who will review the scorecard once the plan is finalized.
   * Undefined while unknown (no employee access); null when the employee has no manager.
   */
  private readonly manager = rxResource({
    // Only roles that can read employees ask (others would get a 403): the name stays unknown.
    params: () =>
      this.editable() && this.permissions.can('employees:View')
        ? this.card()?.employeeId
        : undefined,
    stream: ({ params }) =>
      this.employees.getById(params).pipe(
        switchMap((employee) =>
          employee.managerId === null
            ? of<string | null>(null)
            : this.employees.getById(employee.managerId).pipe(map((m) => fullName(m))),
        ),
        catchError(() => of<string | null | undefined>(undefined)),
      ),
  });
  protected readonly managerName = computed(() => safeValue(this.manager) ?? null);

  protected reload(): void {
    this.conflict.set('');
    this.detail.reload();
  }

  /** Runs one write; the actions stay disabled until it answers. */
  private write<T>(call: Observable<T>): Observable<T> {
    this.writing.set(true);
    return call.pipe(finalize(() => this.writing.set(false)));
  }

  /** A conflict shows the server's message and offers Reload; anything else is a toast. */
  private fail(error: unknown): void {
    if (isAppError(error) && error.code === 'Conflict') {
      this.conflict.set(error.message);
    } else {
      this.toast.error(error);
    }
  }

  /** After a write: say so and read the plan again (new totals and row versions). */
  private done(message: string): void {
    this.conflict.set('');
    this.toast.success(message);
    this.detail.reload();
  }

  protected addKpis(card: ScorecardDetail): void {
    this.dialog
      .open<AssignKpisDialogComponent, AssignKpisDialogData, AssignKpiResult>(
        AssignKpisDialogComponent,
        {
          data: { scorecard: card },
          width: '820px',
          maxWidth: '96vw',
          disableClose: true,
        },
      )
      .afterClosed()
      .pipe(filter((r): r is AssignKpiResult => !!r))
      .subscribe((result) => {
        this.conflict.set('');
        this.toast.success(assignmentMessage(result));
        // The answer is the whole scorecard with its new row version.
        this.detail.set(result.scorecard);
      });
  }

  protected editItem(card: ScorecardDetail, item: ScorecardItem): void {
    const others = sumKpiWeights(card.items.filter((i) => i.id !== item.id).map((i) => i.weight));
    const data: EntityFormDialogData<ScorecardItem> = {
      title: `Edit ${item.kpiCode} · ${item.kpiName}`,
      notice: `${item.direction} · ${item.scoringMethod}. The scoring rule was copied from the template and cannot be changed here.`,
      fields: itemFields(item, others),
      value: { ...item } as unknown as FormValues,
      id: item.id,
      submitLabel: 'Save changes',
      save: (values) =>
        this.write(this.scorecards.updateItem(item.id, itemUpdateFrom(values, item))).pipe(
          tap({
            error: (e: unknown) => {
              if (isAppError(e) && e.code === 'Conflict') this.conflict.set(e.message);
            },
          }),
        ),
    };
    this.dialog
      .open<EntityFormDialogComponent, EntityFormDialogData<ScorecardItem>, ScorecardItem>(
        EntityFormDialogComponent,
        { data, width: '560px', maxWidth: '96vw', disableClose: true, autoFocus: 'first-tabbable' },
      )
      .afterClosed()
      .pipe(filter((saved): saved is ScorecardItem => !!saved))
      .subscribe(() => this.done(`${item.kpiCode} updated.`));
  }

  protected removeItem(item: ScorecardItem): void {
    this.confirm
      .confirm({
        title: 'Remove this KPI?',
        message: `${item.kpiCode} · ${item.kpiName} will be removed from the plan. The template stays available.`,
        confirmText: 'Remove',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.write(this.scorecards.deleteItem(item.id, item.rowVersion))),
      )
      .subscribe({
        next: () => this.done(`${item.kpiCode} removed from the plan.`),
        error: (e: unknown) => this.fail(e),
      });
  }

  /** The sentence of the finalize confirmation that names the reviewer. */
  protected reviewerSentence(): string {
    const reviewer = safeValue(this.manager);
    if (reviewer) {
      return `The reviewer will be ${reviewer}, the employee's current manager.`;
    }
    return reviewer === null
      ? 'No manager is recorded for this employee, so there will be no reviewer: only HR can review it.'
      : "The reviewer will be the employee's current manager.";
  }

  protected finalize(card: ScorecardDetail): void {
    this.confirm
      .confirm({
        title: 'Finalize this plan?',
        message: `${this.reviewerSentence()} ${card.employeeCode} · ${card.employeeName} can then enter results for ${card.periodName}, and the KPIs, targets and weights can no longer be changed.`,
        confirmText: 'Finalize plan',
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.write(this.scorecards.finalizePlan(card.id, card.rowVersion))),
      )
      .subscribe({
        next: (saved) => {
          this.conflict.set('');
          this.toast.success(
            `Plan finalized for ${card.employeeCode}. The scorecard is now in Draft.`,
          );
          this.detail.set(saved);
        },
        error: (e: unknown) => this.fail(e),
      });
  }

  protected deleteCard(card: ScorecardDetail): void {
    this.confirm
      .confirm({
        title: 'Delete this scorecard?',
        message: `The scorecard of ${card.employeeCode} for ${card.periodName} and its ${card.itemCount} KPI${card.itemCount === 1 ? '' : 's'} will be permanently removed. This cannot be undone.`,
        confirmText: 'Delete scorecard',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.write(this.scorecards.deleteScorecard(card.id, card.rowVersion))),
      )
      .subscribe({
        next: () => {
          this.toast.success(`Scorecard of ${card.employeeCode} deleted.`);
          void this.router.navigate(['/performance/assignments']);
        },
        error: (e: unknown) => this.fail(e),
      });
  }
}
