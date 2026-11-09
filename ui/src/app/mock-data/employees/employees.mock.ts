import {
  Employee,
  EmploymentStatus,
  EmploymentType,
  Gender,
  MaritalStatus,
} from '../../core/models/employee.models';
import { addDays, addMonths, todayIso } from '../../shared/utilities/date.util';
import { seededDigits, seededInt, seededPick } from '../mock-random';
import { MOCK_BRANCHES } from '../organization/branches.mock';
import { MOCK_DEPARTMENTS } from '../organization/departments.mock';
import { MOCK_POSITIONS } from '../organization/positions.mock';

/**
 * Joining date: a fixed ISO date, or a number = days relative to today
 * (used so "new joiners" and probation figures stay realistic over time).
 */
type JoiningDate = string | number;

interface EmployeeSeed {
  first: string;
  last: string;
  gender: Gender;
  positionId: number;
  joined: JoiningDate;
  status?: EmploymentStatus;
  type?: EmploymentType;
  exitDate?: string;
}

const s = (
  first: string,
  last: string,
  gender: 'M' | 'F',
  positionId: number,
  joined: JoiningDate,
  extra: Partial<EmployeeSeed> = {},
): EmployeeSeed => ({
  first,
  last,
  gender: gender === 'M' ? 'Male' : 'Female',
  positionId,
  joined,
  ...extra,
});

// Index + 1 = employee id. Referenced by organization mocks (heads, managers).
const SEED: EmployeeSeed[] = [
  s('Thura', 'Aung', 'M', 1, '2014-03-12'),
  s('Su Myat', 'Noe', 'F', 2, '2015-06-01'),
  s('Kyaw Zin', 'Htet', 'M', 10, '2015-09-14'),
  s('May Thu', 'Kyaw', 'F', 7, '2016-01-04'),
  s('Daniel', 'Carter', 'M', 14, '2018-04-16'),
  s('Hnin Ei', 'Phyu', 'F', 3, '2017-02-20'),
  s('Min Khant', 'Zaw', 'M', 18, '2017-09-04'),
  s('Thandar', 'Win', 'F', 9, '2018-11-05'),
  s('Ye Yint', 'Naing', 'M', 21, '2016-08-22'),
  s('Ei Mon', 'Kyaw', 'F', 16, '2019-03-11'),
  s('Sophia', 'Bennett', 'F', 31, '2022-04-01'),
  s('Zaw Lin', 'Oo', 'M', 26, '2018-07-01'),
  s('Nandar', 'Hlaing', 'F', 4, '2019-10-07'),
  s('Htet Aung', 'Lin', 'M', 11, '2019-05-13'),
  s('Khin', 'Sandar', 'F', 5, '2020-02-03'),
  s('Pyae Phyo', 'Kyaw', 'M', 12, '2019-08-19'),
  s('Phyu Sin', 'Thant', 'F', 13, '2020-06-15'),
  s('Arjun', 'Mehta', 'M', 12, '2021-01-11'),
  s('Wai Yan', 'Soe', 'M', 15, '2021-07-05'),
  s('Yamin', 'Aye', 'F', 17, '2021-09-20'),
  s('Naing Lin', 'Tun', 'M', 19, '2020-10-12'),
  s('Chaw Su', 'Mon', 'F', 6, '2022-01-17'),
  s('Kaung Myat', 'Thu', 'M', 15, '2022-03-07'),
  s('Hsu Myat', 'Wai', 'F', 8, '2021-05-24'),
  s('Michael', 'Tan', 'M', 20, '2022-08-08'),
  s('Shwe Sin', 'Oo', 'F', 24, '2019-05-20'),
  s('Myo Min', 'Thein', 'M', 22, '2020-11-02'),
  s('Thiri', 'Hlaing', 'F', 25, '2022-06-13'),
  s('Si Thu', 'Aung', 'M', 27, '2019-01-21'),
  s('Nilar', 'Win', 'F', 8, '2023-02-06'),
  s('Linn Htet', 'Maung', 'M', 23, '2023-04-17'),
  s('Emily', 'Wong', 'F', 32, '2023-01-09'),
  s('Zin Mar', 'Oo', 'F', 25, '2023-06-05'),
  s('Soe Moe', 'Kyaw', 'M', 28, '2021-03-15', { status: 'Resigned', exitDate: '2026-08-31' }),
  s('Aye Chan', 'Moe', 'F', 19, '2022-10-03', { status: 'On Leave' }),
  s('Thant Zin', 'Phyo', 'M', 11, '2023-08-14'),
  s('Moe Moe', 'Khaing', 'F', 25, '2023-09-11'),
  s('Bo Bo', 'Han', 'M', 30, '2022-02-14', { type: 'Contract' }),
  s('Hla Hla', 'Win', 'F', 13, '2024-01-08'),
  s('Nay Myo', 'Aung', 'M', 29, '2020-01-15'),
  s('Pwint Phyu', 'Thu', 'F', 4, '2024-03-04'),
  s('Ko Ko', 'Gyi', 'M', 28, '2023-05-22', { status: 'Suspended' }),
  s('Sandi', 'Lwin', 'F', 17, '2024-05-13'),
  s('Tun Tun', 'Naing', 'M', 30, '2023-11-06', {
    status: 'Terminated',
    type: 'Contract',
    exitDate: '2026-06-15',
  }),
  s('Wint Yee', 'Mon', 'F', 6, '2024-07-01'),
  s('Hein Thant', 'Zin', 'M', 15, '2024-09-09'),
  s('Lwin Mar', 'Htwe', 'F', 25, '2024-10-14', { type: 'Part-time' }),
  s('Aung Myo', 'Oo', 'M', 28, '2025-01-06'),
  s('Yoon Wadi', 'Kyaw', 'F', 19, '2025-02-17'),
  s('Rahul', 'Sharma', 'M', 15, -12),
  s('Khaing Zar', 'Chi', 'F', 32, '2025-04-07'),
  s('Phone Myint', 'Kyaw', 'M', 22, '2025-06-02'),
  s('Ingyin', 'Thwe', 'F', 33, -2, { type: 'Intern' }),
  s('Zarni', 'Tun', 'M', 30, '2025-08-18', { type: 'Contract' }),
  s('Myat Noe', 'Khin', 'F', 5, '2025-10-06'),
  s('Sai Htoo', 'Aung', 'M', 23, '2026-01-12'),
  s('Nang Hseng', 'Hom', 'F', 25, -35),
  s('Chan Myae', 'Aung', 'M', 33, -20, { type: 'Intern' }),
  s('Thet Htar', 'Swe', 'F', 8, '2026-03-02'),
  s('Andrew', 'Lim', 'M', 20, -50),
];

