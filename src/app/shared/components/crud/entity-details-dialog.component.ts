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
      padding: 20px 24px 0;
      font: var(--mat-sys-headline-small);
      font-weight: 700;
      letter-spacing: -0.01em;
      color: var(--app-color-primary);
      border-bottom: 3px solid var(--app-color-accent);
      padding-bottom: 12px;

      &::before {
        display: none;
      }
    }
    mat-dialog-content {
      padding: 16px 24px 20px !important;
      max-height: 68vh;
    }
    mat-dialog-actions {
      padding: 12px 24px !important;
      gap: var(--app-spacing-md);
      border-top: 2px solid var(--app-color-accent);
      background: var(--app-card-bg);
    }
    .subtitle {
      margin: 0 0 var(--app-spacing-lg);
      font: var(--mat-sys-label-large);
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    .details {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--app-spacing-lg);
      margin: 0;
    }
    .details > div {
      display: flex;
      flex-direction: column;
      padding: 0;
      border: none;
      border-radius: 0;
      background: transparent;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      padding-bottom: var(--app-spacing-md);
    }
    .details > div:last-child {
      border-bottom: none;
      padding-bottom: 0;
    }
    .wide {
      grid-column: 1 / -1;
    }
    dt {
      font: var(--mat-sys-label-small);
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: var(--app-spacing-xs) 0 0;
      font: var(--mat-sys-body-large);
      font-weight: 400;
      color: var(--mat-sys-on-surface);
      overflow-wrap: anywhere;
      word-break: break-word;
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
