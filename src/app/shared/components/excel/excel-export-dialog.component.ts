import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { ExcelModuleInfo, ExportRequest, ExportScope } from '../../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelModuleCatalogDataService,
} from '../../../core/services/data/excel-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';

export interface ExcelExportDialogData {
  moduleCode: string;
  /** Search text and filters of the list the export was started from. */
  search?: string;
  filters?: Record<string, unknown>;
  /** Plain-text description of the active filters, e.g. "Department: Engineering". */
  filterSummary?: string[];
  selectedIds?: number[];
}

/** Choose what to export (current filters, everything or the selection), the columns, then download. */
@Component({
  selector: 'app-excel-export-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatRadioModule,
  ],
  template: `
    <h2 mat-dialog-title>Export {{ info()?.label ?? '' }} to Excel</h2>
    <mat-dialog-content>
      @if (info(); as module) {
        @if (error()) {
          <div class="banner" role="alert">{{ error() }}</div>
        }

        <fieldset class="group">
          <legend>What to export</legend>
          <mat-radio-group
            [ngModel]="scope()"
            (ngModelChange)="scope.set($event)"
            aria-label="What to export"
          >
            <mat-radio-button value="filtered">
              Records matching the current filters
              @if (data.filterSummary?.length || data.search) {
                <span class="muted"> — {{ filterText() }}</span>
              } @else {
                <span class="muted"> — no filters applied (all records)</span>
              }
            </mat-radio-button>
            <mat-radio-button value="all">All records</mat-radio-button>
            @if (data.selectedIds?.length) {
              <mat-radio-button value="selected">
                Selected records ({{ selectedCount }})
              </mat-radio-button>
            }
          </mat-radio-group>
        </fieldset>

        @if (module.exportDateLabel) {
          <fieldset class="group">
            <legend>Date range ({{ module.exportDateLabel }})</legend>
            <div class="dates">
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>From</mat-label>
                <input
                  matInput
                  type="date"
                  [ngModel]="dateFrom()"
                  (ngModelChange)="dateFrom.set($event)"
                  [max]="dateTo()"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>To</mat-label>
                <input
                  matInput
                  type="date"
                  [ngModel]="dateTo()"
                  (ngModelChange)="dateTo.set($event)"
                  [min]="dateFrom()"
                />
              </mat-form-field>
            </div>
          </fieldset>
        }

        <fieldset class="group">
          <legend>Columns</legend>
          <mat-checkbox
            [checked]="allChosen()"
            [indeterminate]="someChosen()"
            (change)="toggleAll($event.checked)"
          >
            Select all
          </mat-checkbox>
          <div class="columns">
            @for (column of plainColumns(); track column.key) {
              <mat-checkbox
                [checked]="chosen().has(column.key)"
                (change)="toggle(column.key, $event.checked)"
              >
                {{ column.header }}
              </mat-checkbox>
            }
          </div>
          @if (sensitiveColumns().length) {
            <mat-checkbox
              class="sensitive"
              [ngModel]="includeSensitive()"
              (ngModelChange)="includeSensitive.set($event)"
            >
              Include personal and financial columns
              <span class="muted">({{ sensitiveNames() }})</span>
            </mat-checkbox>
          }
        </fieldset>
      } @else if (catalog.isLoading()) {
        <p>Loading…</p>
      } @else {
        <p class="banner" role="alert">This module cannot be exported.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close [disabled]="exporting()">Cancel</button>
      <button mat-flat-button type="button" [disabled]="!canExport()" (click)="run()">
        @if (exporting()) {
          <mat-spinner diameter="18" aria-hidden="true" />
          <span>Preparing file…</span>
        } @else {
          Export to Excel
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .group {
      border: 0;
      padding: 0;
      margin: 0 0 16px;
    }
    .group legend {
      font: var(--mat-sys-title-small);
      margin-bottom: 6px;
      padding: 0;
    }
    mat-radio-group {
      display: flex;
      flex-direction: column;
    }
    .columns {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      margin: 4px 0 8px 12px;
    }
    .dates {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .banner {
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
      margin-bottom: 12px;
    }
    .sensitive {
      display: block;
      margin-top: 4px;
    }
    button mat-spinner {
      display: inline-block;
      margin-right: 8px;
    }
  `,
})
export class ExcelExportDialogComponent {
  protected readonly data = inject<ExcelExportDialogData>(MAT_DIALOG_DATA);
  protected readonly selectedCount = this.data.selectedIds?.length ?? 0;
  private readonly dialogRef = inject(MatDialogRef<ExcelExportDialogComponent, boolean>);
  private readonly exportApi = inject(ExcelExportDataService);
  private readonly catalogApi = inject(ExcelModuleCatalogDataService);
  private readonly toast = inject(UiNotificationService);