const BANKS = ['KBZ Bank', 'AYA Bank', 'CB Bank', 'Yoma Bank', 'UAB Bank'] as const;
const STATES: Record<string, string> = {
  Yangon: 'Yangon Region',
  Mandalay: 'Mandalay Region',
  'Nay Pyi Taw': 'Nay Pyi Taw Union Territory',
  Singapore: 'Central Region',
};
const RELATIONSHIPS = ['Spouse', 'Father', 'Mother', 'Sibling'] as const;
const MARITAL: readonly MaritalStatus[] = ['Single', 'Married', 'Married', 'Single', 'Divorced'];
const EMERGENCY_FIRST = ['Aung', 'Hla', 'Myint', 'Khin', 'Tin', 'Win', 'San', 'Mya'] as const;
const EMERGENCY_LAST = ['Myint', 'Soe', 'Htun', 'Nwe', 'Oo', 'Maung', 'Kyi', 'Thein'] as const;
const INTERNATIONAL = new Set(['Daniel', 'Sophia', 'Arjun', 'Michael', 'Emily', 'Rahul', 'Andrew']);
const SHIFT_BY_DEPARTMENT: Record<number, number> = { 8: 4, 9: 2, 10: 2 };

function resolveJoiningDate(joined: JoiningDate): string {
  return typeof joined === 'number' ? addDays(todayIso(), joined) : joined;
}

function emailSlug(value: string): string {
  return value.toLowerCase().replace(/[^a-z]+/g, '');
}

function phone(seed: string, international: boolean): string {
  const d = seededDigits(seed, 9);
  return international
    ? `+65 ${d.slice(0, 4)} ${d.slice(4, 8)}`
    : `+95 9 ${d.slice(0, 3)} ${d.slice(3, 6)} ${d.slice(6, 9)}`;
}

