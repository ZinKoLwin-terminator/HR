import {
  Component,
  OnInit,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  output,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NgTemplateOutlet } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import {
  CreateModel,
  Entity,
  PagedResult,
  SortState,
  UpdateModel,
} from '../../../core/models/common.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { ListQuery } from '../../utilities/list-query';
import { ExcelExportService } from '../excel/excel-export.service';
import { DataTableComponent } from '../data-table/data-table.component';
import { TableAction, TableActionEvent } from '../data-table/data-table.models';
import { EmptyStateComponent } from '../empty-state/empty-state.component';
import { FilterFieldComponent, FilterPopupComponent } from '../filter-popup/filter-popup.component';
import { PageHeaderComponent } from '../page-header/page-header.component';
import { SearchFieldComponent } from '../search-field/search-field.component';
import {
  CrudPageConfig,
  EntityDetailsDialogData,
  EntityFormDialogData,
  FormValues,
} from './crud.models';
import { EntityDetailsDialogComponent } from './entity-details-dialog.component';
import { EntityFormDialogComponent } from './entity-form-dialog.component';

type Row = Entity & { status?: string };

/**
 * Generic list + create/edit/view/delete/activate screen driven by a
 * {@link CrudPageConfig}. Depends only on the data-service contract, so it
 * works unchanged with mock or (future) HTTP services.
 */
@Component({
  selector: 'app-crud-page',
  imports: [
    NgTemplateOutlet,
    FilterFieldComponent,
    FilterPopupComponent,
    PageHeaderComponent,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    RouterLink,
    SearchFieldComponent,
    DataTableComponent,
    EmptyStateComponent,
  ],
  templateUrl: './crud-page.component.html',
  styleUrl: './crud-page.component.scss',
})
export class CrudPageComponent<T extends Entity> implements OnInit {
  readonly config = input.required<CrudPageConfig<T>>();
  /** Emitted after any successful create / update / delete (lets the host refresh lookups). */
  readonly changed = output<void>();

  private readonly appConfig = inject(APP_CONFIG);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  private readonly excelExport = inject(ExcelExportService);

  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly query = new ListQuery<Record<string, unknown>>({
    pageSize: this.appConfig.defaultPageSize,
  });

  protected readonly page = rxResource({
    params: () => this.query.request(),
    stream: ({ params }) => {
      const cfg = this.config();
      return cfg.service.query({ ...params, filters: { ...params.filters, ...cfg.lockedFilters } });
    },
  });

  /** Holds the last loaded page so the table does not blank while refetching. */
  protected readonly result = linkedSignal<PagedResult<T> | undefined, PagedResult<T> | undefined>({
    source: this.page.value,
    computation: (value, previous) => value ?? previous?.value,
  });

  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected readonly actions = computed<TableAction<T>[]>(() => {
    const cfg = this.config();
    const list: TableAction<T>[] = [
      { id: 'view', label: 'View details', icon: 'visibility' },
      ...(cfg.extraActions ?? []).map((a) => ({
        id: `extra:${a.id}`,
        label: a.label,
        icon: a.icon,
        destructive: a.destructive,
        visible: a.visible,
      })),
      { id: 'edit', label: 'Edit', icon: 'edit', visible: cfg.canEdit },
    ];
    if (this.config().hasStatus !== false) {
      list.push(
        {
          id: 'activate',
          label: 'Activate',
          icon: 'toggle_on',
          visible: (r) => this.statusOf(r) === 'Inactive',
        },
        {
          id: 'deactivate',
          label: 'Deactivate',
          icon: 'toggle_off',
          visible: (r) => this.statusOf(r) === 'Active',
        },
      );
    }
    list.push({
      id: 'delete',
      label: 'Delete',
      icon: 'delete',
      destructive: true,
      visible: cfg.canDelete,
    });
    return list;
  });

  protected readonly rowLabel = (row: T) => this.config().nameOf(row);

  constructor() {
    // Deleting the last item of the last page makes the server clamp the index; follow it.
    effect(() => {
      const value = this.page.value();
      if (value && value.pageIndex !== this.query.pageIndex()) {
        this.query.pageIndex.set(value.pageIndex);
      }
    });
  }

  ngOnInit(): void {
    const sort = this.config().defaultSort;
    if (sort) {
      this.query.setSort(sort);
    }
  }

  protected onSort(sort: SortState): void {
    this.query.setSort(sort);
  }

  protected setFilter(key: string, value: string | number | null): void {
    this.query.setFilter(key, value);
  }

  protected onAction(event: TableActionEvent<T>): void {
    const { action, row } = event;
    switch (action) {
      case 'view':
        this.openDetails(row);
        break;
      case 'edit':
        this.openForm(row);
        break;
      case 'activate':
        this.setStatus(row, 'Active');
        break;
      case 'deactivate':
        this.setStatus(row, 'Inactive');
        break;
      case 'delete':
        this.delete(row);
        break;
      default:
        if (action.startsWith('extra:')) {
          this.runExtra(action.slice('extra:'.length), row);
        }
    }
  }

