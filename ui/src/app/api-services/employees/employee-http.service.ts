import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  Observable,
  catchError,
  concatMap,
  defer,
  forkJoin,
  map,
  of,
  shareReplay,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import {
  AppError,
  CreateModel,
  PagedResult,
  PageRequest,
  UpdateModel,
  isAppError,
} from '../../core/models/common.models';
import {
  Employee,
  EmployeeFilter,
  EmployeeListItem,
  EmploymentStatus,
} from '../../core/models/employee.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { PermissionService } from '../../core/services/permission.service';
import {
  BranchDataService,
  DepartmentDataService,
  PositionDataService,
} from '../../core/services/data/organization-data.services';
import { todayIso } from '../../shared/utilities/date.util';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import {
  BANK_FIELD_MAP,
  BankAccountApi,
  BankValues,
  EMPLOYMENT_KEYS,
  EmployeeApi,
  EmployeeListItemApi,
  EmployeePersonalApi,
  PERSONAL_FIELD_MAP,
  PERSONAL_KEYS,
  PersonalValues,
  STATUS_FIELD_MAP,
  NOT_PERMITTED,
  assembleEmployee,
  describeFailure,
  emptyPersonal,
  forbiddenPersonal,
  isMasked,
  maskAccount,
  normalizeEmployee,
  sameValue,
  toListItem,
  toPersonalValues,
} from './employee-api.mapper';

/** Page size used when a screen asks for every employee (the API serves at most 200 per page). */
const ALL_PAGE_SIZE = 200;
/** Safety stop for `getAll`: 50 pages = 10,000 employees. */
const MAX_ALL_PAGES = 50;
/** How long organization names stay cached for list rows. */
const LOOKUP_TTL_MS = 30_000;

/** List sort keys that name a column of the table rather than a field of the API. */
const SORT_KEYS: Record<string, string> = {
  branchName: 'branchId',
  departmentName: 'departmentId',
  positionTitle: 'positionId',
  shiftName: 'shiftId',
};

const FILTER_KEYS = [
  'departmentId',
  'branchId',
  'shiftId',
  'employmentStatus',
  'employmentType',
  'joiningFrom',
  'joiningTo',
] as const;

interface PersonalState {
  values: PersonalValues;
  rowVersion?: string;
  /** True when the API refused (403): `values` are placeholders and must never be written back. */
  forbidden: boolean;
}

interface BankState {
  values: BankValues;
  /** Row version of the bank account row; undefined while the employee has none. */
  rowVersion?: string;
  exists: boolean;
  forbidden: boolean;
}

interface Lookups {
  branches: Map<number, string>;
  departments: Map<number, string>;
  positions: Map<number, string>;
}

const EXITED: readonly EmploymentStatus[] = ['Resigned', 'Terminated'];