function buildEmployee(seed: EmployeeSeed, index: number): Employee {
  const id = index + 1;
  const key = `emp-${id}`;
  const position = MOCK_POSITIONS.find((p) => p.id === seed.positionId)!;
  const department = MOCK_DEPARTMENTS.find((d) => d.id === position.departmentId)!;
  const branch = MOCK_BRANCHES.find((b) => b.id === department.branchId)!;
  const joiningDate = resolveJoiningDate(seed.joined);
  const isIntl = INTERNATIONAL.has(seed.first);
  const isSenior = position.jobGradeId >= 6;
  const age = seededInt(`${key}:age`, isSenior ? 36 : 22, isSenior ? 55 : 38);
  const birthYear = new Date().getFullYear() - age;
  const dateOfBirth = `${birthYear}-${String(seededInt(`${key}:bm`, 1, 12)).padStart(2, '0')}-${String(
    seededInt(`${key}:bd`, 1, 28),
  ).padStart(2, '0')}`;
  const probationEndDate = addMonths(joiningDate, 3);
  const inProbation = probationEndDate > todayIso();
  const status: EmploymentStatus = seed.status ?? (inProbation ? 'Probation' : 'Active');
  const slug = `${emailSlug(seed.first)}.${emailSlug(seed.last)}`;
  const emergencyName = `${seededPick(`${key}:ef`, EMERGENCY_FIRST)} ${seededPick(`${key}:el`, EMERGENCY_LAST)}`;

  return {
    id,
    employeeCode: `EMP${String(id).padStart(3, '0')}`,
    firstName: seed.first,
    lastName: seed.last,
    gender: seed.gender,
    dateOfBirth,
    maritalStatus: age < 26 ? 'Single' : seededPick(`${key}:ms`, MARITAL),
    nationality: isIntl
      ? seededPick(`${key}:nat`, ['Singaporean', 'Indian', 'British'])
      : 'Myanmar',
    nationalId: isIntl
      ? `P${seededDigits(`${key}:nid`, 8)}`
      : `12/KaMaYa(N)${seededDigits(`${key}:nid`, 6)}`,
    workEmail: `${slug}@apexhorizon.example`,
    personalEmail: `${slug}${seededInt(`${key}:pe`, 10, 99)}@mail.example`,
    phone: phone(`${key}:ph`, isIntl && branch.country === 'Singapore'),
    alternatePhone: '',
    address: {
      line1: `No. ${seededInt(`${key}:addr`, 1, 250)}, ${seededPick(`${key}:street`, [
        'Bogyoke Road',
        'Kabar Aye Pagoda Road',
        'Insein Road',
        '35th Street',
        'Strand Road',
        'Parami Road',
      ])}`,
      line2: '',
      city: branch.city,
      state: STATES[branch.city] ?? '',
      postalCode: seededDigits(`${key}:zip`, 5),
      country: branch.country,
    },
    emergencyContact: {
      name: emergencyName,
      relationship: seededPick(`${key}:rel`, RELATIONSHIPS),
      phone: phone(`${key}:eph`, false),
    },
    companyId: department.companyId,
    branchId: branch.id,
    departmentId: department.id,
    sectionId: position.sectionId,
    positionId: position.id,
    jobGradeId: position.jobGradeId,
    managerId: null, // resolved below from the reporting structure
    shiftId: SHIFT_BY_DEPARTMENT[department.id] ?? 1,
    employmentType: seed.type ?? 'Full-time',
    employmentStatus: status,
    joiningDate,
    probationEndDate,
    exitDate: seed.exitDate ?? null,
    bank: {
      bankName: seededPick(`${key}:bank`, BANKS),
      branchName: `${branch.city} Branch`,
      accountName: `${seed.first} ${seed.last}`.toUpperCase(),
      accountNumber: seededDigits(`${key}:acct`, 12),
    },
    photoUrl: null,
  };
}

function resolveManagers(employees: Employee[]): Employee[] {
  return employees.map((employee) => {
    const position = MOCK_POSITIONS.find((p) => p.id === employee.positionId)!;
    const manager = employees.find(
      (e) => e.positionId === position.reportsToPositionId && e.id !== employee.id,
    );
    return { ...employee, managerId: manager?.id ?? null };
  });
}

/** SAMPLE DATA — fictional people. Contact details use reserved `.example` domains. */
export const MOCK_EMPLOYEES: Employee[] = resolveManagers(SEED.map(buildEmployee));

// One warehouse associate works the evening shift.
MOCK_EMPLOYEES[47].shiftId = 3;
