import { FieldOption } from '../../../shared/components/crud/crud.models';

/** Dropdown options whose label is the value itself (the API's enumerations are plain texts). */
export function textOptions(values: readonly string[]): FieldOption[] {
  return values.map((value) => ({ value, label: value }));
}

/** `1,234.5` style number for cells; null shows a dash. */
export function plain(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : String(value);
}
