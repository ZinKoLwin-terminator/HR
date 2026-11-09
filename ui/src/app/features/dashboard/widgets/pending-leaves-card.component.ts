import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PendingLeaveItem } from '../../../core/models/dashboard.models';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-pending-leaves-card',
  imports: [DatePipe, RouterLink, AvatarComponent, EmptyStateComponent],
  template: `
    <section class="card" aria-labelledby="pending-heading">
      <div class="card__header">
        <div>
          <h2 id="pending-heading">Pending leave approvals</h2>
          <p>{{ total() }} request{{ total() === 1 ? '' : 's' }} awaiting a decision</p>
        </div>
        <a routerLink="/leave/approvals" class="link-button">Review all</a>
      </div>
      <ul class="list">
        @for (item of items(); track item.id) {
          <li class="list__item">
            <app-avatar [name]="item.employeeName" [size]="36" />
            <div class="list__text">
              <span class="list__title">{{ item.employeeName }}</span>
              <span class="list__meta"
                >{{ item.leaveTypeName }} · {{ item.startDate | date: 'dd MMM' }}
                @if (item.endDate !== item.startDate) {
                  – {{ item.endDate | date: 'dd MMM' }}
                }
              </span>
            </div>
            <span class="days">{{ item.days }} day{{ item.days === 1 ? '' : 's' }}</span>
          </li>
        } @empty {
          <li>
            <app-empty-state
              icon="task_alt"
              title="All caught up"
              message="No leave requests are waiting."
            />
          </li>
        }
      </ul>
    </section>
  `,
  styleUrl: './dashboard-list.scss',
  styles: `
    .days {
      flex: none;
      padding: 2px 10px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class PendingLeavesCardComponent {
  readonly items = input.required<PendingLeaveItem[]>();
  readonly total = input(0);
}
