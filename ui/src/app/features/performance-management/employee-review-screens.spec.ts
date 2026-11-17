import { Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError, PagedResult } from '../../core/models/common.models';
import { Scorecard, ScorecardDetail, ScorecardItem } from '../../core/models/kpi.models';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { ScorecardDataService } from '../../core/services/data/kpi-data.services';
import {
  EMPLOYEE_46,
  EMPLOYEE_56,
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
  pdfFile,
} from '../../mock-services/kpi/kpi-test-support';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { MyKpisComponent } from './my-kpis/my-kpis.component';
import { MyScorecardComponent, ResultsForm } from './my-kpis/my-scorecard.component';
import { PERFORMANCE_ROUTES, performanceLanding } from './performance.routes';
import {
  REASON_REQUIRED,
  ReviewDecisionData,
  ReviewDecisionDialogComponent,
} from './reviews/review-decision-dialog.component';
import { ReviewDetailComponent, ReviewForm } from './reviews/review-detail.component';
import { REVIEW_QUEUE_COLUMNS, ReviewQueueComponent } from './reviews/review-queue.component';

// Seed facts (mock-data/kpi): period 2 is Active without HR review, period 1 Closed with it.
// Scorecard 1: EMP046 Submitted, reviewer EMP005 Daniel Carter. 5: EMP014 Returned with a reason.
// 6: EMP056 Draft, no results; KPIs OUTPUT-TASKS (Number), CUSTOMER-RATING (Rating, 0 to 5),
// RESPONSE-HOURS (Hours, Range Based) and TRAINING-DONE (Boolean). 7: EMP002 Su Myat Noe's own,
// Submitted. 8: EMP046 Finalized in period 1, with the manager's feedback.

const EMPLOYEE_14: CurrentUser = { ...EMPLOYEE_46, id: 44, employeeId: 14, displayName: 'EMP014' };
const NO_EMPLOYEE: CurrentUser = { ...EMPLOYEE_46, id: 45, employeeId: null, displayName: 'Admin' };

async function settle(fixture: ComponentFixture<unknown>, rounds = 6) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

const internals = <T>(fixture: ComponentFixture<unknown>) =>
  fixture.componentInstance as unknown as T;

let current = signal<CurrentUser>(HR_ADMIN);
const confirmed = { answer: true };

function configure(user: CurrentUser, extra: unknown[] = []) {
  current = signal(user);
  confirmed.answer = true;
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current } },
      // Confirmations answer at once (each test decides yes or no).
      { provide: ConfirmDialogService, useValue: { confirm: () => of(confirmed.answer) } },
      ...(extra as never[]),
    ],
  });
}

