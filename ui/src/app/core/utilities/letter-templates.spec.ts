import { LetterData, buildLetter, fillTemplate, formatLetterDate } from './letter-templates';

const base: LetterData = {
  organizationName: 'Example Org',
  date: '2026-11-12',
  referenceNo: 'HR/2026/001',
  signatoryName: 'Fictional Signatory',
  signatoryTitle: 'HR Manager',
  employeeName: 'Aye Aye',
  employeeCode: 'EMP099',
  positionTitle: 'Analyst',
  departmentName: 'Finance',
  employmentType: 'Full-time',
  joiningDate: '2022-03-05',
  exitDate: null,
  leave: null,
};

const approvedLeave = {
  applicationNo: 'LV-0042',
  leaveTypeName: 'Annual Leave',
  startDate: '2026-12-01',
  endDate: '2026-12-03',
  days: 3,
  status: 'Approved',
  actionedOn: '2026-11-10',
};

describe('letter templates', () => {
  it('formats dates and fills placeholders', () => {
    expect(formatLetterDate('2022-03-05')).toBe('5 March 2022');
    expect(formatLetterDate(null)).toBe('');
    expect(fillTemplate('Hello {{name}} {{missing}}', { name: 'Aye' })).toBe(
      'Hello Aye {{missing}}',
    );
  });

  it('fills the employment certificate from the employee data', () => {
    const result = buildLetter('employment', base);
    const text = result.letter?.paragraphs.join('\n') ?? '';
    expect(result.letter?.subject).toBe('Certificate of employment');
    expect(text).toContain('Aye Aye (employee code EMP099)');
    expect(text).toContain('Example Org as Analyst in the Finance department');
    expect(text).toContain('full-time basis, since 5 March 2022');
    expect(text).toContain('issued on 12 November 2026');
    expect(text).not.toContain('{{');
  });

  it('refuses an experience letter without an exit date', () => {
    const result = buildLetter('experience', base);
    expect(result.letter).toBeUndefined();
    expect(result.error).toContain('no exit date');
  });

  it('issues an experience letter when there is an exit date', () => {
    const result = buildLetter('experience', { ...base, exitDate: '2026-10-31' });
    expect(result.letter?.paragraphs.join('\n')).toContain('from 5 March 2022 to 31 October 2026');
  });

  it('refuses a leave letter without a leave application or for one that is not approved', () => {
    expect(buildLetter('leave', base).error).toContain('Choose an approved leave');
    const submitted = buildLetter('leave', {
      ...base,
      leave: { ...approvedLeave, status: 'Submitted' },
    });
    expect(submitted.letter).toBeUndefined();
    expect(submitted.error).toContain('Approved');
  });

  it('fills the leave approval letter from an approved application', () => {
    const result = buildLetter('leave', { ...base, leave: approvedLeave });
    const text = result.letter?.paragraphs.join('\n') ?? '';
    expect(text).toContain('application LV-0042 for Annual Leave');
    expect(text).toContain('3 working day(s), from 1 December 2026 to 3 December 2026');
    expect(text).not.toContain('{{');
  });
});
