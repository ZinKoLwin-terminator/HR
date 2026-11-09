import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { isAppError } from '../../../core/models/common.models';
import { AuthService, MIN_PASSWORD_LENGTH } from '../../../core/services/auth.service';

/** On the confirm control, so the field shows the error. Re-run when the other field changes. */
function matchesNewPassword(confirm: AbstractControl): ValidationErrors | null {
  const other = confirm.parent?.get('newPassword');
  return other && confirm.value !== other.value ? { mismatch: true } : null;
}

/**
 * Reads `email` and `token` from the link in the reset email, then removes them from the address
 * bar so the token does not stay in the browser history.
 */
@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouterLink,
  ],
  templateUrl: './reset-password.component.html',
  styleUrl: '../auth-card.scss',
})
export class ResetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly email: string;
  private readonly token: string;

  protected readonly minLength = MIN_PASSWORD_LENGTH;
  protected readonly linkValid: boolean;
  protected readonly busy = signal(false);
  /** Problems the server reported for fields this screen does not show (token, email). */
  protected readonly failure = signal('');
  /** Server message for the new-password field. */
  protected readonly serverPasswordError = signal('');

  protected readonly form = inject(FormBuilder).nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH)]],
    confirmPassword: ['', [Validators.required, matchesNewPassword]],
  });

  constructor() {
    this.form.controls.newPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.confirmPassword.updateValueAndValidity());

    const route = inject(ActivatedRoute);
    const query = route.snapshot.queryParamMap;
    this.email = query.get('email') ?? '';
    this.token = query.get('token') ?? '';
    this.linkValid = this.email !== '' && this.token !== '';
    if (query.keys.length > 0) {
      void this.router.navigate([], { relativeTo: route, replaceUrl: true, queryParams: {} });
    }
  }

  protected submit(): void {
    if (this.busy() || !this.linkValid) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.busy.set(true);
    this.failure.set('');
    this.serverPasswordError.set('');
    this.auth
      .resetPassword(this.email, this.token, this.form.controls.newPassword.value)
      .subscribe({
        next: () => {
          void this.router.navigate(['/login'], { queryParams: { reset: '1' } });
        },
        error: (err: unknown) => {
          this.busy.set(false);
          if (!isAppError(err)) {
            this.failure.set('Could not reset the password. Please try again.');
            return;
          }
          const fields = err.fieldErrors ?? {};
          this.serverPasswordError.set(fields['newPassword'] ?? '');
          if (fields['newPassword']) {
            // An error on the control makes the field show it; editing the value clears it.
            this.form.controls.newPassword.setErrors({ server: true });
            this.form.controls.newPassword.markAsTouched();
          }
          const others = Object.entries(fields)
            .filter(([field]) => field !== 'newPassword')
            .map(([, message]) => message);
          this.failure.set(
            others.length > 0
              ? others.join(' ')
              : Object.keys(fields).length === 0
                ? err.message
                : '',
          );
        },
      });
  }
}
