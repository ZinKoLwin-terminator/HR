import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { todayIso, yearsBetween } from '../utilities/date.util';

const PHONE_PATTERN = /^\+?[0-9][0-9\s-]{6,18}[0-9]$/;
const CODE_PATTERN = /^[A-Z0-9][A-Z0-9-]*$/;

function isBlank(value: unknown): boolean {
  return value === null || value === undefined || value === '';
}

/** Custom HRMS validators. Each returns a single, well-named error key. */
export class HrmsValidators {
  /** International-style phone: digits, spaces, dashes, optional leading "+". */
  static phone(control: AbstractControl): ValidationErrors | null {
    return isBlank(control.value) || PHONE_PATTERN.test(String(control.value).trim())
      ? null
      : { phone: true };
  }

  /** Upper-case code such as "HR-TA" or "EMP001". */
  static code(control: AbstractControl): ValidationErrors | null {
    return isBlank(control.value) || CODE_PATTERN.test(String(control.value))
      ? null
      : { code: true };
  }

  /** Rejects values that are only whitespace. */
  static notBlank(control: AbstractControl): ValidationErrors | null {
    return typeof control.value === 'string' && control.value.length > 0 && !control.value.trim()
      ? { notBlank: true }
      : null;
  }

  /** ISO date must not be after today. */
  static notInFuture(control: AbstractControl): ValidationErrors | null {
    return isBlank(control.value) || String(control.value) <= todayIso()
      ? null
      : { notInFuture: true };
  }

  /** Person must be at least `years` old on today's date. */
  static minAge(years: number): ValidatorFn {
    return (control) =>
      isBlank(control.value) || yearsBetween(String(control.value), todayIso()) >= years
        ? null
        : { minAge: { required: years } };
  }

  /** Numeric value within inclusive bounds (also rejects non-numbers). */
  static numberBetween(min: number, max: number): ValidatorFn {
    return (control) => {
      if (isBlank(control.value)) {
        return null;
      }
      const value = Number(control.value);
      if (Number.isNaN(value)) {
        return { number: true };
      }
      return value < min || value > max ? { numberBetween: { min, max } } : null;
    };
  }

  /**
   * Cross-field (group) validator: `endKey` must be on/after `startKey`.
   * The error is set on the group AND mirrored on the end control so it renders inline.
   */
  static dateRange(startKey: string, endKey: string): ValidatorFn {
    return (group) => {
      const start = group.get(startKey);
      const end = group.get(endKey);
      if (!start || !end) {
        return null;
      }
      const invalid =
        !isBlank(start.value) && !isBlank(end.value) && String(end.value) < String(start.value);
      const endErrors = { ...(end.errors ?? {}) };
      if (invalid) {
        endErrors['dateRange'] = true;
      } else {
        delete endErrors['dateRange'];
      }
      end.setErrors(Object.keys(endErrors).length ? endErrors : null, { emitEvent: false });
      return invalid ? { dateRange: true } : null;
    };
  }

  /**
   * Cross-field: for a day shift the end time must be after the start time
   * (a night shift may end the next morning).
   */
  static timeOrder(startKey: string, endKey: string, nightKey: string): ValidatorFn {
    return (group) => {
      const start = group.get(startKey);
      const end = group.get(endKey);
      const night = group.get(nightKey);
      if (!start || !end) {
        return null;
      }
      const invalid =
        !isBlank(start.value) &&
        !isBlank(end.value) &&
        !night?.value &&
        String(end.value) <= String(start.value);
      const errors = { ...(end.errors ?? {}) };
      if (invalid) {
        errors['timeOrder'] = true;
      } else {
        delete errors['timeOrder'];
      }
      end.setErrors(Object.keys(errors).length ? errors : null, { emitEvent: false });
      return invalid ? { timeOrder: true } : null;
    };
  }

  /**
   * Cross-field: `targetKey` is required while `predicate(sourceValue)` is true
   * (e.g. an exit date is required once the status is Resigned).
   */
  static requiredWhen(
    targetKey: string,
    sourceKey: string,
    predicate: (sourceValue: unknown) => boolean,
  ): ValidatorFn {
    return (group) => {
      const target = group.get(targetKey);
      const source = group.get(sourceKey);
      if (!target || !source) {
        return null;
      }
      const missing = predicate(source.value) && isBlank(target.value);
      const errors = { ...(target.errors ?? {}) };
      if (missing) {
        errors['required'] = true;
      } else if (!target.hasValidator(Validators.required)) {
        delete errors['required'];
      }
      target.setErrors(Object.keys(errors).length ? errors : null, { emitEvent: false });
      return missing ? { requiredWhen: targetKey } : null;
    };
  }

  /** Cross-field: numeric `maxKey` must be ≥ `minKey`. */
  static minMax(minKey: string, maxKey: string): ValidatorFn {
    return (group) => {
      const min = group.get(minKey);
      const max = group.get(maxKey);
      if (!min || !max) {
        return null;
      }
      const invalid =
        !isBlank(min.value) && !isBlank(max.value) && Number(max.value) < Number(min.value);
      const maxErrors = { ...(max.errors ?? {}) };
      if (invalid) {
        maxErrors['minMax'] = true;
      } else {
        delete maxErrors['minMax'];
      }
      max.setErrors(Object.keys(maxErrors).length ? maxErrors : null, { emitEvent: false });
      return invalid ? { minMax: true } : null;
    };
  }
}
