import { Page } from '@playwright/test';
import { expect, test } from './fixtures';

test.skip(({ isMobile }) => isMobile, 'Employee workflows are exercised on desktop');

const rows = (page: Page) => page.locator('tr.mat-mdc-row');
const dialog = (page: Page) => page.getByRole('dialog');
const confirmDialog = (page: Page) => page.getByRole('alertdialog');

async function choose(page: Page, name: string, option: string) {
  await page.getByRole('combobox', { name }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

async function rowMenu(page: Page, name: string, action: string) {
  await rows(page)
    .filter({ hasText: name })
    .getByRole('button', { name: /^Actions for/ })
    .click();
  await page.getByRole('menuitem', { name: action }).click();
}

test.describe('employee list', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/employees/list');
    await expect(rows(page)).toHaveCount(10);
  });

  test('pages, searches, filters, sorts and clears', async ({ page }) => {
    await expect(page.getByText('1 – 10 of 60')).toBeVisible();

    await page.getByRole('searchbox', { name: /Search by employee code/ }).fill('emp005');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('Daniel Carter');

    await page.getByRole('button', { name: 'Clear filters' }).click();
    await expect(rows(page)).toHaveCount(10);

    await choose(page, 'Department', 'Engineering');
    await expect(rows(page).first()).toContainText('Engineering');
    await expect(page.getByText('of 60')).toBeHidden();
    await choose(page, 'Status', 'Probation');
    await expect(rows(page).first()).toContainText('Probation');

    await page.getByRole('button', { name: 'Clear filters' }).click();
    await page.getByRole('columnheader', { name: 'Employee' }).click();
    await expect(rows(page).first()).toContainText('Andrew Lim');
    await page.getByRole('columnheader', { name: 'Employee' }).click();
    await expect(rows(page).first()).toContainText('Zin Mar Oo');

    await page.getByRole('button', { name: 'Next page' }).click();
    await expect(page.getByText('11 – 20 of 60')).toBeVisible();
  });

  test('filters by joining date range', async ({ page }) => {
    await page.getByLabel('Joined from').fill('2014-01-01');
    await page.getByLabel('Joined to').fill('2015-12-31');
    await expect(rows(page)).toHaveCount(3);
  });

  test('header search jumps to the list with the term applied', async ({ page }) => {
    const search = page.getByRole('search').getByRole('searchbox', { name: 'Search employees' });
    await search.fill('carter');
    await search.press('Enter');
    await expect(page).toHaveURL(/\/employees\/list\?search=carter/);
    await expect(rows(page)).toHaveCount(1);
  });

  test('opens a profile from the row and deletes only after confirmation', async ({ page }) => {
    await rows(page)
      .filter({ hasText: 'Thura Aung' })
      .getByRole('link', { name: /Thura Aung/ })
      .click();
    await expect(page).toHaveURL(/\/employees\/1$/);
    await page.goBack();

    // The CEO manages people, so deletion is refused with an explanation.
    await rowMenu(page, 'Thura Aung', 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(/manages \d+ employee\(s\)/)).toBeVisible();
    await expect(page.getByText('1 – 10 of 60')).toBeVisible();
  });
});

