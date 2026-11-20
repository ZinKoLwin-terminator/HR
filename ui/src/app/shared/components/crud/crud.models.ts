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
  /** Masked text for a secret. Never trimmed, never shown again; use with `createOnly`. */
  | 'password'
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
  /** Only on the create form (e.g. an initial password); not shown when editing or in details. */
  createOnly?: boolean;
  /** Shown in the details dialog only when set. */
  detailsOnly?: boolean;
  /** Only on the forms (create and edit); never in the details dialog (e.g. a write-only value). */
  formOnly?: boolean;
  /**
   * Makes the field read-only and calculated from the other values
   * (e.g. leave days from the dates). Saved with the form.
   */
  derive?: (values: FormValues) => unknown;
  /**
   * The field is shown only while this returns true. A hidden field is disabled, cleared and not
   * validated, so the saved record carries an empty value for it.
   */
  visibleWhen?: (values: FormValues) => boolean;
  /** Read-only when editing a record that matches (judged on the record as it was opened). */
  lockedWhen?: (row: FormValues) => boolean;
  /**
   * A value the rest of the form dictates. While this returns an object the field is disabled and
   * holds `value`; when it returns null the field is editable again.
   */
  force?: (values: FormValues) => { value: unknown } | null;
  /** Cross-field rule: the returned text is shown on this field as an error while it is not null. */
  rule?: (values: FormValues) => string | null;
  /** Hint that depends on the other values; used instead of `hint` while it returns text. */
  hintFor?: (values: FormValues) => string | undefined;
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
  /**
   * Set to `false` for a list whose rows are never edited (no Edit action; the details dialog is
   * read-only). Default true.
   */
  editable?: boolean;
  /** Hide Edit for rows that can no longer be edited (e.g. decided requests). */
  canEdit?: (row: T) => boolean;
  /** Hide Delete for rows that cannot be deleted. */
  canDelete?: (row: T) => boolean;
  /**
   * Overrides the server permission key behind an action, by action name (`Create`, `Edit`,
   * `Delete`). By default the key is the screen's route module plus the action (`leave:Create`).
   * UX only: the server enforces its own keys.
   */
  permissionKeys?: Partial<Record<string, string>>;
  /**
   * Shows a Print action (browser print of the current view, or of every matching row). Default
   * true, except for an embedded list (a profile tab), which is printed with its host screen.
   */
  printable?: boolean;
  /** Note shown at the top of the create/edit form (`null` for a new record); null hides it. */
  formNotice?: (row: FormValues | null) => string | null;
  /** Shows Export (and Import) buttons for this list, backed by the Excel module with this code. */
  excel?: { moduleCode: string; import?: boolean };
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
    /** Longest text accepted (default 250). */
    maxLength?: number;
  };
  run: (row: T, input: string) => Observable<unknown>;
  /** Toast text after `run` emits; `result` is the value it emitted (e.g. the server response). */
  successMessage: (row: T, result: unknown) => string;
  /** Action (within the screen's module) the role needs to see this action: `Approve`, `Create`, ... Default `Edit`. */
  permission?: string;
  /** Skip the success toast and list refresh (e.g. an action that just navigates). */
  silent?: boolean;
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
  /** Note shown above the fields (for example why some are read-only). */
  notice?: string | null;
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
