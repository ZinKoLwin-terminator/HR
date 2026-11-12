/**
 * Plain-text letter templates for the Letters screen. This is the specification of what each letter
 * says and when it may be issued, so a future server can apply the same rules. All text is sample
 * text for demonstration; it is not legal wording.
 */

export type LetterKind = 'employment' | 'experience' | 'leave';

export interface LetterKindInfo {
  kind: LetterKind;
  label: string;
}

export const LETTER_KINDS: readonly LetterKindInfo[] = [
  { kind: 'employment', label: 'Employment certificate' },
  { kind: 'experience', label: 'Experience letter' },
  { kind: 'leave', label: 'Leave approval letter' },
];

/** Everything a template can use; built from existing employee, leave and settings data. */
export interface LetterData {
  organizationName: string;
  /** Letter date (yyyy-MM-dd). */
  date: string;
  /** Typed by the issuer. */
  referenceNo: string;
  signatoryName: string;
  signatoryTitle: string;
  employeeName: string;
  employeeCode: string;
  positionTitle: string;
  departmentName: string;
  employmentType: string;
  /** yyyy-MM-dd */
  joiningDate: string;
  /** yyyy-MM-dd, or null for a current employee. */
  exitDate: string | null;
  /** Needed for the leave letter only. */
  leave?: {
    applicationNo: string;
    leaveTypeName: string;
    startDate: string;
    endDate: string;
    days: number;
    status: string;
    actionedOn: string | null;
  } | null;
}

export interface Letter {
  subject: string;
  /** Paragraphs, already filled in. */
  paragraphs: string[];
}

export type LetterResult =
  { letter: Letter; error?: undefined } | { letter?: undefined; error: string };

interface Template {
  subject: string;
  /** Paragraphs separated by a blank line. `{{name}}` placeholders are filled from the data. */
  body: string;
}

export const LETTER_TEMPLATES: Readonly<Record<LetterKind, Template>> = {
  employment: {
    subject: 'Certificate of employment',
    body: [
      'To whom it may concern,',
      'This is to certify that {{employeeName}} (employee code {{employeeCode}}) is employed by {{organizationName}} as {{positionTitle}} in the {{departmentName}} department, on a {{employmentType}} basis, since {{joiningDate}}.',
      'This certificate is issued on {{date}} at the request of the employee, for whatever lawful purpose it may serve.',
    ].join('\n\n'),
  },
  experience: {
    subject: 'Experience letter',
    body: [
      'To whom it may concern,',
      'This is to certify that {{employeeName}} (employee code {{employeeCode}}) worked for {{organizationName}} as {{positionTitle}} in the {{departmentName}} department from {{joiningDate}} to {{exitDate}}.',
      'We thank {{employeeName}} for the service given to the organization and wish them every success in the future.',
    ].join('\n\n'),
  },
  leave: {
    subject: 'Leave approval',
    body: [
      'Dear {{employeeName}},',
      'We confirm that your application {{applicationNo}} for {{leaveTypeName}} has been approved for {{days}} working day(s), from {{startDate}} to {{endDate}}.',
      'Please hand over your duties as {{positionTitle}} in the {{departmentName}} department before you leave, and report back on the first working day after the leave ends.',
    ].join('\n\n'),
  },
};

/** "5 March 2026" from "2026-03-05"; the input is returned unchanged when it is not a date. */
export function formatLetterDate(iso: string | null | undefined): string {
  if (!iso || !/^\d{4}-\d{2}-\d{2}/.test(iso)) {
    return iso ?? '';
  }
  return new Date(`${iso.slice(0, 10)}T00:00:00Z`).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

/** Replaces every `{{key}}` with its value. A placeholder with no value stays visible. */
export function fillTemplate(template: string, values: Record<string, string>): string {
  return template.replace(/\{\{(\w+)\}\}/g, (match, key: string) => values[key] ?? match);
}

/**
 * Why a letter cannot be issued, or null. An experience letter needs an exit date; a leave letter
 * needs an Approved leave application.
 */
export function letterBlocker(kind: LetterKind, data: LetterData): string | null {
  if (kind === 'experience' && !data.exitDate) {
    return 'An experience letter can only be issued to an employee who has left: this employee has no exit date.';
  }
  if (kind === 'leave') {
    if (!data.leave) {
      return 'Choose an approved leave application first.';
    }
    if (data.leave.status !== 'Approved') {
      return 'A leave approval letter can only be issued for an Approved leave application.';
    }
  }
  return null;
}

/** Builds the letter, or says why it cannot be issued. */
export function buildLetter(kind: LetterKind, data: LetterData): LetterResult {
  const error = letterBlocker(kind, data);
  if (error) {
    return { error };
  }
  const template = LETTER_TEMPLATES[kind];
  const values: Record<string, string> = {
    organizationName: data.organizationName,
    date: formatLetterDate(data.date),
    employeeName: data.employeeName,
    employeeCode: data.employeeCode,
    positionTitle: data.positionTitle,
    departmentName: data.departmentName,
    employmentType: data.employmentType.toLowerCase(),
    joiningDate: formatLetterDate(data.joiningDate),
    exitDate: formatLetterDate(data.exitDate),
    ...(data.leave
      ? {
          applicationNo: data.leave.applicationNo,
          leaveTypeName: data.leave.leaveTypeName,
          startDate: formatLetterDate(data.leave.startDate),
          endDate: formatLetterDate(data.leave.endDate),
          days: String(data.leave.days),
        }
      : {}),
  };
  return {
    letter: {
      subject: fillTemplate(template.subject, values),
      paragraphs: fillTemplate(template.body, values).split('\n\n'),
    },
  };
}
