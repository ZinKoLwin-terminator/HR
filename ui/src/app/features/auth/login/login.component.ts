import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { isAppError } from '../../../core/models/common.models';
import { AuthService } from '../../../core/services/auth.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { DEMO_PASSWORD } from '../../../mock-services/auth/auth-mock.service';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouterLink,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly toast = inject(UiNotificationService);
  protected readonly appName = inject(APP_CONFIG).appName;
  protected readonly demoPassword = DEMO_PASSWORD;
  /** API build: real sign-in. Hides the demo helper and links to the reset flow. */
  protected readonly serverMode = this.auth.serverSession;
  protected readonly passwordReset = this.route.snapshot.queryParamMap.get('reset') === '1';
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);

  protected readonly modules = [
    {
      icon: 'domain',
      label: 'Organization',
      hint: 'Companies & structure',
      x: 14,
      y: 30,
      color: '#3b82f6',
    },
    {
      icon: 'badge',
      label: 'Employees',
      hint: 'Profiles & documents',
      x: 50,
      y: 10,
      color: '#8b5cf6',
    },
    {
      icon: 'fact_check',
      label: 'Attendance',
      hint: 'Shifts & overtime',
      x: 86,
      y: 30,
      color: '#14b8a6',
    },
    {
      icon: 'beach_access',
      label: 'Leave',
      hint: 'Requests & approvals',
      x: 14,
      y: 72,
      color: '#f59e0b',
    },
    {
      icon: 'payments',
      label: 'Payroll',
      hint: 'Payslips & history',
      x: 50,
      y: 90,
      color: '#10b981',
    },
    {
      icon: 'person_search',
      label: 'Recruitment',
      hint: 'Openings & pipeline',
      x: 86,
      y: 72,
      color: '#ec4899',
    },
  ];

  protected readonly highlights = [
    { icon: 'groups', text: 'One home for people, org structure and records' },
    { icon: 'event_available', text: 'Attendance, leave and approvals in a few clicks' },
    { icon: 'payments', text: 'Payroll runs and payslips you can trust' },
  ];

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    remember: false,
  });

  protected forgot(): void {
    this.toast.info('Password resets are handled by your HR administrator. Please contact them.');
  }

  protected fillDemo(): void {
    this.form.patchValue({ email: 'sumyat.noe@apexhorizon.example', password: DEMO_PASSWORD });
    this.error.set('');
  }

  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { email, password, remember } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set('');
    this.auth.signIn(email, password, remember).subscribe({
      next: () => {
        const target = this.route.snapshot.queryParamMap.get('returnUrl');
        // Only follow in-app paths ("/x", never "//host" or absolute URLs).
        const safe = target && target.startsWith('/') && !target.startsWith('//');
        void this.router.navigateByUrl(safe ? target : '/dashboard');
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(isAppError(err) ? err.message : 'Sign-in failed. Please try again.');
        this.form.controls.password.reset('');
      },
    });
  }
}
