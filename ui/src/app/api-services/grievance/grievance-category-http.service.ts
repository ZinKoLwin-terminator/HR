import { Injectable } from '@angular/core';
import { GrievanceCategory } from '../../core/models/grievance.models';
import { GrievanceCategoryDataService } from '../../core/services/data/grievance-data.services';
import { HttpCrudService } from '../http-crud.service';

/**
 * Grievance categories (`/api/grievances/categories`, `MapCrud` with `grievance-management`). The
 * shared CRUD adapter sends every PUT and DELETE with the rowVersion and turns a status-only update
 * into `POST {id}/activate` or `deactivate` (the API's `MapStatusCommands`).
 */
@Injectable({ providedIn: 'root' })
export class GrievanceCategoryHttpService
  extends HttpCrudService<GrievanceCategory>
  implements GrievanceCategoryDataService
{
  protected readonly resource = 'grievances/categories';
  protected readonly filterKeys = ['status', 'allowsAnonymous'] as const;
  protected override readonly sortKeys = [
    'id',
    'code',
    'name',
    'sortOrder',
    'defaultTargetDays',
    'status',
  ] as const;
}
