import { Component, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { HasUnsavedChanges } from '../../../core/guards/unsaved-changes.guard';
import { isAppError } from '../../../core/models/common.models';
import { DATE_FORMATS, GeneralSettings, TIMEZONES } from '../../../core/models/settings.models';
import { AppSettingsService } from '../../../core/services/app-settings.service';
import { AuthService, SERVER_SESSION } from '../../../core/services/auth.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { DemoDataDataService } from '../../../core/services/data/demo-data-data.service';
import { GeneralSettingsDataService } from '../../../core/services/data/settings-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

/** Organization-wide preferences. Saving applies them across the app through AppSettingsService. */
@Component({
  selector: 'app-general-settings',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    PageHeaderComponent,
    EmptyStateComponent,
  ],
  template: `
    <app-page-header title="General settings" [description]="description" />

    @if (resource.error()) {
      <app-empty-state icon="error" title="Settings could not be loaded">
        <button mat-flat-button type="button" (click)="resource.reload()">Try again</button>
      </app-empty-state>
    } @else if (resource.value()) {
      <form [formGroup]="form" (ngSubmit)="save()" novalidate class="form">
        <mat-form-field>
          <mat-label>Organization name</mat-label>
          <input matInput formControlName="organizationName" maxlength="80" />
          @if (error('organizationName'); as message) {
            <mat-error>{{ message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Contact email</mat-label>
          <input matInput type="email" formControlName="contactEmail" />
          @if (error('contactEmail'); as message) {
            <mat-error>{{ message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Time zone</mat-label>
          <mat-select formControlName="timezone">
            @for (z of timezones; track z) {
              <mat-option [value]="z">{{ z }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Date format</mat-label>
          <mat-select formControlName="dateFormat">
            @for (f of dateFormats; track f) {
              <mat-option [value]="f">{{ f }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Currency code</mat-label>
          <input matInput formControlName="currencyCode" maxlength="3" />
          <mat-hint>Three letters, e.g. MMK</mat-hint>
          @if (error('currencyCode'); as message) {
            <mat-error>{{ message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Fiscal year starts in</mat-label>
          <mat-select formControlName="fiscalYearStartMonth">
            @for (m of months; track m; let i = $index) {
              <mat-option [value]="i + 1">{{ m }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <fieldset class="toggles">
          <legend>Notifications</legend>
          <mat-slide-toggle formControlName="emailNotifications">
            Send email notifications
          </mat-slide-toggle>
          <mat-slide-toggle formControlName="weeklyDigest">Send a weekly digest</mat-slide-toggle>
        </fieldset>

        <div class="actions">
          <button mat-stroked-button type="button" [disabled]="form.pristine" (click)="reset()">
            Reset
          </button>
          <button
            mat-flat-button
            type="submit"
            [disabled]="form.pristine || saving() || !permissions.can('settings:Edit')"
          >
            Save settings
          </button>
        </div>
      </form>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading settings" />
    }

    @if (demoData.canReset) {
      <section class="reset-panel" aria-labelledby="reset-heading">
        <div>
          <h2 id="reset-heading">Demo data</h2>
          <p>
            Everything you create or change is kept in this browser, so it is still here after a
            reload. Reset data deletes all of it and brings back the starting data. You will be
            signed out.
          </p>
        </div>
        <button
          mat-stroked-button
          type="button"
          class="reset-button"
          [disabled]="resetting()"
          (click)="resetData()"
        >
          <mat-icon>restart_alt</mat-icon>
          Reset data
        </button>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 4px 16px;
      max-width: 900px;
    }
    .toggles {
      grid-column: 1 / -1;
      display: flex;
      flex-direction: column;
      gap: 8px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      padding: 8px 16px 12px;
      margin: 4px 0 8px;
    }
    .toggles legend {
      font: var(--mat-sys-label-large);
      padding: 0 6px;
    }
    .reset-panel {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
      max-width: 900px;
      margin-top: 24px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      padding: 16px;
    }
    .reset-panel h2 {
      font: var(--mat-sys-title-medium);
      margin: 0 0 4px;
    }
    .reset-panel p {
      margin: 0;
      max-width: 60ch;
      color: var(--mat-sys-on-surface-variant);
    }
    .reset-button {
      color: var(--mat-sys-error);
    }
    .actions {
      grid-column: 1 / -1;
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  `,
})
export class GeneralSettingsComponent implements HasUnsavedChanges {
  private readonly api = inject(GeneralSettingsDataService);
  private readonly toast = inject(UiNotificationService);
  private readonly appSettings = inject(AppSettingsService);
  protected readonly permissions = inject(PermissionService);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);
  private readonly apiMode = inject(SERVER_SESSION);
  protected readonly description = this.apiMode
    ? 'Organization-wide preferences, saved on the server. Only HR Administrators can change them. The date format, currency, time zone and organization name apply to the screens of users who can open this page; other users see the defaults.'
    : 'Organization-wide preferences. Demo only: saved in this browser. Date format, currency, time zone and organization name apply across the app.';
  protected readonly demoData = inject(DemoDataDataService);
  protected readonly resetting = signal(false);

  protected readonly timezones = TIMEZONES;
  protected readonly dateFormats = DATE_FORMATS;
  protected readonly months = MONTHS;
  protected readonly saving = signal(false);
  protected readonly resource = rxResource({ stream: () => this.api.get() });

  protected readonly form = this.fb.group({
    organizationName: ['', [Validators.required, Validators.maxLength(80)]],
    contactEmail: ['', [Validators.required, Validators.email]],
    timezone: ['', Validators.required],
    dateFormat: ['', Validators.required],
    currencyCode: ['', [Validators.required, Validators.pattern(/^[A-Z]{3}$/)]],
    fiscalYearStartMonth: [1, Validators.required],
    emailNotifications: [false],
    weeklyDigest: [false],
  });

  constructor() {
    effect(() => {
      const value = this.resource.value();
      if (value) {
        this.load(value);
      }
    });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  private load(value: GeneralSettings): void {
    this.form.reset({ ...value });
  }

  protected reset(): void {
    const value = this.resource.value();
    if (value) this.load(value);
  }

  protected resetData(): void {
    this.confirmDialog
      .confirm({
        title: 'Reset all data?',
        message:
          'Every record you created or changed will be deleted and the starting data restored. This cannot be undone.',
        confirmText: 'Reset data',
        destructive: true,
      })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.resetting.set(true);
        this.demoData.reset().subscribe({
          next: () => {
            this.resetting.set(false);
            // Unsaved edits are moot now; do not let the leave-page guard block the sign-out.
            this.form.markAsPristine();
            this.toast.success('Data reset. Sign in again to continue.');
            this.auth.signOut();
          },
          error: (error: unknown) => {
            this.resetting.set(false);
            this.toast.error(error);
          },
        });
      });
  }

  protected error(name: keyof typeof this.form.controls): string | null {
    const control = this.form.controls[name];
    if (!control.touched || !control.errors) return null;
    if (control.hasError('server')) return String(control.getError('server'));
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('email')) return 'Enter a valid email address.';
    if (control.hasError('pattern')) return 'Use three capital letters, e.g. MMK.';
    if (control.hasError('maxlength')) return 'This value is too long.';
    return 'Invalid value.';
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    this.api.save(this.form.getRawValue()).subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.load(saved);
        this.appSettings.apply(saved);
        this.toast.success(
          this.apiMode ? 'General settings saved.' : 'General settings saved (simulation).',
        );
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (isAppError(error) && error.fieldErrors) {
          for (const [key, message] of Object.entries(error.fieldErrors)) {
            const control = this.form.get(key);
            control?.setErrors({ server: message });
            control?.markAsTouched();
          }
        }
        this.toast.error(error);
      },
    });
  }
}
