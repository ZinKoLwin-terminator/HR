import { Injectable } from '@angular/core';
import { Observable, catchError, switchMap } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { EmployeeDocument } from '../../core/models/employee-extras.models';
import { EmployeeDocumentDataService } from '../../core/services/data/employee-extras-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  textOrNull,
} from '../recruitment/recruitment-http.base';

/**
 * HTTP implementation of the employee document contract (`/api/employee-documents`). Documents are
 * personal data: reads need `employees:ViewSensitive`, writes `employees:EditSensitive`. Only
 * metadata is sent and stored (no file contents). The profile tab and the documents screen page
 * through `/query?employeeId&type&search`. PUT carries the `rowVersion`; DELETE sends it as a body.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeDocumentHttpService
  extends RecruitmentHttpBase<EmployeeDocument>
  implements EmployeeDocumentDataService
{
  protected readonly resource = 'employee-documents';
  protected readonly filterKeys = ['employeeId', 'type'];
  protected readonly sortKeys = ['id', 'employeeId', 'type', 'title', 'uploadedOn', 'expiryDate'];

  protected normalise(raw: ApiShape<EmployeeDocument>): Versioned<EmployeeDocument> {
    return { ...raw, expiryDate: raw.expiryDate || null } as Versioned<EmployeeDocument>;
  }

  create(model: CreateModel<EmployeeDocument>): Observable<EmployeeDocument> {
    return this.post('', body(model)).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<EmployeeDocument>): Observable<EmployeeDocument> {
    const { rowVersion } = changes as { rowVersion?: string };
    return this.current(id).pipe(
      switchMap((current) =>
        this.put(id, body({ ...current, ...changes }), rowVersion ?? current.rowVersion ?? ''),
      ),
      catchError((e) => this.fail(e)),
    );
  }
}

function body(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  return {
    employeeId: Number(from['employeeId']),
    type: from['type'],
    title: from['title'],
    fileName: from['fileName'],
    uploadedOn: from['uploadedOn'],
    expiryDate: textOrNull(from['expiryDate']),
  };
}
