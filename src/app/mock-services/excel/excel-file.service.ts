import { Injectable, inject } from '@angular/core';
import type { Cell, CellValue, Workbook, Worksheet } from 'exceljs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { ExcelColumn, ExcelModuleInfo, ImportIssue } from '../../core/models/excel.models';
import { mockError } from '../mock-response';
import { RawCell, neutralizeFormula, normalizeHeader } from './excel-coercion';
import { ExportSheet } from './excel-module';

type ExcelLibrary = typeof import('exceljs');

export const XLSX_MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

export interface ParsedSheetRow {
  /** Row number in the worksheet (header = 1). */
  rowNumber: number;
  cells: RawCell[];
}

export interface ParsedSheet {
  name: string;
  /** Number of worksheets in the workbook. */
  sheetCount: number;
  headers: string[];
  rows: ParsedSheetRow[];
}

const HEADER_DARK = 'FF1F3A68';
const HEADER_REQUIRED = 'FF1E4DB7';
const HEADER_OPTIONAL = 'FF5B6B8A';
/** Worksheets of our own templates that are never the data sheet. */
const GUIDE_SHEETS = ['instructions', 'field guide', 'lists'];
const VALIDATION_ROWS = 1000;

const TYPE_LABELS: Record<ExcelColumn['type'], string> = {
  text: 'Text',
  integer: 'Whole number',
  decimal: 'Number',
  date: 'Date (yyyy-MM-dd)',
  time: 'Time (HH:mm)',
  email: 'Email address',
  phone: 'Phone number',
  enum: 'Choose from list',
  boolean: 'Yes / No',
};

