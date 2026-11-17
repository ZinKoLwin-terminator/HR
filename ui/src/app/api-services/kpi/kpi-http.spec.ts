import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  EvaluationPeriodDataService,
  KpiCategoryDataService,
  KpiTemplateDataService,
  RatingBandDataService,
  ScorecardDataService,
} from '../../core/services/data/kpi-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { fileNameFromDisposition } from './kpi-http.util';
import {
  EvaluationPeriodHttpService,
  KpiCategoryHttpService,
  KpiTemplateHttpService,
  RatingBandHttpService,
} from './kpi-setup-http.services';
import { ScorecardHttpService } from './scorecard-http.service';

const KPI = '/api/kpi';

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  http
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return http;
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

const page = <T>(items: T[]) => ({ items, total: items.length, pageIndex: 0, pageSize: 10 });

const category = {
  id: 3,
  code: 'QUALITY',
  name: 'Quality',
  description: '',
  status: 'Active',
  rowVersion: 'c1',
};
const period = {
  id: 7,
  companyId: 1,
  name: '2027 Q1',
  periodType: 'Quarterly',
  startDate: '2027-01-01',
  endDate: '2027-03-31',
  submissionDueDate: null,
  requiresHrReview: false,
  status: 'Draft',
  rowVersion: 'p1',
};

