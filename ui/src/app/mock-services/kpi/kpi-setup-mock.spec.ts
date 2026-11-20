import { TestBed } from '@angular/core/testing';
import { KpiTemplate } from '../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiCategoryDataService,
  KpiTemplateDataService,
  RatingBandDataService,
} from '../../core/services/data/kpi-data.services';
import { validateKpiRule } from '../../core/utilities/kpi-calculation';
import { ActivityMockService } from '../activity/activity-mock.service';
import { configureKpiTest, failure, PAGE, wait } from './kpi-test-support';

type TemplateInput = Omit<KpiTemplate, 'id' | 'rowVersion'>;

const template = (overrides: Partial<TemplateInput> = {}): TemplateInput => ({
  code: 'TST-SALES',
  name: 'Test sales',
  description: '',
  categoryId: 1,
  departmentId: null,
  measurementType: 'Number',
  unit: null,
  direction: 'Higher Is Better',
  scoringMethod: 'Capped Percentage',
  defaultTarget: 100,
  rangeMin: null,
  rangeMax: null,
  maxRating: null,
  defaultWeight: 40,
  maxAchievementPct: 120,
  frequency: 'Quarterly',
  status: 'Active',
  ...overrides,
});

describe('KPI mock: master data', () => {
  let categories: KpiCategoryDataService;
  let templates: KpiTemplateDataService;
  let bands: RatingBandDataService;
  let periods: EvaluationPeriodDataService;

  beforeEach(() => {
    configureKpiTest();
    categories = TestBed.inject(KpiCategoryDataService);
    templates = TestBed.inject(KpiTemplateDataService);
    bands = TestBed.inject(RatingBandDataService);
    periods = TestBed.inject(EvaluationPeriodDataService);
  });

  describe('seed data', () => {
    it('has the 11 example categories', async () => {
      const all = await wait(categories.getAll());
      expect(all.map((c) => c.name)).toEqual([
        'Productivity',
        'Quality',
        'Attendance',
        'Punctuality',
        'Communication',
        'Teamwork',
        'Leadership',
        'Customer Satisfaction',
        'Technical Skills',
        'Problem Solving',
        'Project Delivery',
      ]);
    });

    it('has templates for every direction and scoring method, all with a valid rule', async () => {
      const all = await wait(templates.getAll());
      expect(all).toHaveLength(8);
      expect(new Set(all.map((t) => t.direction))).toEqual(
        new Set(['Higher Is Better', 'Lower Is Better', 'Exact Target', 'Range Based']),
      );
      expect(new Set(all.map((t) => t.scoringMethod))).toEqual(
        new Set([
          'Direct Percentage',
          'Capped Percentage',
          'Rating Based',
          'Boolean',
          'Range Based',
        ]),
      );
      for (const t of all) {
        expect(
          validateKpiRule({ ...t, target: t.defaultTarget }),
          `${t.code} has a consistent rule`,
        ).toEqual({});
      }
      // One template is specific to the ENG department (id 4); none uses a test (TST-) code.
      expect(all.filter((t) => t.departmentId !== null).map((t) => t.departmentId)).toEqual([4]);
      expect(all.some((t) => t.code.startsWith('TST-'))).toBe(false);
    });

    it('has the five default rating bands', async () => {
      const all = await wait(bands.getAll());
      expect(all.map((b) => [b.label, b.minScore])).toEqual([
        ['Outstanding', 90],
        ['Exceeds Expectations', 75],
        ['Meets Expectations', 60],
        ['Needs Improvement', 40],
        ['Unsatisfactory', 0],
      ]);
    });

    it('has a Closed, an Active and a Draft period for company 1 that do not overlap by type', async () => {
      const all = await wait(periods.getAll());
      expect(all.map((p) => p.status)).toEqual(['Closed', 'Active', 'Draft']);
      expect(all.every((p) => p.companyId === 1)).toBe(true);
      const quarterly = all.filter((p) => p.periodType === 'Quarterly');
      expect(quarterly).toHaveLength(2);
      expect(quarterly[0].endDate < quarterly[1].startDate).toBe(true);
    });
  });

  describe('categories', () => {
    it('offers every category, inactive ones included, by name and with only the picker fields', async () => {
      const created = await wait(
        categories.create({ code: 'TSTZ', name: 'Aaa Test', description: 'x', status: 'Inactive' }),
      );
      const options = await wait(categories.options());
      expect(options).toHaveLength((await wait(categories.getAll())).length);
      expect(options[0]).toEqual({
        id: created.id,
        code: 'TSTZ',
        name: 'Aaa Test',
        status: 'Inactive',
      });
      const names = options.map((c) => c.name);
      expect([...names].sort((a, b) => a.localeCompare(b))).toEqual(names);
    });

    it('refuses a duplicate code or name on that field and keeps lists unchanged', async () => {
      const created = await wait(
        categories.create({
          code: 'TSTQ',
          name: 'Test Quality',
          description: '',
          status: 'Active',
        }),
      );
      expect(created.rowVersion).toBeDefined();
      const code = await failure(
        wait(categories.create({ code: 'tstq', name: 'Other', description: '', status: 'Active' })),
      );
      expect(code.code).toBe('Validation');
      expect(code.fieldErrors?.['code']).toBeDefined();
      const name = await failure(
        wait(
          categories.create({
            code: 'TSTQ2',
            name: 'test quality',
            description: '',
            status: 'Active',
          }),
        ),
      );
      expect(name.fieldErrors?.['name']).toBeDefined();
      expect(await wait(categories.getAll())).toHaveLength(12);
    });

    it('checks the code format and the lengths', async () => {
      const bad = await failure(
        wait(categories.create({ code: 'low case', name: 'X', description: '', status: 'Active' })),
      );
      expect(bad.fieldErrors?.['code']).toContain('capital letters');
      const long = await failure(
        wait(
          categories.create({ code: 'A'.repeat(21), name: 'X', description: '', status: 'Active' }),
        ),
      );
      expect(long.fieldErrors?.['code']).toBeDefined();
      const name = await failure(
        wait(categories.create({ code: 'OK', name: ' ', description: '', status: 'Active' })),
      );
      expect(name.fieldErrors?.['name']).toBeDefined();
    });

    it('refuses to delete a category with templates, then deletes an unused one', async () => {
      const refused = await failure(wait(categories.delete(1)));
      expect(refused.code).toBe('Conflict');
      expect(refused.message).toContain('used by');
      expect(refused.message).toContain('Deactivate it instead.');
      const created = await wait(
        categories.create({
          code: 'TSTQ',
          name: 'Test Quality',
          description: '',
          status: 'Active',
        }),
      );
      await wait(categories.delete(created.id));
      expect(await wait(categories.getAll())).toHaveLength(11);
    });

    it('deactivates through update and rejects a stale row version', async () => {
      const [first] = await wait(categories.getAll());
      const inactive = await wait(categories.update(first.id, { status: 'Inactive' }));
      expect(inactive.status).toBe('Inactive');
      expect(inactive.rowVersion).not.toBe(first.rowVersion);
      const stale = await failure(
        wait(categories.update(first.id, { name: 'Renamed', rowVersion: first.rowVersion })),
      );
      expect(stale.code).toBe('Conflict');
    });

    it('records activity under the performance category', async () => {
      await wait(
        categories.create({
          code: 'TSTQ',
          name: 'Test Quality',
          description: '',
          status: 'Active',
        }),
      );
      const latest = TestBed.inject(ActivityMockService).snapshot(1)[0];
      expect(latest.category).toBe('performance');
      expect(latest.message).toBe('KPI category "Test Quality" created');
    });
  });

  describe('templates', () => {
    it('creates a template of each direction', async () => {
      await wait(templates.create(template()));
      await wait(
        templates.create(
          template({
            code: 'TST-DEFECT',
            name: 'Test defects',
            direction: 'Lower Is Better',
            defaultTarget: 5,
            defaultWeight: 35,
            maxAchievementPct: 100,
          }),
        ),
      );
      await wait(
        templates.create(
          template({
            code: 'TST-RATE',
            name: 'Test rating',
            measurementType: 'Rating',
            scoringMethod: 'Rating Based',
            defaultTarget: 5,
            maxRating: 5,
            maxAchievementPct: null,
            defaultWeight: 25,
          }),
        ),
      );
      expect(await wait(templates.getAll())).toHaveLength(11);
    });

    it.each([
      ['higher is better with target 0', { defaultTarget: 0 }, 'defaultTarget'],
      [
        'rating based without a maximum rating',
        { measurementType: 'Rating', scoringMethod: 'Rating Based', maxAchievementPct: null },
        'maxRating',
      ],
      [
        'range 105 to 95',
        {
          direction: 'Range Based',
          scoringMethod: 'Range Based',
          rangeMin: 105,
          rangeMax: 95,
          maxAchievementPct: null,
        },
        'rangeMax',
      ],
      ['capped percentage without a cap', { maxAchievementPct: null }, 'maxAchievementPct'],
      ['boolean method with a number measurement', { scoringMethod: 'Boolean' }, 'scoringMethod'],
      [
        'lower is better without a cap',
        {
          direction: 'Lower Is Better',
          scoringMethod: 'Direct Percentage',
          maxAchievementPct: null,
        },
        'maxAchievementPct',
      ],
    ] as [string, Partial<TemplateInput>, string][])(
      'refuses %s on the field %s',
      async (_name, overrides, field) => {
        const error = await failure(wait(templates.create(template(overrides))));
        expect(error.code).toBe('Validation');
        expect(error.fieldErrors?.[field]).toBeDefined();
        expect(await wait(templates.getAll())).toHaveLength(8);
      },
    );

    it('accepts the weight and cap boundaries and refuses the values just outside', async () => {
      const weight = (value: number, code: string) =>
        wait(templates.create(template({ code, defaultWeight: value })));
      await weight(0.01, 'W-LOW');
      await weight(100, 'W-HIGH');
      expect((await failure(weight(0, 'W-ZERO'))).fieldErrors?.['defaultWeight']).toBeDefined();
      expect(
        (await failure(weight(100.01, 'W-OVER'))).fieldErrors?.['defaultWeight'],
      ).toBeDefined();
      const cap = (value: number, code: string) =>
        wait(templates.create(template({ code, maxAchievementPct: value })));
      await cap(1, 'C-LOW');
      await cap(500, 'C-HIGH');
      expect(
        (await failure(cap(0.99, 'C-UNDER'))).fieldErrors?.['maxAchievementPct'],
      ).toBeDefined();
      expect(
        (await failure(cap(500.01, 'C-OVER'))).fieldErrors?.['maxAchievementPct'],
      ).toBeDefined();
    });

    it('needs a unique code, an active category and an existing department', async () => {
      await wait(templates.create(template()));
      expect(
        (await failure(wait(templates.create(template())))).fieldErrors?.['code'],
      ).toBeDefined();
      expect(
        (await failure(wait(templates.create(template({ code: 'X1', categoryId: 99 })))))
          .fieldErrors?.['categoryId'],
      ).toBeDefined();
      await wait(categories.update(2, { status: 'Inactive' }));
      expect(
        (await failure(wait(templates.create(template({ code: 'X2', categoryId: 2 })))))
          .fieldErrors?.['categoryId'],
      ).toBeDefined();
      expect(
        (await failure(wait(templates.create(template({ code: 'X3', departmentId: 99 })))))
          .fieldErrors?.['departmentId'],
      ).toBeDefined();
    });

    it('filters by category, department and scoring method', async () => {
      const eng = await wait(templates.query({ ...PAGE, filters: { departmentId: 4 } }));
      expect(eng.items.map((t) => t.code)).toEqual(['ENG-REVIEW-HOURS']);
      const rating = await wait(
        templates.query({ ...PAGE, filters: { scoringMethod: 'Rating Based' } }),
      );
      expect(rating.items.map((t) => t.code)).toEqual(['CUSTOMER-RATING']);
    });
  });

  describe('rating bands', () => {
    it('needs a unique label and a unique minimum score', async () => {
      const label = await failure(
        wait(
          bands.create({ label: 'outstanding', minScore: 95, description: '', status: 'Active' }),
        ),
      );
      expect(label.fieldErrors?.['label']).toBeDefined();
      const score = await failure(
        wait(bands.create({ label: 'Great', minScore: 90, description: '', status: 'Active' })),
      );
      expect(score.fieldErrors?.['minScore']).toBeDefined();
      const range = await failure(
        wait(bands.create({ label: 'Huge', minScore: 501, description: '', status: 'Active' })),
      );
      expect(range.fieldErrors?.['minScore']).toBeDefined();
      await wait(bands.create({ label: 'Great', minScore: 95, description: '', status: 'Active' }));
      expect(await wait(bands.getAll())).toHaveLength(6);
    });
  });

  describe('evaluation periods', () => {
    const newPeriod = (overrides: Record<string, unknown> = {}) =>
      periods.create({
        companyId: 1,
        name: 'Test Year 2031',
        periodType: 'Yearly',
        startDate: '2031-01-01',
        endDate: '2031-12-31',
        submissionDueDate: null,
        requiresHrReview: false,
        status: 'Draft',
        ...overrides,
      });

    it('creates a period as Draft and moves it through the commands in order', async () => {
      const created = await wait(newPeriod({ status: 'Active' }));
      expect(created.status).toBe('Draft');
      const active = await wait(periods.activate(created.id, created.rowVersion));
      expect(active.status).toBe('Active');
      const stale = await failure(wait(periods.complete(created.id, created.rowVersion)));
      expect(stale.code).toBe('Conflict');
      const completed = await wait(periods.complete(created.id));
      expect((await wait(periods.close(created.id))).status).toBe('Closed');
      expect(completed.status).toBe('Completed');
      const again = await failure(wait(periods.close(created.id)));
      expect(again.message).toBe(
        'Period "Test Year 2031" is Closed; only a Completed period can be closed.',
      );
    });

    it('refuses to close while scorecards are in review or returned, with the count (M2)', async () => {
      // Seeded period 2: EMP046 and EMP002 Submitted, EMP006 Under Review, EMP014 Returned block;
      // the Draft and Planning ones do not.
      const name = (await wait(periods.getById(2))).name;
      await wait(periods.complete(2));
      const error = await failure(wait(periods.close(2)));
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe(
        `Period "${name}" still has 4 scorecard(s) in review or returned to the employee. Finish them before closing.`,
      );
      expect((await wait(periods.getById(2))).status).toBe('Completed');
      // The status is checked first: closing an Active period is the out-of-order message.
      const created = await wait(newPeriod());
      await wait(periods.activate(created.id));
      expect((await failure(wait(periods.close(created.id)))).message).toContain(
        'only a Completed period can be closed',
      );
    });

    it('refuses a command out of order and names the status', async () => {
      const created = await wait(newPeriod());
      const error = await failure(wait(periods.complete(created.id)));
      expect(error.code).toBe('Conflict');
      // The same wording as the API, including its "a Active".
      expect(error.message).toBe(
        'Period "Test Year 2031" is Draft; only a Active period can be completed.',
      );
    });

    it('validates the dates', async () => {
      const before = await failure(
        wait(newPeriod({ startDate: '2031-01-01', endDate: '2030-12-31' })),
      );
      expect(before.fieldErrors?.['endDate']).toBeDefined();
      const equal = await failure(wait(newPeriod({ endDate: '2031-01-01' })));
      expect(equal.fieldErrors?.['endDate']).toBeDefined();
      const due = await failure(wait(newPeriod({ submissionDueDate: '2030-12-31' })));
      expect(due.fieldErrors?.['submissionDueDate']).toBeDefined();
      // A due date after the end date is allowed.
      await wait(newPeriod({ submissionDueDate: '2032-01-15' }));
    });

    it('refuses an overlap of the same type in a company, naming the period', async () => {
      await wait(newPeriod());
      const clash = await failure(
        wait(newPeriod({ name: 'Second', startDate: '2031-12-31', endDate: '2032-06-30' })),
      );
      expect(clash.code).toBe('Conflict');
      expect(clash.message).toContain('"Test Year 2031" (2031-01-01 to 2031-12-31)');
      // A different type, and the same type in another company, never clash.
      await wait(newPeriod({ name: 'Month', periodType: 'Monthly', endDate: '2031-01-31' }));
      await wait(newPeriod({ name: 'Other company', companyId: 2 }));
    });

    it('refuses a duplicate name within a company on the name field', async () => {
      await wait(newPeriod());
      const error = await failure(
        wait(newPeriod({ periodType: 'Monthly', startDate: '2031-02-01', endDate: '2031-02-28' })),
      );
      expect(error.fieldErrors?.['name']).toBeDefined();
    });

    it('needs an existing, active company', async () => {
      expect(
        (await failure(wait(newPeriod({ companyId: 99 })))).fieldErrors?.['companyId'],
      ).toBeDefined();
      // AHV (company 3) is Inactive.
      expect(
        (await failure(wait(newPeriod({ companyId: 3 })))).fieldErrors?.['companyId'],
      ).toBeDefined();
    });

    it('allows only the name and the due date to change in an Active period', async () => {
      const [, active] = await wait(periods.getAll());
      expect(active.status).toBe('Active');
      const renamed = await wait(
        periods.update(active.id, { name: 'Renamed period', submissionDueDate: '2099-01-01' }),
      );
      expect(renamed.name).toBe('Renamed period');
      expect(renamed.status).toBe('Active');
      const locked = await failure(wait(periods.update(active.id, { requiresHrReview: true })));
      expect(locked.code).toBe('Conflict');
      expect(locked.message).toContain('only its name and submission due date can be changed');
    });

    it('does not let an update change the status, and refuses edits to Closed periods', async () => {
      const created = await wait(newPeriod());
      const sneaky = await wait(periods.update(created.id, { name: 'New name', status: 'Closed' }));
      expect(sneaky.status).toBe('Draft');
      const [closed] = await wait(periods.getAll());
      expect(closed.status).toBe('Closed');
      const error = await failure(wait(periods.update(closed.id, { name: 'Late edit' })));
      expect(error.code).toBe('Conflict');
      expect(error.message).toContain('can no longer be changed');
    });

    it('deletes only a Draft period that no scorecard uses', async () => {
      const closed = await failure(wait(periods.delete(1)));
      expect(closed.message).toContain('only a Draft period can be deleted');
      const created = await wait(newPeriod());
      await wait(periods.delete(created.id));
      expect((await wait(periods.getAll())).some((p) => p.id === created.id)).toBe(false);
    });

    it('lists the picker options without Draft periods, newest first', async () => {
      const options = await wait(periods.options());
      expect(options.map((o) => o.status)).toEqual(['Active', 'Closed']);
      expect(options[0].startDate > options[1].startDate).toBe(true);
      expect(await wait(periods.options(2))).toEqual([]);
      expect('rowVersion' in options[0]).toBe(false);
    });

    it('filters by status, type and the year of the start date', async () => {
      const closed = await wait(periods.query({ ...PAGE, filters: { status: 'Closed' } }));
      expect(closed.items).toHaveLength(1);
      const year = closed.items[0].startDate.slice(0, 4);
      const byYear = await wait(periods.query({ ...PAGE, filters: { year: Number(year) } }));
      expect(byYear.items.every((p) => p.startDate.startsWith(year))).toBe(true);
      const half = await wait(periods.query({ ...PAGE, filters: { periodType: 'Half-Yearly' } }));
      expect(half.items).toHaveLength(1);
    });
  });
});
