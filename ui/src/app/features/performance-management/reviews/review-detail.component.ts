import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Observable, defer, filter, finalize, of, switchMap, tap } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import {
  KpiAttachment,
  ReviewDecision,
  SaveReviewRequest,
  ScorecardDetail,
  ScorecardItem,
} from '../../../core/models/kpi.models';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { EvidenceListComponent } from '../shared/evidence-list.component';
import { KpiItemFactsComponent } from '../shared/kpi-item-facts.component';
import { ReviewHistoryComponent } from '../shared/review-history.component';
import { ScorecardSummaryComponent } from '../shared/scorecard-summary.component';
import {
  NO_REVIEW_ACTIONS,
  ReviewActions,
  noActionReason,
  reviewActions,
  reviewStage,
  rowErrors,
} from '../shared/scorecard-rules';
import { injectScorecardViewer } from '../shared/scorecard-viewer';
import {
  ReviewDecisionData,
  ReviewDecisionDialogComponent,
  ReviewDecisionResult,
} from './review-decision-dialog.component';

const COMMENT_MAX = 1000;

/** A rating of 1 to 5 with at most 2 decimals (the API's rule), or empty. */
export function ratingRule(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  if (value === null || (value as unknown) === '') return null;
  if (!(value >= 1 && value <= 5)) return { rating: 'The rating must be between 1 and 5.' };
  const scaled = value * 100;
  return Math.abs(scaled - Math.round(scaled)) < 1e-6
    ? null
    : { rating: 'Use at most 2 decimal places.' };
}

export interface ReviewControls {
  rating: FormControl<number | null>;
  comment: FormControl<string>;
}

export interface ReviewForm {
  group: FormGroup;
  items: Map<number, ReviewControls>;
  rating: FormControl<number | null>;
  comment: FormControl<string>;
}

const ratingControl = (value: number | null) =>
  new FormControl<number | null>(value, { validators: [ratingRule] });
const commentControl = (value: string | null) =>
  new FormControl(value ?? '', {
    nonNullable: true,
    validators: [Validators.maxLength(COMMENT_MAX)],
  });

export function buildReviewForm(card: ScorecardDetail): ReviewForm {
  const items = new Map<number, ReviewControls>();
  const controls: Record<string, AbstractControl> = {};
  for (const item of card.items) {
    const pair = {
      rating: ratingControl(item.managerRating),
      comment: commentControl(item.managerComment),
    };
    items.set(item.id, pair);
    controls[`rating${item.id}`] = pair.rating;
    controls[`comment${item.id}`] = pair.comment;
  }
  const rating = ratingControl(card.managerRating);
  const comment = commentControl(card.managerComment);
  controls['managerRating'] = rating;
  controls['managerComment'] = comment;
  return { group: new FormGroup(controls), items, rating, comment };
}

const numberOrNull = (value: unknown): number | null =>
  value === null || value === undefined || value === '' || !Number.isFinite(Number(value))
    ? null
    : Number(value);

/** The review-notes body: every KPI in the scorecard's order, so `items[i]` is `card.items[i]`. */
export function reviewRequest(card: ScorecardDetail, form: ReviewForm): SaveReviewRequest {
  return {
    rowVersion: card.rowVersion,
    managerRating: numberOrNull(form.rating.value),
    managerComment: form.comment.value.trim() || null,
    items: card.items.map((item) => {
      const pair = form.items.get(item.id);
      return {
        id: item.id,
        managerRating: numberOrNull(pair?.rating.value),
        managerComment: pair?.comment.value.trim() || null,
      };
    }),
  };
}

/** The saved review notes. Not the status: notes typed before Start review survive it. */
function savedReview(card: ScorecardDetail | null): string {
  if (!card) return '';
  return JSON.stringify([
    card.id,
    card.managerRating,
    card.managerComment,
    card.items.map((i) => [i.id, i.managerRating, i.managerComment]),
  ]);
}

function setServerError(control: AbstractControl | undefined, message: string): void {
  if (!control) return;
  control.setErrors({ ...(control.errors ?? {}), server: message });
  control.markAsTouched();
}

type Decision = 'approve' | 'return' | 'reject' | 'finalize';

