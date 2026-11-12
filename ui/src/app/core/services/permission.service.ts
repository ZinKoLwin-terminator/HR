import { Injectable, computed, inject } from '@angular/core';
import { CurrentUserService } from './current-user.service';

/** A server permission key: `module:Action`, e.g. `employees:ViewBank`. */
export type PermissionKey = string;

/**
 * The one place the UI asks "may the signed-in user do this?". This only decides what to SHOW
 * (menu entries, buttons, sections); the server is what enforces, and it answers 403 whatever the UI
 * shows.
 *
 * API build: fed from `permissions` in `GET /api/auth/me` (the keys the server holds for the user's
 * role). Mock build: the simulated session carries no permission list, so everything is allowed, as
 * before. A list that is present (even an empty one) is always honoured.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly user = inject(CurrentUserService);

  /** The granted keys, or `null` when the session has no list (mock build: allow everything). */
  private readonly granted = computed<ReadonlySet<string> | null>(() => {
    const list = this.user.current().permissions;
    return list ? new Set(list) : null;
  });

  /** True when a server-provided list is being applied. */
  readonly restricted = computed(() => this.granted() !== null);

  /** Reads a signal, so it can be used inside `computed()`, `effect()` and templates. */
  can(key: PermissionKey): boolean {
    const granted = this.granted();
    return granted === null || granted.has(key);
  }

  /** `can('employees:Create')` as `canIn('employees', 'Create')`. */
  canIn(module: string, action: string): boolean {
    return this.can(`${module}:${action}`);
  }

  /** True when at least one key is granted. An empty list means "nothing required". */
  canAny(keys: readonly PermissionKey[]): boolean {
    return keys.length === 0 || keys.some((key) => this.can(key));
  }
}
