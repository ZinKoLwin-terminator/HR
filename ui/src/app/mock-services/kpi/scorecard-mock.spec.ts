import { TestBed } from '@angular/core/testing';
import { AssignKpiRequest, ScorecardDetail } from '../../core/models/kpi.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import {
  EvaluationPeriodDataService,
  KpiTemplateDataService,
  ScorecardDataService,
} from '../../core/services/data/kpi-data.services';
import { ActivityMockService } from '../activity/activity-mock.service';
import { EvaluationPeriodMockService } from './kpi-setup-mock.services';
import {
  EMPLOYEE_23,
  EMPLOYEE_46,
  EMPLOYEE_56,
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
  OTHER_MANAGER,
  PAGE,
  configureKpiTest,
  failure,
  pdfFile,
  wait,
} from './kpi-test-support';

/** Engineering plan: template ids 8, 1, 2, 6, 3 with their default weights 30, 30, 20, 10, 10. */
const ENGINEERING_ITEMS = [8, 1, 2, 6, 3].map((templateId) => ({ templateId }));
/** Results for that plan: 80.00, 92.00, 83.33, 100.00 and 97.89 per cent; overall 88.06. */
const ACTUALS = [30, 92, 6, 1, 93];

describe('KPI mock: scorecards', () => {
  let cards: ScorecardDataService;
  let templates: KpiTemplateDataService;
  let periods: EvaluationPeriodDataService;
  let session: ReturnType<typeof configureKpiTest>;

  beforeEach(() => {
    session = configureKpiTest();
    cards = TestBed.inject(ScorecardDataService);
    templates = TestBed.inject(KpiTemplateDataService);
    periods = TestBed.inject(EvaluationPeriodDataService);
  });

  /** The seeded Active quarter (period 2, no HR review). */
  const ACTIVE_PERIOD = 2;

  /** Creates an Active yearly period (distinct dates per call) for the HR-review flow. */
  async function hrReviewPeriod(): Promise<number> {
    const created = await wait(
      periods.create({
        companyId: 1,
        name: 'Test HR-review year',
        periodType: 'Yearly',
        startDate: '2032-01-01',
        endDate: '2032-12-31',
        submissionDueDate: null,
        requiresHrReview: true,
        status: 'Draft',
      }),
    );
    await wait(periods.activate(created.id));
    return created.id;
  }

  /**
   * Makes the mock read the period as Closed without the close command (which waits for every
   * review, M2), the way direct database work could leave it.
   */
  function closeInStore(periodId: number): void {
    const service = TestBed.inject(EvaluationPeriodMockService);
    const real = service.findById.bind(service);
    vi.spyOn(service, 'findById').mockImplementation((id) => {
      const found = real(id);
      return found && id === periodId ? { ...found, status: 'Closed' } : found;
    });
  }

  /** Creates an Active yearly period without HR review, holding no scorecard yet. */
  async function plainPeriod(): Promise<number> {
    session.as(HR_ADMIN);
    const created = await wait(
      periods.create({
        companyId: 1,
        name: 'Test plain year',
        periodType: 'Yearly',
        startDate: '2033-01-01',
        endDate: '2033-12-31',
        submissionDueDate: null,
        requiresHrReview: false,
        status: 'Draft',
      }),
    );
    await wait(periods.activate(created.id));
    return created.id;
  }

  async function assign(
    employeeId: number,
    periodId = ACTIVE_PERIOD,
    items: AssignKpiRequest['items'] = ENGINEERING_ITEMS,
  ) {
    session.as(HR_ADMIN);
    return wait(cards.assign({ employeeId, periodId, items }));
  }

  /** Employee 23's plan, finalized: a Draft scorecard ready for results. */
  async function draftCard(periodId = ACTIVE_PERIOD): Promise<ScorecardDetail> {
    const { scorecard } = await assign(23, periodId);
    session.as(HR_ADMIN);
    return wait(cards.finalizePlan(scorecard.id, scorecard.rowVersion));
  }

  async function withResults(card: ScorecardDetail, actuals = ACTUALS): Promise<ScorecardDetail> {
    session.as(EMPLOYEE_23);
    return wait(
      cards.saveResults(card.id, {
        rowVersion: card.rowVersion,
        items: card.items.map((item, i) => ({ id: item.id, actualValue: actuals[i] })),
      }),
    );
  }

  async function submittedCard(periodId = ACTIVE_PERIOD): Promise<ScorecardDetail> {
    const saved = await withResults(await draftCard(periodId));
    return wait(cards.submit(saved.id, saved.rowVersion));
  }

  describe('seed data', () => {
    it('has scorecards in several statuses, with figures that reconcile', async () => {
      const page = await wait(cards.query(PAGE));
      expect(new Set(page.items.map((c) => c.status))).toEqual(
        new Set([
          'Planning',
          'Draft',
          'Submitted',
          'Under Review',
          'Returned',
          'Finalized',
          'Rejected',
        ]),
      );
      const hein = page.items.find((c) => c.employeeCode === 'EMP046' && c.status === 'Submitted')!;
      expect(hein.overallScore).toBe(88.06);
      expect(hein.ratingLabel).toBe('Exceeds Expectations');
      expect(hein.reviewerName).toBe('Daniel Carter');
      // Every plan that left Planning adds up to exactly 100%.
      for (const card of page.items.filter((c) => c.status !== 'Planning')) {
        expect(card.totalWeight, card.employeeCode).toBe(100);
      }
      expect(page.items.find((c) => c.status === 'Planning')!.totalWeight).toBe(60);
    });

    it('shows a finalized scorecard with the whole approval history', async () => {
      const page = await wait(cards.query({ ...PAGE, filters: { status: 'Finalized' } }));
      const detail = await wait(cards.getById(page.items[0].id));
      expect(detail.history.map((h) => h.action)).toEqual([
        'Finalize Plan',
        'Submit',
        'Start Review',
        'Approve',
        'Finalize',
      ]);
      expect(detail.periodStatus).toBe('Closed');
    });
  });

  describe('assignment', () => {
    it('creates a Planning scorecard that copies the templates', async () => {
      const { scorecard, created } = await assign(23);
      expect(created).toBe(true);
      expect(scorecard.status).toBe('Planning');
      expect(scorecard.items.map((i) => i.kpiCode)).toEqual([
        'ENG-REVIEW-HOURS',
        'OUTPUT-TASKS',
        'DEFECT-RATE',
        'TRAINING-DONE',
        'ATTENDANCE-RATE',
      ]);
      expect(scorecard.items.map((i) => i.weight)).toEqual([30, 30, 20, 10, 10]);
      expect(scorecard.items.map((i) => i.sortOrder)).toEqual([1, 2, 3, 4, 5]);
      expect(scorecard.reviewerEmployeeId).toBeNull();
      expect(scorecard.items[0].categoryName).toBe('Technical Skills');
      expect(scorecard.items[0].attachments).toEqual([]);
    });

    it('adds items to a Planning scorecard (200) and refuses a duplicate KPI by code', async () => {
      await assign(23, ACTIVE_PERIOD, [{ templateId: 1, weight: 40 }]);
      const added = await wait(
        cards.assign({
          employeeId: 23,
          periodId: ACTIVE_PERIOD,
          items: [{ templateId: 2, weight: 20 }],
        }),
      );
      expect(added.created).toBe(false);
      expect(added.scorecard.items).toHaveLength(2);
      expect(added.scorecard.items[1].sortOrder).toBe(2);
      const duplicate = await failure(
        wait(
          cards.assign({
            employeeId: 23,
            periodId: ACTIVE_PERIOD,
            items: [{ templateId: 1, weight: 10 }],
          }),
        ),
      );
      expect(duplicate.code).toBe('Conflict');
      expect(duplicate.message).toBe(
        'KPI OUTPUT-TASKS is already assigned to the scorecard of EMP023.',
      );
    });

    it('is not changed by a later template edit', async () => {
      const { scorecard } = await assign(23, ACTIVE_PERIOD, [{ templateId: 1, weight: 100 }]);
      await wait(templates.update(1, { defaultTarget: 200, name: 'Renamed tasks' }));
      const reread = await wait(cards.getById(scorecard.id));
      expect(reread.items[0].target).toBe(100);
      expect(reread.items[0].kpiName).toBe('Tasks completed');
      const next = await assign(24, ACTIVE_PERIOD, [{ templateId: 1, weight: 100 }]);
      expect(next.scorecard.items[0].target).toBe(200);
    });

    it('applies a target override and refuses a zero target on Higher Is Better', async () => {
      const { scorecard } = await assign(23, ACTIVE_PERIOD, [
        { templateId: 1, target: 150, weight: 50 },
      ]);
      expect(scorecard.items[0].target).toBe(150);
      const zero = await failure(
        wait(
          cards.assign({
            employeeId: 24,
            periodId: ACTIVE_PERIOD,
            items: [{ templateId: 1, target: 0 }],
          }),
        ),
      );
      expect(zero.fieldErrors?.['items[0].target']).toBeDefined();
    });

    it('refuses ineligible employees, templates and periods', async () => {
      // EMP044 is Terminated.
      const left = await failure(
        wait(cards.assign({ employeeId: 44, periodId: 2, items: [{ templateId: 1 }] })),
      );
      expect(left.fieldErrors?.['employeeId']).toBeDefined();
      expect(left.message).toContain('EMP044 has left the company');
      // EMP012 works for company 2, the period is company 1's.
      const other = await failure(
        wait(cards.assign({ employeeId: 12, periodId: 2, items: [{ templateId: 1 }] })),
      );
      expect(other.message).toContain('does not belong to the company of period');
      // The ENG template for an IT employee.
      const department = await failure(
        wait(cards.assign({ employeeId: 56, periodId: 2, items: [{ templateId: 8 }] })),
      );
      expect(department.fieldErrors?.['items[0].templateId']).toBeDefined();
      // An inactive template.
      await wait(templates.update(1, { status: 'Inactive' }));
      const inactive = await failure(
        wait(cards.assign({ employeeId: 23, periodId: 2, items: [{ templateId: 1 }] })),
      );
      expect(inactive.fieldErrors?.['items[0].templateId']).toContain('inactive');
      // A Closed period, and an unknown one.
      const closed = await failure(
        wait(cards.assign({ employeeId: 23, periodId: 1, items: [{ templateId: 2 }] })),
      );
      expect(closed.code).toBe('Conflict');
      expect(closed.message).toContain('KPIs can only be assigned in a Draft or Active period');
      const unknown = await failure(
        wait(cards.assign({ employeeId: 23, periodId: 99, items: [{ templateId: 2 }] })),
      );
      expect(unknown.fieldErrors?.['periodId']).toBeDefined();
      expect(await wait(cards.query({ ...PAGE, filters: { employeeId: 44 } }))).toMatchObject({
        total: 0,
      });
    });

    it('checks the request shape', async () => {
      const none = await failure(wait(cards.assign({ employeeId: 23, periodId: 2, items: [] })));
      expect(none.fieldErrors?.['items']).toBeDefined();
      const bad = await failure(
        wait(
          cards.assign({
            employeeId: 23,
            periodId: 2,
            items: [
              { templateId: 1, weight: 0 },
              { templateId: 2, weight: 100.01 },
              { templateId: 3, weight: 10.123 },
            ],
          }),
        ),
      );
      expect(Object.keys(bad.fieldErrors ?? {})).toEqual([
        'items[0].weight',
        'items[1].weight',
        'items[2].weight',
      ]);
      const dates = await failure(
        wait(
          cards.assign({
            employeeId: 23,
            periodId: 2,
            items: [{ templateId: 1, startDate: '2000-01-01' }],
          }),
        ),
      );
      expect(dates.fieldErrors?.['items[0].startDate']).toContain('Must be within the period');
    });

    it('refuses weights above 100% together with what the scorecard already has', async () => {
      await assign(23, ACTIVE_PERIOD, [{ templateId: 1, weight: 70 }]);
      const error = await failure(
        wait(cards.assign({ employeeId: 23, periodId: 2, items: [{ templateId: 2, weight: 40 }] })),
      );
      expect(error.code).toBe('Validation');
      expect(error.message).toContain('would add up to 110.00%');
      expect(error.fieldErrors?.['items']).toBeDefined();
    });

    it('cannot add KPIs to a scorecard that has left Planning: its plan is already at 100%', async () => {
      // Seeded EMP046 is Submitted with a full plan. The weight check comes first, as in the API.
      const error = await failure(
        wait(cards.assign({ employeeId: 46, periodId: 2, items: [{ templateId: 7, weight: 5 }] })),
      );
      expect(error.code).toBe('Validation');
      expect(error.message).toContain('would add up to 105.00%');
    });

    describe('bulk', () => {
      it('assigns what it can, skips the rest with reasons, and never assigns twice', async () => {
        const request = {
          periodId: ACTIVE_PERIOD,
          departmentId: 4,
          items: [{ templateId: 8, weight: 30 }],
        };
        const first = await wait(cards.bulkAssign(request));
        expect(first.assigned.length).toBeGreaterThan(5);
        const skippedCodes = first.skipped.map((s) => s.employeeCode);
        expect(skippedCodes).toEqual(
          expect.arrayContaining(['EMP046', 'EMP017', 'EMP019', 'EMP014']),
        );
        const submitted = first.skipped.find((s) => s.employeeCode === 'EMP046')!;
        expect(submitted.reason).toBe(
          'The scorecard is Submitted; KPIs can only be assigned in Planning.',
        );
        const planning = first.skipped.find((s) => s.employeeCode === 'EMP019')!;
        expect(planning.reason).toBe('KPI ENG-REVIEW-HOURS is already assigned.');

        const activity = TestBed.inject(ActivityMockService);
        const bulkLines = () =>
          activity.snapshot(100).filter((a) => a.message.startsWith('KPIs assigned in bulk'));
        expect(bulkLines()).toHaveLength(1);
        expect(bulkLines()[0].category).toBe('performance');
        expect(bulkLines()[0].message).toBe(
          `KPIs assigned in bulk for "${(await wait(periods.getById(2))).name}": ${first.assigned.length} employee(s) assigned, ${first.skipped.length} skipped`,
        );

        const second = await wait(cards.bulkAssign(request));
        expect(second.assigned).toEqual([]);
        expect(second.skipped.length).toBe(first.assigned.length + first.skipped.length);
        expect(bulkLines()).toHaveLength(1); // none for a run that assigned nobody
      });

      it('selects by employee ids, skipping unknown, left and other-company employees', async () => {
        const result = await wait(
          cards.bulkAssign({
            periodId: ACTIVE_PERIOD,
            employeeIds: [23, 23, 44, 12, 999],
            items: [{ templateId: 1, weight: 100 }],
          }),
        );
        expect(result.assigned.map((a) => a.employeeCode)).toEqual(['EMP023']);
        expect(result.assigned[0].itemsAdded).toBe(1);
        expect(result.skipped).toEqual([
          { employeeId: 44, employeeCode: 'EMP044', reason: 'Has left the company.' },
          expect.objectContaining({ employeeId: 12, employeeCode: 'EMP012' }),
          { employeeId: 999, employeeCode: null, reason: 'Employee was not found.' },
        ]);
      });

      it('skips an employee whose weights would pass 100%', async () => {
        await assign(23, ACTIVE_PERIOD, [{ templateId: 1, weight: 80 }]);
        const result = await wait(
          cards.bulkAssign({
            periodId: ACTIVE_PERIOD,
            employeeIds: [23],
            items: [{ templateId: 2, weight: 30 }],
          }),
        );
        expect(result.skipped[0].reason).toContain('110.00%');
      });

      it('needs exactly one kind of selector', async () => {
        const items = [{ templateId: 1 }];
        const both = await failure(
          wait(cards.bulkAssign({ periodId: 2, employeeIds: [23], departmentId: 4, items })),
        );
        expect(both.fieldErrors?.['employeeIds']).toBeDefined();
        const neither = await failure(wait(cards.bulkAssign({ periodId: 2, items })));
        expect(neither.fieldErrors?.['employeeIds']).toBeDefined();
      });

      it('fails the whole call for a bad period or template before writing anything', async () => {
        const before = (await wait(cards.query(PAGE))).total;
        const closed = await failure(
          wait(cards.bulkAssign({ periodId: 1, employeeIds: [23], items: [{ templateId: 1 }] })),
        );
        expect(closed.code).toBe('Conflict');
        const template = await failure(
          wait(cards.bulkAssign({ periodId: 2, employeeIds: [23], items: [{ templateId: 99 }] })),
        );
        expect(template.fieldErrors?.['items[0].templateId']).toBeDefined();
        expect((await wait(cards.query(PAGE))).total).toBe(before);
      });
    });
  });

  describe('plan', () => {
    /** General plan (no department-only template), 100% in all. */
    const GENERAL_ITEMS = [
      { templateId: 1, weight: 40 },
      { templateId: 5, weight: 25 },
      { templateId: 7, weight: 20 },
      { templateId: 6, weight: 15 },
    ];

    it('lets nobody plan their own scorecard; bulk assign skips the caller (separation of duties)', async () => {
      const periodId = await plainPeriod();
      const OWN = 'You cannot plan your own scorecard.';
      // Su Myat Noe (HR_ADMIN) is EMP002.
      session.as(HR_ADMIN);
      const assign = await failure(
        wait(cards.assign({ employeeId: 2, periodId, items: GENERAL_ITEMS })),
      );
      expect(assign).toMatchObject({ code: 'Conflict', message: OWN });

      // Another HR person plans it; Su Myat Noe may still not change, remove, delete or finalize it.
      session.as(HR_OFFICER);
      const { scorecard } = await wait(
        cards.assign({ employeeId: 2, periodId, items: GENERAL_ITEMS }),
      );
      const item = scorecard.items[0];
      session.as(HR_ADMIN);
      for (const attempt of [
        cards.updateItem(item.id, {
          rowVersion: item.rowVersion,
          target: item.target,
          weight: item.weight,
          startDate: item.startDate,
          endDate: item.endDate,
        }),
        cards.deleteItem(item.id, item.rowVersion),
        cards.deleteScorecard(scorecard.id, scorecard.rowVersion),
        cards.finalizePlan(scorecard.id, scorecard.rowVersion),
      ]) {
        expect(await failure(wait(attempt as never))).toMatchObject({
          code: 'Conflict',
          message: OWN,
        });
      }
      expect((await wait(cards.getById(scorecard.id))).status).toBe('Planning');

      // Bulk: the caller is skipped with the same reason, the others are assigned.
      const bulk = await wait(
        cards.bulkAssign({ periodId, employeeIds: [2, 23], items: [{ templateId: 3 }] }),
      );
      expect(bulk.skipped).toEqual([{ employeeId: 2, employeeCode: 'EMP002', reason: OWN }]);
      expect(bulk.assigned.map((a) => a.employeeId)).toEqual([23]);

      // HR_OFFICER finalizes it: allowed, it is not her own.
      session.as(HR_OFFICER);
      expect((await wait(cards.finalizePlan(scorecard.id, scorecard.rowVersion))).status).toBe(
        'Draft',
      );
    });

    it('finalizes a plan at exactly 100% and sets the reviewer to the current manager', async () => {
      const { scorecard } = await assign(23);
      const draft = await wait(cards.finalizePlan(scorecard.id, scorecard.rowVersion));
      expect(draft.status).toBe('Draft');
      expect(draft.reviewerEmployeeId).toBe(5);
      expect(draft.reviewerName).toBe('Daniel Carter');
      expect(draft.planFinalizedOn).not.toBeNull();
      expect(draft.history).toHaveLength(1);
      expect(draft.history[0]).toMatchObject({
        step: 'HR',
        action: 'Finalize Plan',
        fromStatus: 'Planning',
        toStatus: 'Draft',
        actorName: 'Su Myat Noe',
      });
      expect(draft.rowVersion).not.toBe(scorecard.rowVersion);
    });

    it('refuses a plan that does not add up to 100% and names the total', async () => {
      const { scorecard } = await assign(23, ACTIVE_PERIOD, [
        { templateId: 1, weight: 40 },
        { templateId: 2, weight: 35 },
      ]);
      const error = await failure(wait(cards.finalizePlan(scorecard.id, scorecard.rowVersion)));
      expect(error.code).toBe('Validation');
      expect(error.message).toBe('The KPI weights must add up to 100%. They add up to 75.00%.');
      expect(error.fieldErrors?.['weights']).toBeDefined();
      expect((await wait(cards.getById(scorecard.id))).status).toBe('Planning');
    });

    it('finalizes only a total of exactly 100%', async () => {
      const { scorecard } = await assign(23, ACTIVE_PERIOD, [{ templateId: 1, weight: 60 }]);
      // 60 + 40.01 passes 100: the assignment itself is refused.
      const over = await failure(
        wait(
          cards.assign({ employeeId: 23, periodId: 2, items: [{ templateId: 2, weight: 40.01 }] }),
        ),
      );
      expect(over.message).toContain('would add up to 100.01%');
      // 60 + 39.99 can be assigned but not finalized.
      const low = await wait(
        cards.assign({ employeeId: 23, periodId: 2, items: [{ templateId: 2, weight: 39.99 }] }),
      );
      const refused = await failure(
        wait(cards.finalizePlan(low.scorecard.id, low.scorecard.rowVersion)),
      );
      expect(refused.message).toBe('The KPI weights must add up to 100%. They add up to 99.99%.');
      // Raising that item to 40 makes exactly 100%.
      const item = low.scorecard.items[1];
      await wait(
        cards.updateItem(item.id, {
          target: item.target,
          weight: 40,
          startDate: item.startDate,
          endDate: item.endDate,
          rowVersion: item.rowVersion,
        }),
      );
      const ok = await wait(cards.finalizePlan(scorecard.id, low.scorecard.rowVersion));
      expect(ok.status).toBe('Draft');
      expect(ok.totalWeight).toBe(100);
    });

    it('updates and removes items only in Planning, with the item row version', async () => {
      const { scorecard } = await assign(23);
      const item = scorecard.items[1];
      const updated = await wait(
        cards.updateItem(item.id, {
          target: 120,
          weight: 25,
          startDate: item.startDate,
          endDate: item.endDate,
          rowVersion: item.rowVersion,
        }),
      );
      expect(updated.target).toBe(120);
      expect(updated.weight).toBe(25);
      expect(updated.sortOrder).toBe(item.sortOrder);
      expect(updated.rowVersion).not.toBe(item.rowVersion);

      const stale = await failure(
        wait(
          cards.updateItem(item.id, {
            target: 130,
            weight: 25,
            startDate: item.startDate,
            endDate: item.endDate,
            rowVersion: item.rowVersion,
          }),
        ),
      );
      expect(stale.code).toBe('Conflict');

      const over = await failure(
        wait(
          cards.updateItem(item.id, {
            target: 120,
            weight: 60,
            startDate: item.startDate,
            endDate: item.endDate,
            rowVersion: updated.rowVersion,
          }),
        ),
      );
      expect(over.fieldErrors?.['weight']).toBeDefined();

      await wait(cards.deleteItem(item.id, updated.rowVersion));
      const reread = await wait(cards.getById(scorecard.id));
      expect(reread.items).toHaveLength(4);
      expect(reread.totalWeight).toBe(70);

      // After the plan is final, items cannot change any more.
      const sixty = await wait(
        cards.assign({ employeeId: 23, periodId: 2, items: [{ templateId: 7, weight: 30 }] }),
      );
      const finalized = await wait(
        cards.finalizePlan(sixty.scorecard.id, sixty.scorecard.rowVersion),
      );
      const locked = await failure(
        wait(cards.deleteItem(finalized.items[0].id, finalized.items[0].rowVersion)),
      );
      expect(locked.code).toBe('Conflict');
      expect(locked.message).toContain('can only be changed while it is in Planning');
    });

    it('deletes a Planning scorecard with its items, and nothing with a history', async () => {
      const { scorecard } = await assign(23);
      await wait(cards.deleteScorecard(scorecard.id, scorecard.rowVersion));
      expect((await failure(wait(cards.getById(scorecard.id)))).code).toBe('NotFound');
      const draft = await draftCard();
      const refused = await failure(wait(cards.deleteScorecard(draft.id, draft.rowVersion)));
      expect(refused.code).toBe('Conflict');
    });

    it('protects a template and a category used by assigned KPIs from deletion', async () => {
      await assign(23);
      const template = await failure(wait(templates.delete(1)));
      expect(template.message).toContain('used by');
      expect(template.message).toContain('assigned KPI(s)');
    });
  });

  describe('results and submission', () => {
    it('saves a draft with the server figures and no overall score', async () => {
      const draft = await draftCard();
      const saved = await withResults(draft, [30, 92, null as unknown as number, 1, 93]);
      expect(saved.items.map((i) => i.achievementPct)).toEqual([80, 92, null, 100, 97.89]);
      expect(saved.items[0].weightedScore).toBe(24);
      expect(saved.overallScore).toBeNull();
      expect(saved.status).toBe('Draft');
      expect(saved.rowVersion).not.toBe(draft.rowVersion);
    });

    it('refuses a submit with a missing result, pointing at the item', async () => {
      const draft = await draftCard();
      const saved = await withResults(draft, [30, 92, null as unknown as number, 1, 93]);
      const error = await failure(wait(cards.submit(saved.id, saved.rowVersion)));
      expect(error.code).toBe('Validation');
      expect(Object.keys(error.fieldErrors ?? {})).toEqual(['items[2].actualValue']);
      expect((await wait(cards.getById(saved.id))).status).toBe('Draft');
    });

    it('submits, scores and lets nobody change the results afterwards', async () => {
      const submitted = await submittedCard();
      expect(submitted.status).toBe('Submitted');
      expect(submitted.overallScore).toBe(88.06);
      expect(submitted.ratingLabel).toBe('Exceeds Expectations');
      expect(submitted.submittedOn).not.toBeNull();
      expect(submitted.history.map((h) => [h.step, h.action])).toEqual([
        ['HR', 'Finalize Plan'],
        ['Employee', 'Submit'],
      ]);
      expect(submitted.history[1].overallScore).toBe(88.06);

      session.as(EMPLOYEE_23);
      const again = await failure(
        wait(
          cards.saveResults(submitted.id, {
            rowVersion: submitted.rowVersion,
            items: [{ id: submitted.items[0].id, actualValue: 1 }],
          }),
        ),
      );
      expect(again.code).toBe('Conflict');
      expect(again.message).toBe(
        'The scorecard is Submitted; results can only be entered while it is Draft or Returned.',
      );
    });

    it('validates each result on its item', async () => {
      const draft = await draftCard();
      session.as(EMPLOYEE_23);
      const lower = await failure(
        wait(
          cards.saveResults(draft.id, {
            rowVersion: draft.rowVersion,
            items: [
              { id: draft.items[0].id, actualValue: -1 }, // lower is better
              { id: draft.items[3].id, actualValue: 0.5 }, // yes/no
              { id: draft.items[1].id, actualValue: 10.12345 }, // five decimals
            ],
          }),
        ),
      );
      expect(Object.keys(lower.fieldErrors ?? {})).toEqual([
        'items[0].actualValue',
        'items[1].actualValue',
        'items[2].actualValue',
      ]);
      const foreign = await failure(
        wait(
          cards.saveResults(draft.id, {
            rowVersion: draft.rowVersion,
            items: [
              { id: draft.items[0].id, actualValue: 1 },
              { id: draft.items[0].id, actualValue: 2 },
              { id: 99999, actualValue: 2 },
            ],
          }),
        ),
      );
      expect(foreign.fieldErrors?.['items[1].id']).toBe('A KPI can appear only once.');
      expect(foreign.fieldErrors?.['items[2].id']).toBe('This KPI is not on the scorecard.');
      const long = await failure(
        wait(
          cards.saveResults(draft.id, {
            rowVersion: draft.rowVersion,
            employeeComment: 'x'.repeat(1001),
            items: [],
          }),
        ),
      );
      expect(long.fieldErrors?.['employeeComment']).toBeDefined();
    });

    it('lets only the scorecard owner enter results and submit', async () => {
      const draft = await draftCard();
      session.as(HR_ADMIN);
      const results = await failure(
        wait(
          cards.saveResults(draft.id, {
            rowVersion: draft.rowVersion,
            items: [{ id: draft.items[0].id, actualValue: 1 }],
          }),
        ),
      );
      expect(results.code).toBe('Forbidden');
      expect(results.message).toBe('You can only enter results on your own scorecard.');
      const submit = await failure(wait(cards.submit(draft.id, draft.rowVersion)));
      expect(submit.message).toBe('You can only submit your own scorecard.');
      // Outside the caller's scope the scorecard is not even visible.
      session.as(EMPLOYEE_56);
      expect((await failure(wait(cards.getById(draft.id)))).code).toBe('NotFound');
      expect((await failure(wait(cards.submit(draft.id, draft.rowVersion)))).code).toBe('NotFound');
    });

    it('rejects a stale or missing row version', async () => {
      const draft = await draftCard();
      const first = await withResults(draft);
      session.as(EMPLOYEE_23);
      const stale = await failure(wait(cards.submit(draft.id, draft.rowVersion)));
      expect(stale.code).toBe('Conflict');
      const missing = await failure(wait(cards.submit(draft.id, '')));
      expect(missing.fieldErrors?.['rowVersion']).toBeDefined();
      await wait(cards.submit(first.id, first.rowVersion));
    });

    it('needs an Active period for results, submission and evidence', async () => {
      const draft = await draftCard();
      await wait(periods.complete(ACTIVE_PERIOD));
      session.as(EMPLOYEE_23);
      const results = await failure(
        wait(
          cards.saveResults(draft.id, {
            rowVersion: draft.rowVersion,
            items: [{ id: draft.items[0].id, actualValue: 1 }],
          }),
        ),
      );
      expect(results.code).toBe('Conflict');
      expect(results.message).toContain('is Completed; results can only be entered and submitted');
      const submit = await failure(wait(cards.submit(draft.id, draft.rowVersion)));
      expect(submit.message).toContain('is Completed');
      const evidence = await failure(
        wait(cards.uploadEvidence(draft.items[0].id, draft.rowVersion, pdfFile())),
      );
      expect(evidence.message).toContain(
        'evidence can only be added or removed while the period is Active',
      );
    });

    it('lets a Returned scorecard be corrected, given evidence and resubmitted while the period is Completed (M1)', async () => {
      const periodId = await plainPeriod();
      const submitted = await submittedCard(periodId);
      session.as(MANAGER);
      const noted = await wait(
        cards.saveReview(submitted.id, {
          rowVersion: submitted.rowVersion,
          managerRating: 3,
          managerComment: 'Fictional draft note',
          items: [{ id: submitted.items[0].id, managerRating: 3, managerComment: 'Item note' }],
        }),
      );
      session.as(HR_ADMIN);
      await wait(periods.complete(periodId));

      // A reviewer may return it while the period is Completed; the review draft goes with the score.
      session.as(MANAGER);
      const returned = await wait(
        cards.returnForRevision(noted.id, {
          rowVersion: noted.rowVersion,
          comment: 'Fictional reason',
        }),
      );
      expect(returned).toMatchObject({
        status: 'Returned',
        overallScore: null,
        managerRating: null,
        managerComment: null,
        hrComment: null,
      });
      expect(
        returned.items.every((i) => i.managerRating === null && i.managerComment === null),
      ).toBe(true);

      // The employee corrects it, adds evidence and resubmits, all while the period is Completed.
      session.as(EMPLOYEE_23);
      const edited = await wait(
        cards.saveResults(returned.id, {
          rowVersion: returned.rowVersion,
          items: [{ id: returned.items[1].id, actualValue: 100 }],
        }),
      );
      const uploaded = await wait(
        cards.uploadEvidence(edited.items[0].id, edited.rowVersion, pdfFile()),
      );
      const resubmitted = await wait(cards.submit(edited.id, uploaded.scorecardRowVersion));
      expect(resubmitted.status).toBe('Resubmitted');
      expect(resubmitted.history.at(-1)).toMatchObject({ step: 'Employee', action: 'Resubmit' });
    });

    it('refuses a Returned scorecard once the period is Closed, with the API text', async () => {
      const periodId = await plainPeriod();
      const submitted = await submittedCard(periodId);
      session.as(MANAGER);
      const returned = await wait(
        cards.returnForRevision(submitted.id, {
          rowVersion: submitted.rowVersion,
          comment: 'Fictional reason',
        }),
      );
      // Closing waits for it (M2), so close the period in the store the way a database change would.
      closeInStore(periodId);
      session.as(EMPLOYEE_23);
      const refused = await failure(wait(cards.submit(returned.id, returned.rowVersion)));
      expect(refused.code).toBe('Conflict');
      expect(refused.message).toBe(
        'Period "Test plain year" is Closed; a returned scorecard can only be changed and resubmitted while the period is Active or Completed.',
      );
      const evidence = await failure(
        wait(cards.uploadEvidence(returned.items[0].id, returned.rowVersion, pdfFile())),
      );
      expect(evidence.message).toBe(
        'Period "Test plain year" is Closed; evidence of a returned scorecard can only be added or removed while the period is Active or Completed.',
      );
    });
  });

  describe('review', () => {
    it('lets the reviewer start a review, save notes and approve; the period without HR review finalizes at once', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      const started = await wait(cards.startReview(submitted.id, submitted.rowVersion));
      expect(started.status).toBe('Under Review');
      const noted = await wait(
        cards.saveReview(started.id, {
          rowVersion: started.rowVersion,
          managerRating: 4,
          managerComment: 'Fictional feedback',
          items: [{ id: started.items[0].id, managerRating: 4.5, managerComment: 'Quick reviews' }],
        }),
      );
      expect(noted.managerRating).toBe(4);
      expect(noted.items[0].managerRating).toBe(4.5);
      expect(noted.history).toHaveLength(3); // saving a review adds no history
      const approved = await wait(cards.approve(noted.id, { rowVersion: noted.rowVersion }));
      expect(approved.status).toBe('Finalized');
      expect(approved.finalizedOn).not.toBeNull();
      expect(approved.overallScore).toBe(88.06);
      expect(approved.history.map((h) => h.action)).toEqual([
        'Finalize Plan',
        'Submit',
        'Start Review',
        'Approve',
      ]);
      expect(approved.history[3]).toMatchObject({ step: 'Manager', overallScore: 88.06 });
      expect(approved.history[3].actorName).toBe('Daniel Carter');
      // The employee sees the feedback and the final score.
      session.as(EMPLOYEE_23);
      const own = await wait(cards.getById(approved.id));
      expect(own.managerComment).toBe('Fictional feedback');
    });

    it('shows the employee the review only once it is final (review visibility, as the API)', async () => {
      const periodId = await hrReviewPeriod();
      const submitted = await submittedCard(periodId);
      session.as(MANAGER);
      const noted = await wait(
        cards.saveReview(submitted.id, {
          rowVersion: submitted.rowVersion,
          managerRating: 4,
          managerComment: 'Fictional draft',
          items: [{ id: submitted.items[0].id, managerRating: 4.5, managerComment: 'Item note' }],
        }),
      );
      const hidden = { managerRating: null, managerComment: null, hrComment: null };

      // A draft review is the reviewer's: the employee reads nulls, everyone else the notes.
      session.as(EMPLOYEE_23);
      let own = await wait(cards.getById(noted.id));
      expect(own).toMatchObject(hidden);
      expect(own.items[0]).toMatchObject({ managerRating: null, managerComment: null });
      session.as(HR_OFFICER);
      expect((await wait(cards.getById(noted.id))).managerComment).toBe('Fictional draft');

      // Approved: HR can still return it, so it stays hidden, the Approve comment too.
      session.as(MANAGER);
      const approved = await wait(
        cards.approve(noted.id, { rowVersion: noted.rowVersion, comment: 'Approval note' }),
      );
      expect(approved.managerRating).toBe(4); // the reviewer's own answer
      session.as(EMPLOYEE_23);
      own = await wait(cards.getById(approved.id));
      expect(own).toMatchObject({ status: 'Approved', ...hidden });
      expect(own.history.find((h) => h.action === 'Approve')?.comment).toBeNull();

      // Finalized: the employee reads all of it, the HR comment included.
      session.as(HR_ADMIN);
      await wait(
        cards.finalize(approved.id, { rowVersion: approved.rowVersion, comment: 'HR note' }),
      );
      session.as(EMPLOYEE_23);
      own = await wait(cards.getById(approved.id));
      expect(own).toMatchObject({
        status: 'Finalized',
        managerRating: 4,
        managerComment: 'Fictional draft',
        hrComment: 'HR note',
      });
      expect(own.items[0]).toMatchObject({ managerRating: 4.5, managerComment: 'Item note' });
      expect(own.history.find((h) => h.action === 'Approve')?.comment).toBe('Approval note');
      expect(own.history.find((h) => h.action === 'Finalize')?.comment).toBe('HR note');
    });

    it('shows the employee a Return reason at once, and a Rejected review but no HR comment', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      const returned = await wait(
        cards.returnForRevision(submitted.id, {
          rowVersion: submitted.rowVersion,
          comment: 'Fictional reason',
        }),
      );
      session.as(EMPLOYEE_23);
      const own = await wait(cards.getById(returned.id));
      expect(own.history.at(-1)).toMatchObject({ action: 'Return', comment: 'Fictional reason' });

      const resubmitted = await wait(cards.submit(own.id, own.rowVersion));
      session.as(MANAGER);
      const noted = await wait(
        cards.saveReview(resubmitted.id, {
          rowVersion: resubmitted.rowVersion,
          managerRating: 2,
          managerComment: 'Fictional feedback',
          items: [],
        }),
      );
      await wait(
        cards.reject(noted.id, { rowVersion: noted.rowVersion, comment: 'Fictional rejection' }),
      );
      session.as(EMPLOYEE_23);
      const rejected = await wait(cards.getById(noted.id));
      expect(rejected).toMatchObject({
        status: 'Rejected',
        managerRating: 2,
        managerComment: 'Fictional feedback',
        hrComment: null,
      });
      expect(rejected.history.at(-1)?.comment).toBe('Fictional rejection');
    });

    it('validates ratings and comments of a review draft', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      const error = await failure(
        wait(
          cards.saveReview(submitted.id, {
            rowVersion: submitted.rowVersion,
            managerRating: 6,
            items: [{ id: submitted.items[0].id, managerRating: 0.5 }],
          }),
        ),
      );
      expect(Object.keys(error.fieldErrors ?? {})).toEqual([
        'managerRating',
        'items[0].managerRating',
      ]);
    });

    it('holds an approved scorecard for HR when the period requires an HR review', async () => {
      const periodId = await hrReviewPeriod();
      const submitted = await submittedCard(periodId);
      session.as(MANAGER);
      const approved = await wait(
        cards.approve(submitted.id, { rowVersion: submitted.rowVersion }),
      );
      expect(approved.status).toBe('Approved');
      expect(approved.finalizedOn).toBeNull();
      // The manager cannot finalize.
      const manager = await failure(
        wait(cards.finalize(approved.id, { rowVersion: approved.rowVersion })),
      );
      expect(manager.code).toBe('Forbidden');
      expect(manager.message).toBe('Only HR can give the final review of a scorecard.');
      session.as(HR_ADMIN);
      const finalized = await wait(
        cards.finalize(approved.id, { rowVersion: approved.rowVersion, comment: 'HR note' }),
      );
      expect(finalized.status).toBe('Finalized');
      expect(finalized.hrComment).toBe('HR note');
      expect(finalized.history.at(-1)).toMatchObject({ step: 'HR', action: 'Finalize' });
    });

    it('does not let the user who approved at the manager stage give the HR final review', async () => {
      const periodId = await hrReviewPeriod();
      const submitted = await submittedCard(periodId);
      session.as(HR_ADMIN); // HR may approve any scorecard at the manager stage
      const approved = await wait(
        cards.approve(submitted.id, { rowVersion: submitted.rowVersion }),
      );
      expect(approved.status).toBe('Approved');
      for (const action of ['finalize', 'returnForRevision', 'reject'] as const) {
        const same = await failure(
          wait(cards[action](approved.id, { rowVersion: approved.rowVersion, comment: 'Reason' })),
        );
        expect(same.code, action).toBe('Conflict');
        expect(same.message).toBe(
          'The person who approved this scorecard cannot also give the HR final review.',
        );
      }
      session.as(HR_OFFICER);
      const finalized = await wait(
        cards.finalize(approved.id, { rowVersion: approved.rowVersion }),
      );
      expect(finalized.status).toBe('Finalized');
    });

    it('returns with a reason, clears the score, and the resubmission is scored again', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      const blank = await failure(
        wait(
          cards.returnForRevision(submitted.id, { rowVersion: submitted.rowVersion, comment: '' }),
        ),
      );
      expect(blank.fieldErrors?.['comment']).toBe('A reason is required.');
      const spaces = await failure(
        wait(cards.reject(submitted.id, { rowVersion: submitted.rowVersion, comment: '   ' })),
      );
      expect(spaces.message).toBe('A reason is required.');
      expect((await wait(cards.getById(submitted.id))).status).toBe('Submitted');

      const returned = await wait(
        cards.returnForRevision(submitted.id, {
          rowVersion: submitted.rowVersion,
          comment: 'Fictional reason',
        }),
      );
      expect(returned.status).toBe('Returned');
      expect(returned.overallScore).toBeNull();
      expect(returned.ratingLabel).toBeNull();
      expect(returned.history.at(-1)).toMatchObject({
        action: 'Return',
        comment: 'Fictional reason',
        overallScore: 88.06, // the history keeps the score at that time
      });

      // The employee changes a result and submits again.
      session.as(EMPLOYEE_23);
      const edited = await wait(
        cards.saveResults(returned.id, {
          rowVersion: returned.rowVersion,
          items: [{ id: returned.items[1].id, actualValue: 100 }],
        }),
      );
      const resubmitted = await wait(cards.submit(edited.id, edited.rowVersion));
      expect(resubmitted.status).toBe('Resubmitted');
      expect(resubmitted.history.at(-1)).toMatchObject({ step: 'Employee', action: 'Resubmit' });
      // Output now 100 of 100: the overall score rose.
      expect(resubmitted.overallScore).toBe(90.46);
    });

    it('rejects for good', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      const rejected = await wait(
        cards.reject(submitted.id, {
          rowVersion: submitted.rowVersion,
          comment: 'Fictional reason',
        }),
      );
      expect(rejected.status).toBe('Rejected');
      session.as(EMPLOYEE_23);
      const resubmit = await failure(wait(cards.submit(rejected.id, rejected.rowVersion)));
      expect(resubmit.code).toBe('Conflict');
      expect(resubmit.message).toContain('The scorecard is Rejected');
    });

    it('refuses nobody-reviews-their-own and wrong-person reviews', async () => {
      // Seeded: EMP002 (Su Myat Noe) has her own Submitted scorecard.
      const own = (
        await wait(cards.query({ ...PAGE, filters: { employeeId: 2, status: 'Submitted' } }))
      ).items[0];
      session.as(HR_ADMIN);
      for (const attempt of [
        cards.startReview(own.id, own.rowVersion),
        cards.approve(own.id, { rowVersion: own.rowVersion }),
        cards.reject(own.id, { rowVersion: own.rowVersion, comment: 'No' }),
        cards.returnForRevision(own.id, { rowVersion: own.rowVersion, comment: 'No' }),
      ]) {
        const error = await failure(wait(attempt));
        expect(error.code).toBe('Conflict');
        expect(error.message).toBe('You cannot review your own scorecard.');
      }
      // A manager outside the team does not even see it.
      const submitted = await submittedCard();
      session.as(OTHER_MANAGER);
      expect(
        (await failure(wait(cards.approve(submitted.id, { rowVersion: submitted.rowVersion }))))
          .code,
      ).toBe('NotFound');
      // A plain employee outside the scope neither.
      session.as(EMPLOYEE_56);
      expect(
        (await failure(wait(cards.startReview(submitted.id, submitted.rowVersion)))).code,
      ).toBe('NotFound');
    });

    it('keeps the reviewer fixed at plan finalization, even when the manager changes', async () => {
      const submitted = await submittedCard();
      session.as(HR_ADMIN);
      await wait(TestBed.inject(EmployeeDataService).update(23, { managerId: 9 }));
      // The new manager (EMP009) sees the scorecard as a direct report, but cannot review it.
      session.as(OTHER_MANAGER);
      expect((await wait(cards.getById(submitted.id))).reviewerEmployeeId).toBe(5);
      const denied = await failure(wait(cards.startReview(submitted.id, submitted.rowVersion)));
      expect(denied.code).toBe('Forbidden');
      expect(denied.message).toBe("Only the employee's reviewer or HR can review this scorecard.");
      // The old manager still reviews it.
      session.as(MANAGER);
      expect((await wait(cards.startReview(submitted.id, submitted.rowVersion))).status).toBe(
        'Under Review',
      );
    });

    it('refuses invalid transitions with the current status and stale versions', async () => {
      const draft = await draftCard();
      session.as(MANAGER);
      const approveDraft = await failure(
        wait(cards.approve(draft.id, { rowVersion: draft.rowVersion })),
      );
      expect(approveDraft.code).toBe('Conflict');
      expect(approveDraft.message).toBe(
        'The scorecard is Draft; only a Submitted, Resubmitted or Under Review scorecard can be approved.',
      );
    });

    it('records one approval for two competing approvers: the second one is stale', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      await wait(cards.approve(submitted.id, { rowVersion: submitted.rowVersion }));
      session.as(HR_ADMIN);
      const second = await failure(
        wait(cards.approve(submitted.id, { rowVersion: submitted.rowVersion })),
      );
      expect(second.code).toBe('Conflict');
      expect(second.message).toBe('The record changed. Reload and try again.');
      const detail = await wait(cards.getById(submitted.id));
      expect(detail.history.filter((h) => h.action === 'Approve')).toHaveLength(1);
    });

    it('reviews a Completed period, which closes only once the review is finished (M2)', async () => {
      const periodId = await plainPeriod();
      const submitted = await submittedCard(periodId);
      session.as(HR_ADMIN);
      await wait(periods.complete(periodId));
      session.as(MANAGER);
      const started = await wait(cards.startReview(submitted.id, submitted.rowVersion));
      expect(started.status).toBe('Under Review');

      session.as(HR_ADMIN);
      const blocked = await failure(wait(periods.close(periodId)));
      expect(blocked.code).toBe('Conflict');
      expect(blocked.message).toBe(
        'Period "Test plain year" still has 1 scorecard(s) in review or returned to the employee. Finish them before closing.',
      );

      session.as(MANAGER);
      const approved = await wait(cards.approve(started.id, { rowVersion: started.rowVersion }));
      expect(approved.status).toBe('Finalized');
      session.as(HR_ADMIN);
      expect((await wait(periods.close(periodId))).status).toBe('Closed');
    });

    it('refuses a review step in a Closed period', async () => {
      // Seeded period 1 is Closed; card 8 (EMP046) is Finalized there. The period is checked first.
      session.as(HR_ADMIN);
      const card = await wait(cards.getById(8));
      const closed = await failure(wait(cards.approve(card.id, { rowVersion: card.rowVersion })));
      expect(closed.code).toBe('Conflict');
      expect(closed.message).toContain(
        'a scorecard can only be reviewed while its period is Active or Completed',
      );
    });

    it('logs the workflow in the activity feed without comments or results', async () => {
      const submitted = await submittedCard();
      session.as(MANAGER);
      await wait(
        cards.returnForRevision(submitted.id, {
          rowVersion: submitted.rowVersion,
          comment: 'Secret reason',
        }),
      );
      const lines = TestBed.inject(ActivityMockService)
        .snapshot(50)
        .filter((a) => a.category === 'performance')
        .map((a) => a.message);
      const period = (await wait(periods.getById(ACTIVE_PERIOD))).name;
      expect(lines).toContain(`Scorecard of EMP023 for "${period}" submitted`);
      // A decision never tells which one it was (the API's wording).
      expect(lines[0]).toBe(`Scorecard of EMP023 for "${period}" was reviewed`);
      expect(lines.some((l) => /returned|rejected|approved|resubmitted/.test(l))).toBe(false);
      expect(lines.some((l) => l.includes('Secret reason'))).toBe(false);
    });

    it('logs approve, reject, the HR final review and a resubmission without telling them apart', async () => {
      const periodId = await hrReviewPeriod();
      const first = await submittedCard(periodId);
      session.as(MANAGER);
      const returned = await wait(
        cards.returnForRevision(first.id, { rowVersion: first.rowVersion, comment: 'Fictional' }),
      );
      session.as(EMPLOYEE_23);
      const again = await wait(cards.submit(returned.id, returned.rowVersion));
      session.as(MANAGER);
      const approved = await wait(cards.approve(again.id, { rowVersion: again.rowVersion }));
      session.as(HR_ADMIN);
      await wait(cards.finalize(approved.id, { rowVersion: approved.rowVersion }));
      const lines = TestBed.inject(ActivityMockService)
        .snapshot(50)
        .filter((a) => a.category === 'performance')
        .map((a) => a.message);
      const subject = `Scorecard of EMP023 for "Test HR-review year"`;
      // Newest first: finalize, approve, resubmit, return, submit.
      expect(lines.slice(0, 5)).toEqual([
        `${subject} was reviewed`,
        `${subject} was reviewed`,
        `${subject} submitted`,
        `${subject} was reviewed`,
        `${subject} submitted`,
      ]);
    });
  });

  describe('visibility', () => {
    it('shows an employee only their own scorecards, a manager the team, HR everything', async () => {
      session.as(EMPLOYEE_46);
      const own = await wait(cards.query(PAGE));
      expect(own.items.every((c) => c.employeeCode === 'EMP046')).toBe(true);
      expect(own.total).toBe(2);

      session.as(MANAGER);
      const team = await wait(cards.query(PAGE));
      const codes = new Set(team.items.map((c) => c.employeeCode));
      expect(codes.has('EMP046')).toBe(true);
      expect(codes.has('EMP056')).toBe(false);

      session.as(HR_ADMIN);
      const all = await wait(cards.query(PAGE));
      expect(all.total).toBe(12);
      expect(all.total).toBeGreaterThan(team.total);
    });

    it('lists my scorecards without Planning, newest period first', async () => {
      session.as(EMPLOYEE_46);
      const mine = await wait(cards.myScorecards(PAGE));
      expect(mine.items.map((c) => c.status)).toEqual(['Submitted', 'Finalized']);
      session.as({ ...EMPLOYEE_46, employeeId: 19 });
      expect((await wait(cards.myScorecards(PAGE))).total).toBe(0); // EMP019 only has a Planning plan
    });

    it('queues the scorecards waiting for the reviewer, never the caller\u2019s own', async () => {
      session.as(MANAGER);
      const queue = await wait(cards.pendingReviews(PAGE));
      expect(queue.items.map((c) => c.employeeCode)).toContain('EMP046');
      expect(
        queue.items.every((c) => ['Submitted', 'Under Review', 'Resubmitted'].includes(c.status)),
      ).toBe(true);
      expect(queue.items.some((c) => c.employeeCode === 'EMP056')).toBe(false);

      session.as(HR_ADMIN);
      const hr = await wait(cards.pendingReviews(PAGE));
      expect(hr.items.some((c) => c.employeeCode === 'EMP002')).toBe(false); // her own scorecard
      expect(hr.items.some((c) => c.employeeCode === 'EMP046')).toBe(true);

      session.as(EMPLOYEE_46);
      expect((await wait(cards.pendingReviews(PAGE))).total).toBe(0);
    });

    it('queues only scorecards whose period still allows a review: Active or Completed (M2)', async () => {
      session.as(HR_ADMIN);
      const before = await wait(cards.pendingReviews(PAGE));
      expect(before.items.some((c) => c.periodId === ACTIVE_PERIOD)).toBe(true);
      await wait(periods.complete(ACTIVE_PERIOD));
      expect((await wait(cards.pendingReviews(PAGE))).total).toBe(before.total);
      closeInStore(ACTIVE_PERIOD);
      const after = await wait(cards.pendingReviews(PAGE));
      expect(after.items.filter((c) => c.periodId === ACTIVE_PERIOD)).toEqual([]);
    });

    it('searches, filters and sorts', async () => {
      const byCode = await wait(cards.query({ ...PAGE, search: 'emp046' }));
      expect(byCode.items.every((c) => c.employeeCode === 'EMP046')).toBe(true);
      const byPeriod = await wait(cards.query({ ...PAGE, filters: { periodId: 1 } }));
      expect(byPeriod.items.every((c) => c.periodId === 1)).toBe(true);
      const sorted = await wait(
        cards.query({ ...PAGE, sort: { active: 'employeeCode', direction: 'desc' } }),
      );
      const names = sorted.items.map((c) => c.employeeCode);
      expect(names).toEqual([...names].sort().reverse());
    });
  });

  describe('evidence', () => {
    it('adds, lists, downloads and removes a file, changing the scorecard version each time', async () => {
      const draft = await draftCard();
      session.as(EMPLOYEE_23);
      const itemId = draft.items[0].id;
      const uploaded = await wait(
        cards.uploadEvidence(itemId, draft.rowVersion, pdfFile('proof.pdf')),
      );
      expect(uploaded.attachment).toMatchObject({
        fileName: 'proof.pdf',
        contentType: 'application/pdf',
        sizeBytes: 16,
      });
      expect(uploaded.scorecardRowVersion).not.toBe(draft.rowVersion);
      const detail = await wait(cards.getById(draft.id));
      expect(detail.items[0].attachments).toHaveLength(1);
      expect(detail.rowVersion).toBe(uploaded.scorecardRowVersion);

      const download = await wait(cards.downloadEvidence(uploaded.attachment.id));
      expect(download.fileName).toBe('proof.pdf');
      expect(download.blob.size).toBe(16);

      // The reviewer and HR can download; a stranger cannot see it.
      session.as(MANAGER);
      expect((await wait(cards.downloadEvidence(uploaded.attachment.id))).fileName).toBe(
        'proof.pdf',
      );
      session.as(EMPLOYEE_56);
      expect((await failure(wait(cards.downloadEvidence(uploaded.attachment.id)))).code).toBe(
        'NotFound',
      );

      session.as(EMPLOYEE_23);
      const removed = await wait(
        cards.deleteEvidence(uploaded.attachment.id, uploaded.scorecardRowVersion),
      );
      expect(removed.scorecardRowVersion).not.toBe(uploaded.scorecardRowVersion);
      expect((await wait(cards.getById(draft.id))).items[0].attachments).toEqual([]);
      expect((await failure(wait(cards.downloadEvidence(uploaded.attachment.id)))).code).toBe(
        'NotFound',
      );
    });

    it('never writes the file name into the activity feed', async () => {
      const draft = await draftCard();
      session.as(EMPLOYEE_23);
      await wait(
        cards.uploadEvidence(draft.items[0].id, draft.rowVersion, pdfFile('private-name.pdf')),
      );
      const lines = TestBed.inject(ActivityMockService)
        .snapshot(5)
        .map((a) => a.message);
      expect(lines[0]).toMatch(/^Evidence added to the scorecard of EMP023 \(/);
      expect(lines.join(' ')).not.toContain('private-name');
    });

    it('checks the type by content, the extension, the size and the five files per KPI', async () => {
      const draft = await draftCard();
      session.as(EMPLOYEE_23);
      const itemId = draft.items[0].id;
      const refuse = async (file: File, rowVersion = draft.rowVersion) => {
        const error = await failure(wait(cards.uploadEvidence(itemId, rowVersion, file)));
        expect(error.code).toBe('Validation');
        return error;
      };
      expect(
        (await refuse(new File([new Uint8Array(0)], 'empty.pdf'))).fieldErrors?.['file'],
      ).toContain('The file is empty.');
      expect(
        (await refuse(new File([new TextEncoder().encode('<html></html>')], 'page.html')))
          .fieldErrors?.['file'],
      ).toContain('Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx)');
      // The extension must agree with the content.
      expect((await refuse(pdfFile('evidence.png'))).fieldErrors?.['file']).toContain(
        'Allowed evidence files',
      );
      // An executable renamed to .pdf has no PDF signature.
      expect(
        (await refuse(new File([new Uint8Array([0x4d, 0x5a, 0x90, 0])], 'tool.pdf'))).fieldErrors?.[
          'file'
        ],
      ).toBeDefined();
      expect(
        (await refuse(pdfFile('big.pdf', 10 * 1024 * 1024 - 16 + 1))).fieldErrors?.['file'],
      ).toContain('The file is larger than 10 MB.');

      // Exactly 10 MB is allowed.
      let version = (
        await wait(
          cards.uploadEvidence(itemId, draft.rowVersion, pdfFile('max.pdf', 10 * 1024 * 1024 - 16)),
        )
      ).scorecardRowVersion;
      for (let i = 0; i < 4; i++) {
        version = (await wait(cards.uploadEvidence(itemId, version, pdfFile(`f${i}.pdf`))))
          .scorecardRowVersion;
      }
      const sixth = await failure(
        wait(cards.uploadEvidence(itemId, version, pdfFile('sixth.pdf'))),
      );
      expect(sixth.code).toBe('Conflict');
      expect(sixth.message).toBe(
        'A KPI can have at most 5 evidence files. Remove one before adding another.',
      );
    });

    it('accepts PNG, JPEG, Word and Excel by content and cleans the file name', async () => {
      const draft = await draftCard();
      session.as(EMPLOYEE_23);
      const itemId = draft.items[0].id;
      const zip = (entry: string) =>
        new Uint8Array([
          0x50,
          0x4b,
          3,
          4,
          ...new TextEncoder().encode('[Content_Types].xml'),
          ...new TextEncoder().encode(entry),
        ]);
      const files: [File, string, string][] = [
        [
          new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0])], 'a.PNG'),
          'a.PNG',
          'image/png',
        ],
        [
          new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], 'C:\\fakepath\\..\\photo.jpeg'),
          'photo.jpeg',
          'image/jpeg',
        ],
        [
          new File([zip('word/document.xml')], 'report.docx'),
          'report.docx',
          'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        ],
        [
          new File([zip('xl/workbook.xml')], 'sheet<1>.xlsx'),
          'sheet1.xlsx',
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        ],
      ];
      let version = draft.rowVersion;
      for (const [file, name, type] of files) {
        const result = await wait(cards.uploadEvidence(itemId, version, file));
        expect(result.attachment.fileName).toBe(name);
        expect(result.attachment.contentType).toBe(type);
        version = result.scorecardRowVersion;
      }
      // A zip that is neither Word nor Excel is refused.
      const other = await failure(
        wait(cards.uploadEvidence(itemId, version, new File([zip('other/file.xml')], 'x.docx'))),
      );
      expect(other.fieldErrors?.['file']).toBeDefined();
    });

    it('is for the owner only, and only while the scorecard is Draft or Returned', async () => {
      const draft = await draftCard();
      session.as(HR_ADMIN);
      const notOwner = await failure(
        wait(cards.uploadEvidence(draft.items[0].id, draft.rowVersion, pdfFile())),
      );
      expect(notOwner.code).toBe('Forbidden');
      expect(notOwner.message).toBe('You can only add or remove evidence on your own scorecard.');

      const saved = await withResults(draft);
      const submitted = await wait(cards.submit(saved.id, saved.rowVersion));
      const locked = await failure(
        wait(cards.uploadEvidence(submitted.items[0].id, submitted.rowVersion, pdfFile())),
      );
      expect(locked.code).toBe('Conflict');
      expect(locked.message).toContain(
        'evidence can only be added or removed while it is Draft or Returned',
      );
    });

    it('rejects a stale scorecard row version on upload and removal', async () => {
      const draft = await draftCard();
      session.as(EMPLOYEE_23);
      const uploaded = await wait(
        cards.uploadEvidence(draft.items[0].id, draft.rowVersion, pdfFile()),
      );
      const stale = await failure(
        wait(cards.uploadEvidence(draft.items[0].id, draft.rowVersion, pdfFile('again.pdf'))),
      );
      expect(stale.code).toBe('Conflict');
      const staleDelete = await failure(
        wait(cards.deleteEvidence(uploaded.attachment.id, draft.rowVersion)),
      );
      expect(staleDelete.code).toBe('Conflict');
    });
  });
});
