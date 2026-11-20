import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
import { CheckInComponent } from './check-in.component';

/** Renders the screen for Su Myat Noe (General Shift, Monday–Friday) on a fixed date. */
async function renderOn(isoDateTime: string) {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(isoDateTime));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
    ],
  });
  const fixture = TestBed.createComponent(CheckInComponent);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

const notice = (el: HTMLElement) => el.querySelector('.day-off[role="note"]');

describe('CheckInComponent', () => {
  afterEach(() => vi.useRealTimers());

  it('says why work on a holiday is recorded as Present', async () => {
    // 2026-11-20 is the sample holiday "Company Foundation Day".
    const el = await renderOn('2026-11-20T11:59:00');
    expect(notice(el)?.textContent).toContain('Today is a holiday: Company Foundation Day.');
    expect(notice(el)?.textContent).toContain('recorded as Present, with no late minutes');
  });

  it("says when today is not one of the shift's working days", async () => {
    const el = await renderOn('2026-11-21T10:00:00'); // Saturday
    expect(notice(el)?.textContent).toContain('Today is not a working day of the General Shift.');
  });

  it('shows no notice on an ordinary working day', async () => {
    const el = await renderOn('2026-11-24T10:00:00'); // Tuesday, no holiday
    expect(notice(el)).toBeNull();
  });
});
