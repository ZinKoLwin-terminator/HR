import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** "Late" marker for a scorecard submitted after its period's due date. Text, not colour alone. */
@Component({
  selector: 'app-late-chip',
  imports: [MatIconModule],
  template: `<span class="late" data-testid="late-chip"
    ><mat-icon aria-hidden="true">schedule</mat-icon>Late<span class="cdk-visually-hidden">
      (submitted after the due date)</span
    ></span
  >`,
  styles: `
    :host {
      display: inline-flex;
    }
    .late {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      border-radius: 999px;
      border: 1px solid var(--mat-sys-error);
      color: var(--mat-sys-error);
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
    }
    mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
  `,
})
export class LateChipComponent {}
