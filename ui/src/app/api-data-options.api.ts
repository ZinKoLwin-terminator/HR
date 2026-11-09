import { ApiDataOptions } from './api-services/provide-api-data-services';

/** Used only by the `api` build configuration: Organization data comes from the .NET API. */
export const API_DATA_OPTIONS: ApiDataOptions = { enabled: true, baseUrl: '/api' };
