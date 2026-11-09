import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveTypeDataService,
} from '../../../../core/services/data/leave-data.services';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-profile-leave-tab',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatProgressBarModule,
    StatusBadgeComponent,
    EmptyStateComponent,
  ],
  template: `
    @if (data.isLoading() && !data.value()) {
      <mat-progress-bar mode="indeterminate" aria-label="Loading leave" />
    } @else {
      <section class="card" aria-labelledby="lv-balance">
        <div class="card__header">
          <div>
            <h2 id="lv-balance">Leave balances · {{ year }}</h2>
            <p>Approved leave counts as taken; submitted requests are held as pending.</p>
          </div>
          <a routerLink="/leave/balances" class="link-button">All balances</a>
        </div>
        <div class="scroll">
          <table class="simple-table">
            <caption class="cdk-visually-hidden">
              Leave balances
            </caption>
            <thead>
              <tr>
                <th scope="col">Leave type</th>
                <th scope="col" class="num">Entitled</th>
                <th scope="col" class="num">Taken</th>
                <th scope="col" class="num">Pending</th>
                <th scope="col" class="num">Available</th>
                <th scope="col" class="usage-col">Used</th>
              </tr>
            </thead>
            <tbody>
              @for (b of balances(); track b.leaveTypeId) {
                <tr>
                  <th scope="row">
                    <span class="dot" [style.background]="b.color" aria-hidden="true"></span
                    >{{ b.name }}
                  </th>
                  <td class="num">{{ b.entitled }}</td>
                  <td class="num">{{ b.taken | number: '1.0-1' }}</td>
                  <td class="num">{{ b.pending | number: '1.0-1' }}</td>
                  <td class="num strong">{{ b.available | number: '1.0-1' }}</td>
                  <td class="usage-col">
                    <mat-progress-bar
                      mode="determinate"
                      [value]="usage(b.taken + b.pending, b.entitled)"
                      [attr.aria-label]="b.name + ' used'"
                    />
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="card apps" aria-labelledby="lv-apps">
        <div class="card__header">
          <div>
            <h2 id="lv-apps">Leave history</h2>
            <p>Most recent applications</p>
          </div>
          <a routerLink="/leave/applications" class="link-button">All applications</a>
        </div>
        @if (applications().length) {
          <div class="scroll">
            <table class="simple-table">
              <caption class="cdk-visually-hidden">
                Leave applications
              </caption>
              <thead>
                <tr>
                  <th scope="col">Application</th>
                  <th scope="col">Type</th>
                  <th scope="col">Dates</th>
                  <th scope="col" class="num">Days</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                @for (a of applications(); track a.id) {
                  <tr>
                    <th scope="row">{{ a.applicationNo }}</th>
                    <td>{{ typeName(a.leaveTypeId) }}</td>
                    <td>
                      {{ a.startDate | date: 'dd MMM' }}
                      @if (a.endDate !== a.startDate) {
                        – {{ a.endDate | date: 'dd MMM yyyy' }}
                      } @else {
                        {{ a.startDate | date: 'yyyy' }}
                      }
                    </td>
                    <td class="num">{{ a.days }}</td>
                    <td><app-status-badge [status]="a.status" /></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <app-empty-state icon="beach_access" title="No leave applications" />
        }
      </section>
    }
  `,
  styles: `
    .apps {
      margin-top: 20px;
    }
    .scroll {
      overflow-x: auto;
    }
    .dot {
      display: inline-block;
      width: 10px;
      height: 10px;
      margin-right: 8px;
      border-radius: 50%;
    }
    .strong {
      font-weight: 600;
    }
    .usage-col {
      width: 160px;
      min-width: 120px;
    }
  `,
})
export class ProfileLeaveTabComponent {
  readonly employeeId = input.required<number>();
  private readonly typesApi = inject(LeaveTypeDataService);
  private readonly applicationsApi = inject(LeaveApplicationDataService);
  private readonly balancesApi = inject(LeaveBalanceDataService);

  protected readonly year = new Date().getFullYear();

  protected readonly data = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      forkJoin({
        types: this.typesApi.getAll(),
        // Same service as the Leave Balances screen, so entitlements follow the leave policies.
        balances: this.balancesApi.getForEmployee(params.id, this.year),
        applications: this.applicationsApi.query({
          pageIndex: 0,
          pageSize: 200,
          filters: { employeeId: params.id },
          sort: { active: 'startDate', direction: 'desc' },
        }),
      }),
  });

  protected readonly applications = computed(() => this.data.value()?.applications.items ?? []);
  protected readonly balances = computed(() => this.data.value()?.balances ?? []);

  protected typeName(id: number): string {
    return this.data.value()?.types.find((t) => t.id === id)?.name ?? `#${id}`;
  }

  protected usage(used: number, entitled: number): number {
    return entitled ? Math.min(100, Math.round((used / entitled) * 100)) : 0;
  }
}
