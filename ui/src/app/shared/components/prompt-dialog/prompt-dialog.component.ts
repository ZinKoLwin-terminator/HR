import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface PromptDialogData {
  title: string;
  message?: string;
  /** Label of the text box, e.g. "Reason". */
  label: string;
  required?: boolean;
  confirmText: string;
  destructive?: boolean;
  maxLength?: number;
}

/** Asks for a short piece of text (e.g. a rejection reason). Closes with the trimmed text, or `null`. */
@Component({
  selector: 'app-prompt-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <!-- No [formGroup] here, so Angular would not stop the browser's native submit: handle it ourselves. -->
    <form (submit)="submit($event)" novalidate>
      <mat-dialog-content>
        @if (data.message) {
          <p class="message">{{ data.message }}</p>
        }
        <mat-form-field class="full">
          <mat-label>{{ data.label }}</mat-label>
          <textarea
            matInput
            rows="3"
            [formControl]="text"
            [attr.maxlength]="data.maxLength ?? 250"
            cdkFocusInitial
          ></textarea>
          @if (text.hasError('required')) {
            <mat-error>{{ data.label }} is required.</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" [mat-dialog-close]="null">Cancel</button>
        <button mat-flat-button type="submit" [class.danger-button]="data.destructive">
          {{ data.confirmText }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .message {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }
    .full {
      width: 100%;
    }
  `,
})
export class PromptDialogComponent {
  protected readonly data = inject<PromptDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<PromptDialogComponent, string | null>>(MatDialogRef);
  protected readonly text = new FormControl('', {
    nonNullable: true,
    validators: this.data.required ? [Validators.required, Validators.pattern(/\S/)] : [],
  });

  protected submit(event: Event): void {
    event.preventDefault(); // never let the browser submit (and reload) the page
    this.text.markAsTouched();
    if (this.text.invalid) {
      return;
    }
    this.dialogRef.close(this.text.value.trim());
  }
}
