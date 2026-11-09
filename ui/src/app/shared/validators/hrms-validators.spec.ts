import { FormControl, FormGroup } from '@angular/forms';
import { addDays, todayIso } from '../utilities/date.util';
import { HrmsValidators } from './hrms-validators';
import { validationMessage } from './validation-messages';

describe('HrmsValidators', () => {
  it('validates phone numbers', () => {
    expect(HrmsValidators.phone(new FormControl('+95 9 123 456 789'))).toBeNull();
    expect(HrmsValidators.phone(new FormControl(''))).toBeNull();
    expect(HrmsValidators.phone(new FormControl('abc123'))).toEqual({ phone: true });
  });

  it('validates codes', () => {
    expect(HrmsValidators.code(new FormControl('HR-TA'))).toBeNull();
    expect(HrmsValidators.code(new FormControl('hr ta'))).toEqual({ code: true });
  });

  it('rejects future dates and under-age birth dates', () => {
    expect(HrmsValidators.notInFuture(new FormControl(addDays(todayIso(), 1)))).toEqual({
      notInFuture: true,
    });
    expect(HrmsValidators.minAge(18)(new FormControl(addDays(todayIso(), -365 * 10)))).toEqual({
      minAge: { required: 18 },
    });
  });

  it('applies the cross-field date range rule to the end control', () => {
    const group = new FormGroup(
      { start: new FormControl('2026-11-10'), end: new FormControl('2026-11-05') },
      { validators: HrmsValidators.dateRange('start', 'end') },
    );
    expect(group.hasError('dateRange')).toBe(true);
    expect(group.controls.end.hasError('dateRange')).toBe(true);

    group.controls.end.setValue('2026-11-12');
    expect(group.valid).toBe(true);
    expect(group.controls.end.errors).toBeNull();
  });

  it('produces readable messages', () => {
    expect(validationMessage({ required: true }, 'Email')).toBe('Email is required.');
    expect(validationMessage({ minlength: { requiredLength: 3, actualLength: 1 } }, 'Code')).toBe(
      'Code must be at least 3 characters.',
    );
    expect(validationMessage(null, 'X')).toBe('');
  });
});