async function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}) {
  const fixture = TestBed.createComponent(component);
  for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
  await settle(fixture);
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

const buttons = (el: HTMLElement) =>
  Array.from(el.querySelectorAll('app-page-header button, app-page-header a')).map(
    (b) => b.textContent?.replace(/\s+/g, ' ').trim() ?? '',
  );
const kpi = (el: HTMLElement, code: string) =>
  el.querySelector(`[data-kpi="${code}"]`) as HTMLElement;

interface ScorecardPage {
  card(): ScorecardDetail | null;
  form(): ResultsForm;
  save(card: ScorecardDetail): void;
  submit(card: ScorecardDetail): void;
  addEvidence(item: ScorecardItem, file: File): void;
  removeEvidence(item: ScorecardItem, file: { id: number; fileName: string }): void;
  formError(): string;
  evidenceErrors(): Record<number, string>;
}

describe('My KPIs', () => {
  it('lists the employee’s own scorecards, newest period first', async () => {
    configure(EMPLOYEE_56);
    const { el } = await render(MyKpisComponent);
    const rows = Array.from(el.querySelectorAll('tr.mat-mdc-row'));
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Draft');
    expect(rows[1].textContent).toContain('Rejected');
    expect(el.querySelector('a[aria-label^="Open my scorecard for"]')).not.toBeNull();
  });

  it('explains an account without a linked employee and asks for nothing', async () => {
    configure(NO_EMPLOYEE);
    const spy = vi.spyOn(TestBed.inject(ScorecardDataService), 'myScorecards');
    const { el } = await render(MyKpisComponent);
    expect(el.textContent).toContain('No employee record is linked to your account');
    expect(spy).not.toHaveBeenCalled();
  });

  it('shows the Late chip from the list row’s submissionDueDate, without reading details', async () => {
    configure(EMPLOYEE_46);
    const api = TestBed.inject(ScorecardDataService);
    const row = (
      id: number,
      periodId: number,
      submittedOn: string | null,
      submissionDueDate: string | null,
    ) =>
      ({
        id,
        periodId,
        periodName: `P${periodId}`,
        status: 'Submitted',
        itemCount: 3,
        overallScore: null,
        ratingLabel: null,
        submittedOn,
        submissionDueDate,
      }) as Scorecard;
    const page: PagedResult<Scorecard> = {
      items: [
        row(1, 2, '2027-01-20', '2027-01-14'),
        row(2, 1, '2026-10-01', '2026-10-14'),
        row(3, 3, '2026-10-01', null),
      ],
      total: 3,
      pageIndex: 0,
      pageSize: 10,
    };
    vi.spyOn(api, 'myScorecards').mockReturnValue(of(page));
    const details = vi.spyOn(api, 'getById');
    const { el } = await render(MyKpisComponent);
    const rows = Array.from(el.querySelectorAll('tr.mat-mdc-row'));
    expect(rows[0].querySelector('[data-testid="late-chip"]')?.textContent).toContain('Late');
    expect(rows[1].querySelector('[data-testid="late-chip"]')).toBeNull();
    expect(rows[2].querySelector('[data-testid="late-chip"]')).toBeNull(); // no due date
    expect(details).not.toHaveBeenCalled();
  });
});

describe('My scorecard (the employee’s page)', () => {
  it('adapts the actual input to each measurement type, for the owner', async () => {
    configure(EMPLOYEE_56);
    const { el } = await render(MyScorecardComponent, { id: '6' });
    expect(buttons(el)).toEqual(['arrow_backMy KPIs', 'saveSave draft', 'sendSubmit']);
    // Number: a decimal input labelled with the KPI.
    const output = kpi(el, 'OUTPUT-TASKS').querySelector('input[type="number"]')!;
    expect(output.getAttribute('step')).toBe('any');
    expect(kpi(el, 'OUTPUT-TASKS').textContent).toContain('Actual for OUTPUT-TASKS');
    // Rating: 0 to the item's maximum rating.
    const rating = kpi(el, 'CUSTOMER-RATING').querySelector('input[type="number"]')!;
    expect(rating.getAttribute('min')).toBe('0');
    expect(rating.getAttribute('max')).toBe('5');
    // Boolean: a yes/no toggle, no number input.
    const boolean = kpi(el, 'TRAINING-DONE');
    expect(boolean.querySelector('input[type="number"]')).toBeNull();
    expect(
      Array.from(boolean.querySelectorAll('mat-button-toggle')).map((t) => t.textContent?.trim()),
    ).toEqual(['Yes', 'No']);
    // Comments and evidence for every KPI.
    expect(el.querySelectorAll('textarea')).toHaveLength(5); // 4 KPIs and the overall comment
    expect(el.querySelectorAll('button[aria-label^="Add evidence to"]')).toHaveLength(4);
  });

  it('keeps someone else’s scorecard read-only, without evidence changes', async () => {
    configure(HR_ADMIN); // HR can see scorecard 6, but it is EMP056's
    const { el } = await render(MyScorecardComponent, { id: '6' });
    expect(buttons(el)).toEqual(['arrow_backMy KPIs']);
    expect(el.querySelector('input[type="number"]')).toBeNull();
    expect(el.querySelector('button[aria-label^="Add evidence"]')).toBeNull();
    expect(el.textContent).toContain('Only they can enter results and evidence');
  });

  it('keeps a submitted scorecard read-only for its owner', async () => {
    configure(EMPLOYEE_46);
    const { el } = await render(MyScorecardComponent, { id: '1' });
    expect(buttons(el)).toEqual(['arrow_backMy KPIs']);
    expect(el.textContent).toContain('can no longer be changed');
    expect(el.querySelector('[data-testid="item-achievement"]')?.textContent).toMatch(/\d+\.\d\d%/);
  });

  it('puts the missing results of a submit on their KPIs (by the server’s index)', async () => {
    configure(EMPLOYEE_56);
    const { fixture, el } = await render(MyScorecardComponent, { id: '6' });
    const page = internals<ScorecardPage>(fixture);
    const card = page.card()!;
    const output = page.form().items.get(card.items[0].id)!.actual;
    output.setValue(30);
    output.markAsDirty();
    const api = TestBed.inject(ScorecardDataService);
    const save = vi.spyOn(api, 'saveResults');
    const submit = vi.spyOn(api, 'submit');
    page.submit(card);
    await settle(fixture);
    // Unsaved changes are saved first; the submit uses that answer's row version.
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0][1].rowVersion).toBe(card.rowVersion);
    const saved = page.card()!;
    expect(saved.rowVersion).not.toBe(card.rowVersion);
    expect(submit).toHaveBeenCalledWith(6, saved.rowVersion);
    // The server refused: 3 of 4 results missing, each shown on its own KPI.
    expect(page.formError()).toContain('Check: CUSTOMER-RATING, RESPONSE-HOURS, TRAINING-DONE.');
    expect(kpi(el, 'CUSTOMER-RATING').textContent).toContain('The actual result is missing.');
    expect(kpi(el, 'TRAINING-DONE').textContent).toContain('The actual result is missing.');
    expect(kpi(el, 'OUTPUT-TASKS').textContent).not.toContain('The actual result is missing.');
    expect(saved.status).toBe('Draft');
  });

  it('threads the row version through save, evidence and submit', async () => {
    configure(EMPLOYEE_56);
    const { fixture } = await render(MyScorecardComponent, { id: '6' });
    const page = internals<ScorecardPage>(fixture);
    const api = TestBed.inject(ScorecardDataService);
    const upload = vi.spyOn(api, 'uploadEvidence');
    const remove = vi.spyOn(api, 'deleteEvidence');
    const save = vi.spyOn(api, 'saveResults');
    const submit = vi.spyOn(api, 'submit');

    // Every result, then Save draft.
    const card = page.card()!;
    [30, 4, 9, 1].forEach((value, i) =>
      page.form().items.get(card.items[i].id)!.actual.setValue(value),
    );
    page.save(card);
    await settle(fixture);
    expect(save.mock.calls[0][1].rowVersion).toBe(card.rowVersion);
    const afterSave = page.card()!;
    expect(afterSave.items[0].achievementPct).not.toBeNull(); // the server's figure

    // Evidence: the upload answers the scorecard's new row version, which the next write uses.
    page.addEvidence(afterSave.items[0], pdfFile('proof.pdf'));
    await settle(fixture);
    expect(upload).toHaveBeenCalledWith(
      afterSave.items[0].id,
      afterSave.rowVersion,
      expect.any(File),
    );
    const afterUpload = page.card()!;
    expect(afterUpload.rowVersion).not.toBe(afterSave.rowVersion);
    expect(afterUpload.items[0].attachments.map((a) => a.fileName)).toEqual(['proof.pdf']);
    // The form kept its values: an evidence answer does not reset what is typed.
    expect(page.form().items.get(card.items[1].id)!.actual.value).toBe(4);

    page.removeEvidence(afterUpload.items[0], afterUpload.items[0].attachments[0]);
    await settle(fixture);
    expect(remove).toHaveBeenCalledWith(
      afterUpload.items[0].attachments[0].id,
      afterUpload.rowVersion,
    );
    const afterRemove = page.card()!;
    expect(afterRemove.items[0].attachments).toEqual([]);

    page.submit(afterRemove);
    await settle(fixture);
    expect(submit).toHaveBeenCalledWith(6, afterRemove.rowVersion);
    expect(page.card()!.status).toBe('Submitted');
    expect(page.card()!.overallScore).not.toBeNull();
  });

  it('checks a file before uploading it, and shows the refusal on its KPI', async () => {
    configure(EMPLOYEE_56);
    const { fixture, el } = await render(MyScorecardComponent, { id: '6' });
    const page = internals<ScorecardPage>(fixture);
    const upload = vi.spyOn(TestBed.inject(ScorecardDataService), 'uploadEvidence');
    const item = page.card()!.items[0];
    page.addEvidence(item, new File([new Uint8Array([0x68, 0x69])], 'notes.pdf'));
    await settle(fixture);
    expect(upload).not.toHaveBeenCalled();
    expect(page.evidenceErrors()[item.id]).toContain('Allowed evidence files');
    expect(kpi(el, 'OUTPUT-TASKS').querySelector('[role="alert"]')?.textContent).toContain(
      'Allowed evidence files',
    );
  });

  it('shows a conflict with Reload, and Reload reads the latest version', async () => {
    configure(EMPLOYEE_56);
    const { fixture, el } = await render(MyScorecardComponent, { id: '6' });
    const page = internals<ScorecardPage>(fixture);
    const api = TestBed.inject(ScorecardDataService);
    vi.spyOn(api, 'saveResults').mockReturnValueOnce(
      throwError((): AppError => ({
        code: 'Conflict',
        message: 'The record changed. Reload and try again.',
      })),
    );
    page.save(page.card()!);
    await settle(fixture);
    const banner = el.querySelector('.banner--error[role="alert"]')!;
    expect(banner.textContent).toContain('The record changed.');
    const getById = vi.spyOn(api, 'getById');
    (banner.querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);
    expect(getById).toHaveBeenCalledWith(6);
    expect(el.querySelector('.banner--error')).toBeNull();
  });

  it('shows the reviewer’s reason prominently when the scorecard is Returned', async () => {
    configure(EMPLOYEE_14);
    const { el } = await render(MyScorecardComponent, { id: '5' });
    expect(el.querySelector('[data-testid="return-reason"]')?.textContent).toContain(
      'Sample reason: please recheck the task count',
    );
    expect(el.querySelector('#return-title')?.textContent).toContain('Returned for revision by');
    expect(buttons(el)).toContain('sendSubmit');
  });

  it('shows the feedback read-only once Finalized', async () => {
    configure(EMPLOYEE_46);
    const { el } = await render(MyScorecardComponent, { id: '8' });
    expect(buttons(el)).toEqual(['arrow_backMy KPIs']);
    const feedback = el.querySelector('section.feedback')!;
    expect(feedback.textContent).toContain('Sample feedback: reliable and quick reviews.');
    expect(el.querySelector('[data-testid="item-feedback"]')?.textContent).toContain('rating 4');
    expect(el.querySelector('input')).toBeNull();
  });
});

