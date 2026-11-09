import { Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import {
  ColumnMapping,
  ExcelModuleInfo,
  FileInspection,
  IMPORT_MODE_LABELS,
  IMPORT_STRATEGIES,
  IMPORT_STRATEGY_LABELS,
  ImportMode,
  ImportPreview,
  ImportResult,
  ImportStrategy,
} from '../../../core/models/excel.models';
import {
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../../../core/services/data/excel-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { EmptyStateComponent } from '../empty-state/empty-state.component';
import { ExcelColumnMappingComponent } from './excel-column-mapping.component';
import { ExcelFileUploadComponent } from './excel-file-upload.component';
import { ExcelImportPreviewComponent } from './excel-import-preview.component';
import { ExcelImportResultComponent } from './excel-import-result.component';
import { ExcelValidationSummaryComponent } from './excel-validation-summary.component';

const STEP = { template: 0, upload: 1, columns: 2, review: 3, confirm: 4, result: 5 } as const;

/**
 * Reusable import wizard for any registered module:
 * Template → Upload → Columns → Review (validation + preview) → Confirm → Result.
 */
@Component({
  selector: 'app-excel-import-wizard',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatRadioModule,
    MatSelectModule,
    MatStepperModule,
    EmptyStateComponent,
    ExcelFileUploadComponent,
    ExcelColumnMappingComponent,
    ExcelValidationSummaryComponent,
    ExcelImportPreviewComponent,
    ExcelImportResultComponent,
  ],
  templateUrl: './excel-import-wizard.component.html',
  styleUrl: './excel-import-wizard.component.scss',
})
export class ExcelImportWizardComponent {
  /** Module to preselect (e.g. from the `?module=` query parameter). */
  readonly initialModule = input<string | null>(null);

  private readonly catalogApi = inject(ExcelModuleCatalogDataService);
  private readonly importApi = inject(ExcelImportDataService);
  private readonly toast = inject(UiNotificationService);
  protected readonly limits = inject(APP_CONFIG).excel;

  protected readonly catalog = rxResource({ stream: () => this.catalogApi.getModules() });
  protected readonly modules = computed(() =>
    (this.catalog.value() ?? []).filter((m) => m.canImport),
  );

  protected readonly strategies = IMPORT_STRATEGIES;
  protected readonly strategyLabels = IMPORT_STRATEGY_LABELS;
  protected readonly modeLabels = IMPORT_MODE_LABELS;

  protected readonly stepIndex = signal<number>(STEP.template);
  protected readonly moduleCode = signal<string | null>(null);
  protected readonly strategy = signal<ImportStrategy>('InsertOrUpdate');
  protected readonly file = signal<File | null>(null);
  protected readonly inspection = signal<FileInspection | null>(null);
  protected readonly mapping = signal<ColumnMapping>({});
  protected readonly preview = signal<ImportPreview | null>(null);
  protected readonly mode = signal<ImportMode>('AllOrNothing');
  protected readonly result = signal<ImportResult | null>(null);

  protected readonly busy = signal(false);
  protected readonly uploadError = signal('');
  protected readonly reviewError = signal('');
  protected readonly confirmError = signal('');

  protected readonly info = computed<ExcelModuleInfo | undefined>(() =>
    this.modules().find((m) => m.code === this.moduleCode()),
  );

  /** Problems with the current mapping that stop the file from being validated. */
  protected readonly mappingBlocked = computed(() => {
    const info = this.info();
    if (!info) return true;
    const mapped = new Set(Object.values(this.mapping()).filter((k): k is string => !!k));
    const values = Object.values(this.mapping()).filter((k): k is string => !!k);
    return (
      info.columns.some((c) => c.required && !mapped.has(c.key)) || values.length !== mapped.size
    );
  });

  protected readonly problemRows = computed(() => {
    const preview = this.preview();
    return preview ? preview.invalidRows + preview.duplicateRows : 0;
  });
  protected readonly plan = computed(() => {
    const rows = this.preview()?.rows ?? [];
    return {
      insert: rows.filter((r) => r.action === 'Insert').length,
      update: rows.filter((r) => r.action === 'Update').length,
      skipped: rows.filter((r) => r.action === 'None').length,
    };
  });
  protected readonly shownKeys = computed(() => {
    const info = this.info();
    if (!info) return [];
    const keys = [
      ...info.matchKey,
      ...info.columns.filter((c) => c.required && !info.matchKey.includes(c.key)).map((c) => c.key),
    ];
    return keys.slice(0, 4);
  });
  protected readonly canImport = computed(() => (this.preview()?.validRows ?? 0) > 0);

  constructor() {
    // Preselect the module once the catalog has loaded.
    effect(() => {
      const wanted = this.initialModule();
      const modules = this.modules();
      if (this.moduleCode() === null && modules.length) {
        const match =
          modules.find((m) => m.code === wanted) ?? (modules.length === 1 ? modules[0] : undefined);
        if (match) this.moduleCode.set(match.code);
      }
    });
  }

  protected chooseModule(code: string): void {
    this.moduleCode.set(code);
    this.resetFile();
  }

  protected chooseStrategy(strategy: ImportStrategy): void {
    this.strategy.set(strategy);
    // The preview depends on the strategy, so a new validation is needed.
    this.preview.set(null);
  }

  protected downloadTemplate(): void {
    const code = this.moduleCode();
    if (!code) return;
    this.importApi.getTemplate(code).subscribe({
      next: (file) => downloadFile(file),
      error: (error: unknown) => this.toast.error(error),
    });
  }

  protected setFile(file: File | null): void {
    this.file.set(file);
    this.inspection.set(null);
    this.preview.set(null);
    this.uploadError.set('');
  }

  protected checkFile(): void {
    const code = this.moduleCode();
    const file = this.file();
    if (!code || !file) return;
    this.busy.set(true);
    this.uploadError.set('');
    this.importApi.inspect(code, file).subscribe({
      next: (inspection) => {
        this.busy.set(false);
        this.inspection.set(inspection);
        this.mapping.set({ ...inspection.mapping });
        this.preview.set(null);
        this.stepIndex.set(STEP.columns);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.uploadError.set(errorMessage(error));
      },
    });
  }

  protected setMapping(mapping: ColumnMapping): void {
    this.mapping.set(mapping);
    this.preview.set(null);
  }

  protected validate(): void {
    const inspection = this.inspection();
    if (!inspection) return;
    this.busy.set(true);
    this.reviewError.set('');
    this.importApi
      .validate({
        uploadId: inspection.uploadId,
        strategy: this.strategy(),
        mapping: this.mapping(),
      })
      .subscribe({
        next: (preview) => {
          this.busy.set(false);
          this.preview.set(preview);
          this.mode.set(
            preview.invalidRows + preview.duplicateRows > 0 ? 'Partial' : 'AllOrNothing',
          );
          this.stepIndex.set(STEP.review);
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.reviewError.set(errorMessage(error));
        },
      });
  }

  protected goTo(step: number): void {
    this.stepIndex.set(step);
  }

  private readonly stepper = viewChild(MatStepper);

  /** A step can be opened once everything before it is done (the header cannot skip ahead). */
  private reachable(step: number): boolean {
    switch (step) {
      case STEP.template:
        return true;
      case STEP.upload:
        return !!this.info();
      case STEP.columns:
        return !!this.inspection();
      case STEP.review:
        return !!this.preview();
      case STEP.confirm:
        return this.canImport();
      default:
        return !!this.result();
    }
  }

  /** Handles header clicks and programmatic moves: refuses a step that is not reachable yet. */
  protected onStepChange(index: number): void {
    if (this.result() && index !== STEP.result) {
      // a finished import cannot be re-opened step by step; start another one instead
      queueMicrotask(() => this.syncStepper());
      return;
    }
    if (!this.reachable(index)) {
      queueMicrotask(() => this.syncStepper());
      return;
    }
    this.stepIndex.set(index);
  }

  private syncStepper(): void {
    const stepper = this.stepper();
    if (stepper && stepper.selectedIndex !== this.stepIndex()) {
      stepper.selectedIndex = this.stepIndex();
    }
  }

  protected uploadCorrected(): void {
    this.resetFile();
    this.stepIndex.set(STEP.upload);
  }

  protected execute(): void {
    const preview = this.preview();
    if (!preview) return;
    this.busy.set(true);
    this.confirmError.set('');
    this.importApi.execute(preview.batchId, this.mode()).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.result.set(result);
        this.stepIndex.set(STEP.result);
        if (result.status === 'Completed') this.toast.success(result.message);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.confirmError.set(errorMessage(error));
      },
    });
  }

  protected downloadReport(): void {
    const batchId = this.result()?.batchId ?? this.preview()?.batchId;
    if (!batchId) return;
    this.importApi.getErrorReport(batchId).subscribe({
      next: (file) => downloadFile(file),
      error: (error: unknown) => this.toast.error(error),
    });
  }

  protected startAgain(): void {
    this.moduleCode.set(null);
    this.resetFile();
    this.result.set(null);
    this.stepIndex.set(STEP.template);
    // keep the module list, but clear a single-module auto selection so the user sees step 1 again
    queueMicrotask(() => {
      const modules = this.modules();
      if (modules.length === 1) this.moduleCode.set(modules[0].code);
    });
  }

  private resetFile(): void {
    this.file.set(null);
    this.inspection.set(null);
    this.preview.set(null);
    this.result.set(null);
    this.uploadError.set('');
    this.reviewError.set('');
    this.confirmError.set('');
  }

  protected readonly steps = STEP;
}
