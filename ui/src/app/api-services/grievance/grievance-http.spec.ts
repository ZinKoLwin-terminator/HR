import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  GrievanceCaseDataService,
  GrievanceCategoryDataService,
  GrievanceInsightDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { GrievanceCaseHttpService } from './grievance-case-http.service';
import { GrievanceCategoryHttpService } from './grievance-category-http.service';
import { GrievanceInsightHttpService } from './grievance-insight-http.service';
import { MyGrievanceHttpService } from './my-grievance-http.service';

const G = '/api/grievances';

function setup(): HttpTestingController {
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

/** The request once it is made (a lazily bound contract issues it after its chunk loads). */
async function nextRequest(
  http: HttpTestingController,
  match: (url: string) => boolean,
): Promise<TestRequest> {
  for (let i = 0; i < 50; i++) {
    const found = http.match((r) => match(r.url));
    if (found.length > 0) return found[0];
    await new Promise((resolve) => setTimeout(resolve, 10));
  }
  throw new Error('No request was made.');
}

const page = <T>(items: T[]) => ({ items, total: items.length, pageIndex: 0, pageSize: 10 });
const params = (req: TestRequest) =>
  Object.fromEntries(req.request.params.keys().map((k) => [k, req.request.params.get(k)]));
const pdf = () => new File([new Uint8Array([0x25, 0x50, 0x44, 0x46])], 'note.pdf');

describe('grievance HTTP adapters', () => {
  let http: HttpTestingController;
  beforeEach(() => (http = setup()));
  afterEach(() => http.verify());

  describe('bindings', () => {
    it('binds every grievance contract to HTTP, lazily', async () => {
      const calls: [Observable<unknown>, string][] = [
        [TestBed.inject(GrievanceCategoryDataService).getAll(), `${G}/categories`],
        [TestBed.inject(MyGrievanceDataService).options(), `${G}/options`],
        [TestBed.inject(GrievanceCaseDataService).getById(5), `${G}/cases/5`],
        [TestBed.inject(GrievanceInsightDataService).hrDashboard(), `${G}/dashboard/hr`],
      ];
      for (const [call, url] of calls) {
        const promise = firstValueFrom(call);
        (await nextRequest(http, (u) => u === url)).flush(url.endsWith('categories') ? [] : {});
        await promise;
      }
    });
  });

  describe('my grievances', () => {
    let api: MyGrievanceHttpService;
    beforeEach(() => (api = TestBed.inject(MyGrievanceHttpService)));

    it('lists with paging, a whitelisted sort, the search and the status', () => {
      firstValueFrom(
        api.query({
          pageIndex: 1,
          pageSize: 25,
          sort: { active: 'createdAtUtc', direction: 'desc' },
          search: ' GRV ',
          filters: { status: 'Submitted' },
        }),
      );
      const req = http.expectOne((r) => r.url === `${G}/my`);
      expect(req.request.method).toBe('GET');
      expect(params(req)).toEqual({
        pageIndex: '1',
        pageSize: '25',
        sort: 'createdAtUtc:desc',
        search: 'GRV',
        status: 'Submitted',
      });
      req.flush(page([]));
    });

    it('never sends a sort the API does not accept', () => {
      firstValueFrom(
        api.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'categoryName', direction: 'asc' },
        }),
      );
      const req = http.expectOne(`${G}/my?pageIndex=0&pageSize=10`);
      req.flush(page([]));
    });

    it('reads the options, a case and the dashboard', () => {
      firstValueFrom(api.options());
      http
        .expectOne({ method: 'GET', url: `${G}/options` })
        .flush({ categories: [], departments: [] });
      firstValueFrom(api.getById(4));
      http.expectOne({ method: 'GET', url: `${G}/my/4` }).flush({});
      firstValueFrom(api.dashboard());
      http.expectOne({ method: 'GET', url: `${G}/my/dashboard` }).flush({});
    });

    it('creates, updates and deletes a draft (DELETE carries the rowVersion in the body)', () => {
      const draft = {
        categoryId: 1,
        subject: 'Sample',
        description: '',
        incidentDate: null,
        location: null,
        involvedPersonText: null,
        involvedDepartmentId: null,
        isAnonymous: false,
      };
      firstValueFrom(api.createDraft(draft));
      const create = http.expectOne({ method: 'POST', url: `${G}/my` });
      expect(create.request.body).toEqual(draft);
      create.flush({ id: 9 }, { status: 201, statusText: 'Created' });

      firstValueFrom(api.updateDraft(9, { ...draft, rowVersion: 'AAA=' }));
      const update = http.expectOne({ method: 'PUT', url: `${G}/my/9` });
      expect(update.request.body).toEqual({ ...draft, rowVersion: 'AAA=' });
      update.flush({});

      firstValueFrom(api.deleteDraft(9, 'AAB='));
      const remove = http.expectOne({ method: 'DELETE', url: `${G}/my/9` });
      expect(remove.request.body).toEqual({ rowVersion: 'AAB=' });
      remove.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('submits, sends a message and marks seen (204, no body)', async () => {
      firstValueFrom(api.submit(9, { rowVersion: 'r', declarationAccepted: true }));
      const submit = http.expectOne({ method: 'POST', url: `${G}/my/9/submit` });
      expect(submit.request.body).toEqual({ rowVersion: 'r', declarationAccepted: true });
      submit.flush({});

      firstValueFrom(api.sendMessage(9, { rowVersion: 'r', body: 'Hello', inReplyToId: 3 }));
      const message = http.expectOne({ method: 'POST', url: `${G}/my/9/messages` });
      expect(message.request.body).toEqual({ rowVersion: 'r', body: 'Hello', inReplyToId: 3 });
      message.flush({});

      const seen = firstValueFrom(api.markSeen(9));
      const req = http.expectOne({ method: 'POST', url: `${G}/my/9/seen` });
      expect(req.request.body).toBeNull();
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(await seen).toBeUndefined();
    });

    it('uploads multipart rowVersion and file, and reads a 429 as the API words it', async () => {
      const promise = firstValueFrom(api.uploadAttachment(9, 'r', pdf()));
      const req = http.expectOne({ method: 'POST', url: `${G}/my/9/attachments` });
      const body = req.request.body as FormData;
      expect(body).toBeInstanceOf(FormData);
      expect(body.get('rowVersion')).toBe('r');
      expect((body.get('file') as File).name).toBe('note.pdf');
      expect(body.has('visibility')).toBe(false);
      req.flush(
        {
          title: 'Too many requests',
          detail: 'Too many file uploads. Wait a minute and try again.',
        },
        { status: 429, statusText: 'Too Many Requests' },
      );
      const error = await rejection(promise);
      expect(error.message).toBe('Too many file uploads. Wait a minute and try again.');
    });

    it('downloads a Blob named by Content-Disposition, and reads a problem Blob on failure', async () => {
      const ok = firstValueFrom(api.downloadAttachment(3));
      const req = http.expectOne(`${G}/my/attachments/3`);
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['%PDF']), {
        headers: { 'Content-Disposition': 'attachment; filename="note.pdf"' },
      });
      expect((await ok).fileName).toBe('note.pdf');

      const failed = firstValueFrom(api.downloadAttachment(4));
      http.expectOne(`${G}/my/attachments/4`).flush(
        new Blob([JSON.stringify({ code: 'NotFound', detail: 'Attachment #4 was not found.' })], {
          type: 'application/problem+json',
        }),
        { status: 404, statusText: 'Not Found' },
      );
      const error = await rejection(failed);
      expect(error).toMatchObject({ code: 'NotFound', message: 'Attachment #4 was not found.' });
    });
  });

  describe('case team', () => {
    let api: GrievanceCaseHttpService;
    beforeEach(() => (api = TestBed.inject(GrievanceCaseHttpService)));

    it('lists the three tiers with the case filters (booleans and dates as text)', () => {
      const request = {
        pageIndex: 0,
        pageSize: 10,
        sort: { active: 'submittedAtUtc', direction: 'asc' as const },
        filters: {
          status: 'Submitted' as const,
          isAnonymous: false,
          archived: true,
          submittedFrom: '2026-01-01',
          departmentId: 4,
        },
      };
      for (const [call, url] of [
        [() => api.query(request), `${G}/cases/query`],
        [() => api.investigations(request), `${G}/investigations`],
        [() => api.pendingReviews(request), `${G}/reviews/pending`],
      ] as const) {
        firstValueFrom(call());
        const req = http.expectOne((r) => r.url === url);
        expect(req.request.method).toBe('GET');
        expect(params(req)).toEqual({
          pageIndex: '0',
          pageSize: '10',
          sort: 'submittedAtUtc:asc',
          status: 'Submitted',
          departmentId: '4',
          isAnonymous: 'false',
          archived: 'true',
          submittedFrom: '2026-01-01',
        });
        req.flush(page([]));
      }
    });

    it('reads a case, its history, its access log (no sort) and the pickers', () => {
      firstValueFrom(api.getById(7));
      http.expectOne({ method: 'GET', url: `${G}/cases/7` }).flush({});
      firstValueFrom(api.history(7));
      http.expectOne({ method: 'GET', url: `${G}/cases/7/history` }).flush([]);
      firstValueFrom(
        api.audit(7, {
          pageIndex: 0,
          pageSize: 100,
          sort: { active: 'occurredAtUtc', direction: 'asc' },
          filters: { action: 'ViewCase' },
        }),
      );
      const audit = http.expectOne((r) => r.url === `${G}/cases/7/audit`);
      expect(params(audit)).toEqual({ pageIndex: '0', pageSize: '100', action: 'ViewCase' });
      audit.flush(page([]));
      firstValueFrom(api.investigators(' Ye '));
      expect(params(http.expectOne((r) => r.url === `${G}/investigators`))).toEqual({
        search: 'Ye',
      });
      http.match(`${G}/investigators?search=Ye`).forEach((r) => r.flush([]));
      firstValueFrom(api.investigators());
      http.expectOne(`${G}/investigators`).flush([]);
      firstValueFrom(api.employeeOptions('Dan'));
      http.expectOne({ method: 'GET', url: `${G}/employees?search=Dan` }).flush([]);
    });

    it('posts each workflow command to /cases/{id}/{action} with its body as it is', () => {
      const rv = { rowVersion: 'r' };
      const commands: [() => Observable<unknown>, string, object][] = [
        [
          () => api.acknowledge(3, { ...rv, priority: 'High', targetResolutionDate: null }),
          'acknowledge',
          { ...rv, priority: 'High', targetResolutionDate: null },
        ],
        [
          () => api.startReview(3, { ...rv, comment: 'c' }),
          'start-review',
          { ...rv, comment: 'c' },
        ],
        [
          () => api.assign(3, { ...rv, investigatorEmployeeId: 9, role: 'Lead', reason: null }),
          'assign',
          { ...rv, investigatorEmployeeId: 9, role: 'Lead', reason: null },
        ],
        [() => api.unassign(3, { ...rv, assignmentId: 4 }), 'unassign', { ...rv, assignmentId: 4 }],
        [() => api.startInvestigation(3, rv), 'start-investigation', rv],
        [
          () => api.requestInfo(3, { ...rv, message: 'm' }),
          'request-info',
          { ...rv, message: 'm' },
        ],
        [
          () => api.proposeResolution(3, { ...rv, summary: 's' }),
          'propose-resolution',
          { ...rv, summary: 's' },
        ],
        [
          () => api.approveResolution(3, { ...rv, visibleToSubmitter: true }),
          'approve-resolution',
          { ...rv, visibleToSubmitter: true },
        ],
        [
          () => api.rejectResolution(3, { ...rv, reason: 'x' }),
          'reject-resolution',
          { ...rv, reason: 'x' },
        ],
        [() => api.reject(3, { ...rv, reason: 'x' }), 'reject', { ...rv, reason: 'x' }],
        [() => api.close(3, rv), 'close', rv],
        [() => api.reopen(3, { ...rv, reason: 'x' }), 'reopen', { ...rv, reason: 'x' }],
        [
          () => api.escalate(3, { ...rv, reason: 'x', isImmediate: true }),
          'escalate',
          { ...rv, reason: 'x', isImmediate: true },
        ],
        [() => api.archive(3, rv), 'archive', rv],
      ];
      expect(commands).toHaveLength(14);
      for (const [call, action, body] of commands) {
        firstValueFrom(call());
        const req = http.expectOne({ method: 'POST', url: `${G}/cases/3/${action}` });
        expect(req.request.body, action).toEqual(body);
        req.flush({});
      }
    });

    it('maps a conflict-of-interest 400 to a field error', async () => {
      const promise = firstValueFrom(
        api.assign(3, { rowVersion: 'r', investigatorEmployeeId: 9, role: 'Lead' }),
      );
      http.expectOne(`${G}/cases/3/assign`).flush(
        {
          title: 'One or more validation errors occurred.',
          errors: {
            InvestigatorEmployeeId: [
              'This person cannot be assigned to this case (conflict of interest).',
            ],
          },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      const error = await rejection(promise);
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors).toEqual({
        investigatorEmployeeId:
          'This person cannot be assigned to this case (conflict of interest).',
      });
    });

    it('triages with PUT and posts messages and notes', () => {
      const triage = {
        rowVersion: 'r',
        categoryId: 1,
        priority: null,
        targetResolutionDate: null,
        subjectEmployeeId: 9,
        involvedDepartmentId: null,
        legalHold: true,
      };
      firstValueFrom(api.triage(3, triage));
      const put = http.expectOne({ method: 'PUT', url: `${G}/cases/3/triage` });
      expect(put.request.body).toEqual(triage);
      put.flush({});
      firstValueFrom(api.sendMessage(3, { rowVersion: 'r', body: 'Hi' }));
      expect(http.expectOne({ method: 'POST', url: `${G}/cases/3/messages` }).request.body).toEqual(
        {
          rowVersion: 'r',
          body: 'Hi',
        },
      );
      http.match(`${G}/cases/3/messages`).forEach((r) => r.flush({}));
      firstValueFrom(api.addNote(3, { rowVersion: 'r', kind: 'Finding', body: 'f' }));
      http.expectOne({ method: 'POST', url: `${G}/cases/3/notes` }).flush({});
    });

    it('uploads multipart with visibility and downloads from /attachments/{id}', async () => {
      firstValueFrom(api.uploadAttachment(3, 'r', 'CaseTeam', pdf()));
      const upload = http.expectOne({ method: 'POST', url: `${G}/cases/3/attachments` });
      const body = upload.request.body as FormData;
      expect(body.get('rowVersion')).toBe('r');
      expect(body.get('visibility')).toBe('CaseTeam');
      expect((body.get('file') as File).name).toBe('note.pdf');
      upload.flush(
        { attachment: { id: 1 }, caseRowVersion: 's' },
        { status: 201, statusText: 'Created' },
      );

      const download = firstValueFrom(api.downloadAttachment(11));
      const req = http.expectOne(`${G}/attachments/11`);
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['x']), {
        headers: { 'Content-Disposition': "attachment; filename*=UTF-8''witness%20list.pdf" },
      });
      expect((await download).fileName).toBe('witness list.pdf');
    });

    it('adds a corrective action to the case and updates it by its own id', () => {
      const input = { rowVersion: 'r', actionType: 'Training', description: 'd' };
      firstValueFrom(api.addCorrectiveAction(3, input));
      const post = http.expectOne({ method: 'POST', url: `${G}/cases/3/actions` });
      expect(post.request.body).toEqual(input);
      post.flush({ id: 5 }, { status: 201, statusText: 'Created' });
      const changes = {
        rowVersion: 'a',
        actionType: 'Training',
        description: 'd',
        responsibleText: null,
        dueDate: null,
        completedOn: '2026-11-01',
        status: 'Implemented' as const,
      };
      firstValueFrom(api.updateCorrectiveAction(5, changes));
      const put = http.expectOne({ method: 'PUT', url: `${G}/case-actions/5` });
      expect(put.request.body).toEqual(changes);
      put.flush({});
    });
  });

  describe('insights', () => {
    let api: GrievanceInsightHttpService;
    beforeEach(() => (api = TestBed.inject(GrievanceInsightHttpService)));

    it('reads both dashboards', () => {
      firstValueFrom(api.hrDashboard());
      http.expectOne({ method: 'GET', url: `${G}/dashboard/hr` }).flush({});
      firstValueFrom(api.reviewDashboard());
      http.expectOne({ method: 'GET', url: `${G}/dashboard/review` }).flush({});
    });

    it('sends the report filters and a whitelisted sort', () => {
      firstValueFrom(
        api.casesReport({
          pageIndex: 0,
          pageSize: 50,
          sort: { active: 'resolutionDays', direction: 'desc' },
          filters: { departmentId: 4, overdue: true, minResolutionDays: 5 },
        }),
      );
      const req = http.expectOne((r) => r.url === `${G}/reports/cases`);
      expect(params(req)).toEqual({
        pageIndex: '0',
        pageSize: '50',
        sort: 'resolutionDays:desc',
        departmentId: '4',
        minResolutionDays: '5',
        overdue: 'true',
      });
      req.flush(page([]));
    });

    it('sends only the summary’s four filters', () => {
      firstValueFrom(
        api.summary({
          submittedFrom: '2026-01-01',
          categoryId: 2,
          priority: 'High',
          departmentId: 4,
        } as never),
      );
      const req = http.expectOne((r) => r.url === `${G}/reports/summary`);
      expect(params(req)).toEqual({
        submittedFrom: '2026-01-01',
        categoryId: '2',
        priority: 'High',
      });
      req.flush({});
    });
  });

  describe('categories', () => {
    let api: GrievanceCategoryHttpService;
    beforeEach(() => (api = TestBed.inject(GrievanceCategoryHttpService)));

    it('queries /grievances/categories and turns a status change into deactivate', async () => {
      firstValueFrom(
        api.query({
          pageIndex: 0,
          pageSize: 10,
          filters: { status: 'Active', allowsAnonymous: false },
        }),
      );
      const list = http.expectOne((r) => r.url === `${G}/categories/query`);
      expect(list.request.params.get('status')).toBe('Active');
      expect(list.request.params.get('allowsAnonymous')).toBe('false');
      list.flush(page([{ id: 3, status: 'Active', rowVersion: 'c1' }]));

      const promise = firstValueFrom(api.update(3, { status: 'Inactive' }));
      const command = http.expectOne({ method: 'POST', url: `${G}/categories/3/deactivate` });
      expect(command.request.body).toEqual({ rowVersion: 'c1' });
      command.flush({ id: 3, status: 'Inactive', rowVersion: 'c2' });
      expect((await promise).status).toBe('Inactive');
    });
  });
});

describe('grievance HTTP adapters: the module-wide access log', () => {
  let http: HttpTestingController;
  beforeEach(() => (http = setup()));
  afterEach(() => http.verify());

  it('reads GET /audit with the action and date range, never a sort', () => {
    const api = TestBed.inject(GrievanceCaseHttpService);
    firstValueFrom(
      api.auditLog({
        pageIndex: 0,
        pageSize: 25,
        sort: { active: 'occurredAtUtc', direction: 'asc' },
        filters: { action: 'Export', from: '2026-11-01', to: '2026-11-18' },
      }),
    );
    const req = http.expectOne((r) => r.url === `${G}/audit`);
    expect(req.request.method).toBe('GET');
    expect(params(req)).toEqual({
      pageIndex: '0',
      pageSize: '25',
      action: 'Export',
      from: '2026-11-01',
      to: '2026-11-18',
    });
    req.flush(page([]));
  });
});