describe('Reviews queue', () => {
  const rowsOf = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('tr.mat-mdc-row')).map((r) => r.textContent ?? '');

  it('lists the scorecards naming the manager reviewer, with the stage', async () => {
    configure(MANAGER);
    const { el } = await render(ReviewQueueComponent);
    const rows = rowsOf(el);
    expect(rows.some((r) => r.includes('EMP046') && r.includes('Manager review'))).toBe(true);
    expect(rows.some((r) => r.includes('EMP056'))).toBe(false);
    expect(el.querySelector('a[aria-label^="Review the scorecard of EMP046"]')).not.toBeNull();
  });

  it('never lists the viewer’s own scorecard, and keeps the server’s order', async () => {
    configure(HR_ADMIN);
    const api = TestBed.inject(ScorecardDataService);
    const pending = vi.spyOn(api, 'pendingReviews');
    const { el } = await render(ReviewQueueComponent);
    expect(rowsOf(el).some((r) => r.includes('EMP002'))).toBe(false);
    expect(rowsOf(el).length).toBeGreaterThan(0);
    // Nothing sorts on the screen: the oldest submission comes first, as answered.
    expect(pending.mock.calls[0][0].sort).toEqual({ active: '', direction: '' });
    expect(REVIEW_QUEUE_COLUMNS.map((c) => c.header)).toEqual([
      'Code',
      'Employee',
      'Period',
      'Status',
      'Submitted',
      'Score',
      'Stage',
    ]);
  });
});

