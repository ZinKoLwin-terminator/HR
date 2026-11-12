import { Component, computed, effect, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Employee, fullName } from '../../../core/models/employee.models';
import { EmployeeDocumentDataService } from '../../../core/services/data/employee-extras-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { PrintHeaderComponent } from '../../../shared/components/print/print-header.component';
import { formatTenure, todayIso, yearsBetween } from '../../../shared/utilities/date.util';
import { OrgLookups, nameMap } from '../../organization/organization-lookups';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

/**
 * Print layout of an employee profile. It shows what the profile screen shows to this user: the
 * personal and bank sections only with `employees:ViewSensitive` / `employees:ViewBank`, and the
 * document list only with `employees:ViewSensitive`. Hidden data is never fetched or printed. The
 * account number is always masked.
 */
@Component({
  selector: 'app-profile-print',
  imports: [AppDatePipe, PrintHeaderComponent],
  template: `
    @let e = employee();
    <app-print-header [title]="'Employee profile · ' + name()" [subtitle]="e.employeeCode" />

    <section class="block" aria-label="Employment">
      <h2>Employment</h2>
      <dl>
        <div>
          <dt>Employee code</dt>
          <dd>{{ e.employeeCode }}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{{ e.employmentStatus }}</dd>
        </div>
        <div>
          <dt>Employment type</dt>
          <dd>{{ e.employmentType }}</dd>
        </div>
        <div>
          <dt>Joining date</dt>
          <dd>{{ e.joiningDate | appDate }} · {{ tenure() }}</dd>
        </div>
        @if (e.exitDate) {
          <div>
            <dt>Exit date</dt>
            <dd>{{ e.exitDate | appDate }}</dd>
          </div>
        }
        <div>
          <dt>Company</dt>
          <dd>{{ names().company(e.companyId) }}</dd>
        </div>
        <div>
          <dt>Branch</dt>
          <dd>{{ names().branch(e.branchId) }}</dd>
        </div>
        <div>
          <dt>Department</dt>
          <dd>{{ names().department(e.departmentId) }}</dd>
        </div>
        <div>
          <dt>Section</dt>
          <dd>{{ names().section(e.sectionId) }}</dd>
        </div>
        <div>
          <dt>Position</dt>
          <dd>{{ names().position(e.positionId) }}</dd>
        </div>
        <div>
          <dt>Job grade</dt>
          <dd>{{ names().grade(e.jobGradeId) }}</dd>
        </div>
        <div>
          <dt>Reporting manager</dt>
          <dd>{{ names().person(e.managerId) }}</dd>
        </div>
        <div>
          <dt>Work email</dt>
          <dd>{{ e.workEmail }}</dd>
        </div>
      </dl>
    </section>

    @if (canSensitive()) {
      <section class="block" aria-label="Personal information">
        <h2>Personal information</h2>
        <dl>
          <div>
            <dt>Gender</dt>
            <dd>{{ e.gender }}</dd>
          </div>
          <div>
            <dt>Date of birth</dt>
            <dd>{{ e.dateOfBirth | appDate }} ({{ age() }} yrs)</dd>
          </div>
          <div>
            <dt>Marital status</dt>
            <dd>{{ e.maritalStatus }}</dd>
          </div>
          <div>
            <dt>Nationality</dt>
            <dd>{{ e.nationality }}</dd>
          </div>
          <div>
            <dt>National ID / passport</dt>
            <dd>{{ e.nationalId }}</dd>
          </div>
          <div>
            <dt>Personal email</dt>
            <dd>{{ e.personalEmail || '—' }}</dd>
          </div>
          <div>
            <dt>Mobile phone</dt>
            <dd>{{ e.phone }}</dd>
          </div>
          <div>
            <dt>Alternate phone</dt>
            <dd>{{ e.alternatePhone || '—' }}</dd>
          </div>
          <div>
            <dt>Address</dt>
            <dd>{{ address() }}</dd>
          </div>
          <div>
            <dt>Emergency contact</dt>
            <dd>
              {{ e.emergencyContact.name }} ({{ e.emergencyContact.relationship }}) ·
              {{ e.emergencyContact.phone }}
            </dd>
          </div>
        </dl>
      </section>
    }

    @if (canBank()) {
      <section class="block" aria-label="Bank information">
        <h2>Bank information</h2>
        <dl>
          <div>
            <dt>Bank</dt>
            <dd>{{ e.bank.bankName }}</dd>
          </div>
          <div>
            <dt>Branch</dt>
            <dd>{{ e.bank.branchName || '—' }}</dd>
          </div>
          <div>
            <dt>Account holder</dt>
            <dd>{{ e.bank.accountName }}</dd>
          </div>
          <div>
            <dt>Account number</dt>
            <dd>{{ maskedAccount() }}</dd>
          </div>
        </dl>
      </section>
    }

    @if (canSensitive() && documents.value(); as docs) {
      <section class="block" aria-label="Documents">
        <h2>Documents</h2>
        @if (docs.items.length) {
          <table class="print-table">
            <thead>
              <tr>
                <th scope="col">Type</th>
                <th scope="col">Title</th>
                <th scope="col">File</th>
                <th scope="col">Uploaded</th>
                <th scope="col">Expires</th>
              </tr>
            </thead>
            <tbody>
              @for (d of docs.items; track d.id) {
                <tr>
                  <td>{{ d.type }}</td>
                  <td>{{ d.title }}</td>
                  <td>{{ d.fileName }}</td>
                  <td>{{ d.uploadedOn | appDate }}</td>
                  <td>{{ d.expiryDate ? (d.expiryDate | appDate) : '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        } @else {
          <p>No documents on file.</p>
        }
      </section>
    }

    <p class="note">Sample data only. This document is generated by a demonstration system.</p>
  `,
  styles: `
    :host {
      display: block;
      font-size: 11.5px;
      color: #000;
    }
    .block {
      margin: 0 0 14px;
      break-inside: avoid;
    }
    h2 {
      margin: 0 0 6px;
      padding-bottom: 2px;
      border-bottom: 1px solid #000;
      font-size: 13px;
      font-weight: 700;
    }
    dl {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 4px 20px;
      margin: 0;
    }
    dt {
      font-size: 10px;
      font-weight: 600;
      text-transform: uppercase;
    }
    dd {
      margin: 0;
    }
    .note {
      margin: 12px 0 0;
      font-size: 10px;
    }
  `,
})
export class ProfilePrintComponent {
  readonly employee = input.required<Employee>();
  readonly org = input.required<OrgLookups>();
  /** Emitted once everything shown is loaded, so the host can open the print dialog. */
  readonly ready = output<void>();

