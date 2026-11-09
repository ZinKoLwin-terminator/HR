import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { AuthDataService } from '../../core/services/data/auth-data.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';

function setup(query: Record<string, string>, fake: Partial<AuthDataService>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: AuthDataService, useValue: fake },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { queryParamMap: convertToParamMap(query) } },
      },
    ],
  });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  return { navigate };
}

function type(root: HTMLElement, name: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(`input[formcontrolname="${name}"]`)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function submit(root: HTMLElement): void {
  root.querySelector('form')!.dispatchEvent(new Event('submit'));
}

describe('reset-password screen', () => {
  const link = { email: 'a@apexhorizon.example', token: 'tok' };

  it('blocks a password under 12 characters and a mismatch, without calling the API', async () => {
    const resetPassword = vi.fn(() => of(undefined));
    const { navigate } = setup(link, { resetPassword });
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    type(el, 'newPassword', 'short');
    type(el, 'confirmPassword', 'short');
    submit(el);
    fixture.detectChanges();
    expect(el.textContent).toContain('at least 12 characters');

    type(el, 'newPassword', 'twelve-chars-ok');
    type(el, 'confirmPassword', 'twelve-chars-no');
    submit(el);
    fixture.detectChanges();
    expect(el.textContent).toContain('Passwords do not match');

    expect(resetPassword).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalledWith(['/login'], expect.anything());
  });

  it('shows the server field errors', async () => {
    const error: AppError = {
      code: 'Validation',
      message: 'Please correct the highlighted fields.',
      fieldErrors: { newPassword: 'Password is too common.', token: 'The reset link is invalid.' },
    };
    setup(link, { resetPassword: () => throwError(() => error) });
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    type(el, 'newPassword', 'twelve-chars-ok');
    type(el, 'confirmPassword', 'twelve-chars-ok');
    submit(el);
    fixture.detectChanges();

    expect(el.textContent).toContain('Password is too common.');
    expect(el.textContent).toContain('The reset link is invalid.');
  });

  it('sends email and token from the link and goes to /login on success', async () => {
    const resetPassword = vi.fn(() => of(undefined));
    const { navigate } = setup(link, { resetPassword });
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    type(el, 'newPassword', 'twelve-chars-ok');
    type(el, 'confirmPassword', 'twelve-chars-ok');
    submit(el);

    expect(resetPassword).toHaveBeenCalledWith(link.email, link.token, 'twelve-chars-ok');
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { reset: '1' } });
  });

  it('refuses a link without a token', () => {
    setup({ email: link.email }, {});
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('incomplete');
  });
});

describe('forgot-password screen', () => {
  const GENERIC = 'If an account exists, a reset link has been sent.';

  it('always shows the generic message, even when the API rejects the request', () => {
    for (const answer of [
      of(undefined),
      throwError(() => ({ code: 'Validation', message: 'Unknown email' }) satisfies AppError),
    ]) {
      TestBed.resetTestingModule();
      setup({}, { forgotPassword: () => answer });
      const fixture = TestBed.createComponent(ForgotPasswordComponent);
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;

      type(el, 'email', 'someone@apexhorizon.example');
      submit(el);
      fixture.detectChanges();

      expect(el.textContent).toContain(GENERIC);
      expect(el.textContent).not.toContain('Unknown email');
    }
  });
});
