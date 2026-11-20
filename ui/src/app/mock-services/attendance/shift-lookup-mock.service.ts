import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { ShiftRef } from '../../core/models/attendance.models';
import { ShiftLookupDataService } from '../../core/services/data/attendance-data.services';
import { mockResponse } from '../mock-response';
import { ShiftMockService } from './attendance-mock.services';

/**
 * Shift lookup (demo). Same shape as `GET /api/shifts/lookup`: every shift, inactive ones
 * included, ordered by name, without break or grace minutes or colour.
 */
@Injectable({ providedIn: 'root' })
export class ShiftLookupMockService implements ShiftLookupDataService {
  private readonly shifts = inject(ShiftMockService);
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;

  getLookup(): Observable<ShiftRef[]> {
    return mockResponse(
      () =>
        this.shifts
          .records()
          .map(({ id, code, name, startTime, endTime, workDays, isNightShift, status }) => ({
            id,
            code,
            name,
            startTime,
            endTime,
            workDays: [...workDays].sort((a, b) => a - b),
            isNightShift,
            status,
          }))
          .sort((a, b) => a.name.localeCompare(b.name) || a.id - b.id),
      this.latency,
    );
  }
}
