import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  viewChild,
} from '@angular/core';
import { AbstractControl, FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FieldConfig, FieldOption } from '../crud/crud.models';
import { FieldErrorComponent } from '../field-error/field-error.component';

export const ALLOWED_FILE_EXTENSIONS = ['pdf', 'doc', 'docx', 'png', 'jpg', 'jpeg'] as const;
export const MAX_FILE_BYTES = 5 * 1024 * 1024;

/**
 * Renders one {@link FieldConfig} as a Material form field bound to a control.
 * Shared by dialogs and multi-section forms so every form looks and validates the same.
 */
@Component({
  selector: 'app-form-field',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    FieldErrorComponent,
  ],
  // Intentional: cross-field validators update control errors without emitting events.
  // eslint-disable-next-line @angular-eslint/prefer-on-push-component-change-detection
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (field().type === 'toggle') {
      <div class="toggle">
        <mat-slide-toggle [formControl]="ctl()">{{ field().label }}</mat-slide-toggle>
        @if (field().hint) {
          <p class="toggle__hint">{{ field().hint }}</p>
        }
      </div>
    } @else {
      <mat-form-field>
        <mat-label>{{ field().label }}</mat-label>

        @switch (field().type) {
          @case ('multiselect') {
            <mat-select multiple [formControl]="ctl()" [attr.aria-label]="field().label">
              @for (option of options(); track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          }
          @case ('select') {
            <mat-select [formControl]="ctl()" [attr.aria-label]="field().label">
              @if (field().nullable || !field().required) {
                <mat-option [value]="null">— None —</mat-option>
              }
              @for (option of options(); track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          }
          @case ('textarea') {
            <textarea
              matInput
              rows="3"
              [formControl]="ctl()"
              [placeholder]="field().placeholder ?? ''"
            ></textarea>
          }
          @case ('number') {
            <!-- Static type="number" so Angular's NumberValueAccessor returns real numbers. -->
            <input
              matInput
              type="number"
              [formControl]="ctl()"
              [placeholder]="field().placeholder ?? ''"
              [attr.min]="field().min ?? null"
              [attr.max]="field().max ?? null"
              autocomplete="off"
            />
          }
          @case ('file') {
            <input
              matInput
              readonly
              [formControl]="ctl()"
              placeholder="No file chosen"
              (click)="openPicker()"
            />
            <input
              #picker
              type="file"
              hidden
              [accept]="accept"
              (change)="onFile($event)"
              tabindex="-1"
              aria-hidden="true"
            />
          }
          @default {
            <input
              matInput
              [type]="field().type"
              [formControl]="ctl()"
              [placeholder]="field().placeholder ?? ''"
              [attr.maxlength]="field().maxLength ?? null"
              autocomplete="off"
            />
          }
        }

        @if (field().type === 'file') {
          <button
            matSuffix
            mat-icon-button
            type="button"
            aria-label="Choose file"
            (click)="openPicker()"
          >
            <mat-icon>attach_file</mat-icon>
          </button>
        }
        @if (field().hint) {
          <mat-hint>{{ field().hint }}</mat-hint>
        }
        <mat-error><app-field-error [control]="ctl()" [label]="field().label" /></mat-error>
      </mat-form-field>
    }
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    mat-form-field {
      width: 100%;
    }
    .toggle {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 12px 0 18px;
    }
    .toggle__hint {
      margin: 0 0 0 52px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class FormFieldComponent {
  readonly field = input.required<FieldConfig>();
  readonly control = input.required<AbstractControl>();
  readonly options = input<FieldOption[]>([]);

  protected readonly ctl = computed(() => this.control() as FormControl);
  private readonly picker = viewChild<ElementRef<HTMLInputElement>>('picker');

  protected openPicker(): void {
    this.picker()?.nativeElement.click();
  }
  protected readonly accept = ALLOWED_FILE_EXTENSIONS.map((e) => `.${e}`).join(',');

  protected onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // allow choosing the same file again
    if (!file) {
      return;
    }
    const control = this.ctl();
    const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
    control.markAsTouched();
    control.markAsDirty();
    if (!(ALLOWED_FILE_EXTENSIONS as readonly string[]).includes(extension)) {
      control.setValue('');
      control.setErrors({ fileType: true });
    } else if (file.size > MAX_FILE_BYTES) {
      control.setValue('');
      control.setErrors({ fileSize: true });
    } else {
      control.setValue(file.name);
    }
  }
}
