import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ShiftRef } from '../../core/models/attendance.models';
import { ShiftLookupDataService } from '../../core/services/data/attendance-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/**
 * HTTP implementation of the shift lookup: `GET /api/shifts/lookup` (any signed-in user). Every
 * shift with its id, code, name, times ("HH:mm"), work days (0 = Sunday), night flag and status.
 */
@Injectable({ providedIn: 'root' })
export class ShiftLookupHttpService implements ShiftLookupDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getLookup(): Observable<ShiftRef[]> {
    return this.http
      .get<ShiftRef[]>(`${this.baseUrl}/shifts/lookup`)
      .pipe(catchError((e) => throwError(() => mapHttpError(e))));
  }
}