function safeSheetName(name: string): string {
  return (
    name
      .replace(/[\\/*?:[\]]/g, ' ')
      .trim()
      .slice(0, 31) || 'Sheet'
  );
}

function columnLetter(index: number): string {
  let letter = '';
  for (let n = index; n > 0; n = Math.floor((n - 1) / 26)) {
    letter = String.fromCharCode(65 + ((n - 1) % 26)) + letter;
  }
  return letter;
}

function dateFromIso(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match
    ? new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])))
    : null;
}

/**
 * The only place that touches the spreadsheet library (loaded on demand, so it stays out of the
 * main bundle). Reads uploads defensively and generates templates, exports and reports.
 */
@Injectable({ providedIn: 'root' })
export class ExcelFileService {
  private readonly limits = inject(APP_CONFIG).excel;
  private library?: Promise<ExcelLibrary>;

  private lib(): Promise<ExcelLibrary> {
    this.library ??= import('exceljs').then(
      (module) => ((module as { default?: ExcelLibrary }).default ?? module) as ExcelLibrary,
    );
    return this.library;
  }

  // ---- Reading ------------------------------------------------------------------------------

  /**
   * Reads the data worksheet of an uploaded workbook. Never evaluates formulas, and rejects
   * files that are not genuine .xlsx workbooks, contain macros or external links, or exceed the limits.
   */
  async read(file: File, preferredSheet: string): Promise<ParsedSheet> {
    const bytes = await this.checkFile(file);
    const lib = await this.lib();
    const workbook: Workbook = new lib.Workbook();
    try {
      await workbook.xlsx.load(bytes);
    } catch {
      throw mockError(
        'Validation',
        'The workbook is corrupted or cannot be read. Open it in Excel, save it again as .xlsx and retry.',
      );
    }

    if (workbook.worksheets.length === 0) {
      throw mockError('Validation', 'The workbook is empty: it contains no worksheets.');
    }
    if (workbook.worksheets.length > this.limits.maxSheets) {
      throw mockError(
        'Validation',
        `The workbook has ${workbook.worksheets.length} worksheets; at most ${this.limits.maxSheets} are allowed.`,
      );
    }

    const sheet = this.pickSheet(workbook, preferredSheet);
    if (!sheet) {
      throw mockError(
        'Validation',
        `The worksheet "${preferredSheet}" was not found. Use the downloaded template.`,
      );
    }
    return this.parseSheet(sheet, workbook.worksheets.length);
  }

  private async checkFile(file: File): Promise<ArrayBuffer> {
    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      throw mockError(
        'Validation',
        'Only Excel workbooks (.xlsx) can be imported. Other formats, including .xls, .xlsm and .csv, are not accepted.',
      );
    }
    if (file.size === 0) {
      throw mockError('Validation', 'The file is empty.');
    }
    if (file.size > this.limits.maxFileBytes) {
      const mb = (this.limits.maxFileBytes / (1024 * 1024)).toFixed(0);
      throw mockError('Validation', `The file is too large. The maximum size is ${mb} MB.`);
    }
    const bytes = await file.arrayBuffer();
    const head = new Uint8Array(bytes, 0, Math.min(4, bytes.byteLength));
    // An .xlsx file is a ZIP archive ("PK\x03\x04"); a renamed file of another type is refused.
    if (
      head.length < 4 ||
      head[0] !== 0x50 ||
      head[1] !== 0x4b ||
      head[2] !== 0x03 ||
      head[3] !== 0x04
    ) {
      throw mockError(
        'Validation',
        'The file is not a valid Excel workbook (its content does not match the .xlsx format).',
      );
    }
    const index = new TextDecoder('latin1').decode(bytes);
    if (index.includes('vbaProject.bin')) {
      throw mockError(
        'Validation',
        'The workbook contains macros, which are not allowed. Save a copy without macros as .xlsx.',
      );
    }
    if (index.includes('externalLinks/')) {
      throw mockError(
        'Validation',
        'The workbook contains links to other workbooks, which are not allowed. Break the links and retry.',
      );
    }
    return bytes;
  }

  private pickSheet(workbook: Workbook, preferred: string): Worksheet | undefined {
    const wanted = preferred.trim().toLowerCase();
    const named = workbook.worksheets.find((s) => s.name.trim().toLowerCase() === wanted);
    if (named) return named;
    return workbook.worksheets.find(
      (s) =>
        s.state !== 'hidden' &&
        s.state !== 'veryHidden' &&
        !GUIDE_SHEETS.includes(s.name.trim().toLowerCase()),
    );
  }

  private parseSheet(sheet: Worksheet, sheetCount: number): ParsedSheet {
    if (sheet.actualRowCount === 0) {
      throw mockError('Validation', `The worksheet "${sheet.name}" is empty.`);
    }
    const headerRow = sheet.getRow(1);
    const headers: string[] = [];
    for (let col = 1; col <= sheet.columnCount; col++) {
      headers.push(
        this.toRaw(headerRow.getCell(col)).value === null
          ? ''
          : String(this.toRaw(headerRow.getCell(col)).value).trim(),
      );
    }
    while (headers.length && headers[headers.length - 1] === '') headers.pop();
    if (headers.length === 0 || headers.every((h) => h === '')) {
      throw mockError('Validation', 'The first row must contain the column headers.');
    }
    const seen = new Set<string>();
    for (const header of headers.filter(Boolean)) {
      const key = normalizeHeader(header);
      if (seen.has(key)) {
        throw mockError('Validation', `The column header "${header}" appears more than once.`);
      }
      seen.add(key);
    }

    const rows: ParsedSheetRow[] = [];
    for (let rowNumber = 2; rowNumber <= sheet.rowCount; rowNumber++) {
      const row = sheet.getRow(rowNumber);
      const cells = headers.map((_, i) => this.toRaw(row.getCell(i + 1)));
      if (
        cells.every(
          (c) => c.value === null || (typeof c.value === 'string' && c.value.trim() === ''),
        ) &&
        !cells.some((c) => c.formula)
      ) {
        continue; // blank row
      }
      if (rows.length >= this.limits.maxRows) {
        throw mockError(
          'Validation',
          `The worksheet has more than ${this.limits.maxRows} data rows. Split the file into smaller files.`,
        );
      }
      rows.push({ rowNumber, cells });
    }
    return { name: sheet.name, sheetCount, headers, rows };
  }

  /** Reads a cell as plain data. Formulas are flagged, never calculated. */
  private toRaw(cell: Cell): RawCell {
    const value = cell.value as CellValue;
    if (value === null || value === undefined) return { value: null, formula: false };
    if (
      value instanceof Date ||
      typeof value === 'string' ||
      typeof value === 'number' ||
      typeof value === 'boolean'
    ) {
      return { value, formula: false };
    }
    if ('formula' in value || 'sharedFormula' in value) return { value: null, formula: true };
    if ('richText' in value)
      return { value: value.richText.map((part) => part.text).join(''), formula: false };
    if ('text' in value) return { value: String(value.text), formula: false };
    if ('error' in value) return { value: String(value.error), formula: false };
    return { value: null, formula: false };
  }

  // ---- Writing ------------------------------------------------------------------------------

  private async newWorkbook(title: string): Promise<{ workbook: Workbook; lib: ExcelLibrary }> {
    const lib = await this.lib();
    const workbook = new lib.Workbook();
    workbook.creator = 'Apex Horizon HRMS (sample data)';
    workbook.title = title;
    workbook.created = new Date();
    return { workbook, lib };
  }

  private async toBlob(workbook: Workbook): Promise<Blob> {
    const buffer = await workbook.xlsx.writeBuffer();
    return new Blob([buffer as ArrayBuffer], { type: XLSX_MIME });
  }

  private styleHeader(sheet: Worksheet, count: number, fills: readonly string[]): void {
    const row = sheet.getRow(1);
    row.height = 24;
    for (let col = 1; col <= count; col++) {
      const cell = row.getCell(col);
      cell.font = { bold: true, color: { argb: 'FFFFFFFF' } };
      cell.fill = {
        type: 'pattern',
        pattern: 'solid',
        fgColor: { argb: fills[col - 1] ?? HEADER_DARK },
      };
      cell.alignment = { vertical: 'middle', wrapText: true };
    }
    sheet.views = [{ state: 'frozen', ySplit: 1 }];
    sheet.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: count } };
  }

  /** The Excel template for a module: instructions, a header-only data sheet and a field guide. */
  async buildTemplate(
    info: ExcelModuleInfo,
    reference: readonly ExportSheet[] = [],
  ): Promise<Blob> {
    const { workbook } = await this.newWorkbook(`${info.label} import template`);
    const columns = info.columns;
    const limits = this.limits;

    const guide = workbook.addWorksheet('Instructions');
    guide.getColumn(1).width = 4;
    guide.getColumn(2).width = 100;
    guide.getCell('B1').value = `${info.label} — import template`;
    guide.getCell('B1').font = { bold: true, size: 16 };
    const lines = [
      info.description,
      '',
      'How to use this template',
      `1. Fill in the "${safeSheetName(info.sheetName)}" worksheet, one record per row, starting directly below the header row.`,
      '2. Do not rename, reorder or delete the column headers. Dark blue headers are required; grey headers are optional.',
      '3. See the "Field guide" worksheet for what each column means, its allowed values and an example.',
      '4. Use the drop-down lists where offered. Dates use the format yyyy-MM-dd (for example 2026-03-31).',
      '5. Save as .xlsx and upload the file on the Import Data screen. You will see a preview before anything is imported.',
      '',
      'Rules',
      `• Existing records are matched by: ${info.matchKeyLabel}.`,
      `• Limits: up to ${limits.maxRows.toLocaleString('en-US')} data rows and ${(limits.maxFileBytes / (1024 * 1024)).toFixed(0)} MB per file.`,
      '• Formulas, macros and links to other workbooks are not accepted. Enter plain values.',
      '• All data in this demo is sample data.',
    ];
    lines.forEach((text, i) => {
      const cell = guide.getCell(`B${i + 3}`);
      cell.value = text;
      cell.alignment = { wrapText: true, vertical: 'top' };
      if (text === 'How to use this template' || text === 'Rules')
        cell.font = { bold: true, size: 12 };
    });

    const data = workbook.addWorksheet(safeSheetName(info.sheetName));
    // Drop-down lists live on a hidden "Lists" sheet (one column per list), created last.
    const enumColumns = columns.filter((c) => c.type === 'enum' && c.allowed?.length);
    const listColumnOf = new Map(enumColumns.map((c, i) => [c.key, i + 1]));
    columns.forEach((column, i) => {
      const col = i + 1;
      data.getCell(1, col).value = column.header;
      const dataColumn = data.getColumn(col);
      dataColumn.width = column.width ?? Math.min(40, Math.max(14, column.header.length + 4));
      if (column.type === 'date') dataColumn.numFmt = 'yyyy-mm-dd';
      else if (column.type === 'text' || column.type === 'phone' || column.type === 'email')
        dataColumn.numFmt = '@';
      this.addValidation(data, listColumnOf.get(column.key) ?? null, column, col);
    });
    this.styleHeader(
      data,
      columns.length,
      columns.map((c) => (c.required ? HEADER_REQUIRED : HEADER_OPTIONAL)),
    );

    const fieldGuide = workbook.addWorksheet('Field guide');
    ['Column', 'Required', 'Type', 'Allowed values / format', 'Description', 'Example'].forEach(
      (title, i) => {
        fieldGuide.getCell(1, i + 1).value = title;
      },
    );
    [26, 11, 20, 46, 60, 26].forEach((width, i) => (fieldGuide.getColumn(i + 1).width = width));
    columns.forEach((column, i) => {
      const row = i + 2;
      fieldGuide.getCell(row, 1).value = column.header;
      fieldGuide.getCell(row, 2).value = column.required ? 'Yes' : 'No';
      fieldGuide.getCell(row, 3).value = TYPE_LABELS[column.type];
      fieldGuide.getCell(row, 4).value = this.formatRule(column);
      fieldGuide.getCell(row, 5).value = column.description;
      fieldGuide.getCell(row, 6).value = column.example === null ? '' : String(column.example);
      for (let c = 1; c <= 6; c++)
        fieldGuide.getCell(row, c).alignment = { wrapText: true, vertical: 'top' };
    });
    this.styleHeader(fieldGuide, 6, []);

    for (const sheet of reference) {
      this.writeExportSheet(workbook, sheet);
    }

    if (enumColumns.length) {
      const lists = workbook.addWorksheet('Lists');
      enumColumns.forEach((column, i) => {
        lists.getCell(1, i + 1).value = column.header;
        (column.allowed ?? []).forEach(
          (value, row) => (lists.getCell(row + 2, i + 1).value = value),
        );
      });
      lists.state = 'hidden';
    }
    return this.toBlob(workbook);
  }

  private formatRule(column: ExcelColumn): string {
    const parts: string[] = [];
    if (column.type === 'enum') parts.push((column.allowed ?? []).join(', '));
    if (column.maxLength) parts.push(`Up to ${column.maxLength} characters`);
    if (column.type === 'decimal' && column.decimals !== undefined)
      parts.push(`Up to ${column.decimals} decimal places`);
    if (column.min !== undefined || column.max !== undefined) {
      parts.push(`${column.min ?? '…'} to ${column.max ?? '…'}`);
    }
    if (column.type === 'date') parts.push('yyyy-MM-dd');
    if (column.type === 'time') parts.push('HH:mm (24-hour)');
    if (column.type === 'boolean') parts.push('Yes or No');
    return parts.join('; ');
  }

  /** Adds the drop-down / data checks for a column to the first rows of the data sheet. */
  private addValidation(
    data: Worksheet,
    listColumn: number | null,
    column: ExcelColumn,
    col: number,
  ): void {
    const letter = columnLetter(col);
    const range = `${letter}2:${letter}${VALIDATION_ROWS + 1}`;
    const validations = (data as unknown as { dataValidations: { model: Record<string, unknown> } })
      .dataValidations;
    const message = { showErrorMessage: true, errorStyle: 'stop', errorTitle: column.header };

    if (column.type === 'enum' && listColumn !== null && column.allowed?.length) {
      const listLetter = columnLetter(listColumn);
      validations.model[range] = {
        type: 'list',
        allowBlank: !column.required,
        formulae: [`Lists!$${listLetter}$2:$${listLetter}$${column.allowed.length + 1}`],
        ...message,
        error: `Choose one of: ${column.allowed.join(', ')}.`,
      };
    } else if (column.type === 'boolean') {
      validations.model[range] = {
        type: 'list',
        allowBlank: !column.required,
        formulae: ['"Yes,No"'],
        ...message,
        error: 'Choose Yes or No.',
      };
    } else if (column.type === 'integer' || column.type === 'decimal') {
      validations.model[range] = {
        type: column.type === 'integer' ? 'whole' : 'decimal',
        allowBlank: !column.required,
        operator: 'between',
        formulae: [column.min ?? -1e12, column.max ?? 1e12],
        ...message,
        error: `Enter a number${column.min !== undefined || column.max !== undefined ? ` between ${column.min ?? '…'} and ${column.max ?? '…'}` : ''}.`,
      };
    } else if (column.type === 'date') {
      validations.model[range] = {
        type: 'date',
        allowBlank: !column.required,
        operator: 'greaterThan',
        formulae: [new Date(Date.UTC(1900, 0, 1))],
        ...message,
        error: 'Enter a valid date (yyyy-MM-dd).',
      };
    } else if (
      column.maxLength &&
      (column.type === 'text' || column.type === 'email' || column.type === 'phone')
    ) {
      validations.model[range] = {
        type: 'textLength',
        allowBlank: !column.required,
        operator: 'lessThanOrEqual',
        formulae: [column.maxLength],
        ...message,
        error: `At most ${column.maxLength} characters.`,
      };
    }
  }

  /** A professionally formatted export workbook: one main sheet plus optional summary sheets. */
  async buildExport(
    title: string,
    main: ExportSheet,
    extra: readonly ExportSheet[] = [],
  ): Promise<Blob> {
    const { workbook } = await this.newWorkbook(title);
    for (const sheet of [main, ...extra]) {
      this.writeExportSheet(workbook, sheet);
    }
    return this.toBlob(workbook);
  }

  private writeExportSheet(workbook: Workbook, sheet: ExportSheet): void {
    const ws = workbook.addWorksheet(safeSheetName(sheet.name));
    const columns = sheet.columns;
    columns.forEach((column, i) => {
      ws.getCell(1, i + 1).value = column.header;
      const widest = sheet.rows.reduce(
        (max, row) => Math.max(max, String(row[column.key] ?? '').length),
        column.header.length,
      );
      ws.getColumn(i + 1).width = column.width ?? Math.min(50, Math.max(11, widest + 2));
    });
    this.styleHeader(ws, columns.length, []);

    sheet.rows.forEach((record, r) => {
      columns.forEach((column, c) => {
        const cell = ws.getCell(r + 2, c + 1);
        const value = record[column.key] ?? null;
        if (value === null || value === '') return;
        switch (column.type) {
          case 'date': {
            const date = value instanceof Date ? value : dateFromIso(String(value));
            if (date) {
              cell.value = date;
              cell.numFmt = 'yyyy-mm-dd';
            } else {
              cell.value = neutralizeFormula(String(value));
            }
            break;
          }
          case 'integer':
          case 'decimal': {
            const number = typeof value === 'number' ? value : Number(value);
            if (Number.isFinite(number)) {
              cell.value = number;
              cell.numFmt = column.numberFormat ?? (column.type === 'decimal' ? '#,##0.00' : '0');
            } else {
              cell.value = neutralizeFormula(String(value));
            }
            break;
          }
          case 'boolean':
            cell.value = value === true || value === 'true' ? 'Yes' : 'No';
            break;
          default:
            cell.value = neutralizeFormula(String(value));
        }
      });
    });
  }

  /** The downloadable report of validation / import problems. */
  async buildErrorReport(
    moduleLabel: string,
    issues: readonly (ImportIssue & { rowNumber: number })[],
  ): Promise<Blob> {
    const { workbook } = await this.newWorkbook(`${moduleLabel} import report`);
    const ws = workbook.addWorksheet('Validation report');
    ['Row', 'Field', 'Original value', 'Severity', 'Error', 'Suggested correction'].forEach(
      (title, i) => {
        ws.getCell(1, i + 1).value = title;
      },
    );
    [8, 24, 30, 11, 60, 50].forEach((width, i) => (ws.getColumn(i + 1).width = width));
    this.styleHeader(ws, 6, []);
    issues.forEach((issue, i) => {
      const row = i + 2;
      ws.getCell(row, 1).value = issue.rowNumber;
      ws.getCell(row, 2).value = issue.field;
      ws.getCell(row, 3).value = neutralizeFormula(issue.value);
      ws.getCell(row, 4).value = issue.severity;
      ws.getCell(row, 5).value = neutralizeFormula(issue.message);
      ws.getCell(row, 6).value = neutralizeFormula(issue.suggestion ?? '');
      ws.getCell(row, 3).numFmt = '@';
    });
    return this.toBlob(workbook);
  }
}
