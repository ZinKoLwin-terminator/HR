import { Injectable } from '@angular/core';
import { Holiday, Shift } from '../../core/models/attendance.models';
import { RecordStatus } from '../../core/models/common.models';
import {
  HolidayDataService,
  ShiftDataService,
} from '../../core/services/data/attendance-data.services';
import { ApiEntity, HttpCrudService } from '../http-crud.service';

// HTTP implementations of the shift and holiday contracts. Shift status changes go through the
// shared activate/deactivate commands (never a partial PUT) and every PUT/DELETE carries the
// rowVersion (see HttpCrudService).

@Injectable({ providedIn: 'root' })
export class ShiftHttpService extends HttpCrudService<Shift> implements ShiftDataService {
  protected readonly resource = 'shifts';
  protected readonly filterKeys = ['status'] as const;
  /** The API cannot sort by break, grace, work days or colour: those clicks keep the default order. */
  protected override readonly sortKeys = [
    'id',
    'code',
    'name',
    'startTime',
    'endTime',
    'status',
  ] as const;
}

/** A holiday has no status on the API; the shared base only needs the field to exist in its type. */
type HolidayApi = Holiday & ApiEntity & { status?: RecordStatus };

@Injectable({ providedIn: 'root' })
export class HolidayHttpService extends HttpCrudService<HolidayApi> implements HolidayDataService {
  protected readonly resource = 'holidays';
  protected readonly filterKeys = ['dateFrom', 'dateTo', 'type'] as const;
  protected override readonly sortKeys = ['id', 'date', 'name', 'type'] as const;
}
