import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { startWith } from 'rxjs';
import { Entity, isAppError } from '../../../core/models/common.models';
import {
  FieldFormGroup,
  buildFormGroup,
  normalizeValues,
  wireDependentFields,
} from '../../forms/form-builder';
import { FormFieldComponent } from '../form-field/form-field.component';
import { EntityFormDialogData, FieldConfig, FieldOption, FormValues } from './crud.models';

/** Schema-driven create/edit dialog (reactive form, dependent dropdowns, server-error mapping). */
@Component({
  selector: 'app-entity-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormFieldComponent,
  ],
  templateUrl: './entity-form-dialog.component.html',
  styleUrl: './entity-form-dialog.component.scss',
})
export class EntityFormDialogComponent {
  protected readonly data = inject<EntityFormDialogData<Entity>>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<EntityFormDialogComponent, Entity>>(MatDialogRef);

  protected readonly saving = signal(false);
  protected readonly formError = signal('');

  protected readonly form: FieldFormGroup;
  /** Bumps on every value change so option lists (which depend on other fields) re-evaluate. */
  private readonly values;

  constructor() {
    const isEdit = this.data.id !== null;
    this.form = buildFormGroup(this.data.fields, this.data.value, {
      disabledKeys: new Set(
        isEdit ? this.data.fields.filter((f) => f.lockedOnEdit).map((f) => f.key) : [],
      ),
      validators: this.data.groupValidators,
    });
    this.values = toSignal(this.form.valueChanges.pipe(startWith(null)), { initialValue: null });
    wireDependentFields(this.data.fields, this.form, (field) => this.optionsFor(field));
    this.wireDerivedFields();
    this.clearServerErrorsOnEdit();
  }

  /**
   * A rule reported by the server (e.g. "a policy already exists for this group") may depend on
   * any field, so editing anything clears those errors and lets the user submit again.
   */
  private clearServerErrorsOnEdit(): void {
    this.form.valueChanges.subscribe(() => {
      for (const control of Object.values(this.form.controls)) {
        if (control.hasError('server')) {
          const { server: _removed, ...rest } = control.errors ?? {};
          void _removed;
          control.setErrors(Object.keys(rest).length ? rest : null, { emitEvent: false });
        }
      }
      if (this.formError() && !this.saving()) {
        this.formError.set('');
      }
    });
  }

  /** Calculated fields are read-only and recomputed whenever another value changes. */
  private wireDerivedFields(): void {
    const derived = this.data.fields.filter((f) => f.derive);
    if (derived.length === 0) {
      return;
    }
    const recompute = () => {
      const raw = this.form.getRawValue() as FormValues;
      for (const field of derived) {
        this.form.controls[field.key].setValue(field.derive!(raw), { emitEvent: false });
      }
    };
    for (const field of derived) {
      this.form.controls[field.key].disable({ emitEvent: false });
    }
    this.form.valueChanges.subscribe(recompute);
    recompute();
  }

  protected optionsFor(field: FieldConfig): FieldOption[] {
    this.values(); // re-evaluate when any value changes
    return field.options?.(this.form.getRawValue() as FormValues, { id: this.data.id }) ?? [];
  }

  protected control(field: FieldConfig): AbstractControl {
    return this.form.controls[field.key];
  }

  protected submit(): void {
    if (this.saving()) {
      return; // prevent duplicate submissions
    }
    this.formError.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.formError.set('Please correct the highlighted fields.');
      queueMicrotask(() =>
        document
          .querySelector<HTMLElement>(
            '.entity-form .mat-form-field-invalid input, .entity-form .mat-form-field-invalid textarea, .entity-form .mat-form-field-invalid .mat-mdc-select',
          )
          ?.focus(),
      );
      return;
    }
    this.saving.set(true);
    const values = normalizeValues(this.data.fields, this.form.getRawValue() as FormValues);
    this.data.save(values).subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error: unknown) => {
        this.saving.set(false);
        if (isAppError(error)) {
          this.formError.set(error.message);
          for (const [key, message] of Object.entries(error.fieldErrors ?? {})) {
            this.form.controls[key]?.setErrors({ server: message });
            this.form.controls[key]?.markAsTouched();
          }
        } else {
          this.formError.set('The record could not be saved. Please try again.');
        }
      },
    });
  }
}
