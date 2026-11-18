import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Observable, defer, filter, finalize, from, switchMap, tap, throwError } from 'rxjs';
import { AppError, isAppError } from '../../../core/models/common.models';
import {
  KpiAttachment,
  SaveResultsRequest,
  ScorecardDetail,
  ScorecardItem,
} from '../../../core/models/kpi.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { kpiAchievement } from '../../../core/utilities/kpi-calculation';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { EvidenceListComponent } from '../shared/evidence-list.component';
import { KpiItemFactsComponent } from '../shared/kpi-item-facts.component';
import { ReviewHistoryComponent } from '../shared/review-history.component';
import { ScorecardSummaryComponent } from '../shared/scorecard-summary.component';
import {
  ActualInput,
  actualInputFor,
  canWorkOnScorecard,
  employeeNote,
  evidenceProblem,
  latestDecision,
  rowErrors,
} from '../shared/scorecard-rules';
import { injectScorecardViewer } from '../shared/scorecard-viewer';

const COMMENT_MAX = 1000;

/** The inputs of one KPI on the results form. */
export interface ResultControls {
  actual: FormControl<number | null>;
  comment: FormControl<string>;
}

/** The results form: one pair of controls per KPI (by item id) and the overall comment. */
export interface ResultsForm {
  group: FormGroup;
  items: Map<number, ResultControls>;
  comment: FormControl<string>;
}

/** The checks the browser can make before the server does (the server's word is final). */
export function actualValidators(input: ActualInput): ValidatorFn[] {
  if (input.kind === 'rating') return [Validators.min(input.min), Validators.max(input.max)];
  if (input.kind === 'decimal' && input.min !== null) return [Validators.min(input.min)];
  return [];
}

/** Builds the results form from the server's saved values. */
export function buildResultsForm(card: ScorecardDetail): ResultsForm {
  const items = new Map<number, ResultControls>();
  const controls: Record<string, AbstractControl> = {};
  for (const item of card.items) {
    const pair: ResultControls = {
      actual: new FormControl<number | null>(item.actualValue, {
        validators: actualValidators(actualInputFor(item)),
      }),
      comment: new FormControl(item.employeeComment ?? '', {
        nonNullable: true,
        validators: [Validators.maxLength(COMMENT_MAX)],
      }),
    };
    items.set(item.id, pair);
    controls[`actual${item.id}`] = pair.actual;
    controls[`comment${item.id}`] = pair.comment;
  }
  const comment = new FormControl(card.employeeComment ?? '', {
    nonNullable: true,
    validators: [Validators.maxLength(COMMENT_MAX)],
  });
  controls['employeeComment'] = comment;
  return { group: new FormGroup(controls), items, comment };
}

