import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, contentChildren, input, model, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Entity, SortState } from '../../../core/models/common.models';
import { MoneyPipe } from '../../pipes/money.pipe';
import { EmptyStateComponent } from '../empty-state/empty-state.component';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';
import { CellTemplateDirective } from './cell-template.directive';
import { TableAction, TableActionEvent, TableColumn } from './data-table.models';
import { AppDatePipe } from '../../pipes/app-date.pipe';

/**
 * Presentational table for paged data. Paging/sorting are "remote" — the
 * parent owns the query and passes the current page — so it works the same
 * with mock services today and server-side paging later.
 */
@Component({
  selector: 'app-data-table',
  imports: [
    AppDatePipe,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatMenuModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatTooltipModule,
    NgTemplateOutlet,
    DecimalPipe,
    MoneyPipe,
    StatusBadgeComponent,
    EmptyStateComponent,
  ],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent<T extends Entity> {
  readonly columns = input.required<TableColumn<T>[]>();
  readonly rows = input<T[]>([]);
  readonly total = input(0);
  readonly pageIndex = input(0);
  readonly pageSize = input(10);
  readonly pageSizeOptions = input<readonly number[]>([5, 10, 25, 50]);
  readonly sort = input<SortState>({ active: '', direction: '' });
  readonly loading = input(false);
  readonly actions = input<TableAction<T>[]>([]);
  /** Accessible name for the table, e.g. "Employees". */
  readonly caption = input.required<string>();
  /** Accessor used in action-menu labels, e.g. row => row.fullName. */
  readonly rowLabel = input<(row: T) => string>((row) => `#${row.id}`);
  readonly emptyTitle = input('No records found');
  readonly emptyMessage = input('Try adjusting your search or filters.');
  readonly emptyIcon = input('search_off');
  readonly clickableRows = input(false);
  readonly showPaginator = input(true);
  /** Adds a checkbox column. Selection is kept across pages by row id. */
  readonly selectable = input(false);
  readonly selectedIds = model<ReadonlySet<number>>(new Set());

  readonly pageChange = output<PageEvent>();
  readonly sortChange = output<SortState>();
  readonly action = output<TableActionEvent<T>>();
  readonly rowClick = output<T>();

  private readonly cellTemplates = contentChildren(CellTemplateDirective);

  protected readonly templates = computed(
    () => new Map(this.cellTemplates().map((t) => [t.columnKey(), t.template])),
  );

  protected readonly displayedColumns = computed(() => [
    ...(this.selectable() ? ['__select'] : []),
    ...this.columns().map((c) => c.key),
    ...(this.actions().length ? ['__actions'] : []),
  ]);

  protected readonly allSelected = computed(
    () => this.rows().length > 0 && this.rows().every((r) => this.selectedIds().has(r.id)),
  );
  protected readonly someSelected = computed(
    () => !this.allSelected() && this.rows().some((r) => this.selectedIds().has(r.id)),
  );

  protected toggleRow(row: T): void {
    const next = new Set(this.selectedIds());
    if (next.has(row.id)) next.delete(row.id);
    else next.add(row.id);
    this.selectedIds.set(next);
  }

  /** Selects every row on the current page, or clears them when all are already selected. */
  protected toggleAll(): void {
    const next = new Set(this.selectedIds());
    const select = !this.allSelected();
    for (const row of this.rows()) {
      if (select) next.add(row.id);
      else next.delete(row.id);
    }
    this.selectedIds.set(next);
  }

  protected cellValue(column: TableColumn<T>, row: T): string | number | null | undefined {
    return column.value
      ? column.value(row)
      : (row as unknown as Record<string, string | number | null>)[column.key];
  }

  protected visibleActions(row: T): TableAction<T>[] {
    return this.actions().filter((a) => !a.visible || a.visible(row));
  }

  protected onSort(sort: Sort): void {
    this.sortChange.emit({ active: sort.active, direction: sort.direction });
  }

  protected onRowClick(row: T, event: Event): void {
    if (!this.clickableRows()) {
      return;
    }
    // Ignore clicks originating from interactive elements inside the row.
    if ((event.target as HTMLElement).closest('button, a, input, mat-checkbox')) {
      return;
    }
    this.rowClick.emit(row);
  }

  protected trackById = (_: number, row: T) => row.id;
}
