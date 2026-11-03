import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { validationMessage } from '../../validators/validation-messages';

/**
 * Renders the first validation message for a control. Place it inside
 * `<mat-error>`. Uses Eager change detection because reactive-form error
 * state is not signal-based.
 */
@Component({
  selector: 'app-field-error',
  // Intentional: cross-field validators update errors without emitting events,
  // so this tiny leaf re-reads errors whenever its parent form is checked.
  // eslint-disable-next-line @angular-eslint/prefer-on-push-component-change-detection
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `{{ message() }}`,
})
export class FieldErrorComponent {
  readonly control = input.required<AbstractControl | null>();
  readonly label = input('This field');

  protected message(): string {
    return validationMessage(this.control()?.errors, this.label());
  }
}
