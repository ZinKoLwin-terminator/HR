import { ExcelColumn } from '../../../core/models/excel.models';
import { ParsedRow } from '../excel-coercion';

/** Column builders shared by the mock Excel modules (they mirror the columns of the API's modules). */

export const CODE_PATTERN = '^[A-Z0-9][A-Z0-9-]*$';

/** An import column: optional text unless `extra` says otherwise. */
export const column = (
  key: string,
  header: string,
  description: string,
  example: string | number | boolean | null,
  extra: Partial<ExcelColumn> = {},
): ExcelColumn => ({
  key,
  header,
  description,
  example,
  required: false,
  type: 'text',
  ...extra,
});

/** The business code that identifies a record and matches existing ones on import. */
export const codeColumn = (label: string, example: string): ExcelColumn =>
  column(
    'code',
    `${label} Code`,
    `Unique code of the ${label.toLowerCase()}. Existing records are matched by this code.`,
    example,
    {
      required: true,
      maxLength: 20,
      uppercase: true,
      pattern: CODE_PATTERN,
      patternMessage: `${label} Code must use capital letters, numbers and dashes only.`,
      aliases: ['Code'],
    },
  );

/** The Employee Code column used by modules keyed on an employee. */
export const employeeCodeColumn = (description: string): ExcelColumn =>
  column('employeeCode', 'Employee Code', description, 'EMP061', {
    required: true,
    maxLength: 12,
    uppercase: true,
    pattern: CODE_PATTERN,
    patternMessage: 'Employee Code must use capital letters, numbers and dashes only.',
    aliases: ['Employee ID', 'Emp Code'],
  });

/** A column holding the business code of a related record. */
export const referenceColumn = (
  key: string,
  header: string,
  description: string,
  example: string | null,
  required = true,
): ExcelColumn => column(key, header, description, example, { required, maxLength: 20 });

export const STATUSES = ['Active', 'Inactive'] as const;

export const statusColumn = (): ExcelColumn =>
  column(
    'status',
    'Status',
    'Active or Inactive. Blank means Active for a new record and no change for an existing one.',
    'Active',
    { type: 'enum', allowed: STATUSES },
  );

/** An export-only column. */
export const outColumn = (
  key: string,
  header: string,
  type: ExcelColumn['type'] = 'text',
  width?: number,
): ExcelColumn => ({
  key,
  header,
  type,
  width,
  description: '',
  required: false,
  example: null,
  sensitive: false,
});

export const lower = (value: unknown): string =>
  String(value ?? '')
    .trim()
    .toLowerCase();

export const present = (value: unknown): boolean =>
  value !== null && value !== undefined && value !== '';

/** The cell as text, or `current` when the cell is blank (a blank optional cell keeps the value). */
export const pick = (row: ParsedRow, key: string, current: string): string =>
  present(row[key]) ? String(row[key]) : current;
