import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { PayslipDataService } from '../../../core/services/data/payroll-data.services';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { formatDuration } from '../../../shared/utilities/date.util';

/** Printable payslip preview. Clearly marked as sample data. */
@Component({
  selector: 'app-payslip-view',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    EmptyStateComponent,
    MoneyPipe,
  ],
  templateUrl: './payslip-view.component.html',
  styleUrl: './payslip-view.component.scss',
})
export class PayslipViewComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();
  private readonly payslipsApi = inject(PayslipDataService);
  private readonly location = inject(Location);

  protected readonly resource = rxResource({
    params: () => ({ id: Number(this.id()) }),
    stream: ({ params }) => this.payslipsApi.getById(params.id),
  });
  protected readonly payslip = computed(() => this.resource.value() ?? null);
  protected readonly allowances = computed(() => {
    const p = this.payslip();
    return p ? Math.round((p.grossSalary - p.basicSalary) * 100) / 100 : 0;
  });
  protected readonly overtime = computed(() =>
    formatDuration(this.payslip()?.overtimeMinutes ?? 0),
  );

  protected back(): void {
    this.location.back();
  }

  protected print(): void {
    window.print();
  }
}
