import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { ImportResult } from '../../../core/models/excel.models';

/** Outcome of an import: what was imported, skipped or rolled back, with follow-up actions. */
@Component({
  selector: 'app-excel-import-result',
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <section class="result" [attr.data-tone]="tone()" aria-live="polite">
      <mat-icon class="result__icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <div class="result__body">
        <h3 class="result__title">{{ title() }}</h3>
        <p class="result__message">{{ result().message }}</p>
        <dl class="counts">
          <div>
            <dt>Inserted</dt>
            <dd>{{ result().inserted }}</dd>
          </div>
          <div>
            <dt>Updated</dt>
            <dd>{{ result().updated }}</dd>
          </div>
          <div>
            <dt>Skipped</dt>
            <dd>{{ result().skipped }}</dd>
          </div>
          <div>
            <dt>Failed</dt>
            <dd>{{ result().failed }}</dd>
          </div>
        </dl>
        <div class="actions">
          @if (hasProblems()) {
            <button mat-stroked-button type="button" (click)="downloadReport.emit()">
              <mat-icon>download</mat-icon>Download report
            </button>
          }
          <a mat-stroked-button routerLink="/data-exchange/history">View import history</a>
          <button mat-flat-button type="button" (click)="startAgain.emit()">
            Start another import
          </button>
        </div>
      </div>
    </section>
  `,
  styles: `
    .result {
      display: flex;
      gap: 16px;
      padding: 20px;
      border-radius: var(--app-radius-lg);
      border: 1px solid var(--mat-sys-outline-variant);
    }
    .result[data-tone='success'] {
      background: var(--app-success-container);
      color: var(--app-on-success-container);
    }
    .result[data-tone='warning'] {
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .result[data-tone='danger'] {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .result__icon {
      width: 36px;
      height: 36px;
      font-size: 36px;
      flex: none;
    }
    .result__title {
      margin: 0;
      font: var(--mat-sys-title-large);
    }
    .result__message {
      margin: 4px 0 12px;
    }
    .counts {
      display: flex;
      gap: 28px;
      flex-wrap: wrap;
      margin: 0 0 16px;
    }
    .counts dt {
      font: var(--mat-sys-label-medium);
      opacity: 0.85;
    }
    .counts dd {
      margin: 0;
      font: var(--mat-sys-headline-small);
      font-variant-numeric: tabular-nums;
    }
    .actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
  `,
})
export class ExcelImportResultComponent {
  readonly result = input.required<ImportResult>();
  readonly downloadReport = output<void>();
  readonly startAgain = output<void>();

  protected readonly tone = computed(() =>
    this.result().status === 'Completed'
      ? 'success'
      : this.result().status === 'Failed'
        ? 'danger'
        : 'warning',
  );
  protected readonly icon = computed(() =>
    this.result().status === 'Completed'
      ? 'check_circle'
      : this.result().status === 'Failed'
        ? 'error'
        : 'warning',
  );
  protected readonly title = computed(() =>
    this.result().status === 'Completed'
      ? 'Import completed'
      : this.result().status === 'Failed'
        ? 'Import failed — nothing was imported'
        : 'Import completed with some rows not imported',
  );
  protected readonly hasProblems = computed(() => this.result().status !== 'Completed');
}
