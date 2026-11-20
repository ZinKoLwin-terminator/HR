import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { OrganizationLookup } from '../../core/models/organization.models';
import { OrganizationLookupDataService } from '../../core/services/data/organization-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/**
 * HTTP implementation of the organization lookup: `GET /api/organization/lookup` (any signed-in
 * user). Every organization row with only its id, code, name, parent ids and status.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationLookupHttpService implements OrganizationLookupDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getLookup(): Observable<OrganizationLookup> {
    return this.http
      .get<OrganizationLookup>(`${this.baseUrl}/organization/lookup`)
      .pipe(catchError((e) => throwError(() => mapHttpError(e))));
  }
}
