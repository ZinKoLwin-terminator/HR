import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  CreateModel,
  PagedResult,
  PageRequest,
  UpdateModel,
} from '../../core/models/common.models';
import { SalaryComponent, SalaryStructure } from '../../core/models/payroll.models';
import {
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../../core/services/data/payroll-data.services';
import { HttpCrudService } from '../http-crud.service';

/** The API stores no empty text, so an optional description arrives as null. */
function withDescription<T extends { description?: string | null }>(item: T): T {
  return { ...item, description: item.description ?? '' };
}

/**
 * HTTP implementation of the salary component contract. Create sends the starting status; update
 * is a PUT without the status (status changes use the activate and deactivate commands), as in
 * `HttpCrudService`. Delete needs `payroll:Edit` (the module has no Delete permission).
 */
@Injectable({ providedIn: 'root' })
export class SalaryComponentHttpService
  extends HttpCrudService<SalaryComponent>
  implements SalaryComponentDataService
{
  protected readonly resource = 'salary-components';
  protected readonly filterKeys = ['status', 'type', 'calculation'] as const;
  protected override readonly sortKeys = [
    'id',
    'code',
    'name',
    'type',
    'calculation',
    'value',
    'status',
  ] as const;

  override getAll(): Observable<SalaryComponent[]> {
    return super.getAll().pipe(map((items) => items.map(withDescription)));
  }

  override create(model: CreateModel<SalaryComponent>): Observable<SalaryComponent> {
    return super.create(model).pipe(map(withDescription));
  }

  override update(id: number, changes: UpdateModel<SalaryComponent>): Observable<SalaryComponent> {
    return super.update(id, changes).pipe(map(withDescription));
  }

  override getById(id: number): Observable<SalaryComponent> {
    return super.getById(id).pipe(map(withDescription));
  }

  override query(
    request: PageRequest<Record<string, unknown>>,
  ): Observable<PagedResult<SalaryComponent>> {
    return super
      .query(request)
      .pipe(map((page) => ({ ...page, items: page.items.map(withDescription) })));
  }
}

/** HTTP implementation of the salary structure contract (`componentIds` keep their display order). */
@Injectable({ providedIn: 'root' })
export class SalaryStructureHttpService
  extends HttpCrudService<SalaryStructure>
  implements SalaryStructureDataService
{
  protected readonly resource = 'salary-structures';
  protected readonly filterKeys = ['status'] as const;
  protected override readonly sortKeys = ['id', 'code', 'name', 'status'] as const;

  override getAll(): Observable<SalaryStructure[]> {
    return super.getAll().pipe(map((items) => items.map(withDescription)));
  }

  override create(model: CreateModel<SalaryStructure>): Observable<SalaryStructure> {
    return super.create(model).pipe(map(withDescription));
  }

  override update(id: number, changes: UpdateModel<SalaryStructure>): Observable<SalaryStructure> {
    return super.update(id, changes).pipe(map(withDescription));
  }

  override getById(id: number): Observable<SalaryStructure> {
    return super.getById(id).pipe(map(withDescription));
  }

  override query(
    request: PageRequest<Record<string, unknown>>,
  ): Observable<PagedResult<SalaryStructure>> {
    return super
      .query(request)
      .pipe(map((page) => ({ ...page, items: page.items.map(withDescription) })));
  }
}
