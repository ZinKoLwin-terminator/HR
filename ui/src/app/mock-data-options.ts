import { MockDataOptions } from './mock-services/provide-mock-data-services';

/**
 * How the mock data layer starts: every module loads the full fictional sample data (for demos),
 * and what you enter is kept in the browser until Settings > General > Reset data. Set
 * `emptyData: true` to start with empty lists instead. The `e2e` build swaps this file for
 * `mock-data-options.e2e.ts`.
 */
export const MOCK_DATA_OPTIONS: MockDataOptions = { persist: true };
