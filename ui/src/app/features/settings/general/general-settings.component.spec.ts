import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SERVER_SESSION } from '../../../core/services/auth.service';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
import { GeneralSettingsComponent } from './general-settings.component';

async function render(serverSession: boolean): Promise<string> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: SERVER_SESSION, useValue: serverSession },
    ],
  });
  const fixture = TestBed.createComponent(GeneralSettingsComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

describe('GeneralSettingsComponent', () => {
  it('shows accurate server text in API mode', async () => {
    const text = await render(true);
    expect(text).toContain('saved on the server');
    expect(text).not.toContain('Demo only');
  });

  it('keeps the demo text in the mock build', async () => {
    const text = await render(false);
    expect(text).toContain('Demo only: saved in this browser');
  });
});
