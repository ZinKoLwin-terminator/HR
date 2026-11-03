import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { IMPORT_MODE_LABELS, IMPORT_STRATEGY_LABELS } from '../../../core/models/excel.models';
import { ExcelImportDataService } from '../../../core/services/data/excel-data.services';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { BATCH_STATUS_LABELS } from '../../../shared/components/excel/excel-labels';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

/** Details of one import: summary figures and every recorded problem. */
@Component({
  selector: 'app-batch-details-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    StatusBadgeComponent,
  ],
  template: `
    <h2 mat-dialog-title>Import details</h2>
    <mat-dialog-content>
      @if (batch.value(); as b) {
        <p class="file">{{ b.fileName }}</p>
        <dl class="facts">
          <div>
            <dt>Module</dt>
            <dd>{{ b.moduleLabel }}</dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd><app-status-badge [status]="statusLabels[b.status]" /></dd>
          </div>
          <div>
            <dt>Strategy</dt>
            <dd>{{ strategyLabels[b.strategy] }}</dd>
          </div>
          <div>
            <dt>Mode</dt>
            <dd>{{ b.mode ? modeLabels[b.mode] : '—' }}</dd>
          </div>
          <div>
            <dt>Uploaded by</dt>
            <dd>{{ b.uploadedBy }}</dd>
          </div>
          <div>
            <dt>Uploaded</dt>
            <dd>{{ stamp(b.uploadedAt) }}</dd>
          </div>
          <div>
            <dt>Started</dt>
            <dd>{{ b.startedAt ? stamp(b.startedAt) : '—' }}</dd>
          </div>
          <div>
            <dt>Completed</dt>
            <dd>{{ b.completedAt ? stamp(b.completedAt) : '—' }}</dd>
          </div>
          <div>
            <dt>Total rows</dt>
            <dd>{{ b.totalRows }}</dd>
          </div>
          <div>
            <dt>Valid rows</dt>
            <dd>{{ b.validRows }}</dd>
          </div>
          <div>
            <dt>Imported</dt>
            <dd>{{ b.successRows }}</dd>
          </div>
          <div>
            <dt>Skipped / failed</dt>
            <dd>{{ b.skippedRows }} / {{ b.failedRows }}</dd>
          </div>
        </dl>
        @if (b.errorSummary) {
          <p class="summary" role="status">{{ b.errorSummary }}</p>
        }
        <h3>Recorded problems ({{ b.issues.length }})</h3>
        @if (b.issues.length) {
          <div class="scroll">
            <table class="simple-table" aria-label="Recorded problems">
              <thead>
                <tr>
                  <th scope="col">Row</th>
                  <th scope="col">Field</th>
                  <th scope="col">Value</th>
                  <th scope="col">Problem</th>
                </tr>
              </thead>
              <tbody>
                @for (issue of b.issues; track $index) {
                  <tr>
                    <td class="num">{{ issue.rowNumber }}</td>
                    <td>{{ issue.field }}</td>
                    <td>{{ issue.value || '—' }}</td>
                    <td>{{ issue.message }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <p class="muted">No problems were recorded for this import.</p>
        }
      } @else if (batch.error()) {
        <p role="alert">The import details could not be loaded.</p>
      } @else {
        <mat-progress-bar mode="indeterminate" aria-label="Loading import details" />
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if ((batch.value()?.issues?.length ?? 0) > 0) {
        <button mat-stroked-button type="button" (click)="downloadReport()">
          <mat-icon>download</mat-icon>Download report
        </button>
      }
      <button mat-flat-button type="button" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .file {
      margin: 0 0 12px;
      font: var(--mat-sys-title-medium);
      overflow-wrap: anywhere;
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 12px 20px;
      margin: 0 0 12px;
    }
    .facts dt {
      font: var(--mat-sys-label-small);
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    .facts dd {
      margin: 2px 0 0;
      font-weight: 500;
    }
    .summary {
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
    }
    .scroll {
      max-height: 280px;
      overflow: auto;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class BatchDetailsDialogComponent {
  protected readonly data = inject<{ batchId: string }>(MAT_DIALOG_DATA);
  private readonly api = inject(ExcelImportDataService);
  private readonly toast = inject(UiNotificationService);

  protected readonly batch = rxResource({ stream: () => this.api.getBatch(this.data.batchId) });
  protected readonly statusLabels = BATCH_STATUS_LABELS;
  protected readonly strategyLabels = IMPORT_STRATEGY_LABELS;
  protected readonly modeLabels = IMPORT_MODE_LABELS;

  protected stamp(iso: string): string {
    return new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' });
  }

  protected downloadReport(): void {
    this.api.getErrorReport(this.data.batchId).subscribe({
      next: (file) => downloadFile(file),
      error: (error: unknown) => this.toast.error(error),
    });
  }
}
