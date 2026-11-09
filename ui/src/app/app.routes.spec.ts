import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { SESSION_STORAGE_KEY } from './core/services/auth.service';
import { NAVIGATION } from './core/constants/navigation';
import { buildBreadcrumbs } from './layout/breadcrumbs/breadcrumbs.component';
import { provideMockDataServices } from './mock-services/provide-mock-data-services';

const navRoutes = NAVIGATION.flatMap((item) => [item, ...(item.children ?? [])])
  .map((item) => item.route)
  .filter((route): route is string => !!route);

describe('app routes', () => {
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideMockDataServices()],
    });
    router = TestBed.inject(Router);
    // Pages are behind the auth guard: start from a signed-in session.
    sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify({
        id: 1,
        employeeId: 2,
        displayName: 'Su Myat Noe',
        email: 'sumyat.noe@apexhorizon.example',
        jobTitle: '',
        roles: ['HR Administrator'],
      }),
    );
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideMockDataServices()],
    });
    router = TestBed.inject(Router);
  });

  afterEach(() => sessionStorage.clear());

  it('redirects the root to the dashboard', async () => {
    await router.navigateByUrl('/');
    expect(router.url).toBe('/dashboard');
  });

  it.each(navRoutes)('resolves sidebar route %s', async (url) => {
    expect(await router.navigateByUrl(url)).toBe(true);
    expect(router.url).toBe(url);
    const crumbs = buildBreadcrumbs(router.routerState.snapshot.root);
    expect(crumbs.at(-1)?.label).not.toBe('Not found');
  });

  it('shows not-found for unknown routes', async () => {
    await router.navigateByUrl('/does-not-exist');
    expect(buildBreadcrumbs(router.routerState.snapshot.root).at(-1)?.label).toBe('Not found');
  });

  it('redirects module roots to their first screen', async () => {
    await router.navigateByUrl('/organization');
    expect(router.url).toBe('/organization/companies');
  });
});
