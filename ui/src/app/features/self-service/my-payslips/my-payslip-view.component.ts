import { Location } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { MyPayslipDataService } from '../../../core/services/data/payroll-data.services';
import { PrintService } from '../../../core/services/print.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PayslipSheetComponent } from '../../payroll/payslips/payslip-sheet.component';

/**
 * One of the signed-in user's own payslips, with the same sheet and "Print payslip" button as the
 * payroll screen. An id that is not the user's own (or not released yet) shows "not found".
 */
@Component({
  selector: 'app-my-payslip-view',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    EmptyStateComponent,
    PayslipSheetComponent,
  ],
  template: `
    @if (resource.error()) {
      <app-empty-state
        icon="receipt_long"
        title="Payslip not found"
        message="It may not exist, or it is not available to you yet."
      >
        <a mat-flat-button routerLink="/my/payslips">Back to my payslips</a>
      </app-empty-state>
    } @else if (payslip(); as p) {
      <div class="actions no-print">
        <button mat-stroked-button type="button" (click)="back()">
          <mat-icon>arrow_back</mat-icon>Back
        </button>
        <button
          mat-flat-button
          type="button"
          [attr.aria-label]="'Print payslip for ' + p.periodName"
          (click)="print()"
        >
          <mat-icon>print</mat-icon>Print payslip
        </button>
      </div>

      <app-payslip-sheet [payslip]="p" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading payslip" />
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .actions {
      display: flex;
      gap: 8px;
      margin-bottom: 16px;
    }
  `,
})
export class MyPayslipViewComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();
  private readonly api = inject(MyPayslipDataService);
  private readonly location = inject(Location);
  private readonly printer = inject(PrintService);

  protected readonly resource = rxResource({
    params: () => ({ id: Number(this.id()) }),
    stream: ({ params }) => this.api.getById(params.id),
  });
  protected readonly payslip = computed(() => this.resource.value() ?? null);

  protected back(): void {
    this.location.back();
  }

  protected print(): void {
    this.printer.print();
  }
}
