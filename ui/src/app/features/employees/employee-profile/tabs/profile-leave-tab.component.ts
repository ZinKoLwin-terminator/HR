import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { LeaveBalance } from '../../../../core/models/leave.models';
import { balanceYearOptions } from '../../../../core/utilities/leave-balance';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveTypeDataService,
} from '../../../../core/services/data/leave-data.services';
import { errorMessage } from '../../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { safeValue } from '../../../../shared/utilities/resource-value';

@Component({
  selector: 'app-profile-leave-tab',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatProgressBarModule,
    MatSelectModule,
    StatusBadgeComponent,
    EmptyStateComponent,
  ],
  template: `
    @if (data.isLoading() && !data.hasValue()) {
      <mat-progress-bar mode="indeterminate" aria-label="Loading leave" />
    } @else if (data.error()) {
      <app-empty-state icon="error" title="Leave could not be loaded" [message]="errorText()">
        <button mat-flat-button type="button" (click)="data.reload()">Try again</button>
      </app-empty-state>
    } @else {
      <section class="card" aria-labelledby="lv-balance">
        <div class="card__header">
          <div>
            <h2 id="lv-balance">Leave balances · {{ year() }}</h2>
            <p>
              Approved leave counts as taken; submitted requests are held as pending. Leave counts
              in the year it starts.
            </p>
          </div>
          <div class="header-tools">
            <mat-form-field subscriptSizing="dynamic" class="year-field">
              <mat-select
                aria-label="Balance year"
                [value]="year()"
                (selectionChange)="year.set($event.value)"
              >
                @for (y of yearOptions; track y) {
                  <mat-option [value]="y">{{ y }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <a routerLink="/leave/balances" class="link-button">All balances</a>
          </div>
        </div>
        @if (balanceData.isLoading()) {
          <mat-progress-bar mode="indeterminate" aria-label="Loading leave balances" />
        }
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
    .header-tools {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
    }
    .year-field {
      width: 110px;
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

  /** Next year, this year and last year: leave applied for in advance counts in next year. */
  protected readonly yearOptions = balanceYearOptions(new Date().getFullYear());
  protected readonly year = signal(new Date().getFullYear());

  protected readonly errorText = computed(() =>
    this.data.error() ? errorMessage(this.data.error()) : '',
  );
  protected readonly data = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      forkJoin({
        types: this.typesApi.getAll(),
        applications: this.applicationsApi.query({
          pageIndex: 0,
          pageSize: 200,
          filters: { employeeId: params.id },
          sort: { active: 'startDate', direction: 'desc' },
        }),
      }),
  });

  /** Loaded on its own so that changing the year does not reload the leave history. */
  protected readonly balanceData = rxResource({
    params: () => ({ id: this.employeeId(), year: this.year() }),
    // Same service as the Leave Balances screen, so entitlements follow the leave policies.
    stream: ({ params }) => this.balancesApi.getForEmployee(params.id, params.year),
  });

  protected readonly applications = computed(() => safeValue(this.data)?.applications.items ?? []);
  /** Keeps the previous year's rows on screen while the next year loads. */
  protected readonly balances = linkedSignal<LeaveBalance[] | undefined, LeaveBalance[]>({
    source: () => safeValue(this.balanceData),
    computation: (value, previous) => value ?? previous?.value ?? [],
  });

  protected typeName(id: number): string {
    return safeValue(this.data)?.types.find((t) => t.id === id)?.name ?? `#${id}`;
  }

  protected usage(used: number, entitled: number): number {
    return entitled ? Math.min(100, Math.round((used / entitled) * 100)) : 0;
  }
}