describe('KPI HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the KPI mock services only when API mode is on', () => {
    setup();
    expect(TestBed.inject(KpiCategoryDataService)).toBeInstanceOf(KpiCategoryHttpService);
    expect(TestBed.inject(KpiTemplateDataService)).toBeInstanceOf(KpiTemplateHttpService);
    expect(TestBed.inject(RatingBandDataService)).toBeInstanceOf(RatingBandHttpService);
    expect(TestBed.inject(EvaluationPeriodDataService)).toBeInstanceOf(EvaluationPeriodHttpService);
    expect(TestBed.inject(ScorecardDataService)).toBeInstanceOf(ScorecardHttpService);
  });

  describe('categories, templates and rating bands', () => {
    it('query categories with paging, sort, search and the status filter', async () => {
      const http = setup();
      const promise = firstValueFrom(
        TestBed.inject(KpiCategoryDataService).query({
          pageIndex: 1,
          pageSize: 20,
          sort: { active: 'name', direction: 'desc' },
          search: ' quality ',
          filters: { status: 'Active' },
        }),
      );
      const req = http.expectOne((r) => r.url === `${KPI}/categories/query`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('pageIndex')).toBe('1');
      expect(req.request.params.get('sort')).toBe('name:desc');
      expect(req.request.params.get('search')).toBe('quality');
      expect(req.request.params.get('status')).toBe('Active');
      req.flush(page([category]));
      expect((await promise).items[0].code).toBe('QUALITY');
    });

    it('update sends the fields with the row version, and a status change goes through a command', async () => {
      const http = setup();
      const categories = TestBed.inject(KpiCategoryDataService);
      const renamed = firstValueFrom(
        categories.update(3, {
          code: 'QUALITY',
          name: 'Quality 2',
          description: '',
          rowVersion: 'c1',
        }),
      );
      const put = http.expectOne(`${KPI}/categories/3`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        code: 'QUALITY',
        name: 'Quality 2',
        description: '',
        rowVersion: 'c1',
      });
      put.flush({ ...category, name: 'Quality 2', rowVersion: 'c2' });
      await renamed;

      const deactivated = firstValueFrom(categories.update(3, { status: 'Inactive' }));
      const command = http.expectOne(`${KPI}/categories/3/deactivate`);
      expect(command.request.method).toBe('POST');
      expect(command.request.body).toEqual({ rowVersion: 'c2' }); // the version seen in the last answer
      command.flush({ ...category, status: 'Inactive', rowVersion: 'c3' });
      expect((await deactivated).status).toBe('Inactive');
    });

    it('deletes with a JSON body carrying the row version', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(KpiCategoryDataService).delete(3));
      http.expectOne(`${KPI}/categories/3`).flush(category); // learns the version first
      const del = http.expectOne((r) => r.method === 'DELETE');
      expect(del.request.url).toBe(`${KPI}/categories/3`);
      expect(del.request.body).toEqual({ rowVersion: 'c1' });
      del.flush(null, { status: 204, statusText: 'No Content' });
      await promise;
    });

    it('creates templates with their filters and keeps unsupported sort keys out', async () => {
      const http = setup();
      const templates = TestBed.inject(KpiTemplateDataService);
      const query = firstValueFrom(
        templates.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'direction', direction: 'asc' }, // the API cannot sort by direction
          filters: {
            categoryId: 2,
            departmentId: 4,
            scoringMethod: 'Rating Based',
            status: 'Active',
          },
        }),
      );
      const req = http.expectOne((r) => r.url === `${KPI}/templates/query`);
      expect(req.request.params.has('sort')).toBe(false);
      expect(req.request.params.get('categoryId')).toBe('2');
      expect(req.request.params.get('departmentId')).toBe('4');
      expect(req.request.params.get('scoringMethod')).toBe('Rating Based');
      req.flush(page([]));
      await query;

      const create = firstValueFrom(
        templates.create({
          code: 'TST-SALES',
          name: 'Sales',
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
        }),
      );
      const post = http.expectOne(`${KPI}/templates`);
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toMatchObject({
        direction: 'Higher Is Better',
        scoringMethod: 'Capped Percentage',
        maxAchievementPct: 120,
        status: 'Active',
      });
      expect('rowVersion' in (post.request.body as object)).toBe(false);
      post.flush({ id: 9, ...(post.request.body as object), rowVersion: 't1' });
      expect((await create).id).toBe(9);
    });

    it('reads rating bands from /rating-bands', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(RatingBandDataService).getAll());
      const req = http.expectOne(`${KPI}/rating-bands`);
      expect(req.request.method).toBe('GET');
      req.flush([{ id: 1, label: 'Outstanding', minScore: 90, description: '', status: 'Active' }]);
      expect((await promise)[0].label).toBe('Outstanding');
    });

    it('maps a field error from the API', async () => {
      const http = setup();
      const promise = rejection(
        firstValueFrom(
          TestBed.inject(KpiCategoryDataService).create({
            code: 'DUP',
            name: 'Dup',
            description: '',
            status: 'Active',
          }),
        ),
      );
      http.expectOne(`${KPI}/categories`).flush(
        {
          code: 'Validation',
          detail: 'KPI category code "DUP" already exists.',
          errors: { code: ['Code must be unique.'] },
          traceId: 't-1',
        },
        { status: 400, statusText: 'Bad Request' },
      );
      const error = await promise;
      expect(error.code).toBe('Validation');
      expect(error.message).toBe('KPI category code "DUP" already exists.');
      expect(error.fieldErrors).toEqual({ code: 'Code must be unique.' });
      expect(error.traceId).toBe('t-1');
    });
  });

  describe('evaluation periods', () => {
    it('queries with the period filters and sorts only by supported keys', async () => {
      const http = setup();
      const promise = firstValueFrom(
        TestBed.inject(EvaluationPeriodDataService).query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'startDate', direction: 'desc' },
          filters: { companyId: 1, periodType: 'Quarterly', status: 'Active', year: 2027 },
        }),
      );
      const req = http.expectOne((r) => r.url === `${KPI}/periods/query`);
      expect(req.request.params.get('sort')).toBe('startDate:desc');
      expect(req.request.params.get('companyId')).toBe('1');
      expect(req.request.params.get('periodType')).toBe('Quarterly');
      expect(req.request.params.get('status')).toBe('Active');
      expect(req.request.params.get('year')).toBe('2027');
      req.flush(page([period]));
      await promise;
    });

    it('creates without a status and updates with the whole editable set', async () => {
      const http = setup();
      const periods = TestBed.inject(EvaluationPeriodDataService);
      const create = firstValueFrom(
        periods.create({
          companyId: 1,
          name: '2027 Q1',
          periodType: 'Quarterly',
          startDate: '2027-01-01',
          endDate: '2027-03-31',
          submissionDueDate: null,
          requiresHrReview: false,
          status: 'Active',
        }),
      );
      const post = http.expectOne(`${KPI}/periods`);
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({
        companyId: 1,
        name: '2027 Q1',
        periodType: 'Quarterly',
        startDate: '2027-01-01',
        endDate: '2027-03-31',
        submissionDueDate: null,
        requiresHrReview: false,
      });
      post.flush(period);
      await create;

      // A partial change is completed from the current record; the status is never sent.
      const update = firstValueFrom(periods.update(7, { name: 'Renamed', status: 'Closed' }));
      http.expectOne(`${KPI}/periods/7`).flush(period); // current record
      const put = http.expectOne((r) => r.method === 'PUT');
      expect(put.request.body).toEqual({
        companyId: 1,
        name: 'Renamed',
        periodType: 'Quarterly',
        startDate: '2027-01-01',
        endDate: '2027-03-31',
        submissionDueDate: null,
        requiresHrReview: false,
        rowVersion: 'p1',
      });
      put.flush({ ...period, name: 'Renamed', rowVersion: 'p2' });
      expect((await update).rowVersion).toBe('p2');
    });

    it.each([
      ['activate', 'activate'],
      ['complete', 'complete'],
      ['close', 'close'],
    ] as const)('%s posts the row version to the command', async (method, route) => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(EvaluationPeriodDataService)[method](7, 'p9'));
      const req = http.expectOne(`${KPI}/periods/7/${route}`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'p9' });
      req.flush(period);
      await promise;
    });

    it('reads the version first when it has never seen the period', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(EvaluationPeriodDataService).activate(7));
      http.expectOne(`${KPI}/periods/7`).flush(period);
      const req = http.expectOne(`${KPI}/periods/7/activate`);
      expect(req.request.body).toEqual({ rowVersion: 'p1' });
      req.flush({ ...period, status: 'Active' });
      expect((await promise).status).toBe('Active');
    });

    it('loads the picker options, narrowed by company', async () => {
      const http = setup();
      const periods = TestBed.inject(EvaluationPeriodDataService);
      const all = firstValueFrom(periods.options());
      const first = http.expectOne((r) => r.url === `${KPI}/periods/options`);
      expect(first.request.params.has('companyId')).toBe(false);
      first.flush([]);
      await all;
      const some = firstValueFrom(periods.options(2));
      const second = http.expectOne((r) => r.url === `${KPI}/periods/options`);
      expect(second.request.params.get('companyId')).toBe('2');
      second.flush([]);
      await some;
    });

    it('turns a stale-version conflict into a Conflict error', async () => {
      const http = setup();
      const promise = rejection(
        firstValueFrom(TestBed.inject(EvaluationPeriodDataService).close(7, 'old')),
      );
      http
        .expectOne(`${KPI}/periods/7/close`)
        .flush(
          { detail: 'Period "2027 Q1" is Active; only a Completed period can be closed.' },
          { status: 409, statusText: 'Conflict' },
        );
      const error = await promise;
      expect(error.code).toBe('Conflict');
      expect(error.message).toContain('only a Completed period can be closed');
    });

    it('deletes with the row version in the body', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(EvaluationPeriodDataService).delete(7));
      http.expectOne(`${KPI}/periods/7`).flush(period);
      const del = http.expectOne((r) => r.method === 'DELETE');
      expect(del.request.body).toEqual({ rowVersion: 'p1' });
      del.flush(null, { status: 204, statusText: 'No Content' });
      await promise;
    });
  });

  describe('scorecards', () => {
    const detail = { id: 5, status: 'Draft', rowVersion: 's1', items: [], history: [] };

    it('queries with the scorecard filters, and the queues with their own keys', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      const request = {
        pageIndex: 2,
        pageSize: 25,
        search: 'emp046',
        sort: { active: 'overallScore', direction: 'desc' as const },
        filters: {
          periodId: 2,
          employeeId: 46,
          departmentId: 4,
          companyId: 1,
          status: 'Under Review' as const,
        },
      };
      const list = firstValueFrom(cards.query(request));
      const req = http.expectOne((r) => r.url === `${KPI}/scorecards/query`);
      expect(req.request.params.get('sort')).toBe('overallScore:desc');
      expect(req.request.params.get('search')).toBe('emp046');
      expect(req.request.params.get('periodId')).toBe('2');
      expect(req.request.params.get('employeeId')).toBe('46');
      expect(req.request.params.get('departmentId')).toBe('4');
      expect(req.request.params.get('companyId')).toBe('1');
      expect(req.request.params.get('status')).toBe('Under Review');
      req.flush(page([]));
      await list;

      const mine = firstValueFrom(cards.myScorecards(request));
      http.expectOne((r) => r.url === `${KPI}/my-scorecards`).flush(page([]));
      await mine;

      const pending = firstValueFrom(cards.pendingReviews(request));
      const queue = http.expectOne((r) => r.url === `${KPI}/reviews/pending`);
      expect(queue.request.params.get('periodId')).toBe('2');
      expect(queue.request.params.get('status')).toBe('Under Review');
      expect(queue.request.params.has('employeeId')).toBe(false);
      queue.flush(page([]));
      await pending;
    });

    it('reads one scorecard', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(ScorecardDataService).getById(5));
      const req = http.expectOne(`${KPI}/scorecards/5`);
      expect(req.request.method).toBe('GET');
      req.flush(detail);
      expect((await promise).rowVersion).toBe('s1');
    });

    it('assign tells a created scorecard (201) from items added to an existing one (200)', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      const request = { employeeId: 46, periodId: 2, items: [{ templateId: 1, weight: 50 }] };
      const created = firstValueFrom(cards.assign(request));
      const post = http.expectOne(`${KPI}/assignments`);
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual(request);
      post.flush(detail, { status: 201, statusText: 'Created' });
      expect((await created).created).toBe(true);

      const added = firstValueFrom(cards.assign(request));
      http.expectOne(`${KPI}/assignments`).flush(detail);
      const result = await added;
      expect(result.created).toBe(false);
      expect(result.scorecard.id).toBe(5);
    });

    it('bulk assigns and returns the assigned and skipped lists', async () => {
      const http = setup();
      const request = { periodId: 2, departmentId: 4, items: [{ templateId: 8 }] };
      const promise = firstValueFrom(TestBed.inject(ScorecardDataService).bulkAssign(request));
      const req = http.expectOne(`${KPI}/assignments/bulk`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush({
        assigned: [{ employeeId: 23, employeeCode: 'EMP023', scorecardId: 9, itemsAdded: 1 }],
        skipped: [{ employeeId: 44, employeeCode: 'EMP044', reason: 'Has left.' }],
      });
      const result = await promise;
      expect(result.assigned[0].itemsAdded).toBe(1);
      expect(result.skipped[0].reason).toBe('Has left.');
    });

    it('updates an item with its row version and fills in the empty evidence list', async () => {
      const http = setup();
      const promise = firstValueFrom(
        TestBed.inject(ScorecardDataService).updateItem(11, {
          target: 120,
          weight: 25,
          startDate: '2027-01-01',
          endDate: '2027-03-31',
          rowVersion: 'i1',
        }),
      );
      const req = http.expectOne(`${KPI}/scorecard-items/11`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        target: 120,
        weight: 25,
        startDate: '2027-01-01',
        endDate: '2027-03-31',
        rowVersion: 'i1', // no sortOrder: the API keeps its value
      });
      req.flush({ id: 11, target: 120, weight: 25, rowVersion: 'i2' });
      const item = await promise;
      expect(item.rowVersion).toBe('i2');
      expect(item.attachments).toEqual([]);
    });

    it('deletes an item and a scorecard with JSON bodies', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      const item = firstValueFrom(cards.deleteItem(11, 'i1'));
      const itemReq = http.expectOne(`${KPI}/scorecard-items/11`);
      expect(itemReq.request.method).toBe('DELETE');
      expect(itemReq.request.body).toEqual({ rowVersion: 'i1' });
      itemReq.flush(null, { status: 204, statusText: 'No Content' });
      await item;

      const card = firstValueFrom(cards.deleteScorecard(5, 's1'));
      const cardReq = http.expectOne(`${KPI}/scorecards/5`);
      expect(cardReq.request.method).toBe('DELETE');
      expect(cardReq.request.body).toEqual({ rowVersion: 's1' });
      cardReq.flush(null, { status: 204, statusText: 'No Content' });
      await card;
    });

    it.each([
      ['finalizePlan', 'finalize-plan'],
      ['submit', 'submit'],
      ['startReview', 'start-review'],
    ] as const)('%s posts the row version to %s', async (method, route) => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(ScorecardDataService)[method](5, 's1'));
      const req = http.expectOne(`${KPI}/scorecards/5/${route}`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 's1' });
      req.flush(detail);
      await promise;
    });

    it('saves results and review drafts with PUT', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      const results = {
        rowVersion: 's1',
        employeeComment: 'Fictional comment',
        items: [{ id: 11, actualValue: 85, employeeComment: null }],
      };
      const saved = firstValueFrom(cards.saveResults(5, results));
      const put = http.expectOne(`${KPI}/scorecards/5/results`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual(results);
      put.flush(detail);
      await saved;

      const review = {
        rowVersion: 's2',
        managerRating: 4,
        managerComment: 'Fictional feedback',
        items: [{ id: 11, managerRating: 4.5 }],
      };
      const reviewed = firstValueFrom(cards.saveReview(5, review));
      const reviewPut = http.expectOne(`${KPI}/scorecards/5/review`);
      expect(reviewPut.request.method).toBe('PUT');
      expect(reviewPut.request.body).toEqual(review);
      reviewPut.flush(detail);
      await reviewed;
    });

    it('approves and finalizes with an optional trimmed comment', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      const approved = firstValueFrom(
        cards.approve(5, { rowVersion: 's1', comment: '  Good work  ' }),
      );
      const approve = http.expectOne(`${KPI}/scorecards/5/approve`);
      expect(approve.request.body).toEqual({ rowVersion: 's1', comment: 'Good work' });
      approve.flush(detail);
      await approved;

      const finalized = firstValueFrom(cards.finalize(5, { rowVersion: 's2', comment: '   ' }));
      const finalize = http.expectOne(`${KPI}/scorecards/5/finalize`);
      expect(finalize.request.body).toEqual({ rowVersion: 's2' }); // a blank comment is not sent
      finalize.flush(detail);
      await finalized;
    });

    it('returns and rejects with a required reason, refusing a blank one before any call', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      for (const [method, route] of [
        ['returnForRevision', 'return'],
        ['reject', 'reject'],
      ] as const) {
        const blank = await rejection(
          firstValueFrom(cards[method](5, { rowVersion: 's1', comment: '  ' })),
        );
        expect(blank.code).toBe('Validation');
        expect(blank.fieldErrors).toEqual({ comment: 'A reason is required.' });
        http.expectNone(`${KPI}/scorecards/5/${route}`);

        const ok = firstValueFrom(
          cards[method](5, { rowVersion: 's1', comment: ' Fictional reason ' }),
        );
        const req = http.expectOne(`${KPI}/scorecards/5/${route}`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual({ rowVersion: 's1', comment: 'Fictional reason' });
        req.flush(detail);
        await ok;
      }
    });

    it('maps 403, 404 and 409 answers of a scorecard write', async () => {
      const http = setup();
      const cards = TestBed.inject(ScorecardDataService);
      const cases: [number, AppError['code'], string][] = [
        [403, 'Forbidden', 'You can only submit your own scorecard.'],
        [404, 'NotFound', 'Scorecard #5 was not found.'],
        [
          409,
          'Conflict',
          'The scorecard is Submitted; only a Draft or Returned scorecard can be submitted.',
        ],
      ];
      for (const [status, code, detailText] of cases) {
        const promise = rejection(firstValueFrom(cards.submit(5, 's1')));
        http
          .expectOne(`${KPI}/scorecards/5/submit`)
          .flush({ detail: detailText }, { status, statusText: 'Error' });
        const error = await promise;
        expect(error.code).toBe(code);
        expect(error.message).toBe(detailText);
      }
    });

    describe('evidence', () => {
      it('uploads multipart form data with rowVersion and file, and no Content-Type header', async () => {
        const http = setup();
        const file = new File([new Uint8Array([0x25, 0x50, 0x44, 0x46, 0x2d])], 'proof.pdf', {
          type: 'application/pdf',
        });
        const promise = firstValueFrom(
          TestBed.inject(ScorecardDataService).uploadEvidence(11, 's1', file),
        );
        const req = http.expectOne(`${KPI}/scorecard-items/11/attachments`);
        expect(req.request.method).toBe('POST');
        const body = req.request.body as FormData;
        expect(body).toBeInstanceOf(FormData);
        expect(body.get('rowVersion')).toBe('s1');
        const sent = body.get('file') as File;
        expect(sent.name).toBe('proof.pdf');
        expect(Array.from(body.keys())).toEqual(['rowVersion', 'file']);
        // The browser adds the multipart boundary itself.
        expect(req.request.headers.has('Content-Type')).toBe(false);
        req.flush(
          {
            attachment: {
              id: 4,
              fileName: 'proof.pdf',
              contentType: 'application/pdf',
              sizeBytes: 5,
              uploadedOn: '2027-01-05',
            },
            scorecardRowVersion: 's2',
          },
          { status: 201, statusText: 'Created' },
        );
        const result = await promise;
        expect(result.attachment.id).toBe(4);
        expect(result.scorecardRowVersion).toBe('s2');
      });

      it('maps an upload refusal on the file field', async () => {
        const http = setup();
        const promise = rejection(
          firstValueFrom(
            TestBed.inject(ScorecardDataService).uploadEvidence(
              11,
              's1',
              new File([new Uint8Array(1)], 'x.exe'),
            ),
          ),
        );
        http.expectOne(`${KPI}/scorecard-items/11/attachments`).flush(
          {
            code: 'Validation',
            detail:
              'Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), up to 10 MB.',
            errors: {
              file: [
                'Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), up to 10 MB.',
              ],
            },
          },
          { status: 400, statusText: 'Bad Request' },
        );
        const error = await promise;
        expect(error.code).toBe('Validation');
        expect(error.fieldErrors?.['file']).toContain('Allowed evidence files');
      });

      it('downloads a Blob and takes the name from Content-Disposition', async () => {
        const http = setup();
        const promise = firstValueFrom(TestBed.inject(ScorecardDataService).downloadEvidence(4));
        const req = http.expectOne(`${KPI}/attachments/4`);
        expect(req.request.method).toBe('GET');
        expect(req.request.responseType).toBe('blob');
        req.flush(new Blob(['%PDF-'], { type: 'application/pdf' }), {
          headers: {
            'Content-Disposition':
              'attachment; filename="fallback.pdf"; filename*=UTF-8\'\'my%20r%C3%A9sum%C3%A9.pdf',
          },
        });
        const file = await promise;
        expect(file.fileName).toBe('my résumé.pdf');
        expect(file.blob).toBeInstanceOf(Blob);
        expect(file.blob.size).toBe(5);
      });

      it('falls back to a generic name when the header is missing', async () => {
        const http = setup();
        const promise = firstValueFrom(TestBed.inject(ScorecardDataService).downloadEvidence(4));
        http.expectOne(`${KPI}/attachments/4`).flush(new Blob(['x']));
        expect((await promise).fileName).toBe('evidence');
      });

      it('reads the problem body of a failed download, which arrives as a Blob', async () => {
        const http = setup();
        const promise = rejection(
          firstValueFrom(TestBed.inject(ScorecardDataService).downloadEvidence(4)),
        );
        http.expectOne(`${KPI}/attachments/4`).flush(
          new Blob([
            JSON.stringify({
              code: 'NotFound',
              detail: 'The file of this attachment is not available.',
            }),
          ]),
          { status: 404, statusText: 'Not Found' },
        );
        const error = await promise;
        expect(error.code).toBe('NotFound');
        expect(error.message).toBe('The file of this attachment is not available.');
      });

      it('removes evidence with a JSON body and returns the new scorecard version', async () => {
        const http = setup();
        const promise = firstValueFrom(
          TestBed.inject(ScorecardDataService).deleteEvidence(4, 's2'),
        );
        const req = http.expectOne(`${KPI}/attachments/4`);
        expect(req.request.method).toBe('DELETE');
        expect(req.request.body).toEqual({ rowVersion: 's2' });
        req.flush({ scorecardRowVersion: 's3' });
        expect(await promise).toEqual({ scorecardRowVersion: 's3' });
      });
    });
  });
});

describe('fileNameFromDisposition', () => {
  it.each([
    ["attachment; filename*=UTF-8''a%20b.pdf", 'a b.pdf'],
    ['attachment; filename="plain.pdf"', 'plain.pdf'],
    ['attachment; filename="with \\"quote\\".pdf"', 'with "quote".pdf'],
    ['attachment; filename=bare.pdf', 'bare.pdf'],
    ['attachment', null],
    [null, null],
  ])('reads %j as %j', (header, expected) => {
    expect(fileNameFromDisposition(header)).toBe(expected);
  });
});
