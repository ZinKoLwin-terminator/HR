import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { DemoDataDataService } from '../../core/services/data/demo-data-data.service';
import { mockResponse } from '../mock-response';
import { isPersistenceEnabled, resetPersistedData } from '../persistence';

@Injectable({ providedIn: 'root' })
export class DemoDataMockService implements DemoDataDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;

  get canReset(): boolean {
    return isPersistenceEnabled();
  }

  reset(): Observable<void> {
    return mockResponse(() => resetPersistedData(), this.latency);
  }
}
