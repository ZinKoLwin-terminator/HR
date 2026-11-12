import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EmployeeSalaryDataService } from '../../../../core/services/data/employee-extras-data.services';
import { errorMessage } from '../../../../core/services/ui-notification.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';

@Component({
  selector: 'app-profile-salary-tab',
  imports: [MoneyPipe, MatIconModule, MatProgressBarModule, RouterLink],
  template: `
    @if (errorText()) {
      <p class="banner" role="alert">{{ errorText() }}</p>
    } @else if (!summary()) {
      <mat-progress-bar mode="indeterminate" aria-label="Loading salary" />
    } @else {
      @let s = summary()!;
      <p class="banner" role="note">
        <mat-icon aria-hidden="true">science</mat-icon>
        All salary figures on this screen are <strong>sample data</strong> from demonstration rules.
      </p>

      <div class="grid">
        <section class="card" aria-labelledby="sal-monthly">
          <div class="card__header">
            <div>
              <h2 id="sal-monthly">Monthly salary (sample)</h2>
              <p>Grade {{ s.gradeCode }} · {{ s.gradeName }}</p>
            </div>
            <a routerLink="/payroll/employee-salaries" class="link-button">Salary assignments</a>
          </div>
          <table class="simple-table">
            <caption class="cdk-visually-hidden">
              Monthly salary breakdown
            </caption>
            <tbody>
              <tr>
                <th scope="row">Basic salary</th>
                <td class="num">{{ s.basic | money }}</td>
              </tr>
              <tr>
                <th scope="row">Allowances</th>
                <td class="num">{{ s.allowances | money }}</td>
              </tr>
              <tr class="total">
                <th scope="row">Gross salary</th>
                <td class="num">{{ s.gross | money }}</td>
              </tr>
              <tr>
                <th scope="row">Deductions</th>
                <td class="num">− {{ s.deductions | money }}</td>
              </tr>
              <tr class="total net">
                <th scope="row">Net salary</th>
                <td class="num">{{ s.net | money }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="card" aria-labelledby="sal-band">
          <div class="card__header"><h2 id="sal-band">Position in grade band</h2></div>
          <p class="band-values">
            <span>{{ s.bandMin | money }}</span>
            <span>{{ s.bandMax | money }}</span>
          </p>
          <mat-progress-bar
            mode="determinate"
            [value]="s.bandPosition"
            aria-label="Basic salary position in grade band"
          />
          <p class="band-note">
            Basic salary sits at <strong>{{ s.bandPosition }}%</strong> of the
            {{ s.gradeCode }} band.
          </p>

          <h3>Assumptions</h3>
          <ul class="assumptions">
            @for (a of s.assumptions; track a) {
              <li>{{ a }}</li>
            }
          </ul>
        </section>
      </div>
    }
  `,
  styles: `
    .banner {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 20px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .grid {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
      gap: 20px;
    }
    .total th,
    .total td {
      font-weight: 600;
    }
    .net td {
      font-size: 18px;
    }
    .band-values {
      display: flex;
      justify-content: space-between;
      margin: 0 0 8px;
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    .band-note {
      margin: 8px 0 20px;
    }
    h3 {
      margin: 0 0 8px;
      font: var(--mat-sys-title-small);
    }
    .assumptions {
      margin: 0;
      padding-left: 20px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }
    @media (max-width: 899.98px) {
      .grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class ProfileSalaryTabComponent {
  readonly employeeId = input.required<number>();
  private readonly salaryApi = inject(EmployeeSalaryDataService);

  private readonly resource = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) => this.salaryApi.getSummary(params.id),
  });
  protected readonly summary = computed(() => this.resource.value() ?? null);
  protected readonly errorText = computed(() =>
    this.resource.error() ? errorMessage(this.resource.error()) : '',
  );
}
