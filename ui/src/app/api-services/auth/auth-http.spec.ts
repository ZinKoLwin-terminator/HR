import { HttpClient, HttpStatusCode } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { AuthService, SESSION_STORAGE_KEY } from '../../core/services/auth.service';
import { CurrentUserService } from '../../core/services/current-user.service';
import { UiNotificationService } from '../../core/services/ui-notification.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';

const API_USER = {
  id: 5,
  employeeId: 12,
  displayName: 'Aye Chan',
  email: 'aye.chan@apexhorizon.example',
  roleName: 'HR Administrator',
  permissions: ['employees.view', 'employees.edit'],
};

const GENERIC = 'Incorrect email or password.';

/** Builds the API-mode app. The startup `/me` call is answered with the given status. */
function setup(startup: { status: number; body?: unknown } = { status: 401 }) {
  sessionStorage.clear();
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      provideHttpClientTesting(),
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  const auth = TestBed.inject(AuthService);
  const initialized = auth.initialize();
  // The app initializer and the explicit call above each ask who is signed in.
  http.match('/api/auth/me').forEach((r) =>
    r.flush(startup.body ?? { detail: 'Sign in', code: 'Unauthorized' }, {
      status: startup.status,
      statusText: 'x',
    }),
  );
  return { http, auth, initialized };
}

/** Answers the anti-forgery GETs that surround login and logout. */
function flushAntiforgery(http: HttpTestingController): void {
  http.expectOne('/api/auth/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
}

describe('API sign-in', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('treats a 401 from /me at startup as signed out', async () => {
    const { auth, initialized } = setup({ status: 401 });
    await initialized;
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('restores the session from /me at startup, in memory only', async () => {
    const { auth, initialized } = setup({ status: 200, body: API_USER });
    await initialized;
    expect(auth.isAuthenticated()).toBe(true);
    expect(TestBed.inject(CurrentUserService).current().permissions).toEqual(API_USER.permissions);
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('login success keeps the session in memory only', async () => {
    const { http, auth } = setup();
    const result = firstValueFrom(auth.signIn(API_USER.email, 'a-long-password-1', true));
    flushAntiforgery(http);
    http.expectOne('/api/auth/login').flush(API_USER);
    flushAntiforgery(http);
    const user = await result;

    expect(user).toMatchObject({ displayName: 'Aye Chan', roles: ['HR Administrator'] });
    expect(auth.isAuthenticated()).toBe(true);
    expect(sessionStorage.length).toBe(0);
    expect(localStorage.length).toBe(0);
  });

  it('sends email, password and rememberMe in the body', async () => {
    const { http, auth } = setup();
    for (const remember of [true, false]) {
      const result = firstValueFrom(auth.signIn(API_USER.email, 'a-long-password-1', remember));
      flushAntiforgery(http);
      const req = http.expectOne('/api/auth/login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        email: API_USER.email,
        password: 'a-long-password-1',
        rememberMe: remember,
      });
      req.flush(API_USER);
      flushAntiforgery(http);
      await result;
    }
  });

  it('login failure surfaces the API generic message and no session', async () => {
    const { http, auth } = setup();
    const result = firstValueFrom(auth.signIn(API_USER.email, 'wrong'));
    flushAntiforgery(http);
    http
      .expectOne('/api/auth/login')
      .flush(
        { detail: GENERIC, code: 'Unauthorized', traceId: 't1' },
        { status: HttpStatusCode.Unauthorized, statusText: 'Unauthorized' },
      );

    await expect(result).rejects.toMatchObject({ code: 'Unauthorized', message: GENERIC });
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('logout calls the endpoint, clears the session and goes to /login', async () => {
    const { http, auth, initialized } = setup({ status: 200, body: API_USER });
    await initialized;
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    auth.signOut();
    expect(auth.isAuthenticated()).toBe(false);
    http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    flushAntiforgery(http);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('forgot-password and reset-password post to their endpoints', async () => {
    const { http, auth } = setup();
    const forgot = firstValueFrom(auth.forgotPassword('x@apexhorizon.example'));
    flushAntiforgery(http);
    const f = http.expectOne('/api/auth/forgot-password');
    expect(f.request.body).toEqual({ email: 'x@apexhorizon.example' });
    f.flush({ message: 'ok' }, { status: 202, statusText: 'Accepted' });
    await forgot;

    const reset = firstValueFrom(
      auth.resetPassword('x@apexhorizon.example', 'tok', 'twelve-chars-ok'),
    );
    flushAntiforgery(http);
    const r = http.expectOne('/api/auth/reset-password');
    expect(r.request.body).toEqual({
      email: 'x@apexhorizon.example',
      token: 'tok',
      newPassword: 'twelve-chars-ok',
    });
    r.flush(null, { status: 204, statusText: 'No Content' });
    await reset;
  });
});

describe('API auth interceptor', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  async function signedIn() {
    const ctx = setup({ status: 200, body: API_USER });
    await ctx.initialized;
    return ctx;
  }

  it('on 401 clears the session and redirects to /login with the return url', async () => {
    const { http, auth } = await signedIn();
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/employees/list');
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const result = firstValueFrom(TestBed.inject(HttpClient).get('/api/companies'));
    http.expectOne('/api/companies').flush(null, { status: 401, statusText: 'Unauthorized' });
    await expect(result).rejects.toBeDefined();

    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/employees/list' },
    });
  });

  it('does not redirect for a failed sign-in (401 from /auth/login)', async () => {
    const { http, auth } = setup();
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const result = firstValueFrom(auth.signIn('a@apexhorizon.example', 'bad'));
    flushAntiforgery(http);
    http.expectOne('/api/auth/login').flush({ detail: GENERIC }, { status: 401, statusText: 'x' });
    await expect(result).rejects.toBeDefined();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('on 403 shows a forbidden message and keeps the session', async () => {
    const { http, auth } = await signedIn();
    const toast = vi
      .spyOn(TestBed.inject(UiNotificationService), 'error')
      .mockImplementation(() => undefined);
    const result = firstValueFrom(TestBed.inject(HttpClient).get('/api/companies'));
    http
      .expectOne('/api/companies')
      .flush(
        { detail: 'You do not have permission to do that.', code: 'Forbidden' },
        { status: 403, statusText: 'Forbidden' },
      );
    const error = (await result.catch((e: unknown) => e)) as { status?: number } | AppError;

    expect(toast).toHaveBeenCalledWith(
      'You do not have permission to do that.',
      expect.any(String),
    );
    expect(error).toMatchObject({ status: 403 });
    expect(auth.isAuthenticated()).toBe(true);
  });
});
