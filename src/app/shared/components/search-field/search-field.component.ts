import { Component, effect, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { debounce, distinctUntilChanged, map, skip, timer } from 'rxjs';

/** Debounced search box (pill input). Emits the trimmed term. */
@Component({
  selector: 'app-search-field',
  imports: [MatIconModule],
  template: `
    <div class="search">
      <mat-icon class="search__icon" aria-hidden="true">search</mat-icon>
      <input
        class="search__input"
        type="search"
        [attr.aria-label]="label()"
        [placeholder]="placeholder() || label()"
        [value]="term()"
        autocomplete="off"
        (input)="term.set($any($event.target).value)"
        (keydown.escape)="term.set('')"
      />
      @if (term()) {
        <button
          type="button"
          class="search__clear"
          aria-label="Clear search"
          (click)="term.set('')"
        >
          <mat-icon aria-hidden="true">close</mat-icon>
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-width: 220px;
    }
    .search {
      display: flex;
      align-items: center;
      gap: 8px;
      height: 44px;
      padding-inline: 14px 6px;
      border-radius: 999px;
      border: 1px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface-container-low);
      transition:
        border-color 120ms ease,
        background-color 120ms ease,
        box-shadow 120ms ease;
    }
    .search:hover {
      border-color: var(--mat-sys-outline);
    }
    .search:focus-within {
      border-color: var(--mat-sys-primary);
      background: var(--mat-sys-surface-container-lowest);
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--mat-sys-primary) 18%, transparent);
    }
    .search__icon {
      flex: none;
      color: var(--mat-sys-on-surface-variant);
    }
    .search__input {
      flex: 1;
      min-width: 0;
      height: 100%;
      border: 0;
      outline: 0;
      background: transparent;
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-body-large);
    }
    .search__input::placeholder {
      color: var(--mat-sys-on-surface-variant);
      opacity: 1;
    }
    .search__input::-webkit-search-cancel-button {
      display: none;
    }
    .search__clear {
      flex: none;
      display: grid;
      place-items: center;
      width: 32px;
      height: 32px;
      border: 0;
      border-radius: 50%;
      background: transparent;
      color: var(--mat-sys-on-surface-variant);
      cursor: pointer;
    }
    .search__clear:hover {
      background: var(--mat-sys-surface-container-highest);
    }
    .search__clear .mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
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
