import { ExcelColumn, ExcelModuleInfo } from '../../core/models/excel.models';
import { AppError } from '../../core/models/common.models';
import {
  ExcelExportAdapter,
  ExcelImportAdapter,
  ExcelModuleRegistration,
  ExportRecord,
} from './excel-module';
import { ParsedRow } from './excel-coercion';

/**
 * Test helpers: a tiny fake module ("Gadgets") plus helpers that build real .xlsx files with
 * ExcelJS. Used by the engine and wizard specs.
 */
export type ExcelJSModule = typeof import('exceljs');
export async function lib(): Promise<ExcelJSModule> {
  const module = await import('exceljs');
  return ((module as { default?: ExcelJSModule }).default ?? module) as ExcelJSModule;
}

// ---- A small fake module ("Gadgets") that exercises the engine ----------------------------

export interface Gadget {
  id: number;
  code: string;
  name: string;
  qty: number | null;
  price: number | null;
  status: string;
  startDate: string | null;
  email: string | null;
  secret: string | null;
}

export const COLUMNS: ExcelColumn[] = [
  {
    key: 'code',
    header: 'Gadget Code',
    aliases: ['Code'],
    description: 'Unique code',
    required: true,
    type: 'text',
    maxLength: 10,
    example: 'G-001',
  },
  {
    key: 'name',
    header: 'Gadget Name',
    description: 'Name',
    required: true,
    type: 'text',
    maxLength: 40,
    example: 'Widget',
  },
  {
    key: 'qty',
    header: 'Quantity',
    description: 'Units',
    required: false,
    type: 'integer',
    min: 0,
    max: 100,
    example: 5,
  },
  {
    key: 'price',
    header: 'Price',
    description: 'Unit price',
    required: false,
    type: 'decimal',
    decimals: 2,
    min: 0,
    example: 9.5,
    numberFormat: '"$"#,##0.00',
  },
  {
    key: 'status',
    header: 'Status',
    description: 'State',
    required: true,
    type: 'enum',
    allowed: ['Active', 'Inactive'],
    example: 'Active',
  },
  {
    key: 'startDate',
    header: 'Start Date',
    description: 'Start',
    required: false,
    type: 'date',
    example: '2026-03-31',
  },
  {
    key: 'email',
    header: 'Email',
    description: 'Contact',
    required: false,
    type: 'email',
    example: 'a@example.com',
  },
  {
    key: 'secret',
    header: 'Secret Note',
    description: 'Private',
    required: false,
    type: 'text',
    sensitive: true,
    example: 'x',
  },
];

export const INFO: ExcelModuleInfo = {
  code: 'gadgets',
  label: 'Gadgets',
  sheetName: 'Gadgets',
  description: 'Test module',
  columns: COLUMNS,
  matchKey: ['code'],
  matchKeyLabel: 'Gadget Code',
  canImport: true,
  canExport: true,
};

export class GadgetStore {
  items: Gadget[] = [
    {
      id: 1,
      code: 'G-001',
      name: 'Existing',
      qty: 1,
      price: 1,
      status: 'Active',
      startDate: null,
      email: null,
      secret: null,
    },
  ];
  failOn: string | null = null;
}

export function registration(store: GadgetStore): ExcelModuleRegistration {
  const importAdapter: ExcelImportAdapter = {
    findExisting: (row: ParsedRow) =>
      store.items.find((g) => g.code.toLowerCase() === String(row['code']).toLowerCase())?.id ??
      null,
    toModel: (row) => {
      const issues = [];
      if (String(row['name']).startsWith('WARN')) {
        issues.push({
          field: 'Gadget Name',
          value: String(row['name']),
          message: 'Name looks unusual.',
          severity: 'Warning' as const,
        });
      }
      return { model: { ...row }, issues };
    },
    dryRun: (model) =>
      String(model['name']).toLowerCase() === 'forbidden'
        ? ({
            code: 'Validation',
            message: 'Name not allowed.',
            fieldErrors: { name: 'That name is not allowed.' },
          } as AppError)
        : null,
    apply: (model, existingId) => {
      if (store.failOn && String(model['code']) === store.failOn) {
        throw { code: 'Conflict', message: 'Simulated failure.' } as AppError;
      }
      if (existingId === null)
        store.items.push({ id: store.items.length + 100, ...(model as Omit<Gadget, 'id'>) });
      else
        store.items = store.items.map((g) =>
          g.id === existingId ? { ...g, ...(model as Omit<Gadget, 'id'>) } : g,
        );
    },
    snapshot: () => {
      const saved = structuredClone(store.items);
      return () => (store.items = structuredClone(saved));
    },
  };
  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) =>
      store.items
        .filter((g) => request.scope !== 'selected' || request.selectedIds?.includes(g.id))
        .slice(0, limit + 1)
        .map((g): ExportRecord => ({ ...g })),
    extraSheets: (_request, rows) => [
      {
        name: 'Summary',
        columns: [
          {
            key: 'status',
            header: 'Status',
            description: '',
            required: false,
            type: 'text',
            example: null,
          },
          {
            key: 'count',
            header: 'Count',
            description: '',
            required: false,
            type: 'integer',
            example: null,
          },
        ],
        rows: [{ status: 'Active', count: rows.length }],
      },
    ],
  };
  return { info: INFO, importAdapter, exportAdapter };
}

// ---- Helpers -----------------------------------------------------------------------------

export async function workbookFile(
  rows: (string | number | Date | null | { formula: string })[][],
  options: { name?: string; sheet?: string; extraSheets?: number } = {},
): Promise<File> {
  const lib_ = await lib();
  const workbook = new lib_.Workbook();
  const sheet = workbook.addWorksheet(options.sheet ?? 'Gadgets');
  rows.forEach((row) =>
    sheet.addRow(
      row.map((v) => (v && typeof v === 'object' && 'formula' in v ? { formula: v.formula } : v)),
    ),
  );
  for (let i = 0; i < (options.extraSheets ?? 0); i++) workbook.addWorksheet(`Extra ${i}`);
  const buffer = await workbook.xlsx.writeBuffer();
  return new File([buffer], options.name ?? 'gadgets.xlsx');
}

export const HEADERS = [
  'Gadget Code',
  'Gadget Name',
  'Quantity',
  'Price',
  'Status',
  'Start Date',
  'Email',
  'Secret Note',
];
