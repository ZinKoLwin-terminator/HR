import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';
import { EntityDetailsDialogData, FieldConfig, FormValues } from './crud.models';

interface DetailRow {
  label: string;
  value: string;
  badge: boolean;
  wide: boolean;
}

/** Read-only view of a record. Closes with `'edit'` when the user chooses Edit. */
@Component({
  selector: 'app-entity-details-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule, StatusBadgeComponent],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p class="subtitle">{{ data.subtitle }}</p>
      <dl class="details">
        @for (row of rows; track row.label) {
          <div [class.wide]="row.wide">
            <dt>{{ row.label }}</dt>
            <dd>
              @if (row.badge) {
                <app-status-badge [status]="row.value" />
              } @else {
                {{ row.value }}
              }
            </dd>
          </div>
        }
      </dl>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
      @if (!data.readonly) {
        <button mat-flat-button type="button" [mat-dialog-close]="'edit'">
          <mat-icon>edit</mat-icon>Edit
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    h2[mat-dialog-title] {
      margin: 0;
      padding: 24px 28px 2px;
      font: var(--mat-sys-headline-small);
      font-weight: 700;
      letter-spacing: -0.01em;

      &::before {
        display: none;
      }
    }
    mat-dialog-content {
      padding: 6px 28px 8px !important;
      max-height: 68vh;
    }
    mat-dialog-actions {
      padding: 14px 28px !important;
      gap: 8px;
      border-top: 1px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface-container-low);
    }
    .subtitle {
      margin: 0 0 18px;
      font: var(--mat-sys-label-large);
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--mat-sys-primary);
    }
    .details {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px 12px;
      margin: 0;
    }
    .details > div {
      padding: 10px 14px 12px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface-container-low);
    }
    .wide {
      grid-column: 1 / -1;
    }
    dt {
      font: var(--mat-sys-label-small);
      font-weight: 600;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 4px 0 0;
      font: var(--mat-sys-body-large);
      font-weight: 500;
      overflow-wrap: anywhere;
    }
    @media (max-width: 599.98px) {
      .details {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class EntityDetailsDialogComponent {
  protected readonly data = inject<EntityDetailsDialogData>(MAT_DIALOG_DATA);
  protected readonly rows: DetailRow[] = this.data.fields.map((field) => ({
    label: field.label,
    value: this.display(field, this.data.row),
    badge: field.key === 'status',
    wide: field.type === 'textarea' || field.fullWidth === true,
  }));

  private display(field: FieldConfig, row: FormValues): string {
    const value = row[field.key];
    if (value === null || value === undefined || value === '') {
      return '—';
    }
    switch (field.type) {
      case 'toggle':
        return value ? 'Yes' : 'No';
      case 'multiselect': {
        const options = field.options?.(row, { id: this.data.id }) ?? [];
        const chosen = (value as unknown[]).map(
          (v) => options.find((o) => o.value === v)?.label ?? String(v),
        );
        return chosen.length ? chosen.join(', ') : '—';
      }
      case 'select':
        return (
          field.options?.(row, { id: this.data.id })?.find((o) => o.value === value)?.label ??
          String(value)
        );
      case 'date':
        return new Date(`${String(value)}T00:00:00`).toLocaleDateString('en-GB', {
          day: '2-digit',
          month: 'short',
          year: 'numeric',
        });
      case 'number':
        return Number(value).toLocaleString('en-US');
      default:
        return String(value);
    }
  }
}
