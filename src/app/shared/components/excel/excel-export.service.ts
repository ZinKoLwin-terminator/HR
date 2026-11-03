import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { ExcelExportDialogComponent, ExcelExportDialogData } from './excel-export-dialog.component';

/** Opens the Excel export dialog for a module (used by list screens). */
@Injectable({ providedIn: 'root' })
export class ExcelExportService {
  private readonly dialog = inject(MatDialog);

  /** Emits `true` when a file was exported. */
  open(data: ExcelExportDialogData): Observable<boolean | undefined> {
    return this.dialog
      .open<ExcelExportDialogComponent, ExcelExportDialogData, boolean>(
        ExcelExportDialogComponent,
        {
          data,
          width: '640px',
          maxWidth: '96vw',
          autoFocus: 'first-tabbable',
        },
      )
      .afterClosed();
  }
}