  protected readonly catalog = rxResource({ stream: () => this.catalogApi.getModules() });
  protected readonly info = computed<ExcelModuleInfo | undefined>(() =>
    this.catalog.value()?.find((m) => m.code === this.data.moduleCode && m.canExport),
  );

  protected readonly scope = signal<ExportScope>(
    this.data.selectedIds?.length
      ? 'selected'
      : this.data.filterSummary?.length || this.data.search
        ? 'filtered'
        : 'all',
  );
  protected readonly dateFrom = signal('');
  protected readonly dateTo = signal('');
  protected readonly includeSensitive = signal(false);
  protected readonly exporting = signal(false);
  protected readonly error = signal('');
  /** Columns the user unticked (everything is selected by default). */
  private readonly unchecked = signal<ReadonlySet<string>>(new Set());

  protected readonly plainColumns = computed(
    () => this.info()?.columns.filter((c) => !c.sensitive) ?? [],
  );
  protected readonly sensitiveColumns = computed(
    () => this.info()?.columns.filter((c) => c.sensitive) ?? [],
  );
  protected readonly sensitiveNames = computed(() =>
    this.sensitiveColumns()
      .map((c) => c.header)
      .join(', '),
  );
  protected readonly chosen = computed(
    () =>
      new Set(
        this.plainColumns()
          .filter((c) => !this.unchecked().has(c.key))
          .map((c) => c.key),
      ),
  );
  protected readonly allChosen = computed(() => this.chosen().size === this.plainColumns().length);
  protected readonly someChosen = computed(() => this.chosen().size > 0 && !this.allChosen());
  protected readonly canExport = computed(
    () =>
      !!this.info() &&
      !this.exporting() &&
      (this.chosen().size > 0 || (this.includeSensitive() && this.sensitiveColumns().length > 0)),
  );
  protected readonly filterText = computed(() =>
    [
      ...(this.data.filterSummary ?? []),
      ...(this.data.search ? [`Search: "${this.data.search}"`] : []),
    ].join('; '),
  );

  protected toggle(key: string, checked: boolean): void {
    this.unchecked.update((set) => {
      const next = new Set(set);
      if (checked) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  protected toggleAll(checked: boolean): void {
    this.unchecked.set(checked ? new Set() : new Set(this.plainColumns().map((c) => c.key)));
  }

  protected run(): void {
    const info = this.info();
    if (!info) return;
    const sensitive = this.includeSensitive();
    const request: ExportRequest = {
      moduleCode: info.code,
      scope: this.scope(),
      search: this.data.search,
      filters: this.data.filters,
      selectedIds: this.data.selectedIds,
      dateFrom: this.dateFrom() || null,
      dateTo: this.dateTo() || null,
      columnKeys: [
        ...this.chosen(),
        ...(sensitive ? this.sensitiveColumns().map((c) => c.key) : []),
      ],
      includeSensitive: sensitive,
    };
    this.error.set('');
    this.exporting.set(true);
    this.exportApi.export(request).subscribe({
      next: (file) => {
        downloadFile(file);
        this.toast.success(`${file.fileName} is ready.`);
        this.dialogRef.close(true);
      },
      error: (error: unknown) => {
        this.exporting.set(false);
        this.error.set(errorMessage(error));
      },
    });
  }
}
