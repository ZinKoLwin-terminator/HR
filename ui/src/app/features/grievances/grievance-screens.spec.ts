import { Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  provideRouter,
} from '@angular/router';
import { Observable, firstValueFrom, isObservable, of } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { NAVIGATION, visibleNavigation } from '../../core/constants/navigation';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { MyGrievanceDataService } from '../../core/services/data/grievance-data.services';
import { GRIEVANCE_COMPENSATION_CATEGORY_ID } from '../../mock-data/grievance/grievance-categories.mock';
import { DANIEL, NOBODY } from '../../mock-services/grievance/grievance-test-support';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { MyGrievanceDetailComponent } from './detail/my-grievance-detail.component';
import { GrievanceFormComponent } from './form/grievance-form.component';
import { GRIEVANCE_ROUTES } from './grievance.routes';
import { MyGrievancesComponent } from './my/my-grievances.component';
import { GrievanceOverviewComponent } from './overview/grievance-overview.component';

// Seed facts (mock-data/grievance): Daniel Carter (EMP005) owns #1 (draft), #2 (anonymous draft),
// #4 (anonymous, Submitted) and #10 (named, AwaitingEmployeeResponse, with an unread update).
// Category 3 (COMPENSATION) does not accept anonymous grievances.

async function settle(fixture: ComponentFixture<unknown>, rounds = 6) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

const confirmed = { answer: true };

function configure(user: CurrentUser = DANIEL) {
  confirmed.answer = true;
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: signal(user) } },
      { provide: ConfirmDialogService, useValue: { confirm: () => of(confirmed.answer) } },
    ],
  });
  const router = TestBed.inject(Router);
  return { navigate: vi.spyOn(router, 'navigate').mockResolvedValue(true) };
}

