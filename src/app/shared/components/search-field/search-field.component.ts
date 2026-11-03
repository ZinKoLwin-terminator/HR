import { Component, effect, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { debounce, distinctUntilChanged, map, skip, timer } from 'rxjs';

/** Debounced search box. Emits the trimmed term. */
@Component({
  selector: 'app-search-field',
  imports: [MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule],
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search-field">
      <mat-icon matPrefix aria-hidden="true">search</mat-icon>
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        type="search"
        [placeholder]="placeholder()"
        [value]="term()"
        (input)="term.set($any($event.target).value)"
        (keydown.escape)="term.set('')"
      />
      @if (term()) {
        <button
          matIconSuffix
          mat-icon-button
          type="button"
          aria-label="Clear search"
          (click)="term.set('')"
        >
          <mat-icon>close</mat-icon>
        </button>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
      min-width: 220px;
    }
    .search-field {
      width: 100%;
    }
  `,
})
export class SearchFieldComponent {
  readonly label = input('Search');
  readonly placeholder = input('');
  readonly value = input('');
  readonly debounceMs = input(300);
  readonly searchChange = output<string>();

  protected readonly term = signal('');

  constructor() {
    // Keep in sync when the parent resets the value.
    effect(() => this.term.set(this.value()));

    toObservable(this.term)
      .pipe(
        skip(1),
        debounce(() => timer(this.debounceMs())),
        map((value) => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((value) => this.searchChange.emit(value));
  }
}