interface ReviewPage {
  card(): ScorecardDetail | null;
  form(): ReviewForm;
  startReview(card: ScorecardDetail): void;
  saveNotes(card: ScorecardDetail): void;
  decisionDialog(card: ScorecardDetail, kind: string): ReviewDecisionData;
  decide(card: ScorecardDetail, kind: string): void;
}

describe('Review detail', () => {
  it('offers the manager stage to the reviewer and threads the row version', async () => {
    configure(MANAGER);
    const { fixture, el } = await render(ReviewDetailComponent, { id: '1' });
    expect(buttons(el)).toEqual([
      'arrow_backAll reviews',
      'play_arrowStart review',
      'undoReturn for revision',
      'blockReject',
      'task_altApprove',
    ]);
    const page = internals<ReviewPage>(fixture);
    const api = TestBed.inject(ScorecardDataService);
    const start = vi.spyOn(api, 'startReview');
    const saveReview = vi.spyOn(api, 'saveReview');
    const approve = vi.spyOn(api, 'approve');

    const card = page.card()!;
    page.startReview(card);
    await settle(fixture);
    expect(start).toHaveBeenCalledWith(1, card.rowVersion);
    const underReview = page.card()!;
    expect(underReview.status).toBe('Under Review');
    expect(buttons(el)).not.toContain('play_arrowStart review');

    page.form().items.get(underReview.items[0].id)!.rating.setValue(4);
    page.form().rating.setValue(4);
    page.saveNotes(underReview);
    await settle(fixture);
    expect(saveReview.mock.calls[0][1]).toMatchObject({
      rowVersion: underReview.rowVersion,
      managerRating: 4,
    });
    const noted = page.card()!;
    expect(noted.managerRating).toBe(4);

    // Approve: no HR review in period 2, so it finalizes at once (the dialog says so).
    const data = page.decisionDialog(noted, 'approve');
    expect(data.message).toContain('approving finalizes');
    expect(data.required).toBe(false);
    const done = await new Promise<ScorecardDetail>((resolve) =>
      data.send('Fictional feedback').subscribe(resolve),
    );
    expect(approve).toHaveBeenCalledWith(1, {
      rowVersion: noted.rowVersion,
      comment: 'Fictional feedback',
    });
    expect(done.status).toBe('Finalized');
  });

  it('saves unsaved review notes before a decision, then uses the new row version', async () => {
    configure(MANAGER);
    const { fixture } = await render(ReviewDetailComponent, { id: '1' });
    const page = internals<ReviewPage>(fixture);
    const api = TestBed.inject(ScorecardDataService);
    const reject = vi.spyOn(api, 'reject');
    const card = page.card()!;
    page.form().rating.setValue(2);
    page.form().group.markAsDirty();
    const result = await new Promise<ScorecardDetail>((resolve) =>
      page.decisionDialog(card, 'reject').send('Fictional reason').subscribe(resolve),
    );
    const notesVersion = reject.mock.calls[0][1].rowVersion;
    expect(notesVersion).not.toBe(card.rowVersion);
    expect(result.status).toBe('Rejected');
    expect(result.managerRating).toBe(2);
  });

  it('keeps notes typed before Start review, still unsaved', async () => {
    configure(MANAGER);
    const { fixture } = await render(ReviewDetailComponent, { id: '1' });
    const page = internals<ReviewPage>(fixture);
    page.form().comment.setValue('Typed before starting');
    page.form().group.markAsDirty();
    page.startReview(page.card()!);
    await settle(fixture);
    expect(page.card()!.status).toBe('Under Review');
    expect(page.form().comment.value).toBe('Typed before starting');
    expect(page.form().group.dirty).toBe(true);
  });

  it('never offers review actions on the viewer’s own scorecard', async () => {
    configure(HR_ADMIN);
    const { el } = await render(ReviewDetailComponent, { id: '7' });
    expect(buttons(el)).toEqual(['arrow_backAll reviews']);
    expect(el.querySelector('[data-testid="review-note"]')?.textContent).toContain(
      'your own scorecard',
    );
    expect(el.querySelector('input')).toBeNull();
  });

  it('offers the HR stage to HR, but not to the HR person who approved it', async () => {
    // Su Myat Noe approves EMP006's card 4 in a period that asks for an HR review.
    configure(HR_ADMIN);
    const api = TestBed.inject(ScorecardDataService);
    const real = await new Promise<ScorecardDetail>((resolve) => api.getById(4).subscribe(resolve));
    const approved: ScorecardDetail = {
      ...real,
      status: 'Approved',
      requiresHrReview: true,
      history: [
        ...real.history,
        {
          step: 'Manager',
          action: 'Approve',
          fromStatus: 'Under Review',
          toStatus: 'Approved',
          actorUserId: HR_ADMIN.id,
          actorName: 'Su Myat Noe',
          comment: null,
          overallScore: real.overallScore,
          occurredAtUtc: '2026-11-10T09:00:00.000Z',
        },
      ],
    };
    vi.spyOn(api, 'getById').mockReturnValue(of(approved));
    const asApprover = await render(ReviewDetailComponent, { id: '4' });
    expect(buttons(asApprover.el)).toEqual(['arrow_backAll reviews']);
    expect(asApprover.el.textContent).toContain('another HR person');

    current.set(HR_OFFICER); // Thura Aung: HR tier (the mock build allows every key)
    asApprover.fixture.detectChanges();
    await settle(asApprover.fixture);
    expect(buttons(asApprover.el)).toEqual([
      'arrow_backAll reviews',
      'undoReturn',
      'blockReject',
      'verifiedFinalize',
    ]);
    const page = internals<ReviewPage>(asApprover.fixture);
    expect(page.decisionDialog(approved, 'finalize').label).toBe('HR comment (optional)');
  });

  it('hides the HR stage from a manager without the HR tier (restricted permissions)', async () => {
    configure({ ...MANAGER, permissions: ['kpi:View', 'kpi:Create', 'kpi:Approve'] });
    const api = TestBed.inject(ScorecardDataService);
    const real = await new Promise<ScorecardDetail>((resolve) => api.getById(1).subscribe(resolve));
    vi.spyOn(api, 'getById').mockReturnValue(of({ ...real, status: 'Approved' }));
    const { el } = await render(ReviewDetailComponent, { id: '1' });
    expect(buttons(el)).toEqual(['arrow_backAll reviews']);
    expect(el.textContent).toContain('waits for the HR final review');
  });

  it('shows a 403 from the server as its message, without changing the page', async () => {
    configure(MANAGER);
    const { fixture } = await render(ReviewDetailComponent, { id: '1' });
    const page = internals<ReviewPage>(fixture);
    const api = TestBed.inject(ScorecardDataService);
    vi.spyOn(api, 'startReview').mockReturnValueOnce(
      throwError((): AppError => ({
        code: 'Forbidden',
        message: "Only the employee's reviewer or HR can review this scorecard.",
      })),
    );
    const before = page.card()!;
    page.startReview(before);
    await settle(fixture);
    expect(document.body.textContent).toContain("Only the employee's reviewer or HR");
    expect(page.card()).toEqual(before);
  });
});

