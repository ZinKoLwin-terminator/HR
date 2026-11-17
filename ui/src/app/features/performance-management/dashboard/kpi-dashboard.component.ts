import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { BarController, BarElement, CategoryScale, LinearScale, Tooltip } from 'chart.js';
import { provideCharts } from 'ng2-charts';
import { Observable, catchError, map, of } from 'rxjs';
import {
  KpiEmployeeDashboard,
  KpiHrDashboard,
  KpiManagerDashboard,
  KpiPeriodRef,
} from '../../../core/models/kpi-insight.models';
import { isAppError } from '../../../core/models/common.models';
import { EvaluationPeriodOption } from '../../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiInsightDataService,
} from '../../../core/services/data/kpi-data.services';
import { CompanyDataService } from '../../../core/services/data/organization-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { KpiDashboardView, kpiDashboardTabs } from './kpi-dashboard-views';
import { KpiEmployeeViewComponent } from './kpi-employee-view.component';
import { KpiHrViewComponent } from './kpi-hr-view.component';
import { KpiManagerViewComponent } from './kpi-manager-view.component';

/** A loaded dashboard, tagged with its view. */
type Loaded =
  | { view: 'employee'; data: KpiEmployeeDashboard }
  | { view: 'manager'; data: KpiManagerDashboard }
  | { view: 'hr'; data: KpiHrDashboard };

/**
 * KPI dashboard (`/performance/dashboard`, `kpi:View`). The views the role may open are tabs (one
 * view: no tabs): my performance (`kpi:View`), my team (`kpi:Approve`) and the HR overview
 * (`kpi:Approve` with `employees:Edit`). The period picker starts on the server's default period
 * (the most recent Active one) and a choice applies to every view. Only the active view is loaded.
 * Every figure is the server's; the screen computes none.
 */
@Component({
  selector: 'app-kpi-dashboard',
  imports: [
    AppDatePipe,
    EmptyStateComponent,
    KpiEmployeeViewComponent,
    KpiHrViewComponent,
    KpiManagerViewComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule,
    NgTemplateOutlet,
    PageHeaderComponent,
  ],
  providers: [
    provideCharts({
      registerables: [BarController, BarElement, CategoryScale, LinearScale, Tooltip],
    }),
  ],
  template: `
    <app-page-header
      title="KPI dashboard"
      description="Scores, progress and reviews for an evaluation period. Figures come from the server and count only the scorecards you may see."
    >
      <ng-container pageActions>
        <button
          mat-stroked-button
          type="button"
          (click)="dashboard.reload()"
          [disabled]="dashboard.isLoading()"
        >
          <mat-icon>refresh</mat-icon>Refresh
        </button>
      </ng-container>
    </app-page-header>

    @if (tabs().length === 0) {
      <app-empty-state
        icon="lock"
        title="No KPI dashboard for your role"
        message="Your role does not include the Performance permissions."
      />
    } @else {
      <div class="toolbar" role="group" aria-label="Dashboard options">
        <mat-form-field subscriptSizing="dynamic" class="period">
          <mat-label>Evaluation period</mat-label>
          <mat-select
            [value]="selectedPeriod()"
            (selectionChange)="choosePeriod($event.value)"
            data-testid="period-picker"
          >
            @for (p of periodOptions(); track p.id) {
              <mat-option [value]="p.id">{{ p.name }} ({{ p.status }})</mat-option>
            }
          </mat-select>
          @if (shownPeriod(); as p) {
            <mat-hint
              >{{ p.startDate | appDate }} to {{ p.endDate | appDate
              }}{{ periodChoice() === null ? ' · default period' : '' }}</mat-hint
            >
          }
        </mat-form-field>
        @if (view() === 'hr' && companyOptions().length > 1) {
          <mat-form-field subscriptSizing="dynamic" class="company">
            <mat-label>Company</mat-label>
            <mat-select [value]="companyId()" (selectionChange)="companyId.set($event.value)">
              <mat-option [value]="null">All companies</mat-option>
              @for (c of companyOptions(); track c.id) {
                <mat-option [value]="c.id">{{ c.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
      </div>

      @if (tabs().length > 1) {
        <mat-tab-group
          class="tabs"
          animationDuration="0ms"
          mat-align-tabs="start"
          aria-label="KPI dashboards"
          [selectedIndex]="tabIndex()"
          (selectedIndexChange)="selectTab($event)"
        >
          @for (tab of tabs(); track tab.view) {
            <mat-tab [label]="tab.label">
              <ng-template matTabContent>
                <ng-container [ngTemplateOutlet]="body" />
              </ng-template>
            </mat-tab>
          }
        </mat-tab-group>
      } @else {
        <ng-container [ngTemplateOutlet]="body" />
      }
    }

    <ng-template #body>
      <div class="progress" aria-hidden="true">
        @if (dashboard.isLoading()) {
          <mat-progress-bar mode="indeterminate" />
        }
      </div>
      @if (dashboard.error()) {
        <app-empty-state
          [icon]="forbidden() ? 'lock' : 'error'"
          [title]="
            forbidden() ? 'Not available for your role' : 'The dashboard could not be loaded'
          "
          [message]="errorText()"
        >
          @if (!forbidden()) {
            <button mat-flat-button type="button" (click)="dashboard.reload()">Try again</button>
          }
        </app-empty-state>
      } @else if (loaded(); as l) {
        <div
          class="view"
          [attr.aria-busy]="dashboard.isLoading()"
          [class.refreshing]="dashboard.isLoading()"
          [attr.data-view]="l.view"
        >
          @switch (l.view) {
            @case ('employee') {
              <app-kpi-employee-view [dashboard]="l.data" />
            }
            @case ('manager') {
              <app-kpi-manager-view [dashboard]="l.data" />
            }
            @case ('hr') {
              <app-kpi-hr-view [dashboard]="l.data" />
            }
          }
        </div>
      }
    </ng-template>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      align-items: flex-start;
    }
    .period {
      width: min(100%, 320px);
    }
    .company {
      width: min(100%, 260px);
    }
    .progress {
      height: 4px;
    }
    .view {
      padding-top: 12px;
      transition: opacity 150ms ease;
    }
    .view.refreshing {
      opacity: 0.6;
    }
  `,
})
export class KpiDashboardComponent {
  private readonly insights = inject(KpiInsightDataService);
  private readonly periodsApi = inject(EvaluationPeriodDataService);
  private readonly companiesApi = inject(CompanyDataService);
  private readonly permissions = inject(PermissionService);