/** A number input's value as the API wants it: a number, or null for "no result". */
function numberOrNull(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

/** The save body: every KPI in the scorecard's order, so a server error `items[i]` is `card.items[i]`. */
export function resultsRequest(card: ScorecardDetail, form: ResultsForm): SaveResultsRequest {
  return {
    rowVersion: card.rowVersion,
    employeeComment: form.comment.value.trim() || null,
    items: card.items.map((item) => {
      const pair = form.items.get(item.id);
      return {
        id: item.id,
        actualValue: numberOrNull(pair?.actual.value),
        employeeComment: pair?.comment.value.trim() || null,
      };
    }),
  };
}

/** The message under an input. */
export function controlError(control: AbstractControl): string {
  const errors = control.errors ?? {};
  if (errors['server']) return String(errors['server']);
  if (errors['min']) return `Must be at least ${errors['min'].min}.`;
  if (errors['max']) return `Must be at most ${errors['max'].max}.`;
  if (errors['maxlength']) return `At most ${errors['maxlength'].requiredLength} characters.`;
  return '';
}

/** What the saved values look like: the form is rebuilt only when they change on the server. */
function savedValues(card: ScorecardDetail | null): string {
  if (!card) return '';
  return JSON.stringify([
    card.id,
    card.status,
    card.employeeComment,
    card.items.map((i) => [i.id, i.actualValue, i.employeeComment]),
  ]);
}

/**
 * The employee's working page for one scorecard: the KPIs with their targets and weights, an input
 * per KPI that fits its measurement type (yes/no, a rating on its scale, or a decimal), comments,
 * evidence files, Save draft and Submit. Only the scorecard's own employee works on it, while it is
 * Draft or Returned and the period is Active (or Completed, for a Returned scorecard); otherwise
 * the page is read-only and shows why.
 *
 * Every figure of a score is the server's answer; the line under an input while typing is a
 * labelled preview only. Every write sends the row version of the latest answer (evidence answers
 * carry the scorecard's new one) and the buttons wait while a write or a reload runs.
 */
@Component({
  selector: 'app-my-scorecard',
  imports: [
    AppDatePipe,
    DecimalPipe,
    EmptyStateComponent,
    EvidenceListComponent,
    KpiItemFactsComponent,
    MatButtonModule,
    MatButtonToggleModule,
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
        message="It may not exist, or it is not one you can see."
      >
        <a mat-flat-button routerLink="/performance/my-kpis">Back to My KPIs</a>
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
      <app-page-header
        [title]="'My scorecard · ' + c.periodName"
        [description]="c.employeeCode + ' · ' + c.employeeName + ' · ' + c.departmentName"
      >
        <ng-container pageActions>
          <a mat-stroked-button routerLink="/performance/my-kpis">
            <mat-icon>arrow_back</mat-icon>My KPIs
          </a>
          @if (editable()) {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="save(c)">
              <mat-icon>save</mat-icon>Save draft
            </button>
            <button mat-flat-button type="button" [disabled]="busy()" (click)="submit(c)">
              <mat-icon>send</mat-icon>Submit
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
        <div class="banner banner--error" role="alert" data-testid="form-error">
          <mat-icon aria-hidden="true">error</mat-icon>
          <span>{{ formError() }}</span>
        </div>
      }

      @if (returned(); as r) {
        <section class="banner banner--warn" aria-labelledby="return-title">
          <mat-icon aria-hidden="true">undo</mat-icon>
          <div class="text">
            <h2 id="return-title" class="banner-title">
              Returned for revision by {{ r.actorName }} on {{ r.occurredAtUtc | appDate }}
            </h2>
            <p class="reason" data-testid="return-reason">{{ r.comment }}</p>
            @if (editable()) {
              <p class="small">Correct your results, then submit again.</p>
            }
          </div>
        </section>
      }

      @if (note(); as text) {
        <div class="banner" role="status">
          <mat-icon aria-hidden="true">lock</mat-icon>
          <span>{{ text }}</span>
        </div>
      }

      <app-scorecard-summary [card]="c" />

      @if (feedback()) {
        <section class="feedback card" aria-labelledby="feedback-title">
          <h2 id="feedback-title" class="section">Review feedback</h2>
          <dl class="facts">
            <div>
              <dt>Manager's overall rating</dt>
              <dd>{{ c.managerRating === null ? '—' : (c.managerRating | number: '1.0-2') }}</dd>
            </div>
            <div>
              <dt>Manager's comment</dt>
              <dd class="pre">{{ c.managerComment ?? '—' }}</dd>
            </div>
            <div>
              <dt>HR comment</dt>
              <dd class="pre">{{ c.hrComment ?? '—' }}</dd>
            </div>
            @if (rejected(); as r) {
              <div>
                <dt>Reason for rejection</dt>
                <dd class="pre">{{ r.comment }}</dd>
              </div>
            }
          </dl>
        </section>
      }

      <h2 class="section">KPIs</h2>
      <ol class="kpis">
        @for (item of c.items; track item.id) {
          @let f = controls(item);
          <li class="kpi" [attr.aria-labelledby]="'kpi-' + item.id" [attr.data-kpi]="item.kpiCode">
            <h3 class="kpi-title" [id]="'kpi-' + item.id">
              <span class="code">{{ item.kpiCode }}</span> {{ item.kpiName }}
            </h3>
            <app-kpi-item-facts [item]="item" [showResults]="true" />

            @if (editable() && f) {
              <div class="entry">
                @let kind = inputFor(item);
                @if (kind.kind === 'boolean') {
                  <div class="bool">
                    <span class="bool-label" [id]="'actual-label-' + item.id">
                      Achieved? ({{ item.kpiCode }})
                    </span>
                    <mat-button-toggle-group
                      [formControl]="f.actual"
                      [attr.aria-labelledby]="'actual-label-' + item.id"
                      hideSingleSelectionIndicator
                    >
                      <mat-button-toggle [value]="1">Yes</mat-button-toggle>
                      <mat-button-toggle [value]="0">No</mat-button-toggle>
                    </mat-button-toggle-group>
                    @if (f.actual.touched && f.actual.invalid) {
                      <p class="error" role="alert">{{ errorOf(f.actual) }}</p>
                    }
                  </div>
                } @else {
                  <mat-form-field class="actual">
                    <mat-label>Actual for {{ item.kpiCode }}</mat-label>
                    <input
                      matInput
                      type="number"
                      step="any"
                      inputmode="decimal"
                      [attr.min]="minOf(kind)"
                      [attr.max]="maxOf(kind)"
                      [formControl]="f.actual"
                    />
                    <mat-hint>{{ hint(item, kind) }}</mat-hint>
                    <mat-error>{{ errorOf(f.actual) }}</mat-error>
                  </mat-form-field>
                }
                @if (f.actual.dirty && preview(item, f.actual.value); as p) {
                  <p class="preview" data-testid="preview">
                    <mat-icon aria-hidden="true">visibility</mat-icon>
                    Preview: about {{ p }} achievement. Not official: the server calculates it when
                    you save or submit.
                  </p>
                }
                <mat-form-field class="comment">
                  <mat-label>Comment on {{ item.kpiCode }}</mat-label>
                  <textarea matInput rows="2" maxlength="1000" [formControl]="f.comment"></textarea>
                  <mat-error>{{ errorOf(f.comment) }}</mat-error>
                </mat-form-field>
              </div>
            } @else if (item.employeeComment) {
              <p class="said"><strong>Your comment:</strong> {{ item.employeeComment }}</p>
            }

            @if (feedback() && (item.managerRating !== null || item.managerComment)) {
              <p class="said" data-testid="item-feedback">
                <strong>Manager:</strong>
                @if (item.managerRating !== null) {
                  rating {{ item.managerRating | number: '1.0-2' }} of 5.
                }
                {{ item.managerComment }}
              </p>
            }

            <app-evidence-list
              [item]="item"
              [editable]="editable()"
              [disabled]="busy()"
              [error]="evidenceErrors()[item.id] ?? null"
              (add)="addEvidence(item, $event)"
              (remove)="removeEvidence(item, $event)"
              (download)="download($event)"
            />
          </li>
        }
      </ol>

      <h2 class="section">Overall comment</h2>
      @if (editable()) {
        <mat-form-field class="overall">
          <mat-label>Your overall comment</mat-label>
          <textarea matInput rows="3" maxlength="1000" [formControl]="form().comment"></textarea>
          <mat-error>{{ errorOf(form().comment) }}</mat-error>
        </mat-form-field>
      } @else {
        <p class="pre">{{ c.employeeComment ?? 'No overall comment.' }}</p>
      }

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
    .banner {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 10px;
      margin-bottom: 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .banner span,
    .banner .text {
      flex: 1 1 220px;
    }
    .banner--error {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .banner--warn {
      border: 2px solid var(--mat-sys-tertiary);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .banner-title {
      margin: 0;
      font: var(--mat-sys-title-small);
    }
    .reason {
      margin: 6px 0 0;
      font: var(--mat-sys-body-large);
      white-space: pre-line;
    }
    .small {
      margin: 4px 0 0;
      font: var(--mat-sys-body-small);
    }
    .card {
      margin-top: 16px;
      padding: 16px 20px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .feedback .section {
      margin-top: 0;
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 12px 24px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
    dd {
      margin: 2px 0 0;
    }
    .pre {
      white-space: pre-line;
    }
    .section {
      margin: 24px 0 10px;
      font: var(--mat-sys-title-medium);
    }
    .kpis {
      display: grid;
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .kpi {
      display: grid;
      gap: 12px;
      padding: 14px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .kpi-title {
      margin: 0;
      font: var(--mat-sys-title-small);
      overflow-wrap: anywhere;
    }
    .code {
      color: var(--mat-sys-primary);
    }
    .entry {
      display: grid;
      grid-template-columns: minmax(0, 260px) minmax(0, 1fr);
      gap: 4px 16px;
      align-items: start;
    }
    .entry .preview {
      grid-column: 1 / -1;
    }
    .bool {
      display: grid;
      gap: 6px;
    }
    .bool-label {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .actual,
    .comment,
    .overall {
      width: 100%;
    }
    .preview {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      font-style: italic;
    }
    .preview mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .said {
      margin: 0;
      white-space: pre-line;
    }
    .error {
      margin: 0;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
    }
    @media (max-width: 600px) {
      .entry {
        grid-template-columns: minmax(0, 1fr);
      }
      .kpi {
        padding: 12px;
      }
    }
  `,
})
export class MyScorecardComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();

  private readonly scorecards = inject(ScorecardDataService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  protected readonly viewer = injectScorecardViewer();

  /** Set when a write answers 409 (a stale `rowVersion` or a status that moved on). */
  protected readonly conflict = signal('');
  /** A refused save or submit, above the KPIs (the fields carry the details). */
  protected readonly formError = signal('');
  /** A refused evidence file, by item id. */
  protected readonly evidenceErrors = signal<Record<number, string>>({});
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

  /** Owner, Draft or Returned, period Active or (Returned) Completed. UX only; the server enforces. */
  protected readonly editable = computed(() => {
    const card = this.card();
    return !!card && canWorkOnScorecard(card, this.viewer());
  });
  protected readonly note = computed(() => {
    const card = this.card();
    return card ? employeeNote(card, this.viewer()) : null;
  });
  protected readonly returned = computed(() => {
    const card = this.card();
    return card?.status === 'Returned' ? latestDecision(card.history, 'Return') : null;
  });
  protected readonly rejected = computed(() => {
    const card = this.card();
    return card?.status === 'Rejected' ? latestDecision(card.history, 'Reject') : null;
  });
  /** The manager's and HR's feedback, once the review is over. */
  protected readonly feedback = computed(() => {
    const status = this.card()?.status;
    return status === 'Finalized' || status === 'Rejected';
  });

  /**
   * Rebuilt only when the saved values change on the server, so an evidence upload (which moves
   * the row version only) keeps what the employee is typing.
   */
  private readonly savedKey = computed(() => savedValues(this.card()));
  protected readonly form = computed<ResultsForm>(() => {
    this.savedKey();
    const card = untracked(() => this.card());
    return card
      ? buildResultsForm(card)
      : {
          group: new FormGroup({}),
          items: new Map(),
          comment: new FormControl('', { nonNullable: true }),
        };
  });

  protected readonly inputFor = actualInputFor;
  protected readonly errorOf = controlError;

  protected controls(item: ScorecardItem): ResultControls | undefined {
    return this.form().items.get(item.id);
  }

  protected minOf(input: ActualInput): number | null {
    return input.kind === 'boolean' ? null : input.min;
  }

  protected maxOf(input: ActualInput): number | null {
    return input.kind === 'rating' ? input.max : null;
  }

  protected hint(item: ScorecardItem, input: ActualInput): string {
    if (input.kind === 'rating') return `A rating from 0 to ${input.max}`;
    const unit = item.unit ? ` in ${item.unit}` : '';
    return input.kind === 'decimal' && input.min === 0 ? `0 or more${unit}` : `Your result${unit}`;
  }

  /** A labelled, unofficial achievement while typing (the mirror of the server's calculator). */
  protected preview(item: ScorecardItem, value: unknown): string | null {
    const actual = numberOrNull(value);
    if (actual === null) return null;
    const result = kpiAchievement(item, actual);
    return result.ok ? `${result.value.toFixed(2)}%` : null;
  }

  protected reload(): void {
    this.conflict.set('');
    this.formError.set('');
    this.detail.reload();
  }

  /** Runs one write; the actions stay disabled until it answers. */
  private write<T>(call: Observable<T>): Observable<T> {
    this.writing.set(true);
    return call.pipe(finalize(() => this.writing.set(false)));
  }

  private clearErrors(): void {
    this.conflict.set('');
    this.formError.set('');
  }

  /** The answer is the whole scorecard: its values, scores and new row version. */
  private accept(saved: ScorecardDetail): void {
    this.detail.set(saved);
    this.form().group.markAsPristine();
  }

  /**
   * A conflict shows the server's message with Reload; a validation error goes onto the KPI it
   * names (`items[i]` is `card.items[i]`) and lists the KPIs above; anything else is a toast.
   */
  private fail(error: unknown, card: ScorecardDetail): void {
    if (isAppError(error) && error.code === 'Conflict') {
      this.conflict.set(error.message);
      return;
    }
    if (isAppError(error) && error.code === 'Validation') {
      const form = this.form();
      const mapped = rowErrors(
        error.fieldErrors,
        card.items.map((i) => i.id),
      );
      const codes: string[] = [];
      for (const [id, fields] of mapped.items) {
        const pair = form.items.get(id);
        if (fields['actualValue']) setServerError(pair?.actual, fields['actualValue']);
        if (fields['employeeComment']) setServerError(pair?.comment, fields['employeeComment']);
        const code = card.items.find((i) => i.id === id)?.kpiCode;
        if (code) codes.push(code);
      }
      if (mapped.other['employeeComment']) {
        setServerError(form.comment, mapped.other['employeeComment']);
      }
      this.formError.set(
        codes.length ? `${error.message} Check: ${codes.join(', ')}.` : error.message,
      );
      return;
    }
    this.toast.error(error);
  }

  /** Stops before calling the server when a field is plainly wrong. */
  private formIsValid(): boolean {
    const group = this.form().group;
    if (group.valid) return true;
    group.markAllAsTouched();
    this.formError.set('Please correct the highlighted fields.');
    return false;
  }

  protected save(card: ScorecardDetail): void {
    this.clearErrors();
    if (!this.formIsValid()) return;
    this.write(this.scorecards.saveResults(card.id, resultsRequest(card, this.form()))).subscribe({
      next: (saved) => {
        this.accept(saved);
        this.toast.success('Draft saved. Nothing is submitted yet.');
      },
      error: (e: unknown) => this.fail(e, card),
    });
  }

  protected submit(card: ScorecardDetail): void {
    this.clearErrors();
    if (!this.formIsValid()) return;
    const reviewer = card.reviewerName ?? 'HR';
    this.confirm
      .confirm({
        title: 'Submit your results?',
        message: `Your results for ${card.periodName} go to ${reviewer} for review. You cannot change results after submitting, unless the scorecard is returned to you.${this.form().group.dirty ? ' Your unsaved changes are saved first.' : ''}`,
        confirmText: 'Submit',
      })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          // Unsaved changes are saved first; the submit then uses that answer's row version.
          const latest = this.card() ?? card;
          const chain = this.form().group.dirty
            ? this.scorecards.saveResults(latest.id, resultsRequest(latest, this.form())).pipe(
                tap((saved) => this.accept(saved)),
                switchMap((saved) => this.scorecards.submit(saved.id, saved.rowVersion)),
              )
            : this.scorecards.submit(latest.id, latest.rowVersion);
          return this.write(chain);
        }),
      )
      .subscribe({
        next: (submitted) => {
          this.accept(submitted);
          const score =
            submitted.overallScore === null
              ? ''
              : ` Overall score ${submitted.overallScore.toFixed(2)}${submitted.ratingLabel ? ` (${submitted.ratingLabel})` : ''}.`;
          this.toast.success(`Scorecard ${submitted.status.toLowerCase()}.${score}`);
        },
        error: (e: unknown) => this.fail(e, card),
      });
  }

  // ---- Evidence -------------------------------------------------------------------------------

  private setEvidenceError(itemId: number, message: string | null): void {
    this.evidenceErrors.update((all) => {
      const next = { ...all };
      if (message) next[itemId] = message;
      else delete next[itemId];
      return next;
    });
  }

  /** Applies an evidence answer: the new file list of the item and the scorecard's new row version. */
  private patch(
    rowVersion: string,
    itemId: number,
    change: (files: KpiAttachment[]) => KpiAttachment[],
  ) {
    this.detail.update((card) =>
      card
        ? {
            ...card,
            rowVersion,
            items: card.items.map((i) =>
              i.id === itemId ? { ...i, attachments: change(i.attachments) } : i,
            ),
          }
        : card,
    );
  }

  protected addEvidence(item: ScorecardItem, file: File): void {
    this.clearErrors();
    this.setEvidenceError(item.id, null);
    this.write(
      from(evidenceProblem(file, item.attachments.length)).pipe(
        switchMap((problem) =>
          problem
            ? throwError((): AppError => ({
                code: 'Validation',
                message: problem,
                fieldErrors: { file: problem },
              }))
            : // Read the row version only now: it is the latest answer's.
              defer(() =>
                this.scorecards.uploadEvidence(item.id, this.card()?.rowVersion ?? '', file),
              ),
        ),
      ),
    ).subscribe({
      next: (result) => {
        this.patch(result.scorecardRowVersion, item.id, (files) => [...files, result.attachment]);
        this.toast.success(`${result.attachment.fileName} added to ${item.kpiCode}.`);
      },
      error: (e: unknown) => {
        if (isAppError(e) && e.code === 'Validation') {
          this.setEvidenceError(item.id, e.fieldErrors?.['file'] ?? e.message);
        } else {
          const card = this.card();
          if (card) this.fail(e, card);
        }
      },
    });
  }

  protected removeEvidence(item: ScorecardItem, file: KpiAttachment): void {
    this.confirm
      .confirm({
        title: 'Remove this file?',
        message: `${file.fileName} will be removed from the evidence of ${item.kpiCode}.`,
        confirmText: 'Remove',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.clearErrors();
          this.setEvidenceError(item.id, null);
          return this.write(
            defer(() => this.scorecards.deleteEvidence(file.id, this.card()?.rowVersion ?? '')),
          );
        }),
      )
      .subscribe({
        next: (result) => {
          this.patch(result.scorecardRowVersion, item.id, (files) =>
            files.filter((f) => f.id !== file.id),
          );
          this.toast.success(`${file.fileName} removed.`);
        },
        error: (e: unknown) => {
          const card = this.card();
          if (card) this.fail(e, card);
        },
      });
  }

  /** Saves the file through the browser (never opened inline). */
  protected download(file: KpiAttachment): void {
    this.scorecards.downloadEvidence(file.id).subscribe({
      next: (result) => downloadFile(result),
      error: (e: unknown) => this.toast.error(e),
    });
  }
}

/** Puts a server message on a control, shown once touched; the next edit clears it. */
function setServerError(control: AbstractControl | undefined, message: string): void {
  if (!control) return;
  control.setErrors({ ...(control.errors ?? {}), server: message });
  control.markAsTouched();
}
