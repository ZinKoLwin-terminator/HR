import {
  AfterViewInit,
  Component,
  DestroyRef,
  Injector,
  OnInit,
  Signal,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin, map, of, startWith, switchMap } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import { DOCUMENT_TYPES, DocumentType } from '../../../core/models/employee-extras.models';
import { Employee } from '../../../core/models/employee.models';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { EmployeeDocumentDataService } from '../../../core/services/data/employee-extras-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { FieldConfig, FieldOption, FormValues } from '../../../shared/components/crud/crud.models';
import { FormFieldComponent } from '../../../shared/components/form-field/form-field.component';
import {
  FieldFormGroup,
  buildFormGroup,
  normalizeValues,
  wireDependentFields,
} from '../../../shared/forms/form-builder';
import { todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';
import { FormSection, EmployeeFormLookups, buildEmployeeSections } from './employee-form.config';
import { toEmployeeModel, toFormValues } from './employee-form.mapper';

export interface EmployeeFormData {
  lookups: EmployeeFormLookups;
  /** Null when creating. */
  employee: Employee | null;
  /** Suggested code for new employees. */
  suggestedCode: string | null;
}

interface PendingDocument {
  key: number;
  type: DocumentType;
  title: string;
  fileName: string;
}

interface NavItem {
  id: string;
  title: string;
  icon: string;
  /** Present for data sections, used to flag validation errors. */
  section?: FormSection;
}

const MAX_PHOTO_BYTES = 1024 * 1024;

/** Form fields stored by `PUT /employees/{id}/personal` (personal data: the ViewSensitive and EditSensitive keys). */
const SENSITIVE_FIELDS: ReadonlySet<string> = new Set([
  'dateOfBirth',
  'maritalStatus',
  'nationality',
  'nationalId',
  'personalEmail',
  'phone',
  'alternatePhone',
  'addressLine1',
  'addressLine2',
  'city',
  'state',
  'postalCode',
  'country',
  'emergencyName',
  'emergencyRelationship',
  'emergencyPhone',
]);

/** Form fields stored by `PUT /employees/{id}/bank-account` (the ViewBank and EditBank keys). */
const BANK_FIELDS: ReadonlySet<string> = new Set([
  'bankName',
  'bankBranchName',
  'bankAccountName',
  'bankAccountNumber',
]);

@Component({
  selector: 'app-employee-form',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormFieldComponent,
    AvatarComponent,
  ],
  templateUrl: './employee-form.component.html',
  styleUrl: './employee-form.component.scss',
})
export class EmployeeFormComponent implements OnInit, AfterViewInit {
  readonly data = input.required<EmployeeFormData>();
  readonly saved = output<Employee>();
  readonly cancelled = output<void>();

  private readonly employeesApi = inject(EmployeeDataService);
  private readonly documentsApi = inject(EmployeeDocumentDataService);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly permissions = inject(PermissionService);

  /** Section currently in view, highlighted in the side navigation. */
  protected readonly activeId = signal('personal');

  protected sections: FormSection[] = [];
  protected navItems: NavItem[] = [];
  protected form!: FieldFormGroup;
  private allFields: FieldConfig[] = [];
  private values!: Signal<unknown>;

  protected readonly saving = signal(false);
  protected readonly formError = signal('');
  protected readonly submitted = signal(false);
  protected readonly isEdit = computed(() => this.data().employee !== null);

  // Profile photo
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly photoError = signal('');
  private photoChanged = false;

  // Documents (added to the employee when the form is saved)
  protected readonly pendingDocuments = signal<PendingDocument[]>([]);
  private documentKey = 0;
  protected readonly documentTypeOptions: FieldOption[] = DOCUMENT_TYPES.map((t) => ({
    value: t,
    label: t,
  }));
  protected readonly documentTypeField: FieldConfig = {
    key: 'type',
    label: 'Document type',
    type: 'select',
    required: true,
  };
  protected readonly documentTitleField: FieldConfig = {
    key: 'title',
    label: 'Title (optional)',
    type: 'text',
    maxLength: 80,
  };
  protected readonly documentFileField: FieldConfig = {
    key: 'file',
    label: 'File',
    type: 'file',
    required: true,
    hint: 'PDF, Word, PNG or JPG, up to 5 MB',
  };
  protected documentForm = buildFormGroup(
    [this.documentTypeField, this.documentTitleField, this.documentFileField],
    { type: 'Resume' },
  );