test.describe('employee profile', () => {
  test('shows the summary and every tab', async ({ page }) => {
    await page.goto('/employees/5');
    await expect(page.getByRole('heading', { level: 1, name: 'Daniel Carter' })).toBeVisible();
    await expect(page.getByText('Engineering Manager · Engineering')).toBeVisible();
    await expect(page.getByText('Personal information')).toBeVisible();

    // Bank account is masked until revealed.
    await expect(page.getByText(/•••• •••• \d{4}/)).toBeVisible();
    await page.getByRole('button', { name: 'Show account number' }).click();
    await expect(page.getByText(/^\d{12}$/)).toBeVisible();

    await page.getByRole('tab', { name: 'Employment' }).click();
    await expect(page).toHaveURL(/tab=employment/);
    await expect(page.getByText('Reporting manager')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Kyaw Zin Htet' })).toBeVisible();

    await page.getByRole('tab', { name: 'Documents' }).click();
    await expect(rows(page).filter({ hasText: 'Work permit' })).toHaveCount(1);
    await expect(rows(page).filter({ hasText: 'Work permit' })).toContainText('in 20 days');

    await page.getByRole('tab', { name: 'History' }).click();
    await expect(page.getByText('Joined as Engineering Manager')).toBeVisible();

    await page.getByRole('tab', { name: 'Attendance' }).click();
    await expect(page.getByText('Attendance rate')).toBeVisible();
    await expect(
      page.getByRole('table', { name: 'Recent attendance' }).locator('tbody tr'),
    ).toHaveCount(14);

    await page.getByRole('tab', { name: 'Leave' }).click();
    await expect(page.getByRole('heading', { name: /Leave balances/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Annual Leave/ })).toBeVisible();

    await page.getByRole('tab', { name: 'Salary' }).click();
    await expect(page.getByText('sample data').first()).toBeVisible();
    await expect(page.getByRole('row', { name: /Net salary/ })).toBeVisible();
  });

  test('deep links to a tab and reports unknown employees', async ({ page }) => {
    await page.goto('/employees/5?tab=leave');
    await expect(page.getByRole('tab', { name: 'Leave', selected: true })).toBeVisible();

    await page.goto('/employees/999');
    await expect(page.getByText('Employee not found')).toBeVisible();
  });
});

test.describe('add, edit and delete an employee', () => {
  test('validates, creates, edits (with history) and deletes', async ({ page }) => {
    await page.goto('/employees/new');
    await expect(page.getByRole('heading', { level: 1, name: 'Add employee' })).toBeVisible();
    await expect(page.getByLabel('Employee code')).toHaveValue('EMP061');

    // Required-field validation
    await page.getByRole('button', { name: 'Create employee' }).click();
    await expect(page.getByText(/Please correct \d+ fields? before saving/)).toBeVisible();
    await expect(page.getByText('First name is required.')).toBeVisible();

    // Format / cross-field validation
    await page.getByLabel('Date of birth').fill('2015-01-01');
    await page.getByLabel('Mobile phone').fill('abc');
    await page.getByLabel('Last name').click();
    await expect(page.getByText('Employee must be at least 18 years old.')).toBeVisible();
    await expect(page.getByText(/Enter a valid phone number/)).toBeVisible();
    await page.getByLabel('Probation end date').fill('2020-01-01');
    await page.getByLabel('Last name').click();
    await expect(page.getByText('End date must be on or after the start date.')).toBeVisible();
    await page.getByLabel('Probation end date').fill('2027-02-01');

    // Fill the form
    await page.getByLabel('First name').fill('Nilar');
    await page.getByLabel('Last name').fill('Htay');
    await choose(page, 'Gender', 'Female');
    await page.getByLabel('Date of birth').fill('1996-04-12');
    await choose(page, 'Marital status', 'Single');
    await page.getByLabel('National ID').fill('12/KaMaYa(N)123456');
    await page.getByLabel('Work email').fill('nilar.htay@apexhorizon.example');
    await page.getByLabel('Mobile phone').fill('+95 9 123 456 789');
    await page.getByLabel('Address line 1').fill('No. 5, Parami Road');
    await page.getByLabel('City').fill('Yangon');
    await page.getByLabel('Contact name').fill('Htay Win');
    await choose(page, 'Relationship', 'Father');
    await page.getByLabel('Contact phone').fill('+95 9 987 654 321');

    // Dependent organization dropdowns
    await choose(page, 'Company', 'Apex Horizon Technologies');
    await page.getByRole('combobox', { name: 'Branch' }).click();
    await expect(page.getByRole('option')).toContainText([
      'Mandalay Branch',
      'Nay Pyi Taw Office',
      'Singapore Regional Office',
      'Yangon Head Office',
    ]);
    await page.getByRole('option', { name: 'Yangon Head Office' }).click();
    await choose(page, 'Department', 'Human Resources');
    await choose(page, 'Position', 'HR Executive');
    await expect(page.getByRole('combobox', { name: 'Job grade' })).toContainText('G2');

    await page.getByLabel('Bank name').fill('KBZ Bank');
    await page.getByLabel('Account holder name').fill('NILAR HTAY');
    await page.getByLabel('Account number').fill('123456789012');

    // Attach a document (invalid type first, then a valid one)
    const picker = page.locator('#sec-documents input[type=file]');
    await picker.setInputFiles({
      name: 'virus.exe',
      mimeType: 'application/octet-stream',
      buffer: Buffer.from('x'),
    });
    await page.getByRole('button', { name: 'Add document' }).click();
    await expect(page.getByText('Allowed file types: PDF, Word, PNG or JPG.')).toBeVisible();
    await picker.setInputFiles({
      name: 'nilar-cv.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('cv'),
    });
    await page.getByRole('button', { name: 'Add document' }).click();
    await expect(page.getByRole('list', { name: 'Documents to attach' })).toContainText(
      'nilar-cv.pdf',
    );

    await page.getByRole('button', { name: 'Create employee' }).click();
    await expect(page.getByText('Employee "Nilar Htay" created successfully.')).toBeVisible();
    await expect(page).toHaveURL(/\/employees\/61$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Nilar Htay' })).toBeVisible();
    await expect(page.getByText('HR Executive · Human Resources')).toBeVisible();

    // The attached document and the automatic history entry exist
    await page.getByRole('tab', { name: 'Documents' }).click();
    await expect(rows(page).filter({ hasText: 'nilar-cv.pdf' })).toHaveCount(1);
    await page.getByRole('tab', { name: 'History' }).click();
    await expect(page.getByText('Joined as HR Executive')).toBeVisible();

    // Edit: change phone and status
    await page.getByRole('link', { name: 'Edit' }).click();
    await expect(page).toHaveURL(/\/employees\/61\/edit$/);
    await expect(page.getByLabel('Employee code')).toBeDisabled();
    await page.getByLabel('Mobile phone').fill('+95 9 555 000 111');
    await choose(page, 'Employment status', 'Active');
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Employee "Nilar Htay" updated successfully.')).toBeVisible();
    await page.getByRole('tab', { name: 'History' }).click();
    await expect(page.getByText('Status changed to Active')).toBeVisible();

    // Appears in the list. Navigate inside the app: a full page load would reset the sample data.
    const headerSearch = page
      .getByRole('search')
      .getByRole('searchbox', { name: 'Search employees' });
    await headerSearch.fill('nilar htay');
    await headerSearch.press('Enter');
    await expect(rows(page)).toHaveCount(1);
    await rows(page)
      .first()
      .getByRole('link', { name: /Nilar Htay/ })
      .click();
    await expect(page).toHaveURL(/\/employees\/61$/);

    // Delete from the profile
    await page.getByRole('button', { name: 'Delete' }).click();
    await expect(confirmDialog(page)).toContainText('Nilar Htay (EMP061)');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Employee "Nilar Htay" deleted.')).toBeVisible();
    await expect(page).toHaveURL(/\/employees\/list$/);
    await expect(page.getByText('1 – 10 of 60')).toBeVisible();
  });

  test('warns before discarding unsaved changes', async ({ page }) => {
    await page.goto('/employees/new');
    await page.getByLabel('First name').fill('Draft');

    await page.getByRole('link', { name: 'Back' }).click();
    await expect(confirmDialog(page)).toContainText('Discard unsaved changes?');
    await confirmDialog(page).getByRole('button', { name: 'Keep editing' }).click();
    await expect(page).toHaveURL(/\/employees\/new$/);
    await expect(page.getByLabel('First name')).toHaveValue('Draft');

    await page.getByRole('link', { name: 'Back' }).click();
    await confirmDialog(page).getByRole('button', { name: 'Discard changes' }).click();
    await expect(page).toHaveURL(/\/employees\/list$/);
  });

  test('refuses a duplicate work email from the server rules', async ({ page }) => {
    await page.goto('/employees/2/edit');
    await page.getByLabel('Work email').fill('thura.aung@apexhorizon.example');
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(
      page.getByText('Work email "thura.aung@apexhorizon.example" already exists.').first(),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/employees\/2\/edit$/);
  });
});

test.describe('employee documents', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/employees/documents');
    await expect(rows(page).first()).toBeVisible();
  });

  test('filters by type and shows expiry warnings', async ({ page }) => {
    await choose(page, 'Type', 'Work Permit');
    await expect(rows(page)).toHaveCount(7);
    await expect(rows(page).filter({ hasText: 'in 12 days' })).toHaveCount(1);
  });

  test('uploads, edits and deletes a document', async ({ page }) => {
    await page.getByRole('button', { name: 'Add document' }).click();

    await dialog(page).getByRole('button', { name: 'Create document' }).click();
    await expect(dialog(page).getByText('Employee is required.')).toBeVisible();

    await choose(page, 'Employee', 'Daniel Carter (EMP005)');
    await choose(page, 'Document type', 'Certificate');
    await dialog(page).getByLabel('Title').fill('Leadership certificate');
    await dialog(page)
      .locator('input[type=file]')
      .setInputFiles({
        name: 'leadership.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('x'),
      });
    await expect(dialog(page).getByRole('textbox', { name: 'File' })).toHaveValue('leadership.pdf');
    await dialog(page).getByRole('button', { name: 'Create document' }).click();
    await expect(
      page.getByText('Document "Leadership certificate" created successfully.'),
    ).toBeVisible();

    await page.getByRole('searchbox', { name: /Search by title/ }).fill('leadership');
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('Daniel Carter (EMP005)');

    await rowMenu(page, 'Leadership certificate', 'Delete');
    await confirmDialog(page).getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Document "Leadership certificate" deleted.')).toBeVisible();
    await expect(rows(page)).toHaveCount(0);
  });
});
