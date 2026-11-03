import { Entity } from './common.models';

export type DocumentType =
  | 'National ID'
  | 'Passport'
  | 'Resume'
  | 'Offer Letter'
  | 'Employment Contract'
  | 'Certificate'
  | 'Work Permit'
  | 'Medical Report'
  | 'Other';

export const DOCUMENT_TYPES: readonly DocumentType[] = [
  'National ID',
  'Passport',
  'Resume',
  'Offer Letter',
  'Employment Contract',
  'Certificate',
  'Work Permit',
  'Medical Report',
  'Other',
];

/** Document metadata. The demo does not store file contents. */
export interface EmployeeDocument extends Entity {
  employeeId: number;
  type: DocumentType;
  title: string;
  fileName: string;
  uploadedOn: string;
  /** yyyy-MM-dd, for documents that expire (work permits, passports). */
  expiryDate: string | null;
}

export type HistoryEventType =
  | 'Joined'
  | 'Confirmation'
  | 'Promotion'
  | 'Transfer'
  | 'Salary Revision'
  | 'Contract Change'
  | 'Status Change'
  | 'Exit';

export interface EmploymentHistoryEntry extends Entity {
  employeeId: number;
  date: string;
  type: HistoryEventType;
  title: string;
  details: string;
  recordedBy: string;
}

/** SAMPLE salary overview — produced from configurable sample rules, not real payroll. */
export interface EmployeeSalarySummary {
  employeeId: number;
  basic: number;
  allowances: number;
  gross: number;
  deductions: number;
  net: number;
  gradeCode: string;
  gradeName: string;
  bandMin: number;
  bandMax: number;
  /** 0–100: where basic salary sits inside the grade band. */
  bandPosition: number;
  /** Plain-language description of the sample rules behind these figures. */
  assumptions: string[];
}