describe('Return and reject dialog', () => {
  async function open(data: Partial<ReviewDecisionData>) {
    TestBed.configureTestingModule({ imports: [MatDialogModule] });
    const close = vi.fn();
    const send = vi.fn(data.send ?? (() => of({ status: 'Returned' } as ScorecardDetail)));
    TestBed.overrideProvider(MAT_DIALOG_DATA, {
      useValue: {
        title: 'Return for revision?',
        message: 'Say what should change.',
        label: 'Reason',
        required: true,
        confirmText: 'Return',
        ...data,
        send,
      },
    });
    TestBed.overrideProvider(MatDialogRef, { useValue: { close, disableClose: false } });
    const fixture = TestBed.createComponent(ReviewDecisionDialogComponent);
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const type = async (text: string) => {
      const box = el.querySelector('textarea')!;
      box.value = text;
      box.dispatchEvent(new Event('input'));
      await settle(fixture);
    };
    const submit = async () => {
      el.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
      await settle(fixture);
    };
    return { el, close, send, type, submit };
  }

  it('requires a reason and never sends a blank one', async () => {
    const dialog = await open({});
    await dialog.type('   ');
    await dialog.submit();
    expect(dialog.send).not.toHaveBeenCalled();
    expect(dialog.el.textContent).toContain(REASON_REQUIRED);
    await dialog.type('Fictional reason');
    await dialog.submit();
    expect(dialog.send).toHaveBeenCalledWith('Fictional reason');
    expect(dialog.close).toHaveBeenCalledWith({ ok: true, card: { status: 'Returned' } });
  });

  it('shows the server’s comment error on the field and stays open', async () => {
    const dialog = await open({
      send: () =>
        throwError((): AppError => ({
          code: 'Validation',
          message: 'Please correct the highlighted fields.',
          fieldErrors: { comment: 'A reason is required.' },
        })),
    });
    await dialog.type('x');
    await dialog.submit();
    expect(dialog.el.querySelector('mat-error')?.textContent).toContain('A reason is required.');
    expect(dialog.close).not.toHaveBeenCalled();
  });

  it('closes with any other failure for the page to show (a 409)', async () => {
    const conflict: AppError = { code: 'Conflict', message: 'The scorecard is Finalized.' };
    const dialog = await open({ send: () => throwError(() => conflict) });
    await dialog.type('Fictional reason');
    await dialog.submit();
    expect(dialog.close).toHaveBeenCalledWith({ ok: false, error: conflict });
  });

  it('lets an optional comment be empty and caps the length at 1000', async () => {
    const dialog = await open({ required: false, label: 'Comment (optional)' });
    await dialog.submit();
    expect(dialog.send).toHaveBeenCalledWith(null);
    expect(dialog.el.querySelector('textarea')?.getAttribute('maxlength')).toBe('1000');
  });
});