/**
 * The reviewer's page for one scorecard: the employee's results read-only (actuals, the server's
 * achievement and weighted scores, the employee's comments and evidence downloads), the reviewer's
 * rating (1 to 5) and comment per KPI and overall, and the decisions.
 *
 * Buttons follow `reviewActions` (UX only; the server decides and answers 403 or 409, shown with
 * its message): never on the viewer's own scorecard; the manager stage for the reviewer or HR; the
 * HR stage for HR, except whoever approved it. Every write sends the latest answer's row version;
 * unsaved review notes are saved before a decision.
 */
@Component({
  selector: 'app-review-detail',
  imports: [
    DecimalPipe,
    EmptyStateComponent,
    EvidenceListComponent,
    KpiItemFactsComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    ReviewHistoryComponent,
    RouterLink,
    ScorecardSummaryComponent,
  ],
  template: `
    @if (notFound()) {
      <app-empty-state
        icon="event_busy"
        title="Scorecard not found"
        message="It may not exist, or it is not one you can review."
      >
        <a mat-flat-button routerLink="/performance/reviews">Back to reviews</a>
      </app-empty-state>
    } @else if (detail.error() && !card()) {
      <app-empty-state
        icon="error"
        title="The scorecard could not be loaded"
        [message]="loadError()"
      >
        <button mat-flat-button type="button" (click)="reload()">Try again</button>
      </app-empty-state>
    } @else if (card(); as c) {
      @let can = actions();
      <app-page-header
        [title]="'Review · ' + c.employeeCode + ' ' + c.employeeName"
        [description]="c.periodName + ' · ' + c.departmentName"
      >
        <ng-container pageActions>
          <a mat-stroked-button routerLink="/performance/reviews">
            <mat-icon>arrow_back</mat-icon>All reviews
          </a>
          @if (can.startReview) {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="startReview(c)">
              <mat-icon>play_arrow</mat-icon>Start review
            </button>
          }
          @if (can.returnForRevision) {
            <button
              mat-stroked-button
              type="button"
              [disabled]="busy()"
              (click)="decide(c, 'return')"
            >
              <mat-icon>undo</mat-icon>{{ hrStage() ? 'Return' : 'Return for revision' }}
            </button>
          }
          @if (can.reject) {
            <button
              mat-stroked-button
              type="button"
              class="danger-outline"
              [disabled]="busy()"
              (click)="decide(c, 'reject')"
            >
              <mat-icon>block</mat-icon>Reject
            </button>
          }
          @if (can.approve) {
            <button
              mat-flat-button
              type="button"
              [disabled]="busy()"
              (click)="decide(c, 'approve')"
            >
              <mat-icon>task_alt</mat-icon>Approve
            </button>
          }
          @if (can.finalize) {
            <button
              mat-flat-button
              type="button"
              [disabled]="busy()"
              (click)="decide(c, 'finalize')"
            >
              <mat-icon>verified</mat-icon>Finalize
            </button>
          }
        </ng-container>
      </app-page-header>

      @if (detail.isLoading() || busy()) {
        <mat-progress-bar mode="indeterminate" aria-label="Updating the scorecard" />
      }

      @if (conflict()) {
        <div class="banner banner--error" role="alert">
          <mat-icon aria-hidden="true">sync_problem</mat-icon>
          <span>{{ conflict() }}</span>
          <button mat-stroked-button type="button" (click)="reload()">Reload</button>
        </div>
      }
      @if (formError()) {
        <div class="banner banner--error" role="alert">
          <mat-icon aria-hidden="true">error</mat-icon>
          <span>{{ formError() }}</span>
        </div>
      }
      @if (note(); as text) {
        <div class="banner" role="status" data-testid="review-note">
          <mat-icon aria-hidden="true">info</mat-icon>
          <span>{{ text }}</span>
        </div>
      } @else if (hrStage()) {
        <div class="banner" role="status">
          <mat-icon aria-hidden="true">verified_user</mat-icon>
          <span>
            Approved by the reviewer. HR final review: finalize it, or return or reject it with a
            reason.
          </span>
        </div>
      }

      <app-scorecard-summary [card]="c" [showEmployee]="true" />

      <h2 class="section">KPIs and results</h2>
      <ol class="kpis">
        @for (item of c.items; track item.id) {
          @let f = controls(item);
          <li class="kpi" [attr.aria-labelledby]="'kpi-' + item.id" [attr.data-kpi]="item.kpiCode">
            <h3 class="kpi-title" [id]="'kpi-' + item.id">
              <span class="code">{{ item.kpiCode }}</span> {{ item.kpiName }}
            </h3>
            <app-kpi-item-facts [item]="item" [showResults]="true" />
            <p class="said">
              <strong>Employee's comment:</strong> {{ item.employeeComment ?? 'None.' }}
            </p>
            <app-evidence-list [item]="item" (download)="download($event)" />

            @if (can.saveReview && f) {
              <div class="rate">
                <mat-form-field class="rating">
                  <mat-label>Rating for {{ item.kpiCode }}</mat-label>
                  <input
                    matInput
                    type="number"
                    step="0.01"
                    min="1"
                    max="5"
                    inputmode="decimal"
                    [formControl]="f.rating"
                  />
                  <mat-hint>1 to 5</mat-hint>
                  <mat-error>{{ errorOf(f.rating) }}</mat-error>
                </mat-form-field>
                <mat-form-field class="comment">
                  <mat-label>Feedback on {{ item.kpiCode }}</mat-label>
                  <textarea matInput rows="2" maxlength="1000" [formControl]="f.comment"></textarea>
                  <mat-error>{{ errorOf(f.comment) }}</mat-error>
                </mat-form-field>
              </div>
            } @else if (item.managerRating !== null || item.managerComment) {
              <p class="said">
                <strong>Reviewer:</strong>
                @if (item.managerRating !== null) {
                  rating {{ item.managerRating | number: '1.0-2' }} of 5.
                }
                {{ item.managerComment }}
              </p>
            }
          </li>
        }
      </ol>

      <h2 class="section">Overall</h2>
      <section class="card" aria-label="Overall comments">
        <p class="said">
          <strong>Employee's overall comment:</strong> {{ c.employeeComment ?? 'None.' }}
        </p>
        @if (can.saveReview) {
          <div class="rate">
            <mat-form-field class="rating">
              <mat-label>Overall rating</mat-label>
              <input
                matInput
                type="number"
                step="0.01"
                min="1"
                max="5"
                inputmode="decimal"
                [formControl]="form().rating"
              />
              <mat-hint>1 to 5</mat-hint>
              <mat-error>{{ errorOf(form().rating) }}</mat-error>
            </mat-form-field>
            <mat-form-field class="comment">
              <mat-label>Overall feedback</mat-label>
              <textarea
                matInput
                rows="3"
                maxlength="1000"
                [formControl]="form().comment"
              ></textarea>
              <mat-error>{{ errorOf(form().comment) }}</mat-error>
            </mat-form-field>
          </div>
          <div class="save">
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="saveNotes(c)">
              <mat-icon>save</mat-icon>Save review notes
            </button>
            <span class="hint">Notes do not change the status. A decision saves them first.</span>
          </div>
        } @else {
          <p class="said">
            <strong>Reviewer's overall rating:</strong>
            {{ c.managerRating === null ? '—' : (c.managerRating | number: '1.0-2') }}
          </p>
          <p class="said"><strong>Reviewer's feedback:</strong> {{ c.managerComment ?? '—' }}</p>
        }
        @if (c.hrComment) {
          <p class="said"><strong>HR comment:</strong> {{ c.hrComment }}</p>
        }
      </section>

      <h2 class="section">Review history</h2>
      <app-review-history [history]="c.history" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the scorecard" />
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .danger-outline {
      color: var(--mat-sys-error);
    }
    .banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      margin-bottom: 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .banner span {
      flex: 1 1 220px;
    }
    .banner--error {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .section {
      margin: 24px 0 10px;
      font: var(--mat-sys-title-medium);
    }
    .card,
    .kpi {
      padding: 14px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .kpis {
      display: grid;
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .kpi,
    .card {
      display: grid;
      gap: 10px;
    }
    .kpi-title {
      margin: 0;
      font: var(--mat-sys-title-small);
      overflow-wrap: anywhere;
    }
    .code {
      color: var(--mat-sys-primary);
    }
    .said {
      margin: 0;
      white-space: pre-line;
    }
    .rate {
      display: grid;
      grid-template-columns: minmax(0, 200px) minmax(0, 1fr);
      gap: 4px 16px;
    }
    .rating,
    .comment {
      width: 100%;
    }
    .save {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    @media (max-width: 600px) {
      .rate {
        grid-template-columns: minmax(0, 1fr);
      }
      .kpi,
      .card {
        padding: 12px;
      }
    }
  `,
})
export class ReviewDetailComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();

  private readonly scorecards = inject(ScorecardDataService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(UiNotificationService);
  protected readonly viewer = injectScorecardViewer();

  protected readonly conflict = signal('');
  protected readonly formError = signal('');
  private readonly writing = signal(false);

  protected readonly detail = rxResource({
    params: () => Number(this.id()),
    stream: ({ params }) => this.scorecards.getById(params),
  });
  protected readonly card = computed<ScorecardDetail | null>(() => this.detail.value() ?? null);
  protected readonly busy = computed(() => this.writing() || this.detail.isLoading());
  protected readonly notFound = computed(() => {
    const error = this.detail.error();
    const cause = error instanceof Error ? error.cause : error;
    return isAppError(cause) && cause.code === 'NotFound';
  });
  protected readonly loadError = computed(() =>
    this.detail.error() ? errorMessage(this.detail.error()) : '',
  );

  protected readonly actions = computed<ReviewActions>(() => {
    const card = this.card();
    return card ? reviewActions(card, this.viewer()) : NO_REVIEW_ACTIONS;
  });
  protected readonly note = computed(() => {
    const card = this.card();
    return card ? noActionReason(card, this.viewer()) : null;
  });
  protected readonly hrStage = computed(() => {
    const status = this.card()?.status;
    return !!status && reviewStage(status) === 'HR';
  });

  /** Rebuilt only when the saved review notes change on the server. */
  private readonly savedKey = computed(() => savedReview(this.card()));
  protected readonly form = computed<ReviewForm>(() => {
    this.savedKey();
    const card = untracked(() => this.card());
    return card
      ? buildReviewForm(card)
      : {
          group: new FormGroup({}),
          items: new Map(),
          rating: ratingControl(null),
          comment: commentControl(null),
        };
  });

  protected controls(item: ScorecardItem): ReviewControls | undefined {
    return this.form().items.get(item.id);
  }

  protected errorOf(control: AbstractControl): string {
    const errors = control.errors ?? {};
    if (errors['server']) return String(errors['server']);
    if (errors['rating']) return String(errors['rating']);
    if (errors['maxlength']) return `At most ${errors['maxlength'].requiredLength} characters.`;
    return '';
  }

  protected reload(): void {
    this.conflict.set('');
    this.formError.set('');
    this.detail.reload();
  }

  private write<T>(call: Observable<T>): Observable<T> {
    this.writing.set(true);
    return call.pipe(finalize(() => this.writing.set(false)));
  }

  /** Takes the answer (values and row version). Only an answer to saved notes makes the notes clean. */
  private accept(saved: ScorecardDetail, notesSaved = false): void {
    this.detail.set(saved);
    if (notesSaved) this.form().group.markAsPristine();
  }

  /** 409 shows the server's message with Reload; a 400 goes onto its field; anything else a toast. */
  private fail(error: unknown): void {
    const card = this.card();
    if (isAppError(error) && error.code === 'Conflict') {
      this.conflict.set(error.message);
      return;
    }
    if (isAppError(error) && error.code === 'Validation' && card) {
      const form = this.form();
      const mapped = rowErrors(
        error.fieldErrors,
        card.items.map((i) => i.id),
      );
      for (const [id, fields] of mapped.items) {
        const pair = form.items.get(id);
        if (fields['managerRating']) setServerError(pair?.rating, fields['managerRating']);
        if (fields['managerComment']) setServerError(pair?.comment, fields['managerComment']);
      }
      if (mapped.other['managerRating']) setServerError(form.rating, mapped.other['managerRating']);
      if (mapped.other['managerComment'])
        setServerError(form.comment, mapped.other['managerComment']);
      this.formError.set(error.message);
      return;
    }
    this.toast.error(error);
  }

  private clearErrors(): void {
    this.conflict.set('');
    this.formError.set('');
  }

  /** Unsaved notes are saved first, so a decision never drops them. */
  private notesFirst(): Observable<ScorecardDetail> {
    return defer(() => {
      const card = this.card();
      if (!card) throw new Error('No scorecard loaded.');
      if (!this.actions().saveReview || !this.form().group.dirty) return of(card);
      return this.scorecards
        .saveReview(card.id, reviewRequest(card, this.form()))
        .pipe(tap((saved) => this.accept(saved, true)));
    });
  }

  private notesValid(): boolean {
    const group = this.form().group;
    if (!this.actions().saveReview || group.valid) return true;
    group.markAllAsTouched();
    this.formError.set('Please correct the highlighted ratings and comments.');
    return false;
  }

  protected startReview(card: ScorecardDetail): void {
    this.clearErrors();
    this.write(
      defer(() => this.scorecards.startReview(card.id, this.card()?.rowVersion ?? '')),
    ).subscribe({
      next: (saved) => {
        this.accept(saved);
        this.toast.success(`Review of ${saved.employeeCode} started.`);
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  protected saveNotes(card: ScorecardDetail): void {
    this.clearErrors();
    if (!this.notesValid()) return;
    this.write(
      this.scorecards.saveReview(card.id, reviewRequest(this.card() ?? card, this.form())),
    ).subscribe({
      next: (saved) => {
        this.accept(saved, true);
        this.toast.success('Review notes saved.');
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  /** The dialog of each decision: its wording, whether a reason is required, and the call. */
  protected decisionDialog(card: ScorecardDetail, kind: Decision): ReviewDecisionData {
    const who = `${card.employeeCode} · ${card.employeeName}`;
    const command = (id: number, decision: ReviewDecision): Observable<ScorecardDetail> => {
      switch (kind) {
        case 'approve':
          return this.scorecards.approve(id, decision);
        case 'return':
          return this.scorecards.returnForRevision(id, decision);
        case 'reject':
          return this.scorecards.reject(id, decision);
        case 'finalize':
          return this.scorecards.finalize(id, decision);
      }
    };
    const send = (comment: string | null) =>
      this.notesFirst().pipe(
        switchMap((latest) => command(latest.id, { rowVersion: latest.rowVersion, comment })),
      );
    switch (kind) {
      case 'approve':
        return {
          title: 'Approve this scorecard?',
          message: card.requiresHrReview
            ? `The scorecard of ${who} goes to HR for the final review. The server recalculates the score from the employee's results.`
            : `Period ${card.periodName} has no HR final review, so approving finalizes the scorecard of ${who} at once. It cannot be changed afterwards.`,
          label: 'Comment (optional)',
          required: false,
          confirmText: card.requiresHrReview ? 'Approve and send to HR' : 'Approve and finalize',
          send,
        };
      case 'finalize':
        return {
          title: 'Finalize this scorecard?',
          message: `HR final review: the scorecard of ${who} becomes Finalized and read-only. Your comment is saved as the HR comment.`,
          label: 'HR comment (optional)',
          required: false,
          confirmText: 'Finalize',
          send,
        };
      case 'return':
        return {
          title: 'Return for revision?',
          message: `The scorecard goes back to ${who} to correct and submit again; its score is cleared until then. Say what should change.`,
          label: 'Reason',
          required: true,
          confirmText: 'Return',
          send,
        };
      case 'reject':
        return {
          title: 'Reject this scorecard?',
          message: `Rejecting is final: ${who} cannot submit this scorecard again. Say why.`,
          label: 'Reason',
          required: true,
          confirmText: 'Reject',
          destructive: true,
          send,
        };
    }
  }

  protected decide(card: ScorecardDetail, kind: Decision): void {
    this.clearErrors();
    if (!this.notesValid()) return;
    const data = this.decisionDialog(card, kind);
    this.dialog
      .open<ReviewDecisionDialogComponent, ReviewDecisionData, ReviewDecisionResult>(
        ReviewDecisionDialogComponent,
        {
          data,
          width: '520px',
          maxWidth: '96vw',
          autoFocus: 'first-tabbable',
          role: data.destructive ? 'alertdialog' : 'dialog',
        },
      )
      .afterClosed()
      .pipe(filter((result): result is ReviewDecisionResult => !!result))
      .subscribe((result) => {
        if (!result.ok) {
          this.fail(result.error);
          return;
        }
        this.accept(result.card);
        this.toast.success(this.decisionMessage(result.card, kind));
      });
  }

  private decisionMessage(card: ScorecardDetail, kind: Decision): string {
    const who = card.employeeCode;
    switch (kind) {
      case 'approve':
        return card.status === 'Finalized'
          ? `Scorecard of ${who} approved and finalized.`
          : `Scorecard of ${who} approved. It now waits for the HR final review.`;
      case 'finalize':
        return `Scorecard of ${who} finalized.`;
      case 'return':
        return `Scorecard of ${who} returned for revision.`;
      case 'reject':
        return `Scorecard of ${who} rejected.`;
    }
  }

  /** Saves the file through the browser (never opened inline). */
  protected download(file: KpiAttachment): void {
    this.scorecards.downloadEvidence(file.id).subscribe({
      next: (result) => downloadFile(result),
      error: (e: unknown) => this.toast.error(e),
    });
  }
}
