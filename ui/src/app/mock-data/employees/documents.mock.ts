import { DocumentType, EmployeeDocument } from '../../core/models/employee-extras.models';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from './employees.mock';

/** Work-permit expiry, in days from today, for specific foreign employees (some expire soon). */
const PERMIT_EXPIRY_DAYS: Record<number, number> = {
  5: 20,
  11: 210,
  18: 340,
  25: 12,
  32: 280,
  51: 390,
  60: 150,
};

function slug(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function generate(): EmployeeDocument[] {
  const today = todayIso();
  const docs: EmployeeDocument[] = [];
  const add = (
    employeeId: number,
    code: string,
    type: DocumentType,
    title: string,
    uploadedOn: string,
    expiryDate: string | null = null,
  ) => {
    docs.push({
      id: docs.length + 1,
      employeeId,
      type,
      title,
      fileName: `${code}-${slug(type)}.pdf`,
      uploadedOn: uploadedOn > today ? today : uploadedOn,
      expiryDate,
    });
  };

  for (const e of MOCK_EMPLOYEES) {
    const foreign = e.nationality !== 'Myanmar';
    add(
      e.id,
      e.employeeCode,
      'National ID',
      foreign ? 'Passport copy' : 'National ID card',
      addDays(e.joiningDate, 1),
    );
    add(e.id, e.employeeCode, 'Resume', 'Curriculum vitae', addDays(e.joiningDate, -14));
    if (e.employmentType !== 'Intern') {
      add(
        e.id,
        e.employeeCode,
        'Employment Contract',
        'Signed employment contract',
        addDays(e.joiningDate, 2),
      );
    }
    if (e.id % 3 === 0) {
      add(e.id, e.employeeCode, 'Offer Letter', 'Offer letter', addDays(e.joiningDate, -20));
    }
    if (e.id % 5 === 0 && !foreign) {
      add(
        e.id,
        e.employeeCode,
        'Certificate',
        'Professional certificate',
        addDays(e.joiningDate, 30),
      );
    }
    if (foreign) {
      const days = PERMIT_EXPIRY_DAYS[e.id] ?? 365;
      add(
        e.id,
        e.employeeCode,
        'Work Permit',
        'Work permit',
        addDays(e.joiningDate, 10),
        addDays(today, days),
      );
    }
  }
  return docs;
}

/** SAMPLE DATA — metadata only. */
export const MOCK_EMPLOYEE_DOCUMENTS: EmployeeDocument[] = generate();