async function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}) {
  const fixture = TestBed.createComponent(component);
  for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
  await settle(fixture);
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

const text = (el: HTMLElement) => el.textContent?.replace(/\s+/g, ' ') ?? '';
const internals = <T>(fixture: ComponentFixture<unknown>) =>
  fixture.componentInstance as unknown as T;

interface FormInternals {
  form: GrievanceFormComponent['form'];
  submit(): void;
  saveDraft(): void;
  formError(): string | null;
  hasUnsavedChanges(): boolean;
}

describe('grievance routes and navigation', () => {
  it('guards the draft forms against unsaved changes and keys every screen', () => {
    const my = GRIEVANCE_ROUTES.find((r) => r.path === 'my')!;
    const form = my.children!.filter((r) => r.path === 'new' || r.path === ':id/edit');
    expect(form).toHaveLength(2);
    for (const route of form) {
      expect(route.canDeactivate).toEqual([unsavedChangesGuard]);
      expect(route.data?.['permission']).toBe('grievance:Create');
    }
    expect(my.data?.['permission']).toBe('grievance:View');
  });

  it('shows the Grievances group to a role with grievance:View only', () => {
    const labels = (held: string[]) =>
      visibleNavigation(NAVIGATION, (k) => held.includes(k)).flatMap((i) => [
        i.label,
        ...(i.children ?? []).map((c) => c.label),
      ]);
    // My Payslips needs no key and is always listed.
    expect(labels(['grievance:View'])).toEqual([
      'Grievances',
      'Overview',
      'My Grievances',
      'My Payslips',
    ]);
    expect(labels(['dashboard:View'])).not.toContain('Grievances');
  });
});

describe('grievance overview', () => {
  it('shows the counts, the unread updates and a New grievance button', async () => {
    configure();
    const { el } = await render(GrievanceOverviewComponent);
    expect(text(el)).toContain('Updates to read');
    expect(text(el)).toContain('Waiting for your answer');
    expect(el.querySelector('a[href="/grievances/my/new"]')).not.toBeNull();
    expect(text(el)).toContain('Simulation only');
  });

  it('offers no New grievance button without grievance:Create', async () => {
    configure({ ...DANIEL, permissions: ['grievance:View'] });
    const { el } = await render(GrievanceOverviewComponent);
    expect(el.querySelector('a[href="/grievances/my/new"]')).toBeNull();
  });
});

describe('my grievances', () => {
  it('lists the employee’s own cases with an unread dot', async () => {
    configure();
    const { el } = await render(MyGrievancesComponent);
    const rows = Array.from(el.querySelectorAll('tr.mat-mdc-row'));
    expect(rows).toHaveLength(4);
    expect(el.querySelectorAll('.unread').length).toBeGreaterThanOrEqual(1);
    expect(text(el)).toContain('Awaiting Employee Response');
    expect(text(el)).toContain('Anonymous');
  });
});

describe('grievance form', () => {
  it('asks for a category and a subject before saving a draft', async () => {
    configure();
    const create = vi.spyOn(TestBed.inject(MyGrievanceDataService), 'createDraft');
    const { fixture } = await render(GrievanceFormComponent);
    const page = internals<FormInternals>(fixture);
    page.saveDraft();
    expect(create).not.toHaveBeenCalled();
    expect(page.formError()).toContain('category and a subject');
    expect(page.form.controls.categoryId.hasError('required')).toBe(true);
  });

  it('applies the submission minimums and the declaration on Submit only', async () => {
    configure();
    const { fixture } = await render(GrievanceFormComponent);
    const page = internals<FormInternals>(fixture);
    page.form.patchValue({ categoryId: 1, subject: 'Hi' });
    page.submit();
    expect(page.form.controls.subject.hasError('minlength')).toBe(true);
    expect(page.form.controls.description.hasError('required')).toBe(true);
    expect(page.form.controls.declaration.hasError('required')).toBe(true);
    page.saveDraft();
    expect(page.form.controls.subject.valid).toBe(true);
    // The draft (category and subject only) is saved: let the save finish inside the test.
    await settle(fixture);
  });

  it('refuses an incident date in the future', async () => {
    configure();
    const { fixture } = await render(GrievanceFormComponent);
    const page = internals<FormInternals>(fixture);
    page.form.controls.incidentDate.setValue('2999-01-01');
    expect(page.form.controls.incidentDate.hasError('notInFuture')).toBe(true);
  });

  it('locks the anonymous switch off for a category that does not accept it', async () => {
    configure();
    const { fixture, el } = await render(GrievanceFormComponent);
    const page = internals<FormInternals>(fixture);
    page.form.patchValue({ categoryId: 1, isAnonymous: true });
    await settle(fixture);
    expect(page.form.controls.isAnonymous.enabled).toBe(true);
    page.form.controls.categoryId.setValue(GRIEVANCE_COMPENSATION_CATEGORY_ID);
    await settle(fixture);
    expect(page.form.controls.isAnonymous.disabled).toBe(true);
    expect(page.form.controls.isAnonymous.value).toBe(false);
    expect(text(el)).toContain('does not accept anonymous grievances');
    // The honest account of what HR will and will not see, and what can still identify you.
    expect(text(el)).toContain('HR will not see');
    expect(text(el)).toContain('Writing style');
  });

  it('submits a complete grievance after confirmation', async () => {
    const { navigate } = configure();
    const service = TestBed.inject(MyGrievanceDataService);
    const submit = vi.spyOn(service, 'submit');
    const { fixture } = await render(GrievanceFormComponent);
    const page = internals<FormInternals>(fixture);
    page.form.patchValue({
      categoryId: 9,
      subject: 'Noise from the loading bay',
      description: 'Sample text: the doors stay open all morning and the noise is constant.',
      declaration: true,
    });
    page.form.markAsDirty();
    page.submit();
    await settle(fixture);
    expect(submit).toHaveBeenCalledTimes(1);
    expect(page.hasUnsavedChanges()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/grievances/my', expect.any(Number)]);
  });

  it('keeps the user on the form when they decline to leave unsaved changes', async () => {
    configure();
    const { fixture } = await render(GrievanceFormComponent);
    const page = internals<FormInternals>(fixture);
    expect(page.hasUnsavedChanges()).toBe(false);
    page.form.controls.subject.setValue('Something changed');
    page.form.markAsDirty();
    expect(page.hasUnsavedChanges()).toBe(true);
    confirmed.answer = false;
    const result = TestBed.runInInjectionContext(() =>
      unsavedChangesGuard(
        page as never,
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
        {} as RouterStateSnapshot,
      ),
    );
    const allowed = isObservable(result)
      ? await firstValueFrom(result as Observable<boolean>)
      : result;
    expect(allowed).toBe(false);
  });

  it('loads a draft for editing', async () => {
    configure();
    const { fixture } = await render(GrievanceFormComponent, { id: '1' });
    const page = internals<FormInternals>(fixture);
    expect(page.form.controls.subject.value).toContain('Air conditioning');
    expect(page.hasUnsavedChanges()).toBe(false);
  });
});

describe('my grievance detail', () => {
  it('turns the reply box into a response while HR waits, and returns the case', async () => {
    configure();
    const service = TestBed.inject(MyGrievanceDataService);
    const seen = vi.spyOn(service, 'markSeen');
    const { fixture, el } = await render(MyGrievanceDetailComponent, { id: '10' });
    expect(seen).toHaveBeenCalledWith(10);
    expect(text(el)).toContain("Respond to HR's request");
    const textarea = el.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = 'March and April.';
    textarea.dispatchEvent(new Event('input'));
    await settle(fixture);
    (el.querySelector('form button[type="submit"]') as HTMLButtonElement).click();
    await settle(fixture);
    expect(text(el)).toContain('Under Investigation');
    expect(text(el)).not.toContain("Respond to HR's request");
  });

  // Regression: the lists stayed blank when refused, because value() of a resource in error throws.
  it('shows the error state of the employee screens when they are refused', async () => {
    for (const component of [MyGrievancesComponent, GrievanceOverviewComponent]) {
      configure(NOBODY);
      const { el } = await render(component as Type<unknown>);
      expect(text(el)).toContain('Your grievances could not be loaded');
      TestBed.resetTestingModule();
    }
  });

  // Regression: the detail kept loading on a 404 because value() of a resource in error throws.
  it('shows the not-found state for someone else’s grievance', async () => {
    configure();
    const service = TestBed.inject(MyGrievanceDataService);
    const seen = vi.spyOn(service, 'markSeen');
    const { el } = await render(MyGrievanceDetailComponent, { id: '3' });
    expect(text(el)).toContain('This grievance could not be opened');
    expect(el.querySelector('mat-progress-bar')).toBeNull();
    expect(seen).not.toHaveBeenCalled();
  });

  it('offers a plain message on a submitted case and Edit on a draft', async () => {
    configure();
    const submitted = await render(MyGrievanceDetailComponent, { id: '4' });
    expect(text(submitted.el)).toContain('Message to HR');
    expect(text(submitted.el)).not.toContain('Edit draft');
    const draft = await render(MyGrievanceDetailComponent, { id: '1' });
    expect(text(draft.el)).toContain('Edit draft');
    expect(draft.el.querySelector('textarea')).toBeNull();
  });
});

describe('anonymity safeguards on the employee screens', () => {
  it('fixes how an existing draft reports and warns about file details', async () => {
    configure();
    // #2 is Daniel Carter's anonymous draft.
    const { el } = await render(GrievanceFormComponent, { id: '2' });
    expect(el.querySelector('mat-slide-toggle')).toBeNull();
    const fixed = el.querySelector('[data-testid="anonymity-fixed"]')?.textContent ?? '';
    expect(fixed).toContain('You are reporting anonymously.');
    expect(fixed).toContain('Start a new draft to change how you report.');
    expect(text(el)).toContain('details inside PDF files are NOT');
  });

  it('offers the switch on a new draft and warns once it is on', async () => {
    configure();
    const { fixture, el } = await render(GrievanceFormComponent);
    expect(el.querySelector('mat-slide-toggle')).not.toBeNull();
    expect(text(el)).not.toContain('details inside PDF files are NOT');
    const page = internals<FormInternals>(fixture);
    page.form.patchValue({ categoryId: 1, isAnonymous: true });
    await settle(fixture);
    expect(text(el)).toContain('details inside PDF files are NOT');
  });

  it('warns about file details on an anonymous case', async () => {
    configure();
    const { el } = await render(MyGrievanceDetailComponent, { id: '4' });
    expect(text(el)).toContain('File names are visible to HR');
  });
});
