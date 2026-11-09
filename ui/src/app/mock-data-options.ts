import { MockDataOptions } from './mock-services/provide-mock-data-services';

/**
 * How the mock data layer starts. Branch feature/empty-data: lists start empty and what you
 * enter is kept in the browser. The `e2e` build swaps this file for `mock-data-options.e2e.ts`.
 */
export const MOCK_DATA_OPTIONS: MockDataOptions = { emptyData: true, persist: true };