  protected readonly tabs = computed(() => kpiDashboardTabs((key) => this.permissions.can(key)));
  /** The chosen view; by default the widest one the role may open (HR, then team, then own). */
  protected readonly view = linkedSignal<KpiDashboardView | null>(
    () => this.tabs().at(-1)?.view ?? null,
  );
  protected readonly tabIndex = computed(() =>
    Math.max(
      0,
      this.tabs().findIndex((t) => t.view === this.view()),
    ),
  );

  /** The period the user chose; null: the server's default period for the view. */
  protected readonly periodChoice = signal<number | null>(null);
  protected readonly companyId = signal<number | null>(null);

  protected readonly dashboard = rxResource({
    params: () => {
      const view = this.view();
      return view
        ? { view, periodId: this.periodChoice(), companyId: this.companyId() }
        : undefined;
    },
    stream: ({ params }): Observable<Loaded> => {
      switch (params.view) {
        case 'employee':
          return this.insights
            .employeeDashboard(params.periodId)
            .pipe(map((data) => ({ view: 'employee' as const, data })));
        case 'manager':
          return this.insights
            .managerDashboard(params.periodId)
            .pipe(map((data) => ({ view: 'manager' as const, data })));
        case 'hr':
          return this.insights
            .hrDashboard(params.periodId, params.companyId)
            .pipe(map((data) => ({ view: 'hr' as const, data })));
      }
    },
  });
  /** Keeps the last dashboard of the same view while the next one loads. */
  protected readonly loaded = linkedSignal<Loaded | undefined, Loaded | undefined>({
    // value() throws while the resource is in error: read it only when there is one.
    source: () => (this.dashboard.hasValue() ? this.dashboard.value() : undefined),
    computation: (value, previous) =>
      value ?? (previous?.value?.view === this.view() ? previous.value : undefined),
  });

  /** A 403 (the HR overview without the HR tier): shown as "not available", not as a retry. */
  protected readonly forbidden = computed(() => {
    const error = this.dashboard.error();
    // A resource wraps a non-Error rejection (an AppError) and keeps it as `cause`.
    const cause = error instanceof Error && isAppError(error.cause) ? error.cause : error;
    return isAppError(cause) && cause.code === 'Forbidden';
  });
  protected readonly errorText = computed(() =>
    this.dashboard.error() ? errorMessage(this.dashboard.error()) : '',
  );

  /** The period the shown dashboard is about (the server's choice when none was made). */
  protected readonly shownPeriod = computed<KpiPeriodRef | null>(
    () => this.loaded()?.data.period ?? null,
  );

  private readonly periods = rxResource({
    stream: () => this.periodsApi.options().pipe(catchError(() => of([]))),
  });
  private readonly companies = rxResource({
    stream: () => this.companiesApi.getAll().pipe(catchError(() => of([]))),
  });
  protected readonly companyOptions = computed(() =>
    (this.companies.value() ?? []).map((c) => ({ id: c.id, name: c.name })),
  );

  /** The picker's periods (not Draft), plus the shown one if the list lacks it. */
  protected readonly periodOptions = computed<
    Pick<EvaluationPeriodOption, 'id' | 'name' | 'status'>[]
  >(() => {
    const list = this.periods.value() ?? [];
    const shown = this.shownPeriod();
    return shown && !list.some((p) => p.id === shown.id) ? [shown, ...list] : list;
  });
  /** The picker shows the chosen period, or the one the server picked by default. */
  protected readonly selectedPeriod = computed(
    () => this.periodChoice() ?? this.shownPeriod()?.id ?? null,
  );

  protected choosePeriod(id: number | null): void {
    this.periodChoice.set(id);
  }

  protected selectTab(index: number): void {
    const tab = this.tabs()[index];
    if (tab) this.view.set(tab.view);
  }
}
