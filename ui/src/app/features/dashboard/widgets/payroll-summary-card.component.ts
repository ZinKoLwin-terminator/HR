import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { PayrollSummary } from '../../../core/models/dashboard.models';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-payroll-summary-card',
  imports: [MoneyPipe, DatePipe, DecimalPipe, StatusBadgeComponent, MatIconModule, RouterLink],
  template: `
    <section class="card payroll" aria-labelledby="payroll-heading">
      <div class="card__header">
        <div>
          <h2 id="payroll-heading">Payroll summary</h2>
          <p>Latest processed period · <span class="sample-tag">Sample data</span></p>
        </div>
        <a routerLink="/payroll/processing" class="link-button">Open payroll</a>
      </div>
      @if (payroll(); as p) {
        <div class="payroll__period">
          <span class="payroll__name">{{ p.periodName }}</span>
          <app-status-badge [status]="p.status" />
        </div>
        <p class="payroll__label">Estimated net payroll</p>
        <p class="payroll__hero">{{ p.totalNet | money }}</p>
        <p class="payroll__delta">
          <mat-icon aria-hidden="true">{{ trendIcon() }}</mat-icon>
          <span
            >{{ p.netChangePercent > 0 ? '+' : '' }}{{ p.netChangePercent | number: '1.1-1' }}% vs
            previous period</span
          >
        </p>
        <dl class="payroll__facts">
          <div>
            <dt>Gross</dt>
            <dd>{{ p.totalGross | money }}</dd>
          </div>
          <div>
            <dt>Deductions</dt>
            <dd>{{ p.totalDeductions | money }}</dd>
          </div>
          <div>
            <dt>Employees</dt>
            <dd>{{ p.employeeCount }}</dd>
          </div>
          <div>
            <dt>Pay date</dt>
            <dd>{{ p.payDate | date }}</dd>
          </div>
        </dl>
      } @else {
        <p class="muted">No payroll periods yet.</p>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .payroll {
      height: 100%;
    }
    .payroll__period {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .payroll__name {
      font: var(--mat-sys-title-small);
    }
    .payroll__label {
      margin: 14px 0 0;
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    .payroll__hero {
      margin: 2px 0 0;
      font-size: 28px;
      line-height: 1.15;
      letter-spacing: -0.02em;
      font-weight: 650;
      color: var(--mat-sys-on-surface);
    }
    .payroll__delta {
      display: flex;
      align-items: center;
      gap: 4px;
      margin: 4px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .payroll__delta mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .payroll__facts {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px 16px;
      margin: 20px 0 0;
      padding-top: 16px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 2px 0 0;
      font: var(--mat-sys-title-small);
      font-variant-numeric: tabular-nums;
    }
  `,
})
export class PayrollSummaryCardComponent {
  readonly payroll = input.required<PayrollSummary | null>();
  protected readonly trendIcon = computed(() => {
    const change = this.payroll()?.netChangePercent ?? 0;
    return change > 0 ? 'trending_up' : change < 0 ? 'trending_down' : 'trending_flat';
  });
}