  private completed = false;

  ngOnInit(): void {
    const { lookups, employee, suggestedCode } = this.data();
    const allSections = buildEmployeeSections(lookups);
    this.allFields = allSections.flatMap((s) => s.fields);
    // Editing: fields the role may not read are left out and fields it may not change are read-only
    // (their values are the server's placeholders and are never sent back). Creating needs only
    // employees:Create, so nothing is restricted. UX only; the server enforces.
    const hidden = new Set<string>();
    const locked = new Set<string>();
    if (employee) {
      for (const { key } of this.allFields) {
        const kind = SENSITIVE_FIELDS.has(key) ? 'Sensitive' : BANK_FIELDS.has(key) ? 'Bank' : null;
        if (!kind) continue;
        if (!this.permissions.can(`employees:View${kind}`)) hidden.add(key);
        if (!this.permissions.can(`employees:Edit${kind}`)) locked.add(key);
      }
    }
    this.sections = allSections
      .map((section) => ({ ...section, fields: section.fields.filter((f) => !hidden.has(f.key)) }))
      .filter((section) => section.fields.length > 0);
    this.navItems = [
      ...this.sections.map((section) => ({
        id: section.id,
        title: section.title,
        icon: section.icon,
        section,
      })),
      { id: 'photo', title: 'Profile photo', icon: 'photo_camera' },
      { id: 'documents', title: 'Documents', icon: 'folder_shared' },
    ];

    this.form = buildFormGroup(this.allFields, toFormValues(employee, suggestedCode), {
      disabledKeys: new Set([
        ...(employee ? this.allFields.filter((f) => f.lockedOnEdit).map((f) => f.key) : []),
        ...hidden,
        ...locked,
      ]),
      validators: [
        HrmsValidators.dateRange('joiningDate', 'probationEndDate'),
        HrmsValidators.dateRange('joiningDate', 'exitDate'),
        HrmsValidators.requiredWhen(
          'exitDate',
          'employmentStatus',
          (status) => status === 'Resigned' || status === 'Terminated',
        ),
      ],
    });
    this.values = toSignal(this.form.valueChanges.pipe(startWith(null)), {
      injector: this.injector,
      initialValue: null,
    });
    wireDependentFields(this.allFields, this.form, (field) => this.optionsFor(field));

    // Choosing a position suggests its job grade.
    this.form.controls['positionId'].valueChanges.subscribe((positionId) => {
      const position = lookups.org.positions.find((p) => p.id === positionId);
      if (position) {
        this.form.controls['jobGradeId'].setValue(position.jobGradeId);
      }
    });
    this.photoUrl.set(employee?.photoUrl ?? null);
  }

