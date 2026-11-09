export type ColumnType = 'text' | 'number' | 'date' | 'currency' | 'status';

export interface TableColumn<T> {
  /** Unique id; also the sort key sent in `SortState.active`. */
  key: string;
  header: string;
  type?: ColumnType;
  sortable?: boolean;
  /** Value accessor; defaults to `row[key]`. */
  value?: (row: T) => string | number | null | undefined;
  align?: 'start' | 'end';
  /** Hide on narrow screens to keep tables usable on tablet/mobile. */
  hideBelow?: 'sm' | 'md' | 'lg';
  /** Keep the value on one line (short values such as dates and types). */
  nowrap?: boolean;
  width?: string;
}

export interface TableAction<T> {
  id: string;
  label: string;
  icon: string;
  destructive?: boolean;
  visible?: (row: T) => boolean;
}

export interface TableActionEvent<T> {
  action: string;
  row: T;
}
