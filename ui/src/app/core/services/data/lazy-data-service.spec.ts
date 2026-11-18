import { Injectable } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom, of } from 'rxjs';
import { KpiInsightHttpService } from '../../../api-services/kpi/kpi-insight-http.service';
import { KpiInsightMockService } from '../../../mock-services/kpi/kpi-insight-mock.service';
import { HR_ADMIN, configureKpiTest } from '../../../mock-services/kpi/kpi-test-support';
import { KPI_INSIGHT_METHODS, KpiInsightDataService } from './kpi-data.services';
import { lazyDataService } from './lazy-data-service';

@Injectable({ providedIn: 'root' })
class GreeterImpl {
  calls: string[] = [];
  greet(name: string, punctuation: string): Observable<string> {
    this.calls.push(name);
    return of(`Hello ${name}${punctuation}`);
  }
  count(): Observable<number> {
    return of(this.calls.length);
  }
}

describe('lazyDataService', () => {
  it('loads the implementation once, on the first subscription, and forwards every call', async () => {
    let loads = 0;
    const factory = lazyDataService<GreeterImpl>(() => {
      loads++;
      return Promise.resolve(GreeterImpl);
    }, ['greet', 'count']);
    const greeter = TestBed.runInInjectionContext(factory);

    expect(Object.keys(greeter).sort()).toEqual(['count', 'greet']);
    const pending = greeter.greet('Aye', '!');
    expect(loads).toBe(0); // nothing runs until it is subscribed

    expect(await firstValueFrom(pending)).toBe('Hello Aye!');
    expect(await firstValueFrom(greeter.greet('Mya', '.'))).toBe('Hello Mya.');
    expect(await firstValueFrom(greeter.count())).toBe(2);
    expect(loads).toBe(1);
    expect(TestBed.inject(GreeterImpl).calls).toEqual(['Aye', 'Mya']);
  });
});

describe('KPI insight lazy binding', () => {
  it('lists every method of the contract once, each implemented by the mock and the HTTP adapter', () => {
    expect(new Set(KPI_INSIGHT_METHODS).size).toBe(KPI_INSIGHT_METHODS.length);
    expect(KPI_INSIGHT_METHODS.length).toBe(9);
    for (const method of KPI_INSIGHT_METHODS) {
      expect(typeof KpiInsightMockService.prototype[method], method).toBe('function');
      expect(typeof KpiInsightHttpService.prototype[method], method).toBe('function');
    }
  });

  it('forwards the bound contract to the lazily loaded mock', async () => {
    configureKpiTest(HR_ADMIN);
    const insights = TestBed.inject(KpiInsightDataService);
    for (const method of KPI_INSIGHT_METHODS) {
      expect(typeof insights[method], method).toBe('function');
    }
    const dashboard = await firstValueFrom(insights.hrDashboard());
    expect(dashboard.period).not.toBeNull();
  });
});
