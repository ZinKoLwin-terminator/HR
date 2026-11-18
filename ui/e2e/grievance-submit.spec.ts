import { Page } from '@playwright/test';
import { expect, signedOutTest as test } from './fixtures';
import {
  CASE_NO,
  DANIEL,
  alertDialog,
  card,
  choose,
  dialog,
  fileInput,
  myStatus,
  openGrievances,
  pdf,
  rows,
  signIn,
  toast,
} from './grievance-helpers';

/**
 * Employee grievance submission (GRV-E2E-01 to GRV-E2E-08): Daniel Carter (EMP005, the Employee
 * role) writes a draft, edits it, attaches files and submits; the anonymity rules of the form. Mock
 * `e2e` build: sample data, reset on every page load, so the flows stay inside the app after signing
 * in. Every text and file is fictional.
 */

test.skip(({ isMobile }) => isMobile, 'Write flows run on desktop');

const anonymousSwitch = (page: Page) => page.getByRole('switch', { name: 'Report anonymously' });
const files = (page: Page) => page.getByRole('group', { name: 'Files of this grievance' });
const formError = (page: Page) => page.locator('form .error[role="alert"]').last();

async function startNew(page: Page) {
  await signIn(page, DANIEL);
  await openGrievances(page, 'My Grievances');
  await expect(page.getByRole('heading', { name: 'My grievances', level: 1 })).toBeVisible();
  // His own four sample cases, drafts included (#1, #2, #4 and #10).
  await expect(rows(page)).toHaveCount(4);
  await page.getByRole('link', { name: 'New grievance' }).click();
  await expect(page).toHaveURL(/\/grievances\/my\/new$/);
  await expect(page.getByRole('heading', { name: 'New grievance', level: 1 })).toBeVisible();
}

