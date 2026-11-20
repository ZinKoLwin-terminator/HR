import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { ExcelModuleCatalogDataService } from '../../../core/services/data/excel-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { safeValue } from '../../utilities/resource-value';
import { ExcelExportService } from './excel-export.service';

/**
 * Export and Import buttons of a list screen for one Excel module. A button shows only when the
 * module catalog allows it for the caller (`canExport` / `canImport`) and the role holds the
 * `excel:Export` / `excel:Import` key. Both are UX only: the server enforces its own policies.
 */
@Component({
  selector: 'app-excel-actions',
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    @if (canExport()) {
      <button mat-stroked-button type="button" (click)="openExport()">
        <mat-icon>download</mat-icon>Export
      </button>
    }
    @if (canImport()) {
      <a
        mat-stroked-button
        routerLink="/data-exchange/import"
        [queryParams]="{ module: moduleCode() }"
      >
        <mat-icon>upload_file</mat-icon>Import
      </a>
    }
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
})
export class ExcelActionsComponent {
  readonly moduleCode = input.required<string>();
  /** Set to `false` for screens that only export. */
  readonly allowImport = input(true);
  readonly search = input<string | undefined>(undefined);
  readonly filters = input<object>({});
  /** Short texts describing the applied filters, shown in the export dialog. */
  readonly filterSummary = input<string[]>([]);

  private readonly catalogApi = inject(ExcelModuleCatalogDataService);
  private readonly permissions = inject(PermissionService);
  private readonly exporter = inject(ExcelExportService);

  private readonly mayExport = computed(() => this.permissions.can('excel:Export'));
  private readonly mayImport = computed(
    () => this.allowImport() && this.permissions.can('excel:Import'),
  );

  /**
   * The module catalog (`/api/excel/modules`, excel:View). Requested only when the role holds
   * excel:View and could use a button, so roles without Excel access never get a 403 for it; a
   * failure just hides both.
   */
  private readonly catalog = rxResource({
    params: () =>
      this.permissions.can('excel:View') && (this.mayExport() || this.mayImport())
        ? true
        : undefined,
    stream: () => this.catalogApi.getModules(),
  });
  private readonly info = computed(() =>
    safeValue(this.catalog)?.find((m) => m.code === this.moduleCode()),
  );

  protected readonly canExport = computed(() => this.mayExport() && !!this.info()?.canExport);
  protected readonly canImport = computed(() => this.mayImport() && !!this.info()?.canImport);

  protected openExport(): void {
    this.exporter
      .open({
        moduleCode: this.moduleCode(),
        search: this.search() || undefined,
        filters: { ...this.filters() },
        filterSummary: this.filterSummary(),
      })
      .subscribe();
  }
}
