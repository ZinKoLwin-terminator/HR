import { Component, computed, input, linkedSignal, output } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ColumnMapping, ExcelColumn, FileInspection } from '../../../core/models/excel.models';

/**
 * Lets the user match the headers of an uploaded file to the system fields. When the file already
 * follows the template, the headers are matched automatically and this is only a summary.
 */
@Component({
  selector: 'app-excel-column-mapping',
  imports: [MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    @if (problems().length) {
      <div class="notice notice--warn" role="alert">
        <mat-icon aria-hidden="true">warning</mat-icon>
        <div>
          <strong>Some columns need your attention</strong>
          <ul>
            @for (problem of problems(); track problem) {
              <li>{{ problem }}</li>
            }
          </ul>
        </div>
      </div>
    } @else {
      <div class="notice notice--ok">
        <mat-icon aria-hidden="true">check_circle</mat-icon>
        <span>All required columns are mapped.</span>
      </div>
    }

    <table class="map" aria-label="Column mapping">
      <thead>
        <tr>
          <th scope="col">Column in your file</th>
          <th scope="col">Maps to</th>
        </tr>
      </thead>
      <tbody>
        @for (header of inspection().headers; track header) {
          <tr>
            <th scope="row">{{ header }}</th>
            <td>
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  [value]="mapping()[header] ?? null"
                  [attr.aria-label]="'System field for column ' + header"
                  (selectionChange)="choose(header, $event.value)"
                >
                  <mat-option [value]="null">Ignore this column</mat-option>
                  @for (column of columns(); track column.key) {
                    <mat-option [value]="column.key">
                      {{ column.header }}{{ column.required ? ' *' : '' }}
                    </mat-option>
                  }
                </mat-select>
              </mat-form-field>
            </td>
          </tr>
        }
      </tbody>
    </table>
    <p class="legend">* required field. Ignored columns are not imported.</p>
  `,
  styles: `
    :host {
      display: block;
    }
    .notice {
      display: flex;
      gap: 10px;
      padding: 10px 14px;
      margin-bottom: 14px;
      border-radius: var(--app-radius-md);
      font: var(--mat-sys-body-medium);
      align-items: flex-start;
    }
    .notice ul {
      margin: 4px 0 0;
      padding-left: 18px;
    }
    .notice--warn {
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .notice--ok {
      background: var(--app-success-container);
      color: var(--app-on-success-container);
      align-items: center;
    }
    .map {
      width: 100%;
      border-collapse: collapse;
    }
    .map th,
    .map td {
      padding: 4px 8px;
      text-align: start;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .map thead th {
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    .map tbody th {
      font-weight: 500;
      overflow-wrap: anywhere;
      width: 42%;
    }
    .legend {
      margin: 8px 0 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class ExcelColumnMappingComponent {
  readonly inspection = input.required<FileInspection>();
  readonly columns = input.required<readonly ExcelColumn[]>();
  readonly mappingChange = output<ColumnMapping>();

  protected readonly mapping = linkedSignal<ColumnMapping>(() => ({
    ...this.inspection().mapping,
  }));

  /** Things to fix before the file can be validated. */
  protected readonly problems = computed(() => {
    const columns = this.columns();
    const mapping = this.mapping();
    const problems: string[] = [];
    const used = new Map<string, string>();
    for (const [header, key] of Object.entries(mapping)) {
      if (!key) continue;
      const label = columns.find((c) => c.key === key)?.header ?? key;
      if (used.has(key)) {
        problems.push(
          `"${used.get(key)}" and "${header}" are both mapped to ${label}. Ignore one of them.`,
        );
      } else {
        used.set(key, header);
      }
    }
    for (const column of columns.filter((c) => c.required && !used.has(c.key))) {
      problems.push(`The required field "${column.header}" is not mapped to any column.`);
    }
    return problems;
  });

  protected choose(header: string, key: string | null): void {
    this.mapping.update((current) => ({ ...current, [header]: key }));
    this.mappingChange.emit(this.mapping());
  }
}
