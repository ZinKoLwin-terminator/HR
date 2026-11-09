import { HttpErrorResponse } from '@angular/common/http';
import { CONFLICT_RELOAD_MESSAGE, mapHttpError } from './http-error.mapper';

function failure(status: number, body?: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body });
}

describe('mapHttpError', () => {
  it('maps a 400 problem to Validation with the first message per field and the trace id', () => {
    const mapped = mapHttpError(
      failure(400, {
        detail: 'One or more fields are invalid.',
        code: 'Validation',
        errors: { Code: ['Code is required.', 'Too short.'], name: ['Name is required.'] },
        traceId: 'trace-1',
      }),
    );
    expect(mapped).toEqual({
      code: 'Validation',
      message: 'One or more fields are invalid.',
      fieldErrors: { code: 'Code is required.', name: 'Name is required.' },
      traceId: 'trace-1',
    });
  });

  it('maps 404 to NotFound with the API message', () => {
    const mapped = mapHttpError(
      failure(404, { detail: 'Company 9 was not found.', code: 'NotFound' }),
    );
    expect(mapped.code).toBe('NotFound');
    expect(mapped.message).toBe('Company 9 was not found.');
    expect(mapped.fieldErrors).toBeUndefined();
  });

  it('maps 409 to Conflict and asks the user to reload when the API gives no message', () => {
    const mapped = mapHttpError(failure(409, { code: 'Conflict' }));
    expect(mapped.code).toBe('Conflict');
    expect(mapped.message).toBe(CONFLICT_RELOAD_MESSAGE);
  });

  it('keeps a better 409 message from the API', () => {
    const mapped = mapHttpError(
      failure(409, { detail: 'Company has 3 branches.', code: 'Conflict' }),
    );
    expect(mapped.message).toBe('Company has 3 branches.');
  });

  it('maps 500 to Unknown and keeps the trace id', () => {
    const mapped = mapHttpError(failure(500, { detail: 'Unexpected error.', traceId: 't-9' }));
    expect(mapped).toEqual({ code: 'Unknown', message: 'Unexpected error.', traceId: 't-9' });
  });

  it('derives the code from the status when the body has none', () => {
    expect(mapHttpError(failure(404, 'plain text')).code).toBe('NotFound');
    expect(mapHttpError(failure(409)).code).toBe('Conflict');
    expect(mapHttpError(failure(403)).code).toBe('Forbidden');
    expect(mapHttpError(failure(401)).code).toBe('Unauthorized');
    expect(mapHttpError(failure(502)).code).toBe('Unknown');
  });

  it('reports an unreachable server and non-HTTP errors as Unknown', () => {
    expect(mapHttpError(failure(0)).code).toBe('Unknown');
    expect(mapHttpError(failure(0)).message).toContain('could not be reached');
    expect(mapHttpError(new Error('boom'))).toEqual({
      code: 'Unknown',
      message: 'Something went wrong. Please try again.',
    });
  });
});
