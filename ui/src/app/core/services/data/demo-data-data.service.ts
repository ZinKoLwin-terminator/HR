import { Observable } from 'rxjs';

/**
 * Housekeeping for the frontend-only demo. A real API has nothing to reset, so its implementation
 * reports `canReset = false` and the Reset button is hidden.
 */
export abstract class DemoDataDataService {
  /** True when records entered by the user are kept between visits and can be thrown away. */
  abstract readonly canReset: boolean;
  /** Discards everything the user created or changed and restores the starting data. */
  abstract reset(): Observable<void>;
}
