import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Payslip } from '../../../core/models/payroll.models';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { formatDuration } from '../../../shared/utilities/date.util';

/**
 * One payslip as a document: shown on the payslip screen and repeated, one per page, when a run is
 * printed. Prints the masked account only, because that is all the payslip holds.
 */
@Component({
  selector: 'app-payslip-sheet',
  imports: [DatePipe, MatIconModule, MoneyPipe],
  templateUrl: './payslip-sheet.component.html',
  styleUrl: './payslip-sheet.component.scss',
})
export class PayslipSheetComponent {
  readonly payslip = input.required<Payslip>();
  protected readonly today = new Date();

  protected readonly overtime = computed(() => formatDuration(this.payslip().overtimeMinutes));
}
