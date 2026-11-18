import { GRIEVANCE_STATUSES, GrievanceStatus } from '../../core/models/grievance.models';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { MOCK_DEPARTMENTS } from '../organization/departments.mock';
import { MOCK_GRIEVANCE_CASES, MOCK_GRIEVANCE_SUBMITTERS } from './grievance-cases.mock';
import { MOCK_GRIEVANCE_CATEGORIES } from './grievance-categories.mock';

/** The database CHECK constraints of `grv.Case` and friends, applied to the sample data. */
describe('grievance mock data integrity', () => {
  it('has the 10 default categories with unique codes and names', () => {
    expect(MOCK_GRIEVANCE_CATEGORIES).toHaveLength(10);
    expect(new Set(MOCK_GRIEVANCE_CATEGORIES.map((c) => c.code)).size).toBe(10);
    expect(new Set(MOCK_GRIEVANCE_CATEGORIES.map((c) => c.name)).size).toBe(10);
  });

  it('covers most statuses, named and anonymous', () => {
    const statuses = new Set(MOCK_GRIEVANCE_CASES.map((c) => c.status));
    expect(statuses.size).toBeGreaterThanOrEqual(GRIEVANCE_STATUSES.length - 1);
    expect(MOCK_GRIEVANCE_CASES.some((c) => c.isAnonymous && c.status !== 'Draft')).toBe(true);
    expect(MOCK_GRIEVANCE_CASES.some((c) => c.priority === 'Critical' && c.isEscalated)).toBe(true);
    expect(
      MOCK_GRIEVANCE_CASES.some((c) => c.proposals.some((p) => p.decision === 'Pending')),
    ).toBe(true);
  });

  it('links every case to exactly one of employee id and pseudonym', () => {
    expect(MOCK_GRIEVANCE_SUBMITTERS).toHaveLength(MOCK_GRIEVANCE_CASES.length);
    for (const c of MOCK_GRIEVANCE_CASES) {
      const link = MOCK_GRIEVANCE_SUBMITTERS.find((l) => l.caseId === c.id)!;
      expect(link, `#${c.id}`).toBeDefined();
      expect((link.employeeId === null) !== (link.pseudonym === null), `#${c.id}`).toBe(true);
      expect(link.pseudonym !== null, `#${c.id}`).toBe(c.isAnonymous);
    }
  });

  it('keeps the employee id off every case record', () => {
    const json = JSON.stringify(MOCK_GRIEVANCE_CASES);
    expect(json).not.toContain('pseudonym');
    for (const c of MOCK_GRIEVANCE_CASES.filter((r) => r.isAnonymous)) {
      expect([c.companyId, c.branchId, c.departmentId, c.positionId], `#${c.id}`).toEqual([
        null,
        null,
        null,
        null,
      ]);
      for (const h of c.history.filter((e) => e.actorRole === 'Submitter')) {
        expect([h.actorUserId, h.actorEmployeeId, h.actorName]).toEqual([
          null,
          null,
          'Anonymous submitter',
        ]);
      }
    }
  });

  it('meets the case CHECK constraints', () => {
    const numbers = MOCK_GRIEVANCE_CASES.flatMap((c) => (c.caseNo ? [c.caseNo] : []));
    expect(new Set(numbers).size).toBe(numbers.length);
    for (const c of MOCK_GRIEVANCE_CASES) {
      const name = `#${c.id}`;
      expect(GRIEVANCE_STATUSES, name).toContain(c.status);
      if (c.status === 'Draft') {
        expect(c.caseNo, name).toBeNull();
      } else {
        expect(c.caseNo, name).toMatch(/^GRV-\d{4}-\d{5}$/);
        expect(c.submittedAtUtc, name).not.toBeNull();
        expect(c.declarationAcceptedAtUtc, name).not.toBeNull();
      }
      if (c.status === 'Rejected') {
        expect(c.rejectionReason, name).toBeTruthy();
      }
      if (c.status === 'Resolved' || c.status === 'Closed') {
        expect(c.resolutionSummary, name).toBeTruthy();
      }
      if (c.archivedAtUtc !== null) {
        expect(['Closed', 'Rejected'], name).toContain(c.status);
      }
      const leads = c.assignments.filter((a) => a.role === 'Lead' && a.unassignedAtUtc === null);
      expect(leads.length, name).toBeLessThanOrEqual(1);
      expect(c.proposals.filter((p) => p.decision === 'Pending').length, name).toBeLessThanOrEqual(
        1,
      );
      for (const p of c.proposals.filter((x) => x.decidedByUserId !== null)) {
        expect(p.decidedByUserId, name).not.toBe(p.proposedByUserId);
      }
      expect(
        MOCK_GRIEVANCE_CATEGORIES.some((k) => k.id === c.categoryId),
        name,
      ).toBe(true);
    }
  });

  it('writes a history that ends in the case status, with reasons where required', () => {
    const needsReason = ['Reject', 'Reopen', 'Escalate', 'RejectResolution', 'RequestInfo'];
    for (const c of MOCK_GRIEVANCE_CASES.filter((r) => r.status !== 'Draft')) {
      const last = c.history.at(-1)!;
      expect(last.toStatus as GrievanceStatus, `#${c.id}`).toBe(c.status);
      for (const h of c.history) {
        if (needsReason.includes(h.action)) {
          expect(h.reason, `#${c.id} ${h.action}`).toBeTruthy();
        }
      }
    }
    const ids = MOCK_GRIEVANCE_CASES.flatMap((c) => c.history.map((h) => h.id));
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('refers to existing employees and departments', () => {
    const employees = new Set(MOCK_EMPLOYEES.map((e) => e.id));
    const departments = new Set(MOCK_DEPARTMENTS.map((d) => d.id));
    for (const c of MOCK_GRIEVANCE_CASES) {
      for (const a of c.assignments) {
        expect(employees.has(a.investigatorEmployeeId)).toBe(true);
      }
      if (c.subjectEmployeeId !== null) {
        expect(employees.has(c.subjectEmployeeId)).toBe(true);
      }
      if (c.departmentId !== null) {
        expect(departments.has(c.departmentId)).toBe(true);
      }
    }
    for (const link of MOCK_GRIEVANCE_SUBMITTERS.filter((l) => l.employeeId !== null)) {
      expect(employees.has(link.employeeId!)).toBe(true);
    }
  });
});
