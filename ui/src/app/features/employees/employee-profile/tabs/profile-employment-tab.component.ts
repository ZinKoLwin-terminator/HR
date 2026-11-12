import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Employee, fullName } from '../../../../core/models/employee.models';
import { ShiftDataService } from '../../../../core/services/data/attendance-data.services';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { formatTenure, todayIso } from '../../../../shared/utilities/date.util';
import { OrgLookups, nameMap } from '../../../organization/organization-lookups';
import { AppDatePipe } from '../../../../shared/pipes/app-date.pipe';

@Component({
  selector: 'app-profile-employment-tab',
  imports: [AppDatePipe, RouterLink, StatusBadgeComponent],
  template: `
    @let e = employee();
    <div class="cards">
      <section class="card" aria-labelledby="em-employment">
        <div class="card__header"><h2 id="em-employment">Employment details</h2></div>
        <dl class="info-grid">
          <div>
            <dt>Employee code</dt>
            <dd>{{ e.employeeCode }}</dd>
          </div>
          <div>
            <dt>Employment type</dt>
            <dd>{{ e.employmentType }}</dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd><app-status-badge [status]="e.employmentStatus" /></dd>
          </div>
          <div>
            <dt>Joining date</dt>
            <dd>{{ e.joiningDate | appDate }}</dd>
          </div>
          <div>
            <dt>Tenure</dt>
            <dd>{{ tenure() }}</dd>
          </div>
          <div>
            <dt>Probation end</dt>
            <dd>{{ e.probationEndDate ? (e.probationEndDate | appDate) : '—' }}</dd>
          </div>
          @if (e.exitDate) {
            <div>
              <dt>Exit date</dt>
              <dd>{{ e.exitDate | appDate }}</dd>
            </div>
          }
        </dl>
      </section>

      <section class="card" aria-labelledby="em-org">
        <div class="card__header"><h2 id="em-org">Organization</h2></div>
        <dl class="info-grid">
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
            <dd>
              @if (e.managerId) {
                <a [routerLink]="['/employees', e.managerId]">{{ names().person(e.managerId) }}</a>
              } @else {
                —
              }
            </dd>
          </div>
          <div>
            <dt>Work shift</dt>
            <dd>{{ shiftName() }}</dd>
          </div>
        </dl>
      </section>

      <section class="card wide" aria-labelledby="em-reports">
        <div class="card__header">
          <div>
            <h2 id="em-reports">Direct reports</h2>
            <p>{{ reports().length }} employee{{ reports().length === 1 ? '' : 's' }}</p>
          </div>
        </div>
        @if (reports().length) {
          <ul class="reports">
            @for (r of reports(); track r.id) {
              <li>
                <a [routerLink]="['/employees', r.id]">{{ r.name }}</a>
                <span class="muted">{{ r.title }}</span>
              </li>
            }
          </ul>
        } @else {
          <p class="muted">No direct reports.</p>
        }
      </section>
    </div>
  `,
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 20px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .reports {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 8px 24px;
    }
    .reports li {
      display: flex;
      flex-direction: column;
      padding: 6px 0;
    }
    @media (max-width: 899.98px) {
      .cards {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class ProfileEmploymentTabComponent {
  readonly employee = input.required<Employee>();
  readonly org = input.required<OrgLookups>();
  private readonly shiftsApi = inject(ShiftDataService);
  private readonly shifts = rxResource({ stream: () => this.shiftsApi.getAll() });

  protected readonly tenure = computed(() => {
    const e = this.employee();
    return formatTenure(e.joiningDate, e.exitDate ?? todayIso());
  });

  protected readonly names = computed(() => {
    const o = this.org();
    return {
      company: nameMap(o.companies, (c) => c.name),
      branch: nameMap(o.branches, (b) => b.name),
      department: nameMap(o.departments, (d) => d.name),
      section: nameMap(o.sections, (s) => s.name),
      position: nameMap(o.positions, (p) => p.title),
      grade: nameMap(o.grades, (g) => `${g.code} · ${g.name}`),
      person: nameMap(o.employees, (e) => fullName(e)),
    };
  });

  protected readonly shiftName = computed(() => {
    const id = this.employee().shiftId;
    const shift = this.shifts.value()?.find((s) => s.id === id);
    return shift ? `${shift.name} (${shift.startTime}–${shift.endTime})` : '—';
  });

  protected readonly reports = computed(() => {
    const o = this.org();
    const title = nameMap(o.positions, (p) => p.title);
    return o.employees
      .filter((e) => e.managerId === this.employee().id)
      .map((e) => ({ id: e.id, name: fullName(e), title: title(e.positionId) }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });
}
