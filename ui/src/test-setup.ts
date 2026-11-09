import { SESSION_STORAGE_KEY } from './app/core/services/auth.service';

/**
 * Unit tests run as the sample HR administrator (the identity the specs assert on).
 * Specs that test signed-out behaviour clear the storage themselves.
 */
beforeEach(() => {
  sessionStorage.setItem(
    SESSION_STORAGE_KEY,
    JSON.stringify({
      id: 1,
      employeeId: 2,
      displayName: 'Su Myat Noe',
      email: 'sumyat.noe@apexhorizon.example',
      jobTitle: 'Director of Human Resources',
      roles: ['HR Administrator'],
    }),
  );
});

afterEach(() => {
  sessionStorage.clear();
  localStorage.clear();
});
