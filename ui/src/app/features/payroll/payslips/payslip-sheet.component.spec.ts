import { TestBed } from '@angular/core/testing';
import { Payslip } from '../../../core/models/payroll.models';
import { PayslipSheetComponent } from './payslip-sheet.component';

const payslip: Payslip = {
  id: 7,
  runId: 1,
  periodName: 'October 2026',
  employeeId: 3,
  employeeCode: 'EMP003',
  employeeName: 'Fictional Person',
  departmentId: 2,
  departmentName: 'Finance',
  positionTitle: 'Analyst',
  companyName: 'Example Org',
  bankName: 'Example Bank',
  accountMasked: '•••• 4821',
  basicSalary: 1000,
  earnings: [{ code: 'TRN', name: 'Transport', basis: 'Fixed', amount: 100 }],
  deductions: [{ code: 'TAX', name: 'Income tax', basis: 'Sample rate', amount: 50 }],
  grossSalary: 1100,
  totalDeductions: 50,
  netSalary: 1050,
  periodDays: 31,
  daysEmployed: 31,
  workingDays: 22,
  unpaidLeaveDays: 0,
  overtimeMinutes: 0,
  generatedOn: '2026-10-31',
} as unknown as Payslip;

describe('PayslipSheetComponent (printed payslip)', () => {
  function render(value: Payslip) {
    const fixture = TestBed.createComponent(PayslipSheetComponent);
    fixture.componentRef.setInput('payslip', value);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows period, employee, department, lines, totals and the demonstration note', () => {
    const text = render(payslip).textContent ?? '';
    for (const expected of [
      'October 2026',
      'Fictional Person',
      'EMP003',
      'Finance',
      'Transport',
      'Income tax',
      'Gross salary',
      'Net salary',
      'demonstration',
    ]) {
      expect(text).toContain(expected);
    }
  });

  it('shows only the masked account', () => {
    const text = render(payslip).textContent ?? '';
    expect(text).toContain('Example Bank •••• 4821');
    // No run of digits long enough to be an account number appears anywhere on the sheet.
    expect(text).not.toMatch(/\d{8,}/);
  });

  it('is labelled for assistive technology and carries a print date', () => {
    const el = render(payslip);
    expect(el.querySelector('article')?.getAttribute('aria-label')).toContain('Fictional Person');
    expect(el.querySelector('.print-only')?.textContent).toContain('Printed on');
  });
});
