import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { routes } from '../../app.routes';
import { DEMO_PASSWORD } from '../../mock-services/auth/auth-mock.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { AuthService, SESSION_STORAGE_KEY } from './auth.service';
import { CurrentUserService } from './current-user.service';

const ADMIN_EMAIL = 'sumyat.noe@apexhorizon.example';

describe('authentication', () => {
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideMockDataServices()],
    });
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  it('signs in with valid credentials (case-insensitive email)', async () => {
    const user = await firstValueFrom(auth.signIn(` ${ADMIN_EMAIL.toUpperCase()} `, DEMO_PASSWORD));
    expect(user.roles).toEqual(['HR Administrator']);
    expect(auth.isAuthenticated()).toBe(true);
    expect(TestBed.inject(CurrentUserService).current().email).toBe(ADMIN_EMAIL);
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).not.toBeNull();
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('persists to localStorage when "remember me" is set', async () => {
    await firstValueFrom(auth.signIn(ADMIN_EMAIL, DEMO_PASSWORD, true));
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).not.toBeNull();
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('uses one message for a wrong password and an unknown email', async () => {
    const wrongPassword = firstValueFrom(auth.signIn(ADMIN_EMAIL, 'nope'));
    const unknownEmail = firstValueFrom(auth.signIn('nobody@apexhorizon.example', DEMO_PASSWORD));
    await expect(wrongPassword).rejects.toMatchObject({ message: 'Incorrect email or password.' });
    await expect(unknownEmail).rejects.toMatchObject({ message: 'Incorrect email or password.' });
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('rejects locked and deactivated accounts', async () => {
    const { MOCK_USERS } = await import('../../mock-data/settings/settings.mock');
    const locked = MOCK_USERS.find((u) => u.status === 'Locked')!;
    const inactive = MOCK_USERS.find((u) => u.status === 'Inactive')!;
    await expect(firstValueFrom(auth.signIn(locked.email, DEMO_PASSWORD))).rejects.toMatchObject({
      message: expect.stringContaining('locked'),
    });
    await expect(firstValueFrom(auth.signIn(inactive.email, DEMO_PASSWORD))).rejects.toMatchObject({
      message: expect.stringContaining('deactivated'),
    });
  });

  it('signs out, clears storage and returns to /login', async () => {
    await firstValueFrom(auth.signIn(ADMIN_EMAIL, DEMO_PASSWORD, true));
    auth.signOut();
    await TestBed.inject(Router).navigateByUrl('/login');
    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('redirects signed-out visitors to /login with a return URL', async () => {
    await router.navigateByUrl('/employees/list');
    expect(router.url).toBe('/login?returnUrl=%2Femployees%2Flist');
  });

  it('keeps signed-in users off /login', async () => {
    await firstValueFrom(auth.signIn(ADMIN_EMAIL, DEMO_PASSWORD));
    await router.navigateByUrl('/login');
    expect(router.url).toBe('/dashboard');
  });
});
