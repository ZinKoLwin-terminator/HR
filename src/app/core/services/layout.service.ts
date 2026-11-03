import { BreakpointObserver } from '@angular/cdk/layout';
import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

/** Responsive shell state: sidebar mode, collapse and mobile drawer (in-memory only). */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly breakpoints = inject(BreakpointObserver);

  /** Tablet portrait and below use an overlay drawer. */
  readonly isCompact = toSignal(
    this.breakpoints.observe('(max-width: 1023.98px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  private readonly collapsedState = signal(false);
  readonly mobileOpen = signal(false);

  /** Icon-only rail (desktop only). */
  readonly collapsed = computed(() => !this.isCompact() && this.collapsedState());

  toggleSidebar(): void {
    if (this.isCompact()) {
      this.mobileOpen.update((open) => !open);
    } else {
      this.collapsedState.update((value) => !value);
    }
  }

  expandSidebar(): void {
    this.collapsedState.set(false);
  }

  closeMobile(): void {
    this.mobileOpen.set(false);
  }
}
