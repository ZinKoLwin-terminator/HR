import { Component, computed, input, linkedSignal, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { PageEvent } from '@angular/material/paginator';
import {
  ExcelColumn,
  ImportPreview,
  ImportPreviewRow,
  ImportRowStatus,
} from '../../../core/models/excel.models';
import { DataTableComponent } from '../data-table/data-table.component';
import { TableColumn } from '../data-table/data-table.models';

type Filter = 'All' | ImportRowStatus;
interface PreviewTableRow extends ImportPreviewRow {
  /** Table identity (the worksheet row number). */
  id: number;
}

/** Row-by-row preview of a validated import with status filters and the validation messages. */
@Component({
  selector: 'app-excel-import-preview',
  imports: [MatButtonToggleModule, DataTableComponent],
  template: `
    <mat-button-toggle-group
      class="filters"
      aria-label="Filter rows by status"
      [value]="filter()"
      (change)="setFilter($event.value)"
    >
      @for (option of options(); track option.value) {
        <mat-button-toggle [value]="option.value">
          {{ option.label }} ({{ option.count }})
        </mat-button-toggle>
      }
    </mat-button-toggle-group>

    <app-data-table
      caption="Import preview"
      [columns]="tableColumns()"
      [rows]="pageRows()"
      [total]="filtered().length"
      [pageIndex]="pageIndex()"
      [pageSize]="pageSize()"
      [pageSizeOptions]="[10, 25, 50]"
      [rowLabel]="rowLabel"
      emptyTitle="No rows with this status"
      emptyMessage="Choose another status above."
      emptyIcon="filter_alt_off"
      (pageChange)="onPage($event)"
    />
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
  `,
})
export class ExcelImportPreviewComponent {
  readonly preview = input.required<ImportPreview>();
  readonly columns = input.required<readonly ExcelColumn[]>();
  /** Column keys shown besides the row number, status, action and messages. */
  readonly shownKeys = input<readonly string[]>([]);

  protected readonly filter = signal<Filter>('All');
  protected readonly pageIndex = linkedSignal(() => {
    this.filter();
    this.preview();
    return 0;
  });
  protected readonly pageSize = signal(10);
  protected readonly rowLabel = (row: PreviewTableRow) => `row ${row.rowNumber}`;

  protected readonly options = computed(() => {
    const rows = this.preview().rows;
    const count = (status: ImportRowStatus) => rows.filter((r) => r.status === status).length;
    return [
      { value: 'All' as Filter, label: 'All', count: rows.length },
      { value: 'Valid' as Filter, label: 'Valid', count: count('Valid') },
      { value: 'Warning' as Filter, label: 'Warning', count: count('Warning') },
      { value: 'Invalid' as Filter, label: 'Invalid', count: count('Invalid') },
      { value: 'Duplicate' as Filter, label: 'Duplicate', count: count('Duplicate') },
    ];
  });

  protected readonly filtered = computed<PreviewTableRow[]>(() => {
    const filter = this.filter();
    return this.preview()
      .rows.filter((r) => filter === 'All' || r.status === filter)
      .map((r) => ({ ...r, id: r.rowNumber }));
  });

  protected readonly pageRows = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  protected readonly tableColumns = computed<TableColumn<PreviewTableRow>[]>(() => {
    const byKey = new Map(this.columns().map((c) => [c.key, c]));
    const keys = this.shownKeys().length
      ? this.shownKeys()
      : this.columns()
          .filter((c) => c.required)
          .slice(0, 4)
          .map((c) => c.key);
    return [
      { key: 'rowNumber', header: 'Row', width: '70px', nowrap: true },
      { key: 'status', header: 'Status', type: 'status', nowrap: true },
      {
        key: 'action',
        header: 'Will',
        nowrap: true,
        hideBelow: 'sm',
        value: (r) => (r.action === 'None' ? 'Skip' : r.action),
      },
      ...keys.map<TableColumn<PreviewTableRow>>((key, i) => ({
        key: `v:${key}`,
        header: byKey.get(key)?.header ?? key,
        hideBelow: i < 2 ? undefined : 'lg',
        value: (r) => r.values[key] || '—',
      })),
      {
        key: 'messages',
        header: 'Validation messages',
        value: (r) =>
          r.issues.length ? r.issues.map((i) => `${i.field}: ${i.message}`).join(' · ') : '—',
      },
    ];
  });

  protected setFilter(value: Filter): void {
    this.filter.set(value);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }
}