test('GRV-E2E-01 to 05: a draft needs category and subject; edit, files, then submit', async ({
  page,
}) => {
  test.setTimeout(90_000);
  await startNew(page);

  // GRV-E2E-01: an empty draft is refused on its two required fields.
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(formError(page)).toHaveText('A draft needs a category and a subject.');
  await expect(page.getByText('Category is required.')).toBeVisible();
  await expect(page.getByText('Subject is required.')).toBeVisible();

  // A draft with a category and a 4-character subject is accepted (the minimum applies on submit).
  await choose(page, page, 'Category', 'Working Conditions');
  await page.getByLabel('Subject', { exact: true }).fill('Lift');
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(toast(page, 'Draft saved. Nothing has been sent to HR yet.')).toBeVisible();
  await expect(page).toHaveURL(/\/grievances\/my\/\d+\/edit$/);
  await expect(page.getByRole('heading', { name: 'Edit grievance draft', level: 1 })).toBeVisible();

  // GRV-E2E-07: on an existing draft the reporting choice is fixed and shown read-only.
  await expect(anonymousSwitch(page)).toHaveCount(0);
  await expect(page.getByTestId('anonymity-fixed')).toContainText(
    'You are reporting with your name.',
  );
  await expect(page.getByTestId('anonymity-fixed')).toContainText(
    'Start a new draft to change how you report.',
  );

  // GRV-E2E-02: edit the draft; leaving with unsaved edits asks first.
  await page.getByLabel('Description').fill('Fictional: the lift');
  await page.getByLabel('Location').fill('Fictional block B');
  await page.getByRole('link', { name: 'Back' }).click();
  await expect(alertDialog(page)).toContainText('Discard unsaved changes?');
  await alertDialog(page).getByRole('button', { name: 'Keep editing' }).click();
  await expect(alertDialog(page)).toHaveCount(0);
  await expect(page).toHaveURL(/\/edit$/);

  // GRV-E2E-03: a valid PDF is kept until the draft is saved, then uploaded.
  await fileInput(page).setInputFiles(pdf('fictional-lift-notice.pdf', 4096));
  await expect(files(page)).toContainText('fictional-lift-notice.pdf');
  await expect(files(page)).toContainText('uploaded when you save');

  // GRV-E2E-04: a type that is not allowed and a file over 10 MB are refused before uploading.
  await fileInput(page).setInputFiles({
    name: 'fictional-tool.exe',
    mimeType: 'application/octet-stream',
    buffer: Buffer.from('MZ fictional'),
  });
  await expect(files(page).getByRole('alert')).toContainText(
    '"fictional-tool.exe" is not an allowed file type.',
  );
  await fileInput(page).setInputFiles(pdf('fictional-big.pdf', 10 * 1024 * 1024 + 1));
  await expect(files(page).getByRole('alert')).toHaveText(
    '"fictional-big.pdf" is larger than 10 MB.',
  );
  await expect(files(page).locator('li')).toHaveCount(1);

  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(toast(page, 'Draft saved. Nothing has been sent to HR yet.')).toBeVisible();
  await expect(
    files(page).getByRole('button', { name: 'Download fictional-lift-notice.pdf' }),
  ).toBeVisible();
  await expect(files(page)).not.toContainText('uploaded when you save');

  // GRV-E2E-05: submitting checks the stricter rules: lengths and the declaration.
  await page.getByRole('button', { name: 'Submit', exact: true }).click();
  await expect(formError(page)).toHaveText(
    'Please complete the highlighted fields before submitting.',
  );
  await expect(page.getByText('Subject must be at least 5 characters.')).toBeVisible();
  await expect(page.getByText('Description must be at least 20 characters.')).toBeVisible();
  await expect(page.getByText('Please confirm the declaration to submit.')).toBeVisible();
  await expect(dialog(page)).toHaveCount(0);

  await page.getByLabel('Subject', { exact: true }).fill('Lift out of order');
  await page
    .getByLabel('Description')
    .fill('Fictional: the lift in block B has been out of order for two weeks.');
  await page.getByRole('checkbox', { name: /I confirm that what I have written is true/ }).check();

  // The confirmation can be cancelled: nothing is sent.
  await page.getByRole('button', { name: 'Submit', exact: true }).click();
  await expect(dialog(page)).toContainText('Submit this grievance?');
  await expect(dialog(page)).toContainText('HR will receive it with your name.');
  await dialog(page).getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog(page)).toHaveCount(0);
  await expect(page).toHaveURL(/\/edit$/);

  await page.getByRole('button', { name: 'Submit', exact: true }).click();
  await dialog(page).getByRole('button', { name: 'Submit' }).click();
  await expect(toast(page, new RegExp(`Grievance ${CASE_NO.source} submitted\\.`))).toBeVisible();
  await expect(page).toHaveURL(/\/grievances\/my\/\d+$/);
  await expect(page.getByRole('heading', { name: 'Lift out of order', level: 1 })).toBeVisible();
  const about = card(page, 'Your grievance');
  await expect(about).toContainText(CASE_NO);
  await expect(about).toContainText('With your name');
  await expect(myStatus(page)).toHaveText('Submitted');
  await expect(
    page.getByRole('group', { name: 'Shared files' }).getByRole('button', {
      name: 'Download fictional-lift-notice.pdf',
    }),
  ).toBeVisible();
  await expect(page.getByRole('list', { name: 'Grievance timeline' })).toContainText('Submitted');
  // A submitted grievance is no longer a draft: no Edit or Delete.
  await expect(page.getByRole('link', { name: 'Edit draft' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Delete draft' })).toHaveCount(0);

  // The list shows the case number and the status.
  await page.getByRole('link', { name: 'Back' }).click();
  await expect(rows(page)).toHaveCount(5);
  const mine = rows(page).filter({ hasText: 'Lift out of order' });
  await expect(mine).toContainText(CASE_NO);
  await expect(mine).toContainText('Submitted');
});

test('GRV-E2E-06/08: anonymity is locked for Salary and Compensation and warns about files', async ({
  page,
}) => {
  await startNew(page);

  // GRV-E2E-06: a named-only category locks the switch off and says why.
  await choose(page, page, 'Category', 'Salary and Compensation Issues (named only)');
  await expect(anonymousSwitch(page)).toBeDisabled();
  await expect(anonymousSwitch(page)).not.toBeChecked();
  await expect(
    page.getByText('This category does not accept anonymous grievances', { exact: false }),
  ).toBeVisible();

  // Another category frees it; anonymous changes "Who is reporting" and warns about file metadata.
  await choose(page, page, 'Category', 'Workplace Harassment');
  await expect(anonymousSwitch(page)).toBeEnabled();
  await anonymousSwitch(page).click();
  await expect(anonymousSwitch(page)).toBeChecked();
  await expect(card(page, 'Who is reporting')).toContainText(
    'Anonymous: HR will not see your name, department or position.',
  );
  await expect(card(page, 'Who is reporting').getByLabel('Name')).toHaveCount(0);
  // GRV-E2E-08: PDFs keep their details; file names are visible to HR.
  const warning = files(page).getByRole('note');
  await expect(warning).toContainText('Files can identify you.');
  await expect(warning).toContainText('details inside PDF files are NOT');
  await expect(warning).toContainText('File names are visible to HR');
  await expect(card(page, 'Anonymity')).toContainText('What can still identify you');

  // Back to the named-only category: the switch is turned off again and locked; no warning.
  await choose(page, page, 'Category', 'Salary and Compensation Issues (named only)');
  await expect(anonymousSwitch(page)).not.toBeChecked();
  await expect(anonymousSwitch(page)).toBeDisabled();
  await expect(files(page).getByRole('note')).toHaveCount(0);

  // An anonymous draft: once saved, the choice is read-only and named-only categories are disabled.
  await choose(page, page, 'Category', 'Workplace Harassment');
  await anonymousSwitch(page).click();
  await page.getByLabel('Subject', { exact: true }).fill('Fictional remarks at stand-up');
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(toast(page, 'Draft saved. Nothing has been sent to HR yet.')).toBeVisible();
  await expect(page).toHaveURL(/\/edit$/);
  await expect(anonymousSwitch(page)).toHaveCount(0);
  await expect(page.getByTestId('anonymity-fixed')).toContainText('You are reporting anonymously.');
  await expect(files(page).getByRole('note')).toContainText('Files can identify you.');
  await page.getByRole('combobox', { name: 'Category' }).click({ position: { x: 20, y: 20 } });
  await expect(
    page.getByRole('option', { name: 'Salary and Compensation Issues (named only)' }),
  ).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('listbox')).toHaveCount(0);

  // The seeded anonymous draft (#2) is read-only on anonymity too.
  // The draft is saved, so leaving asks nothing.
  await openGrievances(page, 'My Grievances');
  await expect(page.getByRole('heading', { name: 'My grievances', level: 1 })).toBeVisible();
  await page
    .getByRole('link', { name: 'Open grievance Comments made during team meetings' })
    .click();
  await expect(card(page, 'Your grievance')).toContainText('Anonymously');
  await expect(card(page, 'Your grievance')).toContainText('Not submitted yet');
  await page.getByRole('link', { name: 'Edit draft' }).click();
  await expect(page.getByTestId('anonymity-fixed')).toContainText('You are reporting anonymously.');
  await expect(anonymousSwitch(page)).toHaveCount(0);
});