  private readonly permissions = inject(PermissionService);
  private readonly documentsApi = inject(EmployeeDocumentDataService);

  protected readonly canSensitive = computed(() => this.permissions.can('employees:ViewSensitive'));
  protected readonly canBank = computed(() => this.permissions.can('employees:ViewBank'));

  /** Metadata only, and only when the Documents tab is available to this user. */
  protected readonly documents = rxResource({
    params: () => (this.canSensitive() ? { id: this.employee().id } : undefined),
    stream: ({ params }) =>
      this.documentsApi.query({
        pageIndex: 0,
        pageSize: 200,
        filters: { employeeId: params.id },
        sort: { active: 'uploadedOn', direction: 'desc' },
      }),
  });

  protected readonly name = computed(() => fullName(this.employee()));
  protected readonly tenure = computed(() => {
    const e = this.employee();
    return formatTenure(e.joiningDate, e.exitDate ?? todayIso());
  });
  protected readonly age = computed(() => yearsBetween(this.employee().dateOfBirth, todayIso()));
  protected readonly names = computed(() => {
    const o = this.org();
    return {
      company: nameMap(o.companies, (c) => c.name),
      branch: nameMap(o.branches, (b) => b.name),
      department: nameMap(o.departments, (d) => d.name),
      section: nameMap(o.sections, (s) => s.name),
      position: nameMap(o.positions, (p) => p.title),
      grade: nameMap(o.grades, (g) => `${g.code} · ${g.name}`),
      person: nameMap(o.employees, (p) => fullName(p)),
    };
  });
  protected readonly address = computed(() => {
    const a = this.employee().address;
    const place = [a.city, a.state].filter(Boolean).join(', ');
    return [a.line1, a.line2, [place, a.postalCode].filter(Boolean).join(' '), a.country]
      .filter(Boolean)
      .join(', ');
  });
  protected readonly maskedAccount = computed(
    () => `•••• •••• ${this.employee().bank.accountNumber.slice(-4)}`,
  );

  private announced = false;

  constructor() {
    effect(() => {
      if (!this.announced && !this.documents.isLoading()) {
        this.announced = true;
        this.ready.emit();
      }
    });
  }
}
