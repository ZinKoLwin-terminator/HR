import { ExcelColumn } from '../../core/models/excel.models';
import {
  RawCell,
  closestAllowed,
  coerceCell,
  matchHeaders,
  matchKeyOf,
  mappingProblems,
  neutralizeFormula,
  normalizeHeader,
} from './excel-coercion';

const col = (overrides: Partial<ExcelColumn>): ExcelColumn => ({
  key: 'x',
  header: 'X',
  description: '',
  required: false,
  type: 'text',
  example: null,
  ...overrides,
});
const cell = (value: RawCell['value'], formula = false): RawCell => ({ value, formula });

describe('excel coercion helpers', () => {
  it('normalises headers', () => {
    expect(normalizeHeader(' Employee  Code * ')).toBe('employeecode');
    expect(normalizeHeader('E-mail address')).toBe('emailaddress');
  });

  it('matches headers, aliases and reports missing, unknown and ambiguous ones', () => {
    const columns = [
      col({ key: 'a', header: 'Alpha', required: true, aliases: ['First'] }),
      col({ key: 'b', header: 'Beta', required: true }),
    ];
    const ok = matchHeaders(['first', 'BETA', 'Other'], columns);
    expect(ok.mapping).toEqual({ first: 'a', BETA: 'b', Other: null });
    expect(ok.unknownHeaders).toEqual(['Other']);
    expect(ok.missingRequired).toEqual([]);
    const bad = matchHeaders(['Alpha', 'First'], columns);
    expect(bad.ambiguousHeaders).toEqual(['Alpha', 'First']);
    expect(bad.missingRequired).toEqual(['b']);
    expect(mappingProblems({ Alpha: 'a', First: 'a', Beta: 'b' }, columns)[0]).toMatch(
      /both mapped/,
    );
    expect(mappingProblems({ Alpha: 'zzz' }, columns).join(' ')).toMatch(/unknown field/);
  });

  it('converts dates from cells, text and Excel serial numbers', () => {
    const date = col({ type: 'date', header: 'D' });
    expect(coerceCell(date, cell(new Date(Date.UTC(2026, 0, 5)))).value).toBe('2026-01-05');
    expect(coerceCell(date, cell('2026-12-31')).value).toBe('2026-12-31');
    expect(coerceCell(date, cell('3 Feb 2026')).value).toBe('2026-02-03');
    expect(coerceCell(date, cell(46023)).value).toBe('2026-01-01'); // Excel serial
    for (const bad of ['2026-13-01', '31/12/2026', 'tomorrow', true, 0]) {
      expect(coerceCell(date, cell(bad as string)).error).toMatch(/not a valid date/);
    }
  });

  it('converts times, booleans and numbers', () => {
    expect(coerceCell(col({ type: 'time' }), cell('8:05')).value).toBe('08:05');
    expect(coerceCell(col({ type: 'time' }), cell(0.5)).value).toBe('12:00');
    expect(coerceCell(col({ type: 'time' }), cell('25:00')).error).toMatch(/valid time/);
    expect(coerceCell(col({ type: 'boolean' }), cell('Yes')).value).toBe(true);
    expect(coerceCell(col({ type: 'boolean' }), cell('no')).value).toBe(false);
    expect(coerceCell(col({ type: 'boolean' }), cell('maybe')).error).toMatch(/Yes or No/);
    expect(coerceCell(col({ type: 'integer', min: 1, max: 3 }), cell('2')).value).toBe(2);
    expect(coerceCell(col({ type: 'integer', min: 1 }), cell(0)).error).toMatch(/at least 1/);
    expect(coerceCell(col({ type: 'decimal', decimals: 2 }), cell(1.005)).error).toMatch(
      /2 decimal places/,
    );
    expect(coerceCell(col({ type: 'decimal', decimals: 2 }), cell(10.25)).value).toBe(10.25);
  });

  it('reports required, empty and formula cells', () => {
    expect(coerceCell(col({ required: true, header: 'Name' }), cell(null)).error).toBe(
      'Name is required.',
    );
    expect(coerceCell(col({ required: true, header: 'Name' }), cell('   ')).error).toBe(
      'Name is required.',
    );
    expect(coerceCell(col({}), cell(null)).error).toBeNull();
    expect(coerceCell(col({}), cell(null, true)).error).toMatch(/Formulas are not allowed/);
  });

  it('matches enum values case-insensitively and suggests the closest one', () => {
    const status = col({
      type: 'enum',
      header: 'Status',
      allowed: ['Active', 'Inactive', 'On Leave'],
    });
    expect(coerceCell(status, cell('active')).value).toBe('Active');
    expect(coerceCell(status, cell('Actve')).suggestion).toContain('Did you mean "Active"');
    expect(coerceCell(status, cell('zzzzzz')).suggestion).toBe(
      'Allowed values: Active, Inactive, On Leave.',
    );
    expect(closestAllowed('inactiv', ['Active', 'Inactive'])).toBe('Inactive');
  });

  it('validates emails and phone numbers', () => {
    expect(coerceCell(col({ type: 'email' }), cell('a@b.co')).value).toBe('a@b.co');
    expect(coerceCell(col({ type: 'email' }), cell('nope')).error).toMatch(/valid email/);
    expect(coerceCell(col({ type: 'phone' }), cell('+95 9 123 456 789')).value).toBe(
      '+95 9 123 456 789',
    );
    expect(coerceCell(col({ type: 'phone' }), cell('12')).error).toMatch(/valid phone/);
    expect(coerceCell(col({ type: 'phone' }), cell(959123456789)).value).toBe('959123456789'); // number cell → text
  });

  it('builds a case-insensitive match key and skips incomplete keys', () => {
    expect(matchKeyOf({ a: 'EMP1', b: 'X' }, ['a', 'b'])).toBe(
      matchKeyOf({ a: 'emp1', b: 'x ' }, ['a', 'b']),
    );
    expect(matchKeyOf({ a: null, b: 'X' }, ['a', 'b'])).toBeNull();
  });

  describe('neutralizeFormula', () => {
    it('prefixes text that spreadsheet software could run as a formula', () => {
      for (const text of [
        '=SUM(A1)',
        '+cmd|calc',
        '-2+3+cmd',
        '@SUM(1)',
        '\tcmd',
        '\rcmd',
        '=1+1',
      ]) {
        expect(neutralizeFormula(text)).toBe(`'${text}`);
      }
    });
    it('leaves ordinary text, numbers and phone numbers alone', () => {
      for (const text of [
        'Hello',
        'a=b',
        '-5',
        '+95 9 123 456',
        '-1.5',
        '(+1) 555-1234',
        '',
        "'quoted",
      ]) {
        expect(neutralizeFormula(text)).toBe(text);
      }
    });
  });
});
