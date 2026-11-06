import { DashboardSummary } from '../../core/models/dashboard.models';

const cell = (value: string | number): string => {
  const text = String(value);
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};

/** Flat CSV (Section, Metric, Value) of the headline dashboard figures. */
export function buildDashboardReportCsv(s: DashboardSummary): string {
  const a = s.attendanceToday;
  const rows: (string | number)[][] = [
    ['Section', 'Metric', 'Value'],
    ['Headcount', 'Total employees', s.headcount.total],
    ['Headcount', 'Active', s.headcount.active],
    ['Headcount', 'On probation', s.headcount.probation],
    ['Headcount', 'New joiners (30 days)', s.headcount.newThisMonth],
    ['Headcount', 'Exits this month', s.headcount.exitsThisMonth],
    ['Attendance', 'Date', a.date],
    ['Attendance', 'Scheduled', a.scheduled],
    ['Attendance', 'Present', a.present],
    ['Attendance', 'Late', a.late],
    ['Attendance', 'Half day', a.halfDay],
    ['Attendance', 'On leave', a.onLeave],
    ['Attendance', 'Absent', a.absent],
    ['Attendance', 'Attendance rate (%)', a.attendanceRate],
    ['Leave', 'Pending approvals', s.pendingLeaveApprovals],
    ['Recruitment', 'Open positions', s.recruitment.openPositions],
    ['Recruitment', 'Active candidates', s.recruitment.activeCandidates],
    ['Recruitment', 'Upcoming interviews', s.recruitment.upcomingInterviews],
    ...s.recruitment.funnel.map((f): (string | number)[] => [
      'Recruitment funnel',
      f.label,
      f.count,
    ]),
    ...(s.payroll
      ? [
          ['Payroll', 'Period', s.payroll.periodName],
          ['Payroll', 'Status', s.payroll.status],
          ['Payroll', 'Gross', s.payroll.totalGross],
          ['Payroll', 'Deductions', s.payroll.totalDeductions],
          ['Payroll', 'Net', s.payroll.totalNet],
        ]
      : []),
    ...s.departmentDistribution.map((d): (string | number)[] => [
      'Department headcount',
      d.label,
      d.count,
    ]),
  ];
  return rows.map((r) => r.map(cell).join(',')).join('\r\n');
}

/** Saves the report as a CSV file (BOM so Excel reads UTF-8). */
export function downloadDashboardReport(s: DashboardSummary): void {
  const blob = new Blob(['﻿', buildDashboardReportCsv(s)], {
    type: 'text/csv;charset=utf-8',
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `dashboard-report-${s.attendanceToday.date}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}
