import { FormControl, FormGroup, ValidatorFn, Validators } from '@angular/forms';
import { FieldConfig, FormValues } from '../components/crud/crud.models';
import { HrmsValidators } from '../validators/hrms-validators';

/** Validators implied by a field's declarative configuration. */
export function validatorsFor(field: FieldConfig): ValidatorFn[] {
  const list: ValidatorFn[] = [];
  if (field.required) list.push(Validators.required);
  if (field.type === 'text' || field.type === 'textarea') list.push(HrmsValidators.notBlank);
  if (field.type === 'email') list.push(Validators.email);
  if (field.minLength) list.push(Validators.minLength(field.minLength));
  if (field.maxLength) list.push(Validators.maxLength(field.maxLength));
  if (field.type === 'number') {
    list.push(
      HrmsValidators.numberBetween(
        field.min ?? Number.MIN_SAFE_INTEGER,
        field.max ?? Number.MAX_SAFE_INTEGER,
      ),
    );
  }
  if (field.pattern === 'code') list.push(HrmsValidators.code);
  if (field.pattern === 'phone' || field.type === 'tel') list.push(HrmsValidators.phone);
  if (field.pattern === 'digits') list.push(Validators.pattern(/^\d+$/));
  if (field.notInFuture) list.push(HrmsValidators.notInFuture);
  if (field.minAge) list.push(HrmsValidators.minAge(field.minAge));
  return list;
}

export function emptyValue(field: FieldConfig): unknown {
  switch (field.type) {
    case 'select':
    case 'number':
      return null;
    case 'multiselect':
      return [];
    case 'toggle':
      return false;
    default:
      return '';
  }
}

export type FieldFormGroup = FormGroup<Record<string, FormControl<unknown>>>;

/** Builds a flat reactive form from field configs. */
export function buildFormGroup(
  fields: readonly FieldConfig[],
  values: FormValues,
  options: { disabledKeys?: ReadonlySet<string>; validators?: ValidatorFn[] } = {},
): FieldFormGroup {
  const controls: Record<string, FormControl<unknown>> = {};
  for (const field of fields) {
    controls[field.key] = new FormControl<unknown>(
      {
        value: values[field.key] ?? emptyValue(field),
        disabled: options.disabledKeys?.has(field.key) ?? false,
      },
      validatorsFor(field),
    );
  }
  return new FormGroup(controls, { validators: options.validators ?? [] });
}

/** Trims text; blank optional selects/numbers become null. */
export function normalizeValues(fields: readonly FieldConfig[], raw: FormValues): FormValues {
  const result: FormValues = { ...raw };
  for (const field of fields) {
    const value = raw[field.key];
    if (typeof value === 'string' && field.type !== 'password') {
      result[field.key] = value.trim();
    }
    if (
      (field.type === 'select' || field.type === 'number') &&
      (value === '' || value === undefined)
    ) {
      result[field.key] = null;
    }
  }
  return result;
}

/**
 * Clears dependent dropdowns when their parent changes and the current value
 * is no longer one of the available options.
 */
export function wireDependentFields(
  fields: readonly FieldConfig[],
  form: FieldFormGroup,
  optionsFor: (field: FieldConfig) => { value: unknown }[],
): void {
  for (const field of fields.filter((f) => f.dependsOn)) {
    form.controls[field.dependsOn!].valueChanges.subscribe(() => {
      const control = form.controls[field.key];
      const current = control.value;
      if (current !== null && current !== undefined && current !== '') {
        if (!optionsFor(field).some((o) => o.value === current)) {
          control.setValue(null);
        }
      }
    });
  }
}