  /** Runs a configured custom action (optionally after a confirmation). */
  private runExtra(id: string, row: T): void {
    const action = this.config().extraActions?.find((a) => a.id === id);
    if (!action) {
      return;
    }
    const execute = (input = '') =>
      action.run(row, input).subscribe({
        next: () => {
          if (!action.silent) {
            this.toast.success(action.successMessage(row));
            this.refresh();
          }
        },
        error: (error: unknown) => this.toast.error(error),
      });
    if (action.prompt) {
      this.confirm
        .prompt({
          title: action.prompt.title,
          message: action.prompt.message?.(row),
          label: action.prompt.label,
          required: action.prompt.required,
          confirmText: action.prompt.confirmText,
          destructive: action.destructive,
        })
        .pipe(filter((text): text is string => text !== null))
        .subscribe((text) => execute(text));
      return;
    }
    if (!action.confirm) {
      execute();
      return;
    }
    this.confirm
      .confirm({
        title: action.confirm.title,
        message: action.confirm.message(row),
        confirmText: action.confirm.confirmText,
        destructive: action.destructive,
      })
      .pipe(filter(Boolean))
      .subscribe(() => execute());
  }

  /** Current value of a date filter input ('' when unset). */
  protected dateValue(key: string): string {
    return String(this.query.filters()[key] ?? '');
  }

  protected setDateFilter(key: string, value: string): void {
    this.query.setFilter(key, value || null);
  }

  /** Opens the Excel export dialog with the list's current search and filters. */
  protected openExport(): void {
    const cfg = this.config();
    if (!cfg.excel) return;
    const active = this.query.filters();
    const summary = (cfg.filters ?? []).flatMap((f) => {
      const value = active[f.key];
      if (value === null || value === undefined || value === '') return [];
      const label = f.options.find((o) => o.value === value)?.label ?? String(value);
      return [`: ${label}`];
    });
    const df = cfg.dateFilter;
    if (df && this.dateValue(df.fromKey)) summary.push(`: ${this.dateValue(df.fromKey)}`);
    if (df && this.dateValue(df.toKey)) summary.push(`: ${this.dateValue(df.toKey)}`);
    this.excelExport
      .open({
        moduleCode: cfg.excel.moduleCode,
        search: this.query.search() || undefined,
        filters: { ...active, ...cfg.lockedFilters },
        filterSummary: summary,
      })
      .subscribe();
  }

  protected openForm(row?: T): void {
    const cfg = this.config();
    const isEdit = !!row;
    const data: EntityFormDialogData<T> = {
      title: isEdit
        ? `Edit ${cfg.entityLabel.toLowerCase()}`
        : `Add ${cfg.entityLabel.toLowerCase()}`,
      fields: cfg.fields.filter((f) => !f.detailsOnly),
      groupValidators: cfg.groupValidators,
      value: row ? (row as unknown as FormValues) : cfg.newItem(),
      id: row?.id ?? null,
      submitLabel: isEdit ? 'Save changes' : `Create ${cfg.entityLabel.toLowerCase()}`,
      save: (formValues) => {
        const values = { ...formValues, ...cfg.fixedValues };
        return row
          ? cfg.service.update(row.id, values as UpdateModel<T>)
          : cfg.service.create(values as unknown as CreateModel<T>);
      },
    };
    this.dialog
      .open<EntityFormDialogComponent, EntityFormDialogData<T>, T>(EntityFormDialogComponent, {
        data,
        width: '760px',
        maxWidth: '96vw',
        disableClose: true,
        autoFocus: 'first-tabbable',
      })
      .afterClosed()
      .pipe(filter((saved): saved is T => !!saved))
      .subscribe((saved) => {
        this.toast.success(
          `${cfg.entityLabel} "${cfg.nameOf(saved)}" ${isEdit ? 'updated' : 'created'} successfully.`,
        );
        this.refresh();
      });
  }

  protected openDetails(row: T): void {
    const cfg = this.config();
    this.dialog
      .open<EntityDetailsDialogComponent, EntityDetailsDialogData, string>(
        EntityDetailsDialogComponent,
        {
          data: {
            title: cfg.nameOf(row),
            subtitle: `${cfg.entityLabel} details`,
            fields: cfg.fields,
            row: row as unknown as FormValues,
            id: row.id,
          },
          width: '640px',
          maxWidth: '96vw',
        },
      )
      .afterClosed()
      .pipe(filter((result) => result === 'edit'))
      .subscribe(() => this.openForm(row));
  }

  private setStatus(row: T, status: 'Active' | 'Inactive'): void {
    const cfg = this.config();
    cfg.service.update(row.id, { status } as unknown as UpdateModel<T>).subscribe({
      next: () => {
        this.toast.success(
          `${cfg.entityLabel} "${cfg.nameOf(row)}" ${status === 'Active' ? 'activated' : 'deactivated'}.`,
        );
        this.refresh();
      },
      error: (error: unknown) => this.toast.error(error),
    });
  }

  private delete(row: T): void {
    const cfg = this.config();
    this.confirm
      .confirmDelete(cfg.entityLabel, cfg.nameOf(row))
      .pipe(filter(Boolean))
      .subscribe(() =>
        cfg.service.delete(row.id).subscribe({
          next: () => {
            this.toast.success(`${cfg.entityLabel} "${cfg.nameOf(row)}" deleted.`);
            this.refresh();
          },
          error: (error: unknown) => this.toast.error(error),
        }),
      );
  }

  protected refresh(): void {
    this.page.reload();
    this.changed.emit();
  }

  private statusOf(row: T): string | undefined {
    return (row as Row).status;
  }
}
