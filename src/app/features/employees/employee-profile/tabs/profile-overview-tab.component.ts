import { DatePipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Employee } from '../../../../core/models/employee.models';
import { todayIso, yearsBetween } from '../../../../shared/utilities/date.util';

@Component({
  selector: 'app-profile-overview-tab',
  imports: [DatePipe, MatButtonModule, MatIconModule],
  template: `
    @let e = employee();
    <div class="cards">
      <section class="card" aria-labelledby="ov-personal">
        <div class="card__header"><h2 id="ov-personal">Personal information</h2></div>
        <dl class="info-grid">
          <div>
            <dt>Full name</dt>
            <dd>{{ e.firstName }} {{ e.lastName }}</dd>
          </div>
          <div>
            <dt>Gender</dt>
            <dd>{{ e.gender }}</dd>
          </div>
          <div>
            <dt>Date of birth</dt>
            <dd>{{ e.dateOfBirth | date }} ({{ age() }} yrs)</dd>
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
        </dl>
      </section>

      <section class="card" aria-labelledby="ov-contact">
        <div class="card__header"><h2 id="ov-contact">Contact details</h2></div>
        <dl class="info-grid">
          <div>
            <dt>Work email</dt>
            <dd>
              <a [href]="'mailto:' + e.workEmail">{{ e.workEmail }}</a>
            </dd>
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
        </dl>
      </section>

      <section class="card" aria-labelledby="ov-address">
        <div class="card__header"><h2 id="ov-address">Address</h2></div>
        <address class="address">
          {{ e.address.line1 }}<br />
          @if (e.address.line2) {
            {{ e.address.line2 }}<br />
          }
          {{ cityLine() }}<br />
          {{ e.address.country }}
        </address>
      </section>

      <section class="card" aria-labelledby="ov-emergency">
        <div class="card__header"><h2 id="ov-emergency">Emergency contact</h2></div>
        <dl class="info-grid">
          <div>
            <dt>Name</dt>
            <dd>{{ e.emergencyContact.name }}</dd>
          </div>
          <div>
            <dt>Relationship</dt>
            <dd>{{ e.emergencyContact.relationship }}</dd>
          </div>
          <div>
            <dt>Phone</dt>
            <dd>{{ e.emergencyContact.phone }}</dd>
          </div>
        </dl>
      </section>

      <section class="card wide" aria-labelledby="ov-bank">
        <div class="card__header">
          <h2 id="ov-bank">Bank information</h2>
          <button
            mat-button
            type="button"
            (click)="reveal.set(!reveal())"
            [attr.aria-pressed]="reveal()"
          >
            <mat-icon>{{ reveal() ? 'visibility_off' : 'visibility' }}</mat-icon>
            {{ reveal() ? 'Hide' : 'Show' }} account number
          </button>
        </div>
        <dl class="info-grid">
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
            <dd class="num">{{ accountNumber() }}</dd>
          </div>
        </dl>
        <p class="muted note">Sample data only. No real banking details are stored or used.</p>
      </section>
    </div>
  `,
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
    }
    .card {
      padding: 12px 16px 14px;
    }
    .card__header {
      min-height: 0;
      margin-bottom: 10px;
      padding-bottom: 8px;
    }
    .card__header h2 {
      font: var(--mat-sys-title-small);
      font-size: 14px;
      font-weight: 600;
    }
    // As many columns as fit, so a card is short and wide instead of tall and narrow.
    .info-grid {
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 10px 16px;
    }
    .info-grid dt {
      font-size: 10.5px;
    }
    .info-grid dd {
      margin-top: 1px;
      font: var(--mat-sys-body-medium);
      font-size: 13px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .address {
      font-style: normal;
      font: var(--mat-sys-body-medium);
      font-size: 13px;
      line-height: 1.5;
    }
    .note {
      margin: 8px 0 0;
      font: var(--mat-sys-body-small);
      font-size: 11px;
    }
    @media (max-width: 899.98px) {
      .cards {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class ProfileOverviewTabComponent {
  readonly employee = input.required<Employee>();
  protected readonly reveal = signal(false);
  /** "Yangon, Yangon Region 11041" — skips parts that are empty. */
  protected readonly cityLine = computed(() => {
    const a = this.employee().address;
    const place = [a.city, a.state].filter(Boolean).join(', ');
    return [place, a.postalCode].filter(Boolean).join(' ');
  });
  protected readonly age = computed(() => yearsBetween(this.employee().dateOfBirth, todayIso()));
  protected readonly accountNumber = computed(() => {
    const number = this.employee().bank.accountNumber;
    return this.reveal() ? number : `•••• •••• ${number.slice(-4)}`;
  });
}
