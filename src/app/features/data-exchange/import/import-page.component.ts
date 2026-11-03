import { Component, input } from '@angular/core';
import { ExcelImportWizardComponent } from '../../../shared/components/excel/excel-import-wizard.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

/** Import data from Excel: choose a module, download its template, upload, review and import. */
@Component({
  selector: 'app-import-page',
  imports: [PageHeaderComponent, ExcelImportWizardComponent],
  template: `
    <app-page-header
      title="Import data"
      description="Import records from an Excel file. You will see a full preview before anything is changed. All data in this demo is sample data."
    />
    <app-excel-import-wizard [initialModule]="module() ?? null" />
  `,
})
export class ImportPageComponent {
  /** `?module=` query parameter (set by the Import button of a list screen). */
  readonly module = input<string>();
}
