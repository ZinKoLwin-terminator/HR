import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { isAppError } from '../../../core/models/common.models';
import { AuthService } from '../../../core/services/auth.service';

/** The only confirmation shown, whether or not the account exists (no account enumeration). */
export const FORGOT_PASSWORD_MESSAGE = 'If an account exists, a reset link has been sent.';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouterLink,
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: '../auth-card.scss',
})
export class ForgotPasswordComponent {
  private readonly auth = inject(AuthService);

  protected readonly busy = signal(false);
  protected readonly sent = signal(false);
  protected readonly failure = signal('');
  protected readonly confirmation = FORGOT_PASSWORD_MESSAGE;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.busy.set(true);
    this.failure.set('');
    this.auth.forgotPassword(this.form.controls.email.value.trim()).subscribe({
      next: () => this.done(),
      error: (err: unknown) => {
        // Only a connectivity problem is reported; every API answer shows the same message.
        if (isAppError(err) && err.code === 'Unknown') {
          this.busy.set(false);
          this.failure.set(err.message);
        } else {
          this.done();
        }
      },
    });
  }

  private done(): void {
    this.busy.set(false);
    this.sent.set(true);
  }
}
