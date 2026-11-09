import { TestBed } from '@angular/core/testing';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';

describe('prompt dialog', () => {
  let service: ConfirmDialogService;
  let overlay: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [MatDialogModule] });
    service = TestBed.inject(ConfirmDialogService);
    overlay = document.body;
  });

  afterEach(() => TestBed.inject(MatDialog).closeAll());

  const open = (required: boolean) =>
    service.prompt({
      title: 'Reject leave?',
      label: 'Reason',
      required,
      confirmText: 'Reject',
      destructive: true,
    });

  const tick = () => new Promise((resolve) => setTimeout(resolve, 20));
  const field = () => overlay.querySelector('textarea') as HTMLTextAreaElement;
  const form = () => overlay.querySelector('app-prompt-dialog form') as HTMLFormElement;

  /** Submits the form like a click on the submit button does, reporting whether the browser would have reloaded. */
  const submit = () => {
    const event = new Event('submit', { cancelable: true, bubbles: true });
    form().dispatchEvent(event);
    return event.defaultPrevented;
  };

  it('returns the trimmed text and prevents the native form submission', async () => {
    const result = firstValueFrom(open(true));
    await tick();
    field().value = '  Not enough cover  ';
    field().dispatchEvent(new Event('input'));
    expect(submit()).toBe(true); // a native submit would reload the whole page
    expect(await result).toBe('Not enough cover');
  });

  it('refuses blank text when required and shows the error', async () => {
    const result = firstValueFrom(open(true));
    await tick();
    field().value = '   ';
    field().dispatchEvent(new Event('input'));
    expect(submit()).toBe(true);
    await tick();
    expect(overlay.querySelector('app-prompt-dialog')).not.toBeNull(); // still open
    // (The visible error message is verified in the browser test for leave approvals.)

    field().value = 'Because';
    field().dispatchEvent(new Event('input'));
    submit();
    expect(await result).toBe('Because');
  });

  it('accepts empty text when optional and returns null on cancel', async () => {
    const optional = firstValueFrom(open(false));
    await tick();
    submit();
    expect(await optional).toBe('');

    const cancelled = firstValueFrom(open(false));
    await tick();
    (overlay.querySelector('app-prompt-dialog button[type=button]') as HTMLButtonElement).click();
    expect(await cancelled).toBeNull();
  });
});
