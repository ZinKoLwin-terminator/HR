import { MockDataOptions } from './mock-services/provide-mock-data-services';

/**
 * End-to-end tests run against the sample data, and every page load starts from it again
 * (no persistence), so each test begins from the same known state.
 */
export const MOCK_DATA_OPTIONS: MockDataOptions = {};
