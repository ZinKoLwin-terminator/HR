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
    <div class="dialog-head">
      <span class="dialog-head__icon" aria-hidden="true"><mat-icon>badge</mat-icon></span>
      <div class="dialog-head__text">
        <h2 mat-dialog-title>{{ data.title }}</h2>
        <p class="subtitle">{{ data.subtitle }}</p>
      </div>
      <button mat-icon-button type="button" class="close-btn" mat-dialog-close aria-label="Close">
        <mat-icon>close</mat-icon>
      </button>
    </div>
    <mat-dialog-content>
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
    :host {
      display: block;
    }
    .dialog-head {
      display: flex;
      align-items: flex-start;
      gap: 14px;
      padding: 22px 24px 16px;
    }
    .dialog-head__icon {
      flex: none;
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 12px;
      background: color-mix(in srgb, var(--mat-sys-primary) 12%, transparent);
      color: var(--mat-sys-primary);
      .mat-icon {
        font-size: 22px;
        width: 22px;
        height: 22px;
      }
    }
    .dialog-head__text {
      flex: 1;
      min-width: 0;
    }
    h2[mat-dialog-title] {
      margin: 0;
      padding: 0;
      font-size: 18px;
      line-height: 24px;
      font-weight: 700;
      letter-spacing: -0.01em;
      color: var(--mat-sys-on-surface);
      overflow-wrap: anywhere;

      &::before {
        display: none;
      }
    }
    .subtitle {
      margin: 2px 0 0;
      font-size: 12px;
      line-height: 16px;
      font-weight: 500;
      color: var(--mat-sys-on-surface-variant);
    }
    .close-btn {
      flex: none;
      margin: -4px -8px 0 0;
      color: var(--mat-sys-on-surface-variant);
    }
    mat-dialog-content {
      padding: 4px 24px 22px !important;
      max-height: 64vh;
      border-top: 1px solid var(--app-card-border);
      padding-top: 18px !important;
    }
    mat-dialog-actions {
      padding: 12px 24px !important;
      gap: 8px;
      border-top: 1px solid var(--app-card-border);
      background: light-dark(
        color-mix(in srgb, var(--mat-sys-primary) 3%, #f8fafc),
        color-mix(in srgb, var(--mat-sys-primary) 8%, transparent)
      );
    }
    .details {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px 12px;
      margin: 0;
    }
    .details > div {
      display: flex;
      flex-direction: column;
      padding: 10px 14px 12px;
      border-radius: 10px;
      border: 1px solid color-mix(in srgb, var(--mat-sys-outline-variant) 55%, transparent);
      background: light-dark(
        color-mix(in srgb, var(--mat-sys-primary) 2.5%, #fff),
        color-mix(in srgb, var(--mat-sys-on-surface) 4%, transparent)
      );
    }
    .wide {
      grid-column: 1 / -1;
    }
    dt {
      font-size: 11px;
      line-height: 16px;
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 3px 0 0;
      font-size: 13.5px;
      line-height: 20px;
      font-weight: 500;
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
