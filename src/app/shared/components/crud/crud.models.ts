import { ValidatorFn } from '@angular/forms';
import { Observable } from 'rxjs';
import { Entity, SortState } from '../../../core/models/common.models';
import { CrudDataService } from '../../../core/services/data/crud-data.service';
import { TableColumn } from '../data-table/data-table.models';

export type FieldType =
  | 'text'
  | 'textarea'
  | 'number'
  | 'select'
  | 'date'
  | 'email'
  | 'tel'
  | 'url'
  /** Chooses a file; the control stores the file name (demo: contents are not kept). */
  | 'file'
  /** HH:mm value from a native time picker. */
  | 'time'
  /** Native colour picker; stores `#rrggbb`. */
  | 'color'
  /** Several options; the control stores an array of option values. */
  | 'multiselect'
  /** On/off switch; the control stores a boolean. */
  | 'toggle';

export interface FieldOption {
  value: string | number | null;
  label: string;
}

export type FormValues = Record<string, unknown>;

export interface FieldContext {
  /** Id of the record being edited, or null when creating. */
  id: number | null;
}

/** Declarative description of one form control (rendered by the entity form dialog). */
export interface FieldConfig {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  minLength?: number;
  maxLength?: number;
  min?: number;
  max?: number;
  /** Named reusable validator: upper-case code, phone number or digits only. */
  pattern?: 'code' | 'phone' | 'digits';
  /** Date must not be in the future. */
  notInFuture?: boolean;
  /** Date of birth: person must be at least this many years old. */
  minAge?: number;
  hint?: string;
  placeholder?: string;
  /** Options for `select`, derived from the current form values (enables dependent dropdowns). */
  options?: (values: FormValues, context: FieldContext) => FieldOption[];
  /** Key of a field this one depends on; its value is cleared when the parent changes to something incompatible. */
  dependsOn?: string;
  /** Select shows a "None" entry and stores null. */
  nullable?: boolean;
  /** Spans both columns of the form grid. */
  fullWidth?: boolean;
  /** Cannot be changed after creation (e.g. codes). */
  lockedOnEdit?: boolean;
  /** Shown in the details dialog only when set. */
  detailsOnly?: boolean;
  /**
   * Makes the field read-only and calculated from the other values
   * (e.g. leave days from the dates). Saved with the form.
   */
  derive?: (values: FormValues) => unknown;
}

export interface FilterConfig {
  key: string;
  label: string;
  options: FieldOption[];
}

/** Everything the generic CRUD page needs to render an entity screen. */
export interface CrudPageConfig<T extends Entity> {
  title: string;
  description: string;
  /** Singular label, e.g. "Department". */
  entityLabel: string;
  service: CrudDataService<T>;
  columns: TableColumn<T>[];
  fields: FieldConfig[];
  groupValidators?: ValidatorFn[];
  filters?: FilterConfig[];
  searchLabel?: string;
  defaultSort?: SortState;
  /** Default values for the "add" form. */
  newItem: () => FormValues;
  /** Name used in confirmations and toasts. */
  nameOf: (row: T) => string;
  /** Rows have a `status: 'Active' | 'Inactive'` and can be (de)activated. Default true. */
  hasStatus?: boolean;
  /** Render without the page header (e.g. inside a profile tab). */
  embedded?: boolean;
  /** Filters always applied to the query (e.g. `{ employeeId: 5 }` in a profile tab). */
  lockedFilters?: Record<string, unknown>;
  /** Values merged into every created/updated record (e.g. the owning employee id). */
  fixedValues?: Record<string, unknown>;
  /** Set to `false` to hide the "Add" button (list-only screens). Default true. */
  canCreate?: boolean;
  /** Adds "from / to" date filters to the toolbar. */
  dateFilter?: DateRangeFilterConfig;
  /** Extra row actions shown before Edit / Delete. */
  extraActions?: CrudExtraAction<T>[];
  /** Hide Edit for rows that can no longer be edited (e.g. decided requests). */
  canEdit?: (row: T) => boolean;
  /** Hide Delete for rows that cannot be deleted. */
  canDelete?: (row: T) => boolean;
}

/** A custom row action (e.g. Approve / Reject) rendered in the row menu. */
export interface CrudExtraAction<T extends Entity> {
  id: string;
  label: string;
  icon: string;
  destructive?: boolean;
  visible?: (row: T) => boolean;
  /** Ask for confirmation first. */
  confirm?: { title: string; message: (row: T) => string; confirmText: string };
  /** Ask for a text (e.g. a rejection reason) first; it is passed to `run` as `input`. */
  prompt?: {
    title: string;
    message?: (row: T) => string;
    label: string;
    required: boolean;
    confirmText: string;
  };
  run: (row: T, input: string) => Observable<unknown>;
  successMessage: (row: T) => string;
}

/** Two native date inputs that filter by an inclusive range (sent as `fromKey` / `toKey`). */
export interface DateRangeFilterConfig {
  fromKey: string;
  toKey: string;
  fromLabel: string;
  toLabel: string;
}

export interface EntityFormDialogData<T extends Entity = Entity> {
  title: string;
  fields: FieldConfig[];
  groupValidators?: ValidatorFn[];
  value: FormValues;
  id: number | null;
  submitLabel: string;
  save: (values: FormValues) => Observable<T>;
}

export interface EntityDetailsDialogData {
  title: string;
  subtitle: string;
  fields: FieldConfig[];
  row: FormValues;
  id: number;
  /** Hide the Edit button (view-only records). */
  readonly?: boolean;
}
