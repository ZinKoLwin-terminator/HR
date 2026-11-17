import { DecimalPipe } from '@angular/common';
import { Component, computed, input, model, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { KpiTemplate } from '../../../core/models/kpi.models';
import {
  AssignmentRow,
  assignmentTotal,
  exceedsTotal,
  rowError,
  rowFromTemplate,
} from './assignment-rows';

/** Row fields whose errors are shown under the KPI name (target and weight show under their input). */
const ROW_FIELDS: readonly { key: string; label: string }[] = [
  { key: 'templateId', label: 'KPI' },
  { key: 'startDate', label: 'Start date' },
  { key: 'endDate', label: 'End date' },
  { key: 'sortOrder', label: 'Sort order' },
];

let nextId = 0;

/**
 * The KPI rows of an assign dialog (single or bulk): add KPIs from a list of templates, change the
 * target and weight of each, and see the running total of the weights. Errors (local or from the
 * server) arrive keyed `items[i].field` or `items` and are shown on their row.
 */
@Component({
  selector: 'app-kpi-assignment-rows',
  imports: [DecimalPipe, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    <mat-form-field class="adder" subscriptSizing="dynamic">
      <mat-label>Add a KPI</mat-label>
      <mat-select
        [value]="null"
        [disabled]="disabled() || available().length === 0"
        (selectionChange)="add($event)"
      >
        @for (template of available(); track template.id) {
          <mat-option [value]="template.id">{{ template.code }} · {{ template.name }}</mat-option>
        }
      </mat-select>
      <mat-hint>
        @if (available().length === 0) {
          No more active KPI templates are available.
        } @else {
          {{ hint() }}
        }
      </mat-hint>
    </mat-form-field>

    @if (rows().length > 0) {
      <div class="scroll" role="region" aria-label="KPIs to assign" tabindex="0">
        <table class="rows">
          <caption class="cdk-visually-hidden">
            KPIs to assign, with target and weight
          </caption>
          <thead>
            <tr>
              <th scope="col">KPI</th>
              <th scope="col" class="num">Target</th>
              <th scope="col" class="num">Weight %</th>
              <th scope="col"><span class="cdk-visually-hidden">Remove</span></th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.templateId; let i = $index) {
              @let template = templateOf(row.templateId);
              @let code = template?.code ?? '#' + row.templateId;
              <tr [attr.data-kpi]="code">
                <td>
                  <strong>{{ code }}</strong>
                  <span class="name">{{ template?.name }}</span>
                  @if (template?.unit) {
                    <span class="name">Unit: {{ template?.unit }}</span>
                  }
                  @for (field of rowFields; track field.key) {
                    @if (error(i, field.key)) {
                      <span class="error" role="alert"
                        >{{ field.label }}: {{ error(i, field.key) }}</span
                      >
                    }
                  }
                </td>
                <td class="num">
                  <input
                    type="number"
                    class="cell"
                    min="0"
                    step="any"
                    [class.invalid]="!!error(i, 'target')"
                    [attr.aria-label]="'Target for ' + code"
                    [attr.aria-invalid]="!!error(i, 'target')"
                    [attr.aria-describedby]="error(i, 'target') ? errorId(i, 'target') : null"
                    [value]="row.target ?? ''"
                    [disabled]="disabled()"
                    (input)="setValue(i, 'target', $any($event.target).value)"
                  />
                  @if (error(i, 'target')) {
                    <span class="error" role="alert" [id]="errorId(i, 'target')">{{
                      error(i, 'target')
                    }}</span>
                  }
                </td>
                <td class="num">
                  <input
                    type="number"
                    class="cell"
                    min="0.01"
                    max="100"
                    step="any"
                    [class.invalid]="!!error(i, 'weight')"
                    [attr.aria-label]="'Weight for ' + code"
                    [attr.aria-invalid]="!!error(i, 'weight')"
                    [attr.aria-describedby]="error(i, 'weight') ? errorId(i, 'weight') : null"
                    [value]="row.weight ?? ''"
                    [disabled]="disabled()"
                    (input)="setValue(i, 'weight', $any($event.target).value)"
                  />
                  @if (error(i, 'weight')) {
                    <span class="error" role="alert" [id]="errorId(i, 'weight')">{{
                      error(i, 'weight')
                    }}</span>
                  }
                </td>
                <td class="remove">
                  <button
                    mat-icon-button
                    type="button"
                    [attr.aria-label]="'Remove ' + code"
                    [disabled]="disabled()"
                    (click)="remove(i)"
                  >
                    <mat-icon>close</mat-icon>
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <p class="total" [class.over]="over()" role="status" data-testid="weight-total">
      <strong>Total weight: {{ total() | number: '1.0-2' }}%</strong>
      @if (existingWeight() > 0) {
        <span>
          ({{ existingWeight() | number: '1.0-2' }}% already on the scorecard, plus
          {{ added() | number: '1.0-2' }}% new)
        </span>
      }
      @if (over()) {
        <span class="warn">The weights cannot add up to more than 100%.</span>
      } @else {
        <span>A plan must add up to exactly 100% before it is finalized.</span>
      }
    </p>
    @if (errors()['items'] && !over()) {
      <p class="error" role="alert">{{ errors()['items'] }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .adder {
      width: 100%;
    }
    .scroll {
      overflow-x: auto;
      margin: 12px 0 4px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
    }
    .scroll:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
    }
    .rows {
      width: 100%;
      border-collapse: collapse;
      font: var(--mat-sys-body-medium);
    }
    th,
    td {
      padding: 8px 8px;
      text-align: start;
      vertical-align: top;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    thead th {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
    }
    tbody tr:last-child td {
      border-bottom: 0;
    }
    .num {
      text-align: end;
      width: 96px;
    }
    .remove {
      width: 44px;
      padding: 2px;
    }
    .name {
      display: block;
      color: var(--mat-sys-on-surface-variant);
    }
    .cell {
      box-sizing: border-box;
      width: 84px;
      padding: 6px 8px;
      text-align: end;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 6px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      font: inherit;
    }
    .cell:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: 1px;
    }
    .cell.invalid {
      border-color: var(--mat-sys-error);
    }
    .error {
      display: block;
      margin-top: 2px;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
      text-align: start;
    }
    .total {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 12px;
      margin: 10px 0 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }
    .total.over strong,
    .total .warn {
      color: var(--mat-sys-error);
    }
  `,
})
export class KpiAssignmentRowsComponent {
  /** Templates that may be offered (already narrowed to active ones that fit the employee). */
  readonly templates = input.required<KpiTemplate[]>();
  /** Template ids that cannot be added again (already on the scorecard). */
  readonly excludedIds = input<readonly number[]>([]);
  /** Weight already on the scorecard (adding KPIs to a plan). */
  readonly existingWeight = input(0);
  /** Errors, keyed `items[i].field` or `items` (local checks and the server's). */
  readonly errors = input<Readonly<Record<string, string>>>({});
  readonly disabled = input(false);
  readonly hint = input(
    'Active templates; department-specific ones only for a matching department.',
  );
  readonly rows = model<AssignmentRow[]>([]);
  /** Emitted after the user adds, removes or edits a row (row indexes may have shifted). */
  readonly edited = output<void>();

  private readonly id = `kpi-rows-${nextId++}`;
  protected readonly rowFields = ROW_FIELDS;

  protected readonly available = computed(() => {
    const taken = new Set([...this.excludedIds(), ...this.rows().map((r) => r.templateId)]);
    return this.templates().filter((t) => !taken.has(t.id));
  });
  protected readonly added = computed(() => assignmentTotal(this.rows()));
  protected readonly total = computed(() => assignmentTotal(this.rows(), this.existingWeight()));
  protected readonly over = computed(() => exceedsTotal(this.total()));

  protected templateOf(id: number): KpiTemplate | undefined {
    return this.templates().find((t) => t.id === id);
  }

  protected error(index: number, field: string): string {
    return rowError(this.errors(), index, field);
  }

  protected errorId(index: number, field: string): string {
    return `${this.id}-${index}-${field}`;
  }

  protected add(event: MatSelectChange): void {
    const template = this.templates().find((t) => t.id === event.value);
    // The picker only adds: it goes back to empty for the next KPI.
    event.source.value = null;
    if (template) {
      this.rows.update((rows) => [...rows, rowFromTemplate(template)]);
      this.edited.emit();
    }
  }

  protected remove(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
    this.edited.emit();
  }

  protected setValue(index: number, field: 'target' | 'weight', text: string): void {
    const value = text.trim() === '' ? null : Number(text);
    this.rows.update((rows) =>
      rows.map((row, i) =>
        i === index ? { ...row, [field]: Number.isFinite(value) ? value : null } : row,
      ),
    );
    this.edited.emit();
  }
}
