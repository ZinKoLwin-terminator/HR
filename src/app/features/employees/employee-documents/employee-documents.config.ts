import { DOCUMENT_TYPES, EmployeeDocument } from '../../../core/models/employee-extras.models';
import { Employee, fullName } from '../../../core/models/employee.models';
import { EmployeeDocumentDataService } from '../../../core/services/data/employee-extras-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { diffInDays, todayIso } from '../../../shared/utilities/date.util';
import { employeeOptions } from '../../organization/organization-lookups';

const formatDate = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });

/** "12 Nov 2026 · in 12 days", "… · expired", or an em dash. */
export function expiryLabel(expiryDate: string | null): string {
  if (!expiryDate) {
    return '—';
  }
  const days = diffInDays(todayIso(), expiryDate);
  const when =
    days < 0 ? 'expired' : days === 0 ? 'today' : `in ${days} day${days === 1 ? '' : 's'}`;
  return `${formatDate(expiryDate)} · ${when}`;
}

const typeOptions = DOCUMENT_TYPES.map((t) => ({ value: t, label: t }));

export interface DocumentsConfigOptions {
  service: EmployeeDocumentDataService;
  /** All employees (names for the employee column / dropdown). */
  employees: Employee[];
  /** When set, the screen is scoped to one employee (profile tab). */
  employeeId?: number;
}

export function buildDocumentsConfig(
  opts: DocumentsConfigOptions,
): CrudPageConfig<EmployeeDocument> {
  const scoped = opts.employeeId !== undefined;
  const names = new Map(opts.employees.map((e) => [e.id, `${fullName(e)} (${e.employeeCode})`]));
  const employeeColumn: TableColumn<EmployeeDocument>[] = scoped
    ? []
    : [
        {
          key: 'employeeId',
          header: 'Employee',
          value: (d) => names.get(d.employeeId) ?? `#${d.employeeId}`,
        },
      ];

  return {
    title: 'Employee documents',
    description: 'Contracts, IDs, certificates and permits. Keep an eye on expiry dates.',
    entityLabel: 'Document',
    service: opts.service,
    hasStatus: false,
    embedded: scoped,
    lockedFilters: scoped ? { employeeId: opts.employeeId } : undefined,
    fixedValues: scoped ? { employeeId: opts.employeeId } : undefined,
    searchLabel: 'Search by title, type or file name',
    defaultSort: { active: 'uploadedOn', direction: 'desc' },
    nameOf: (d) => d.title,
    filters: [
      ...(scoped
        ? []
        : [{ key: 'employeeId', label: 'Employee', options: employeeOptions(opts.employees) }]),
      { key: 'type', label: 'Type', options: typeOptions },
    ],
    columns: [
      ...employeeColumn,
      { key: 'type', header: 'Type', sortable: true },
      { key: 'title', header: 'Title', sortable: true },
      { key: 'fileName', header: 'File', hideBelow: 'md' },
      { key: 'uploadedOn', header: 'Uploaded', type: 'date', sortable: true, hideBelow: 'sm' },
      {
        key: 'expiryDate',
        header: 'Expiry',
        sortable: true,
        hideBelow: 'sm',
        value: (d) => expiryLabel(d.expiryDate),
      },
    ],
    newItem: () => ({
      type: 'Other',
      uploadedOn: todayIso(),
      ...(scoped ? { employeeId: opts.employeeId } : {}),
    }),
    fields: [
      ...(scoped
        ? []
        : [
            {
              key: 'employeeId',
              label: 'Employee',
              type: 'select' as const,
              required: true,
              fullWidth: true,
              options: () => employeeOptions(opts.employees),
            },
          ]),
      {
        key: 'type',
        label: 'Document type',
        type: 'select',
        required: true,
        options: () => typeOptions,
      },
      { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 80 },
      {
        key: 'fileName',
        label: 'File',
        type: 'file',
        required: true,
        hint: 'PDF, Word, PNG or JPG up to 5 MB (demo: not stored)',
      },
      { key: 'uploadedOn', label: 'Uploaded on', type: 'date', required: true, notInFuture: true },
      {
        key: 'expiryDate',
        label: 'Expiry date',
        type: 'date',
        hint: 'Leave empty if the document does not expire',
      },
    ],
  };
}
