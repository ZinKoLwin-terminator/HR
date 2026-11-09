import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import {
  afterNextRender,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/** Labelled wrapper for one control inside {@link FilterPopupComponent}. */
@Component({
  selector: 'app-filter-field',
  template: `
    <span class="label" [id]="labelId">{{ label() }}</span>
    <ng-content />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .label {
      font: var(--mat-sys-label-medium);
      font-weight: 600;
      letter-spacing: 0.02em;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class FilterFieldComponent {
  private static nextId = 0;
  readonly label = input.required<string>();
  protected readonly labelId = `filter-field-label-${FilterFieldComponent.nextId++}`;

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    // Name the projected control by this label (a select would otherwise be named by its value).
    afterNextRender(() => {
      host
        .querySelector('[role="combobox"], input, textarea, select')
        ?.setAttribute('aria-labelledby', this.labelId);
    });
  }
}

/**
 * Filter icon button that opens a popup (CDK overlay) holding projected filter
 * controls. Wrap each control in `<app-filter-field label="…">`.
 */
@Component({
  selector: 'app-filter-popup',
  imports: [CdkConnectedOverlay, CdkOverlayOrigin, MatButtonModule, MatIconModule],
  template: `
    <button
      mat-stroked-button
      type="button"
      class="toggle"
      cdkOverlayOrigin
      #origin="cdkOverlayOrigin"
      [class.toggle--active]="open() || activeCount() > 0"
      aria-label="Toggle filters"
      aria-haspopup="dialog"
      [attr.aria-expanded]="open()"
      (click)="open.set(!open())"
    >
      <mat-icon>tune</mat-icon>Filter
      @if (activeCount() > 0) {
        <span class="toggle__count">{{ activeCount() }}</span>
      }
    </button>

    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="origin"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayPositions]="positions"
      [cdkConnectedOverlayOffsetY]="8"
      (overlayOutsideClick)="onOutsideClick($event)"
      (overlayKeydown)="$event.key === 'Escape' && open.set(false)"
      (detach)="open.set(false)"
    >
      <div class="popup" role="dialog" [attr.aria-label]="title()">
        <div class="popup__header">
          <mat-icon class="popup__icon" aria-hidden="true">tune</mat-icon>
          <span class="popup__title">{{ title() }}</span>
          @if (activeCount() > 0) {
            <span class="popup__badge">{{ activeCount() }}</span>
          }
          <button
            mat-icon-button
            type="button"
            class="popup__close"
            aria-label="Close filters"
            (click)="open.set(false)"
          >
            <mat-icon>close</mat-icon>
          </button>
        </div>
        <div class="popup__body"><ng-content /></div>
        <div class="popup__footer">
          <button
            mat-button
            type="button"
            [disabled]="activeCount() === 0"
            (click)="cleared.emit()"
          >
            Clear all
          </button>
          <button mat-flat-button type="button" (click)="open.set(false)">Done</button>
        </div>
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: inline-flex;
    }
    .toggle {
      position: relative;
      height: 36px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 10px;
      color: var(--mat-sys-on-surface);
    }
    .toggle--active {
      color: var(--mat-sys-primary);
      border-color: var(--mat-sys-primary);
      background: color-mix(in srgb, var(--mat-sys-primary) 10%, transparent);
    }
    .toggle__count {
      position: absolute;
      top: -6px;
      right: -6px;
      min-width: 18px;
      height: 18px;
      padding-inline: 4px;
      border-radius: 9px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      font: var(--mat-sys-label-small);
      line-height: 18px;
      text-align: center;
    }

    .popup {
      display: flex;
      flex-direction: column;
      width: 320px;
      max-width: calc(100vw - 32px);
      overflow: hidden;
      border: 1px solid var(--app-card-border);
      border-radius: 14px;
      background: var(--app-card-bg);
      box-shadow:
        0 12px 32px color-mix(in srgb, #000 16%, transparent),
        0 2px 6px color-mix(in srgb, #000 10%, transparent);
    }
    .popup__header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 6px 6px 16px;
      border-bottom: 1px solid var(--app-card-border);
      background: var(--mat-sys-surface-container-low);
    }
    .popup__icon {
      width: 20px;
      height: 20px;
      font-size: 20px;
      color: var(--app-color-primary);
    }
    .popup__title {
      font: var(--mat-sys-title-small);
      font-weight: 600;
      color: var(--mat-sys-on-surface);
    }
    .popup__badge {
      min-width: 20px;
      height: 20px;
      padding-inline: 6px;
      border-radius: 10px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      font: var(--mat-sys-label-small);
      line-height: 20px;
      text-align: center;
    }
    .popup__close {
      margin-left: auto;
      color: var(--mat-sys-on-surface-variant);
    }
    .popup__body {
      display: flex;
      flex-direction: column;
      gap: 14px;
      max-height: min(60vh, 480px);
      overflow-y: auto;
      padding: 16px;
    }
    .popup__body ::ng-deep mat-form-field {
      width: 100%;
      --mat-form-field-container-height: 40px;
      --mat-form-field-container-vertical-padding: 8px;
    }
    /* No selection means "All": show the placeholder as normal text. */
    .popup__body ::ng-deep .mat-mdc-select-placeholder {
      color: var(--mat-sys-on-surface);
    }
    .popup__footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 12px;
      border-top: 1px solid var(--app-card-border);
      background: var(--mat-sys-surface-container-low);
    }
  `,
})
export class FilterPopupComponent {
  /** Number of active filters (shown as a badge). */
  readonly activeCount = input(0);
  readonly title = input('Filters');
  /** Emitted when "Clear all" is pressed. */
  readonly cleared = output<void>();

  protected readonly open = signal(false);

  /** Opens under the icon, right-aligned; flips above if there is no room. */
  protected readonly positions: ConnectedPosition[] = [
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top' },
    { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom' },
  ];

  /** Close on outside click, except inside select/datepicker panels or on the toggle itself. */
  protected onOutsideClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    const keepOpen = '.mat-mdc-select-panel, .mat-datepicker-content, .toggle';
    if (target?.closest(keepOpen)) return;
    this.open.set(false);
  }
}
