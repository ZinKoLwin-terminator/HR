import { Component, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/** Drag-and-drop / browse file picker for one Excel (.xlsx) file. Shows the file and lets it be replaced. */
@Component({
  selector: 'app-excel-file-upload',
  imports: [MatButtonModule, MatIconModule],
  template: `
    @if (file(); as picked) {
      <div class="picked">
        <mat-icon class="picked__icon" aria-hidden="true">description</mat-icon>
        <div class="picked__text">
          <strong class="picked__name">{{ picked.name }}</strong>
          <span class="picked__size">{{ size(picked.size) }}</span>
        </div>
        <button mat-stroked-button type="button" (click)="browse.click()">Replace file</button>
        <button
          mat-button
          type="button"
          [attr.aria-label]="'Remove ' + picked.name"
          (click)="clear()"
        >
          Remove
        </button>
      </div>
    } @else {
      <div
        class="drop"
        role="button"
        tabindex="0"
        [class.drop--over]="over()"
        (click)="browse.click()"
        (keydown.enter)="browse.click()"
        (keydown.space)="$event.preventDefault(); browse.click()"
        (dragover)="onDragOver($event)"
        (dragleave)="over.set(false)"
        (drop)="onDrop($event)"
      >
        <mat-icon class="drop__icon" aria-hidden="true">upload_file</mat-icon>
        <span class="drop__title">Drag an Excel file here, or click to browse</span>
        <span class="drop__hint"
          >.xlsx files only, up to {{ maxMegabytes() }} MB. Use the downloaded template.</span
        >
      </div>
    }
    <input
      #browse
      class="file-input"
      type="file"
      hidden
      accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      tabindex="-1"
      aria-hidden="true"
      (change)="onPick($event)"
    />
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .drop {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      padding: 32px 16px;
      border: 2px dashed var(--mat-sys-outline);
      border-radius: var(--app-radius-lg);
      background: var(--mat-sys-surface-container-low);
      cursor: pointer;
      text-align: center;
      transition:
        border-color 120ms ease,
        background-color 120ms ease;
    }
    .drop:hover,
    .drop--over {
      border-color: var(--mat-sys-primary);
      background: color-mix(in srgb, var(--mat-sys-primary) 8%, transparent);
    }
    .drop__icon {
      width: 40px;
      height: 40px;
      font-size: 40px;
      color: var(--mat-sys-primary);
    }
    .drop__title {
      font: var(--mat-sys-title-medium);
    }
    .drop__hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .picked {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
      padding: 14px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-lg);
      background: var(--mat-sys-surface-container-low);
    }
    .picked__icon {
      color: var(--mat-sys-primary);
    }
    .picked__text {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-width: 160px;
    }
    .picked__name {
      overflow-wrap: anywhere;
    }
    .picked__size {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .error {
      margin: 8px 0 0;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-medium);
    }
  `,
})
export class ExcelFileUploadComponent {
  readonly file = input<File | null>(null);
  readonly maxBytes = input(5 * 1024 * 1024);
  readonly fileChange = output<File | null>();

  protected readonly over = signal(false);
  protected readonly error = signal('');

  protected maxMegabytes(): number {
    return Math.round(this.maxBytes() / (1024 * 1024));
  }

  protected size(bytes: number): string {
    return bytes < 1024 * 1024
      ? `${Math.max(1, Math.round(bytes / 1024))} KB`
      : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.over.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.over.set(false);
    this.accept(event.dataTransfer?.files?.[0] ?? null);
  }

  protected onPick(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.accept(input.files?.[0] ?? null);
    input.value = ''; // allow choosing the same file again
  }

  protected clear(): void {
    this.error.set('');
    this.fileChange.emit(null);
  }

  /** Quick checks for a friendly message; the importer repeats them (and more) on its side. */
  private accept(file: File | null): void {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      this.error.set('Only Excel workbooks (.xlsx) can be imported.');
      return;
    }
    if (file.size > this.maxBytes()) {
      this.error.set(`The file is too large. The maximum size is ${this.maxMegabytes()} MB.`);
      return;
    }
    this.error.set('');
    this.fileChange.emit(file);
  }
}
