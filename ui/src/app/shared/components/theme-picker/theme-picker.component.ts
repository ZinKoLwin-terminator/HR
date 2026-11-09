import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ThemeService } from '../../../core/services/theme.service';

/** Row of accent-color swatches (a radio group). */
@Component({
  selector: 'app-theme-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="picker" role="radiogroup" aria-label="Accent color">
      @for (a of theme.accents; track a.id) {
        <button
          type="button"
          role="radio"
          class="swatch"
          [style.--swatch]="a.swatch"
          [attr.aria-checked]="theme.accent() === a.id"
          [attr.aria-label]="a.label"
          [title]="a.label"
          (click)="$event.stopPropagation(); theme.setAccent(a.id)"
        >
          @if (theme.accent() === a.id) {
            <mat-icon aria-hidden="true">check</mat-icon>
          }
        </button>
      }
    </div>
  `,
  styles: `
    .picker {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
    }
    .swatch {
      display: grid;
      place-items: center;
      width: 28px;
      height: 28px;
      padding: 0;
      border-radius: 50%;
      border: 2px solid transparent;
      background: var(--swatch);
      color: #fff;
      cursor: pointer;
      box-shadow: 0 0 0 1px rgb(0 0 0 / 15%);
      transition:
        transform 120ms ease,
        box-shadow 120ms ease;
    }
    .swatch:hover {
      transform: scale(1.12);
    }
    .swatch[aria-checked='true'] {
      box-shadow:
        0 0 0 2px var(--mat-sys-surface),
        0 0 0 4px var(--swatch);
    }
    .swatch mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
  `,
})
export class ThemePickerComponent {
  protected readonly theme = inject(ThemeService);
}
