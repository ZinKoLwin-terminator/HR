import { ApiDataOptions } from './api-services/provide-api-data-services';

/**
 * The HTTP adapter switch. Off by default: the app uses the in-memory mocks (npm start, unit
 * tests, e2e, GitHub Pages). The `api` build configuration swaps this file for
 * `api-data-options.api.ts` (npm run start:api).
 */
export const API_DATA_OPTIONS: ApiDataOptions = { enabled: false };
