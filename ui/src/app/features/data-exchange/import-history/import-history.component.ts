import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Entity } from '../../../core/models/common.models';
import {
  IMPORT_MODE_LABELS,
  IMPORT_STRATEGY_LABELS,
  ImportBatchSummary,
} from '../../../core/models/excel.models';
import { ExcelImportDataService } from '../../../core/services/data/excel-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  TableAction,
  TableActionEvent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { BATCH_STATUS_LABELS } from '../../../shared/components/excel/excel-labels';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { BatchDetailsDialogComponent } from './batch-details-dialog.component';

type Row = ImportBatchSummary & Entity;

/** Audit trail of Excel imports: who imported what, when, and the outcome. */
@Component({
  selector: 'app-import-history',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
    PageHeaderComponent,
    DataTableComponent,
    EmptyStateComponent,
  ],
  template: `
    <app-page-header
      title="Import history"
      description="Every Excel import, its outcome and the problems found. Sample data only: history is cleared when the page is reloaded."
    >
      @if (permissions.can('excel:Import')) {
        <a pageActions mat-flat-button routerLink="/data-exchange/import">
          <mat-icon>upload_file</mat-icon>New import
        </a>
      }
    </app-page-header>

    @if (history.error()) {
      <app-empty-state icon="error" title="Import history could not be loaded">
        <button mat-flat-button type="button" (click)="history.reload()">Try again</button>
      </app-empty-state>
    } @else if (history.isLoading() && !history.value()) {
      <mat-progress-bar mode="indeterminate" aria-label="Loading import history" />
    } @else {
      <app-data-table
        caption="Import history"
        [columns]="columns"
        [rows]="rows()"
        [total]="rows().length"
        [pageSize]="25"
        [pageSizeOptions]="[10, 25, 50]"
        [actions]="actions"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyTitle="No imports yet"
        emptyMessage="Imports you run will be listed here."
        emptyIcon="upload_file"
        (action)="onAction($event)"
        (rowClick)="view($event)"
      />
    }
  `,
})
export class ImportHistoryComponent {
  private readonly api = inject(ExcelImportDataService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(UiNotificationService);
  protected readonly permissions = inject(PermissionService);

  protected readonly history = rxResource({ stream: () => this.api.getHistory() });
  protected readonly rows = computed<Row[]>(() =>
    (this.history.value() ?? []).map((batch, index) => ({ ...batch, id: index + 1 })),
  );

  protected readonly columns: TableColumn<Row>[] = [
    {
      key: 'uploadedAt',
      header: 'Uploaded',
      nowrap: true,
      value: (r) =>
        new Date(r.uploadedAt).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' }),
    },
    { key: 'moduleLabel', header: 'Module' },
    { key: 'fileName', header: 'File', hideBelow: 'md' },
    {
      key: 'strategy',
      header: 'Strategy',
      hideBelow: 'lg',
      value: (r) => IMPORT_STRATEGY_LABELS[r.strategy],
    },
    {
      key: 'mode',
      header: 'Mode',
      hideBelow: 'lg',
      value: (r) => (r.mode ? IMPORT_MODE_LABELS[r.mode] : '—'),
    },
    {
      key: 'status',
      header: 'Status',
      type: 'status',
      value: (r) => BATCH_STATUS_LABELS[r.status],
    },
    { key: 'totalRows', header: 'Rows', type: 'number', align: 'end', hideBelow: 'sm' },
    { key: 'successRows', header: 'Imported', type: 'number', align: 'end', hideBelow: 'sm' },
    {
      key: 'notImported',
      header: 'Not imported',
      type: 'number',
      align: 'end',
      hideBelow: 'md',
      value: (r) => (r.status === 'Validated' ? null : r.skippedRows + r.failedRows),
    },
    { key: 'uploadedBy', header: 'By', hideBelow: 'lg' },
  ];

  protected readonly actions: TableAction<Row>[] = [
    { id: 'view', label: 'View details', icon: 'visibility' },
    {
      id: 'report',
      label: 'Download report',
      icon: 'download',
      visible: (r) => r.invalidRows > 0 || r.failedRows > 0 || r.skippedRows > 0,
    },
  ];
  protected readonly rowLabel = (r: Row) => `${r.moduleLabel} import ${r.fileName}`;

  protected onAction(event: TableActionEvent<Row>): void {
    if (event.action === 'view') this.view(event.row);
    else if (event.action === 'report') {
      this.api.getErrorReport(event.row.batchId).subscribe({
        next: (file) => downloadFile(file),
        error: (error: unknown) => this.toast.error(error),
      });
    }
  }

  protected view(row: Row): void {
    this.dialog.open(BatchDetailsDialogComponent, {
      data: { batchId: row.batchId },
      width: '760px',
      maxWidth: '96vw',
    });
  }
}
