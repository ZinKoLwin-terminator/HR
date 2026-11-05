import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { isAppError } from '../../../core/models/common.models';
import { AuthService } from '../../../core/services/auth.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { ThemeService } from '../../../core/services/theme.service';
import { ThemePickerComponent } from '../../../shared/components/theme-picker/theme-picker.component';
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
    MatTooltipModule,
    ThemePickerComponent,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly toast = inject(UiNotificationService);
  protected readonly theme = inject(ThemeService);
  protected readonly appName = inject(APP_CONFIG).appName;
  protected readonly demoPassword = DEMO_PASSWORD;
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);

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
