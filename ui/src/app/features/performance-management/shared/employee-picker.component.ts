import { Component, effect, inject, input, model, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import {
  MatAutocompleteModule,
  MatAutocompleteSelectedEvent,
} from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { EMPTY, catchError, debounceTime, map, of, startWith, switchMap } from 'rxjs';
import { EmployeeLookupItem } from '../../../core/models/employee.models';
import { EmployeeLookupDataService } from '../../../core/services/data/employee-data.service';

const PAGE_SIZE = 20;

/**
 * Searchable employee picker over the employee lookup contract (current employees, non-personal
 * fields only). Type a code or name, then choose from the list. `selected` holds the choice and is
 * cleared when the text is edited again.
 */
@Component({
  selector: 'app-employee-picker',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatFormFieldModule, MatInputModule],
  template: `
    <mat-form-field class="picker">
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        type="text"
        autocomplete="off"
        [formControl]="text"
        [matAutocomplete]="auto"
        placeholder="Type a code or name"
      />
      <mat-autocomplete
        #auto="matAutocomplete"
        [displayWith]="display"
        (optionSelected)="choose($event)"
      >
        @for (employee of results(); track employee.id) {
          <mat-option [value]="employee" [disabled]="excluded().includes(employee.id)">
            {{ employee.employeeCode }} · {{ employee.displayName }}
            <span class="sub">{{ employee.departmentName }}</span>
          </mat-option>
        } @empty {
          <mat-option disabled>No employee found</mat-option>
        }
      </mat-autocomplete>
      @if (hint()) {
        <mat-hint>{{ hint() }}</mat-hint>
      }
      <mat-error>{{ error() || 'Choose an employee from the list.' }}</mat-error>
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .picker {
      width: 100%;
    }
    .sub {
      margin-left: 8px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class EmployeePickerComponent {
  readonly label = input('Employee');
  readonly hint = input('');
  /** A server message for this field; shown as the field's error. */
  readonly error = input('');
  /** Ids that cannot be chosen again (already picked). */
  readonly excluded = input<readonly number[]>([]);
  /** Clears the text after a choice (for a "add to a list" picker). */
  readonly clearOnPick = input(false);
  /** The chosen employee, or null. */
  readonly selected = model<EmployeeLookupItem | null>(null);
  readonly picked = output<EmployeeLookupItem>();

  private readonly lookup = inject(EmployeeLookupDataService);
  protected readonly text = new FormControl<string | EmployeeLookupItem>('', { nonNullable: true });

  protected readonly results = toSignal(
    this.text.valueChanges.pipe(
      startWith(''),
      debounceTime(150),
      switchMap((value) =>
        typeof value === 'string'
          ? this.lookup
              .query({ search: value.trim(), pageIndex: 0, pageSize: PAGE_SIZE })
              // A failed search (a 403 included) shows no matches; a throw would break the signal.
              .pipe(
                map((page) => page.items),
                catchError(() => of([] as EmployeeLookupItem[])),
              )
          : EMPTY,
      ),
    ),
    { initialValue: [] as EmployeeLookupItem[] },
  );

  protected readonly display = (value: EmployeeLookupItem | string | null): string =>
    value && typeof value === 'object' ? `${value.employeeCode} · ${value.displayName}` : '';

  constructor() {
    // Editing the text after a choice drops the choice.
    this.text.valueChanges.subscribe((value) => {
      if (typeof value === 'string' && this.selected()) {
        this.selected.set(null);
      }
    });
    effect(() => {
      const message = this.error();
      this.text.setErrors(message ? { server: message } : null);
      if (message) {
        this.text.markAsTouched();
      }
    });
  }

  protected choose(event: MatAutocompleteSelectedEvent): void {
    const employee = event.option.value as EmployeeLookupItem;
    this.picked.emit(employee);
    if (this.clearOnPick()) {
      this.text.setValue('');
      return;
    }
    this.selected.set(employee);
  }
}