/**
 * HTTP implementation of the Employee contract. The API splits what the mock keeps in one object,
 * so the adapter reassembles it:
 *
 *  - list rows: `GET /query` (no personal data) plus branch/department/position names from the
 *    organization contracts. Never calls the sensitive endpoints.
 *  - `getById`: `GET /{id}` + `GET /{id}/personal` + `GET /{id}/bank-account`. A 403 on the last two
 *    gives placeholder text ("Not permitted") instead of failing the screen; a 404 on the bank
 *    account means "no account yet". The profile only gets the MASKED account number: the full number
 *    is read again at save time when the bank section changed and is never kept.
 *  - `update`: PUT /{id} (employment), PUT /{id}/personal, PUT /{id}/bank-account, each only when its
 *    section changed and each with its own rowVersion; a status change calls the exit, reinstate or
 *    status command. A failure names the section that failed.
 *
 * Personal data lives only in memory here, per record, and is dropped on delete. Nothing is written
 * to browser storage.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeHttpService implements EmployeeDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly permissions = inject(PermissionService);
  private readonly branches = inject(BranchDataService);
  private readonly departments = inject(DepartmentDataService);
  private readonly positions = inject(PositionDataService);

  private readonly records = new Map<number, EmployeeApi>();
  private readonly personals = new Map<number, PersonalState>();
  private readonly banks = new Map<number, BankState>();
  private readonly listVersions = new Map<number, string>();

  private lookupCache?: Observable<Lookups>;
  private lookupAt = 0;

  private get url(): string {
    return `${this.baseUrl}/employees`;
  }

  // ---- Reads --------------------------------------------------------------------------------

  /**
   * There is no "get all" endpoint: this pages through `/query` (200 per page). The rows carry no
   * personal data, so personal and bank fields are empty. Used by other screens that only need
   * names and ids.
   */
  getAll(): Observable<Employee[]> {
    const loadPage = (pageIndex: number, acc: EmployeeListItemApi[]): Observable<Employee[]> =>
      this.fetchPage(pageIndex, ALL_PAGE_SIZE, new HttpParams()).pipe(
        switchMap((page) => {
          const all = [...acc, ...page.items];
          return page.items.length === 0 ||
            all.length >= page.total ||
            pageIndex + 1 >= MAX_ALL_PAGES
            ? of(all.map((item) => this.fromListItem(item)))
            : loadPage(pageIndex + 1, all);
        }),
      );
    return loadPage(0, []).pipe(catchError((e) => this.fail(e)));
  }

  getById(id: number): Observable<Employee> {
    return this.http.get<EmployeeApi>(`${this.url}/${id}`).pipe(
      map((dto) => normalizeEmployee(dto)),
      tap((dto) => this.records.set(id, dto)),
      switchMap((dto) =>
        forkJoin({ personal: this.loadPersonal(id), bank: this.loadBank(id, dto) }).pipe(
          map(({ personal, bank }) => assembleEmployee(dto, personal.values, bank.values)),
        ),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  query(request: PageRequest<EmployeeFilter>): Observable<PagedResult<EmployeeListItem>> {
    return forkJoin({
      page: this.fetchPage(request.pageIndex, request.pageSize, this.buildParams(request)),
      lookups: this.lookups(),
    }).pipe(
      map(({ page, lookups }) => ({
        items: page.items.map((item) =>
          toListItem(item, {
            branch: lookups.branches.get(item.branchId) ?? '—',
            department: lookups.departments.get(item.departmentId) ?? '—',
            position: lookups.positions.get(item.positionId) ?? '—',
          }),
        ),
        total: page.total,
        pageIndex: page.pageIndex,
        pageSize: page.pageSize,
      })),
      catchError((e) => this.fail(e)),
    );
  }

  /** Consumes a number of the server's code sequence: call it only when the create form opens. */
  nextEmployeeCode(): Observable<string> {
    return this.http.get<{ code: string }>(`${this.url}/next-code`).pipe(
      map((r) => r.code),
      catchError((e) => this.fail(e)),
    );
  }

  // ---- Create -------------------------------------------------------------------------------

  create(model: CreateModel<Employee>): Observable<Employee> {
    const body = {
      employeeCode: model.employeeCode || undefined,
      firstName: model.firstName,
      lastName: model.lastName,
      gender: model.gender,
      workEmail: model.workEmail,
      photoUrl: model.photoUrl,
      companyId: model.companyId,
      branchId: model.branchId,
      departmentId: model.departmentId,
      sectionId: model.sectionId,
      positionId: model.positionId,
      jobGradeId: model.jobGradeId,
      managerId: model.managerId,
      shiftId: model.shiftId,
      employmentType: model.employmentType,
      joiningDate: model.joiningDate,
      probationEndDate: model.probationEndDate,
      employmentStatus: model.employmentStatus,
      dateOfBirth: model.dateOfBirth,
      maritalStatus: model.maritalStatus,
      nationality: model.nationality,
      nationalId: model.nationalId,
      personalEmail: model.personalEmail || null,
      phone: model.phone,
      alternatePhone: model.alternatePhone || null,
      address: model.address,
      emergencyContact: model.emergencyContact,
    };
    const hasBank = model.bank.accountNumber.trim() !== '';

    return this.http.post<EmployeeApi>(this.url, body).pipe(
      map((dto) => normalizeEmployee(dto)),
      tap((dto) => this.records.set(dto.id, dto)),
      catchError((e) =>
        throwError(() => describeFailure(mapHttpError(e), 'Employee', PERSONAL_FIELD_MAP)),
      ),
      switchMap((dto) =>
        this.saveNewBank(dto, model, hasBank).pipe(
          map((bank) =>
            assembleEmployee(
              dto,
              {
                dateOfBirth: model.dateOfBirth,
                maritalStatus: model.maritalStatus,
                nationality: model.nationality,
                nationalId: model.nationalId,
                personalEmail: model.personalEmail,
                phone: model.phone,
                alternatePhone: model.alternatePhone,
                address: model.address,
                emergencyContact: model.emergencyContact,
              },
              bank?.values ?? { bankName: '', branchName: '', accountName: '', accountNumber: '' },
            ),
          ),
        ),
      ),
    );
  }

  /** The bank account of a new employee is a second call; a failure says the employee exists. */
  private saveNewBank(
    dto: EmployeeApi,
    model: CreateModel<Employee>,
    hasBank: boolean,
  ): Observable<BankState | null> {
    if (!hasBank) {
      return of(null);
    }
    return this.putBank(dto.id, model.bank, undefined).pipe(
      catchError((e) =>
        throwError(() => {
          const error: AppError = isAppError(e) ? e : mapHttpError(e);
          return {
            ...error,
            message: `The employee was created (${dto.employeeCode}), but the bank account was not saved. ${error.message}`,
          } satisfies AppError;
        }),
      ),
    );
  }

  // ---- Update -------------------------------------------------------------------------------

  /**
   * Splits the change by section. A section is only sent when one of its fields differs from what the
   * adapter last read, so saving the whole form does not rewrite sections the user did not touch.
   * Order: employment, personal, bank, then the status command (which needs the newest rowVersion).
   */
  update(id: number, changes: UpdateModel<Employee>): Observable<Employee> {
    return this.ensureRecord(id).pipe(
      concatMap(() => this.saveEmployment(id, changes)),
      concatMap(() => this.savePersonal(id, changes)),
      concatMap(() => this.saveBank(id, changes)),
      concatMap(() => this.saveStatus(id, changes)),
      map(() => this.assemble(id)),
      catchError((e) => this.fail(e)),
    );
  }

  private saveEmployment(id: number, changes: UpdateModel<Employee>): Observable<void> {
    const current = this.records.get(id)!;
    const patch = pick(changes, EMPLOYMENT_KEYS);
    const differs = Object.entries(patch).some(
      ([key, value]) => !sameValue(value, (current as unknown as Record<string, unknown>)[key]),
    );
    if (!differs) {
      return of(undefined);
    }
    const merged = { ...current, ...patch } as EmployeeApi;
    const body = {
      employeeCode: current.employeeCode,
      ...Object.fromEntries(
        EMPLOYMENT_KEYS.map((k) => [k, (merged as unknown as Record<string, unknown>)[k] ?? null]),
      ),
      rowVersion: current.rowVersion,
    };
    return this.http.put<EmployeeApi>(`${this.url}/${id}`, body).pipe(
      tap((dto) => this.records.set(id, normalizeEmployee(dto))),
      map(() => undefined),
      catchError((e) => throwError(() => describeFailure(mapHttpError(e), 'Employment details'))),
    );
  }

  private savePersonal(id: number, changes: UpdateModel<Employee>): Observable<void> {
    const patch = pick(changes, PERSONAL_KEYS);
    if (Object.keys(patch).length === 0) {
      return of(undefined);
    }
    return this.ensurePersonal(id).pipe(
      concatMap((state) => {
        const merged: PersonalValues = { ...state.values, ...patch };
        if (sameValue(merged, state.values)) {
          return of(undefined);
        }
        if (state.forbidden) {
          return throwError(() =>
            describeFailure(forbidden('change personal details'), 'Personal details'),
          );
        }
        return this.http
          .put<EmployeePersonalApi>(`${this.url}/${id}/personal`, {
            ...merged,
            personalEmail: merged.personalEmail || null,
            alternatePhone: merged.alternatePhone || null,
            rowVersion: state.rowVersion,
          })
          .pipe(
            tap((api) =>
              this.personals.set(id, {
                values: toPersonalValues(api),
                rowVersion: api.rowVersion,
                forbidden: false,
              }),
            ),
            map(() => undefined),
            catchError((e) =>
              throwError(() =>
                describeFailure(mapHttpError(e), 'Personal details', PERSONAL_FIELD_MAP),
              ),
            ),
          );
      }),
    );
  }

  private saveBank(id: number, changes: UpdateModel<Employee>): Observable<void> {
    if (!changes.bank) {
      return of(undefined);
    }
    const patch = changes.bank;
    return this.ensureBank(id).pipe(
      concatMap((state) => {
        const merged: BankValues = { ...state.values, ...patch };
        if (sameValue(merged, state.values)) {
          return of(undefined);
        }
        if (state.forbidden) {
          return throwError(() =>
            describeFailure(forbidden('change the bank account'), 'Bank account'),
          );
        }
        if (!state.exists && merged.accountNumber.trim() === '') {
          return of(undefined);
        }
        return this.putBank(id, merged, state).pipe(
          map(() => undefined),
          catchError((e) =>
            throwError(() =>
              describeFailure(isAppError(e) ? e : mapHttpError(e), 'Bank account', BANK_FIELD_MAP),
            ),
          ),
        );
      }),
    );
  }

  /**
   * PUT /{id}/bank-account. When the account number is still the masked value (the user changed
   * another bank field), the full number and the current rowVersion are read first and not kept.
   */
  private putBank(
    id: number,
    values: BankValues,
    state: BankState | undefined,
  ): Observable<BankState> {
    const full$: Observable<{ accountNumber: string; rowVersion?: string }> =
      isMasked(values.accountNumber) && state?.exists
        ? this.http
            .get<BankAccountApi>(`${this.url}/${id}/bank-account`)
            .pipe(map((a) => ({ accountNumber: a.accountNumber, rowVersion: a.rowVersion })))
        : of({ accountNumber: values.accountNumber, rowVersion: state?.rowVersion });
    return full$.pipe(
      switchMap(({ accountNumber, rowVersion }) =>
        this.http.put<BankAccountApi>(`${this.url}/${id}/bank-account`, {
          bankName: values.bankName,
          branchName: values.branchName || undefined,
          accountName: values.accountName,
          accountNumber,
          rowVersion,
        }),
      ),
      map((api) => this.bankState(api, undefined)),
      tap((bank) => this.banks.set(id, bank)),
    );
  }

  /**
   * Status is never part of a PUT. A change calls the command: exit (Resigned/Terminated),
   * reinstate (leaving an exited status) or the plain status command.
   */
  private saveStatus(id: number, changes: UpdateModel<Employee>): Observable<void> {
    const next = changes.employmentStatus;
    const current = this.records.get(id)!;
    if (next === undefined || next === current.employmentStatus) {
      return of(undefined);
    }
    let command: string;
    let body: Record<string, unknown>;
    if (EXITED.includes(next)) {
      command = 'exit';
      body = {
        rowVersion: current.rowVersion,
        exitDate: changes.exitDate || todayIso(),
        status: next,
      };
    } else if (EXITED.includes(current.employmentStatus)) {
      command = 'reinstate';
      body = { rowVersion: current.rowVersion, status: next };
    } else {
      command = 'status';
      body = { rowVersion: current.rowVersion, status: next };
    }
    return this.http.post<EmployeeApi>(`${this.url}/${id}/${command}`, body).pipe(
      tap((dto) => this.records.set(id, normalizeEmployee(dto))),
      map(() => undefined),
      catchError((e) =>
        throwError(() => describeFailure(mapHttpError(e), 'Employment status', STATUS_FIELD_MAP)),
      ),
    );
  }

  // ---- Delete -------------------------------------------------------------------------------

  delete(id: number): Observable<void> {
    return this.rowVersionFor(id).pipe(
      switchMap((rowVersion) =>
        this.http.delete<void>(`${this.url}/${id}`, { body: { rowVersion } }),
      ),
      tap(() => this.forget(id)),
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  // ---- Helpers ------------------------------------------------------------------------------

  private fetchPage(pageIndex: number, pageSize: number, params: HttpParams) {
    return this.http
      .get<PagedResult<EmployeeListItemApi>>(`${this.url}/query`, {
        params: params.set('pageIndex', pageIndex).set('pageSize', pageSize),
      })
      .pipe(tap((page) => page.items.forEach((item) => this.rememberListItem(item))));
  }

  private buildParams(request: PageRequest<EmployeeFilter>): HttpParams {
    let params = new HttpParams();
    if (request.sort?.active && request.sort.direction) {
      const field = SORT_KEYS[request.sort.active] ?? request.sort.active;
      params = params.set('sort', `${field}:${request.sort.direction}`);
    }
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    for (const key of FILTER_KEYS) {
      const value = request.filters?.[key];
      if (value === null || value === undefined) {
        continue;
      }
      const text = String(value).trim();
      if (text !== '' && text.toLowerCase() !== 'all') {
        params = params.set(key, text);
      }
    }
    return params;
  }

  /** Remembers the newest rowVersion; a cached record older than the list row is dropped. */
  private rememberListItem(item: EmployeeListItemApi): void {
    this.listVersions.set(item.id, item.rowVersion);
    const cached = this.records.get(item.id);
    if (cached && cached.rowVersion !== item.rowVersion) {
      this.records.delete(item.id);
    }
  }

  private fromListItem(item: EmployeeListItemApi): Employee {
    return {
      id: item.id,
      employeeCode: item.employeeCode,
      firstName: item.firstName,
      lastName: item.lastName,
      gender: item.gender,
      workEmail: item.workEmail,
      photoUrl: item.photoUrl || null,
      companyId: item.companyId,
      branchId: item.branchId,
      departmentId: item.departmentId,
      positionId: item.positionId,
      shiftId: item.shiftId ?? null,
      employmentType: item.employmentType,
      employmentStatus: item.employmentStatus,
      joiningDate: item.joiningDate,
      // Not in the list DTO (see the report): the screens that need them load the record by id.
      sectionId: null,
      jobGradeId: 0,
      managerId: null,
      probationEndDate: null,
      exitDate: null,
      ...emptyPersonal(),
      bank: { bankName: '', branchName: '', accountName: '', accountNumber: '' },
    };
  }

  private assemble(id: number): Employee {
    const dto = this.records.get(id)!;
    const personal = this.personals.get(id)?.values ?? emptyPersonal();
    const bank = this.banks.get(id)?.values ?? {
      bankName: '',
      branchName: '',
      accountName: '',
      accountNumber: dto.bankAccountMasked ?? '',
    };
    return assembleEmployee(dto, personal, bank);
  }

  private ensureRecord(id: number): Observable<EmployeeApi> {
    const cached = this.records.get(id);
    if (cached) {
      return of(cached);
    }
    return this.http.get<EmployeeApi>(`${this.url}/${id}`).pipe(
      map((dto) => normalizeEmployee(dto)),
      tap((dto) => this.records.set(id, dto)),
    );
  }

  private rowVersionFor(id: number): Observable<string> {
    const cached = this.records.get(id)?.rowVersion ?? this.listVersions.get(id);
    return cached ? of(cached) : this.ensureRecord(id).pipe(map((dto) => dto.rowVersion));
  }

  private ensurePersonal(id: number): Observable<PersonalState> {
    const cached = this.personals.get(id);
    return cached ? of(cached) : this.loadPersonal(id);
  }

  private ensureBank(id: number): Observable<BankState> {
    const cached = this.banks.get(id);
    if (cached) {
      return of(cached);
    }
    return this.ensureRecord(id).pipe(switchMap((dto) => this.loadBank(id, dto)));
  }

  /** GET /{id}/personal. A 403 gives placeholders; every other failure is an error. */
  private loadPersonal(id: number): Observable<PersonalState> {
    // Without the key the call can only answer 403 (and a toast): do not make it.
    if (!this.permissions.can('employees:ViewSensitive')) {
      const state: PersonalState = { values: forbiddenPersonal(), forbidden: true };
      this.personals.set(id, state);
      return of(state);
    }
    return this.http.get<EmployeePersonalApi>(`${this.url}/${id}/personal`).pipe(
      map((api) => ({
        values: toPersonalValues(api),
        rowVersion: api.rowVersion,
        forbidden: false,
      })),
      catchError((e) => {
        if (mapHttpError(e).code === 'Forbidden') {
          return of({ values: forbiddenPersonal(), forbidden: true });
        }
        return throwError(() => e);
      }),
      tap((state) => this.personals.set(id, state)),
    );
  }

  /**
   * GET /{id}/bank-account. 403: placeholder; 404: the employee has no account yet. The full number
   * is replaced by its masked form before anything is stored or returned.
   */
  private loadBank(id: number, dto: EmployeeApi): Observable<BankState> {
    const masked = dto.bankAccountMasked ?? '';
    if (!this.permissions.can('employees:ViewBank')) {
      const state: BankState = {
        values: {
          bankName: NOT_PERMITTED,
          branchName: '',
          accountName: NOT_PERMITTED,
          accountNumber: masked,
        },
        exists: masked !== '',
        forbidden: true,
      };
      this.banks.set(id, state);
      return of(state);
    }
    return this.http.get<BankAccountApi>(`${this.url}/${id}/bank-account`).pipe(
      map((api) => this.bankState(api, masked)),
      catchError((e) => {
        const code = mapHttpError(e).code;
        if (code === 'Forbidden') {
          return of<BankState>({
            values: {
              bankName: NOT_PERMITTED,
              branchName: '',
              accountName: NOT_PERMITTED,
              accountNumber: masked,
            },
            exists: masked !== '',
            forbidden: true,
          });
        }
        if (code === 'NotFound') {
          return of<BankState>({
            values: { bankName: '', branchName: '', accountName: '', accountNumber: '' },
            exists: false,
            forbidden: false,
          });
        }
        return throwError(() => e);
      }),
      tap((state) => this.banks.set(id, state)),
    );
  }

  private bankState(api: BankAccountApi, masked: string | undefined): BankState {
    return {
      values: {
        bankName: api.bankName,
        branchName: api.branchName ?? '',
        accountName: api.accountName,
        accountNumber: masked || maskAccount(api.accountNumber),
      },
      rowVersion: api.rowVersion,
      exists: true,
      forbidden: false,
    };
  }

  private forget(id: number): void {
    this.records.delete(id);
    this.personals.delete(id);
    this.banks.delete(id);
    this.listVersions.delete(id);
  }

  /** Branch, department and position names for list rows; refreshed after a short while. */
  private lookups(): Observable<Lookups> {
    const now = Date.now();
    if (!this.lookupCache || now - this.lookupAt > LOOKUP_TTL_MS) {
      this.lookupAt = now;
      const names = <T extends { id: number }>(source: Observable<T[]>, label: (x: T) => string) =>
        source.pipe(
          map((items) => new Map(items.map((x) => [x.id, label(x)] as const))),
          catchError(() => of(new Map<number, string>())),
        );
      this.lookupCache = defer(() =>
        forkJoin({
          branches: names(this.branches.getAll(), (b) => b.name),
          departments: names(this.departments.getAll(), (d) => d.name),
          positions: names(this.positions.getAll(), (p) => p.title),
        }),
      ).pipe(shareReplay(1));
    }
    return this.lookupCache;
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}

function forbidden(action: string): AppError {
  return { code: 'Forbidden', message: `You are not allowed to ${action}.` };
}

/** The listed keys that are present in `source`. */
function pick<T extends object, K extends keyof T>(
  source: T,
  keys: readonly K[],
): Partial<Pick<T, K>> {
  const result: Partial<Pick<T, K>> = {};
  for (const key of keys) {
    if (key in source && source[key] !== undefined) {
      result[key] = source[key];
    }
  }
  return result;
}
