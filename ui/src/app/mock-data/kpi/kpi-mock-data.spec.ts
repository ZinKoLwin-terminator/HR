import { validateKpiRule } from '../../core/utilities/kpi-calculation';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { MOCK_COMPANIES } from '../organization/companies.mock';
import { MOCK_DEPARTMENTS } from '../organization/departments.mock';
import { MOCK_EVALUATION_PERIODS } from './evaluation-periods.mock';
import { MOCK_KPI_CATEGORIES } from './kpi-categories.mock';
import { MOCK_KPI_TEMPLATES } from './kpi-templates.mock';
import { DEFAULT_RATING_BANDS } from './rating-bands.mock';
import { MOCK_SCORECARDS } from './scorecards.mock';

describe('KPI mock data integrity', () => {
  it('links templates to categories and departments, with consistent rules', () => {
    const categories = new Set(MOCK_KPI_CATEGORIES.map((c) => c.id));
    const departments = new Set(MOCK_DEPARTMENTS.map((d) => d.id));
    for (const t of MOCK_KPI_TEMPLATES) {
      expect(categories.has(t.categoryId), t.code).toBe(true);
      if (t.departmentId !== null) {
        expect(departments.has(t.departmentId), t.code).toBe(true);
      }
      expect(validateKpiRule({ ...t, target: t.defaultTarget }), t.code).toEqual({});
    }
    expect(new Set(MOCK_KPI_TEMPLATES.map((t) => t.code)).size).toBe(MOCK_KPI_TEMPLATES.length);
  });

  it('keeps the periods in valid companies with end after start', () => {
    const companies = new Set(MOCK_COMPANIES.map((c) => c.id));
    for (const p of MOCK_EVALUATION_PERIODS) {
      expect(companies.has(p.companyId), p.name).toBe(true);
      expect(p.endDate > p.startDate, p.name).toBe(true);
    }
  });

  it('has default bands that start at 0 and have unique minimum scores', () => {
    expect(DEFAULT_RATING_BANDS.some((b) => b.minScore === 0)).toBe(true);
    expect(new Set(DEFAULT_RATING_BANDS.map((b) => b.minScore)).size).toBe(5);
  });

  describe('scorecards', () => {
    it('has unique ids, one scorecard per employee and period, and unique item ids', () => {
      expect(new Set(MOCK_SCORECARDS.map((s) => s.id)).size).toBe(MOCK_SCORECARDS.length);
      const keys = MOCK_SCORECARDS.map((s) => `${s.employeeId}/${s.periodId}`);
      expect(new Set(keys).size).toBe(keys.length);
      const itemIds = MOCK_SCORECARDS.flatMap((s) => s.items.map((i) => i.id));
      expect(new Set(itemIds).size).toBe(itemIds.length);
    });

    it('points at existing employees, periods and templates, and copies the employee org data', () => {
      const periods = new Set(MOCK_EVALUATION_PERIODS.map((p) => p.id));
      const templates = new Set(MOCK_KPI_TEMPLATES.map((t) => t.id));
      for (const card of MOCK_SCORECARDS) {
        const employee = MOCK_EMPLOYEES.find((e) => e.id === card.employeeId)!;
        expect(employee, `employee ${card.employeeId}`).toBeDefined();
        expect(periods.has(card.periodId)).toBe(true);
        expect(card.departmentId).toBe(employee.departmentId);
        expect(card.companyId).toBe(employee.companyId);
        card.items.forEach((item) => expect(templates.has(item.templateId)).toBe(true));
      }
    });

    it('names the employee manager as reviewer once the plan is final, never the employee', () => {
      for (const card of MOCK_SCORECARDS) {
        const employee = MOCK_EMPLOYEES.find((e) => e.id === card.employeeId)!;
        if (card.status === 'Planning') {
          expect(card.reviewerEmployeeId).toBeNull();
          expect(card.history).toEqual([]);
        } else {
          expect(card.reviewerEmployeeId).toBe(employee.managerId);
          expect(card.reviewerEmployeeId).not.toBe(card.employeeId);
        }
      }
    });

    it('uses Daniel Carter (EMP005) as the reviewer of his team', () => {
      const team = MOCK_SCORECARDS.filter((s) => s.reviewerEmployeeId === 5).map(
        (s) => s.employeeId,
      );
      expect(team).toEqual(expect.arrayContaining([46, 17, 14]));
    });

    it('keeps the Closed period history final', () => {
      const closed = MOCK_EVALUATION_PERIODS.find((p) => p.status === 'Closed')!;
      const history = MOCK_SCORECARDS.filter((s) => s.periodId === closed.id);
      expect(history.length).toBeGreaterThanOrEqual(3);
      expect(history.every((s) => s.status === 'Finalized' || s.status === 'Rejected')).toBe(true);
    });
  });
});
