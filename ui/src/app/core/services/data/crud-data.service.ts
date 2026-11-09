import { Observable } from 'rxjs';
import {
  CreateModel,
  Entity,
  PagedResult,
  PageRequest,
  UpdateModel,
} from '../../models/common.models';

/**
 * Contract every data service fulfils. UI code depends ONLY on these abstract
 * classes (used as DI tokens). The current implementations are in-memory mocks
 * (`src/app/mock-services`); a future phase can provide HTTP implementations
 * against the same contract without changing components.
 *
 * Errors are emitted as `AppError` objects.
 */
export abstract class CrudDataService<
  T extends Entity,
  TList extends Entity = T,
  TFilter extends object = Record<string, unknown>,
> {
  abstract getAll(): Observable<T[]>;
  abstract getById(id: number): Observable<T>;
  abstract query(request: PageRequest<TFilter>): Observable<PagedResult<TList>>;
  abstract create(model: CreateModel<T>): Observable<T>;
  abstract update(id: number, changes: UpdateModel<T>): Observable<T>;
  abstract delete(id: number): Observable<void>;
}