describe('Performance routes for the employee and reviewer screens', () => {
  it('guards My KPIs with kpi:View and Reviews with kpi:Approve', () => {
    const route = (path: string) => PERFORMANCE_ROUTES.find((r) => r.path === path)!;
    expect(route('my-kpis').data?.['permission']).toBe('kpi:View');
    expect(route('reviews').data?.['permission']).toBe('kpi:Approve');
    for (const path of ['my-kpis', 'reviews']) {
      expect(route(path).children?.map((c) => c.path)).toEqual(['', ':id']);
      expect(route(path).children?.every((c) => !!c.loadComponent)).toBe(true);
    }
  });

  function landing(permissions?: string[]): unknown {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: CurrentUserService,
          useValue: { current: signal({ ...EMPLOYEE_56, permissions }) },
        },
      ],
    });
    return TestBed.runInInjectionContext(() => performanceLanding({} as never));
  }

  it('sends /performance to the first screen the role may see, never setup for an employee', () => {
    expect(landing(['dashboard:View', 'kpi:View', 'kpi:Create'])).toBe('/performance/dashboard');
  });

  it('sends a setup-only role to Assignments and a role with nothing to the dashboard', () => {
    expect(landing(['kpi-setup:View'])).toBe('/performance/assignments');
    expect(landing(['kpi:Approve'])).toBe('/performance/reviews');
    expect(landing([])).toBe('/performance/dashboard');
  });

  it('redirects /performance in the router (Employee role) and refuses Reviews', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'performance', children: PERFORMANCE_ROUTES },
          { path: 'forbidden', children: [] },
        ]),
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
        {
          provide: CurrentUserService,
          useValue: {
            current: signal({ ...EMPLOYEE_56, permissions: ['kpi:View', 'kpi:Create'] }),
          },
        },
      ],
    });
    const harness = await RouterTestingHarness.create('/performance');
    await harness.fixture.whenStable();
    const router = TestBed.inject(Router);
    expect(router.url).toBe('/performance/dashboard');
    await harness.navigateByUrl('/performance/reviews');
    expect(router.url).toBe('/forbidden');
    await harness.navigateByUrl('/performance/assignments');
    expect(router.url).toBe('/forbidden');
  });
});
