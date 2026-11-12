import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { PrintService } from '../../../core/services/print.service';
import { PayslipDataService } from '../../../core/services/data/payroll-data.services';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PayslipSheetComponent } from './payslip-sheet.component';

/** Printable payslip preview. Clearly marked as sample data. */
@Component({
  selector: 'app-payslip-view',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    EmptyStateComponent,
    PayslipSheetComponent,
  ],
  templateUrl: './payslip-view.component.html',
  styleUrl: './payslip-view.component.scss',
})
export class PayslipViewComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();
  private readonly payslipsApi = inject(PayslipDataService);
  private readonly location = inject(Location);
  private readonly printer = inject(PrintService);

  protected readonly resource = rxResource({
    params: () => ({ id: Number(this.id()) }),
    stream: ({ params }) => this.payslipsApi.getById(params.id),
  });
  protected readonly payslip = computed(() => this.resource.value() ?? null);

  protected back(): void {
    this.location.back();
  }

  protected print(): void {
    this.printer.print();
  }
}