  ngAfterViewInit(): void {
    if (typeof IntersectionObserver === 'undefined') {
      return;
    }
    const visible = new Set<string>();
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          const id = entry.target.id.replace('sec-', '');
          if (entry.isIntersecting) {
            visible.add(id);
          } else {
            visible.delete(id);
          }
        }
        // First section (in page order) that is on screen wins.
        const first = this.navItems.find((item) => visible.has(item.id));
        if (first) {
          this.activeId.set(first.id);
        }
      },
      { rootMargin: '-20% 0px -60% 0px' },
    );
    for (const item of this.navItems) {
      const el = document.getElementById(`sec-${item.id}`);
      if (el) {
        observer.observe(el);
      }
    }
    this.destroyRef.onDestroy(() => observer.disconnect());
  }

  hasUnsavedChanges(): boolean {
    if (this.completed || !this.form) {
      return false;
    }
    return this.form.dirty || this.photoChanged || this.pendingDocuments().length > 0;
  }

  protected optionsFor(field: FieldConfig): FieldOption[] {
    this.values(); // re-evaluate whenever any value changes
    return (
      field.options?.(this.form.getRawValue() as FormValues, {
        id: this.data().employee?.id ?? null,
      }) ?? []
    );
  }

  protected control(field: FieldConfig): AbstractControl {
    return this.form.controls[field.key];
  }

  protected documentControl(field: FieldConfig): AbstractControl {
    return this.documentForm.controls[field.key];
  }

  protected sectionHasErrors(item: NavItem): boolean {
    this.values();
    return (
      this.submitted() && !!item.section?.fields.some((f) => this.form.controls[f.key].invalid)
    );
  }

  protected goTo(id: string): void {
    this.activeId.set(id);
    const el = document.getElementById(`sec-${id}`);
    el?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
    el?.focus({ preventScroll: true });
  }

  // ---- Photo ------------------------------------------------------------------

  protected onPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    this.photoError.set('');
    if (!file) {
      return;
    }
    if (!['image/png', 'image/jpeg'].includes(file.type)) {
      this.photoError.set('Choose a PNG or JPG image.');
      return;
    }
    if (file.size > MAX_PHOTO_BYTES) {
      this.photoError.set('The image is larger than 1 MB.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.photoUrl.set(String(reader.result));
      this.photoChanged = true;
    };
    reader.onerror = () => this.photoError.set('The image could not be read.');
    reader.readAsDataURL(file);
  }

  protected removePhoto(): void {
    this.photoUrl.set(null);
    this.photoChanged = true;
    this.photoError.set('');
  }

  // ---- Documents --------------------------------------------------------------

  protected addDocument(): void {
    this.documentForm.markAllAsTouched();
    if (this.documentForm.invalid) {
      return;
    }
    const raw = this.documentForm.getRawValue() as FormValues;
    const type = raw['type'] as DocumentType;
    this.pendingDocuments.update((docs) => [
      ...docs,
      {
        key: ++this.documentKey,
        type,
        title: String(raw['title'] ?? '').trim() || type,
        fileName: String(raw['file']),
      },
    ]);
    this.documentForm.reset({ type: 'Resume', title: '', file: '' });
  }

  protected removeDocument(key: number): void {
    this.pendingDocuments.update((docs) => docs.filter((d) => d.key !== key));
  }

  // ---- Save -------------------------------------------------------------------

  protected submit(): void {
    if (this.saving()) {
      return; // prevent duplicate submissions
    }
    this.submitted.set(true);
    this.formError.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      const count = this.allFields.filter((f) => this.form.controls[f.key].invalid).length;
      this.formError.set(
        `Please correct ${count || 'the highlighted'} field${count === 1 ? '' : 's'} before saving.`,
      );
      queueMicrotask(() => {
        const invalid = document.querySelector<HTMLElement>('.emp-form .mat-form-field-invalid');
        invalid?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
        invalid?.querySelector<HTMLElement>('input, textarea, .mat-mdc-select')?.focus();
      });
      return;
    }

    this.saving.set(true);
    const values = normalizeValues(this.allFields, this.form.getRawValue() as FormValues);
    const model = toEmployeeModel(values, this.photoUrl());
    const existing = this.data().employee;
    const documents = this.pendingDocuments();

    const request = existing
      ? this.employeesApi.update(existing.id, model)
      : this.employeesApi.create(model);
    request
      .pipe(
        switchMap((employee) =>
          documents.length
            ? forkJoin(
                documents.map((d) =>
                  this.documentsApi.create({
                    employeeId: employee.id,
                    type: d.type,
                    title: d.title,
                    fileName: d.fileName,
                    uploadedOn: todayIso(),
                    expiryDate: null,
                  }),
                ),
              ).pipe(map(() => employee))
            : of(employee),
        ),
      )
      .subscribe({
        next: (employee) => {
          this.completed = true;
          this.saved.emit(employee);
        },
        error: (error: unknown) => {
          this.saving.set(false);
          if (isAppError(error)) {
            this.formError.set(error.message);
            for (const [key, message] of Object.entries(error.fieldErrors ?? {})) {
              this.form.controls[key]?.setErrors({ server: message });
              this.form.controls[key]?.markAsTouched();
            }
          } else {
            this.formError.set('The employee could not be saved. Please try again.');
          }
        },
      });
  }
}
