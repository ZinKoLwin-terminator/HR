import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, from, map, switchMap, throwError } from 'rxjs';
import { GrievanceAttachmentDownload } from '../../core/models/grievance.models';
import { readFileBytes } from '../../core/utilities/kpi-evidence';
import { mapHttpError } from '../http-error.mapper';
import { fileNameFromDisposition } from '../kpi/kpi-http.util';

/** Used when a download answer carries no usable `Content-Disposition`. */
export const GRIEVANCE_FALLBACK_FILE_NAME = 'grievance-file';

/** Any HTTP failure as the UI's `AppError` (the API's problem details, a 429's text included). */
export function failGrievance(error: unknown): Observable<never> {
  return throwError(() => mapHttpError(error));
}

/**
 * An error answer to a Blob request is a Blob too: its problem body is read first so the message
 * and field errors come through as for any other call.
 */
function failFromBlob(error: unknown): Observable<never> {
  if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
    const original = error;
    return from(readFileBytes(error.error)).pipe(
      map((bytes) => {
        try {
          return JSON.parse(new TextDecoder().decode(bytes)) as unknown;
        } catch {
          return null;
        }
      }),
      switchMap((body) =>
        failGrievance(
          new HttpErrorResponse({
            error: body,
            status: original.status,
            statusText: original.statusText,
            url: original.url ?? undefined,
            headers: original.headers,
          }),
        ),
      ),
    );
  }
  return failGrievance(error);
}

/** A file download: the bytes as a Blob, the name from `Content-Disposition`. Never shown inline. */
export function downloadGrievanceFile(
  http: HttpClient,
  url: string,
): Observable<GrievanceAttachmentDownload> {
  return http.get(url, { observe: 'response', responseType: 'blob' }).pipe(
    map((response) => ({
      blob: response.body as Blob,
      fileName:
        fileNameFromDisposition(response.headers.get('Content-Disposition')) ??
        GRIEVANCE_FALLBACK_FILE_NAME,
    })),
    catchError((e) => failFromBlob(e)),
  );
}

/**
 * The multipart body of an upload: `rowVersion`, the optional extra fields (`visibility`) and
 * `file`. The browser sets the Content-Type with its boundary, so it is never set by hand.
 */
export function uploadForm(
  rowVersion: string,
  file: File,
  extra: Record<string, string> = {},
): FormData {
  const body = new FormData();
  body.append('rowVersion', rowVersion);
  for (const [key, value] of Object.entries(extra)) {
    body.append(key, value);
  }
  body.append('file', file, file.name);
  return body;
}
