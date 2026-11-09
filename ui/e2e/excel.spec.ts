import { Locator, Page } from '@playwright/test';
import { expect, searchEmployeesInApp, test } from './fixtures';
import { closeFilters, openFilters } from './filters';

type ExcelJSModule = typeof import('exceljs');
async function lib(): Promise<ExcelJSModule> {
  const module = await import('exceljs');
  return ((module as { default?: ExcelJSModule }).default ?? module) as ExcelJSModule;
}

const dialog = (page: Page) => page.getByRole('dialog');

/** Opens a Material select by its arrow end (the label can sit over the centre). */
async function openSelect(select: Locator) {
  const box = await select.boundingBox();
  if (!box) throw new Error('Select is not visible');
  await select.click({ position: { x: box.width - 22, y: box.height / 2 } });
}
async function choose(page: Page, name: string, option: string) {
  const inFilters = await openFilters(page);
  await openSelect(page.getByRole('combobox', { name }));
  await page.getByRole('option', { name: option, exact: true }).click();
  await expect(page.getByRole('listbox')).toHaveCount(0);
  if (inFilters) await closeFilters(page);
}
async function total(page: Page): Promise<number> {
  const text = await page.locator('mat-paginator .mat-mdc-paginator-range-label').innerText();
  return Number(/of (\d+)/.exec(text)?.[1] ?? 0);
}

/** Clicks a button that starts a download and returns the downloaded workbook. */
async function downloadWorkbook(page: Page, click: () => Promise<void>, expectToast = false) {
  const downloadPromise = page.waitForEvent('download');
  await click();
  const download = await downloadPromise;
  // The "is ready" toast is short-lived, so check it before parsing the file.
  if (expectToast) {
    await expect(page.getByText(/employees-export-.*is ready\./).first()).toBeVisible();
  }
  const path = await download.path();
  const workbook = new (await lib()).Workbook();
  await workbook.xlsx.readFile(path);
  return { workbook, fileName: download.suggestedFilename() };
}

test.describe('Excel export', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('exports the employees matching the current filters as a formatted workbook', async ({
    page,
  }) => {
    await page.goto('/employees/list');
    await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
    await choose(page, 'Department', 'Engineering');
    await expect.poll(() => total(page)).toBeLessThan(60);
    const engineering = await total(page);

    await page.getByRole('button', { name: 'Export' }).click();
    await expect(
      dialog(page).getByRole('heading', { name: 'Export Employees to Excel' }),
    ).toBeVisible();
    await expect(dialog(page)).toContainText('Department: Engineering');
    await expect(
      dialog(page).getByRole('radio', { name: /matching the current filters/ }),
    ).toBeChecked();
    // Personal and financial columns are off unless asked for
    await expect(
      dialog(page).getByRole('checkbox', { name: /Include personal and financial columns/ }),
    ).not.toBeChecked();

    const { workbook, fileName } = await downloadWorkbook(
      page,
      () => dialog(page).getByRole('button', { name: 'Export to Excel' }).click(),
      true,
    );
    expect(fileName).toMatch(/^employees-export-\d{8}-\d{4}\.xlsx$/);
    expect(workbook.worksheets.map((w) => w.name)).toEqual([
      'Employees',
      'Department Summary',
      'Branch Summary',
    ]);
    const sheet = workbook.getWorksheet('Employees')!;
    expect(sheet.rowCount - 1).toBe(engineering); // all matching rows, not just the visible page
    expect(sheet.getCell(1, 1).value).toBe('Employee Code');
    expect(sheet.getCell(1, 1).font?.bold).toBe(true);
    expect(sheet.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 });
    const headers = (sheet.getRow(1).values as unknown[]).slice(1);
    expect(headers).not.toContain('National ID');
    expect(headers).not.toContain('Bank Account Number');
  });

  test('exports everything with sensitive columns only when requested', async ({ page }) => {
    await page.goto('/employees/list');
    await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
    await page.getByRole('button', { name: 'Export' }).click();
    await dialog(page).getByRole('radio', { name: 'All records', exact: true }).check();
    await dialog(page)
      .getByRole('checkbox', { name: /Include personal and financial columns/ })
      .check();
    const { workbook } = await downloadWorkbook(
      page,
      () => dialog(page).getByRole('button', { name: 'Export to Excel' }).click(),
      true,
    );
    const sheet = workbook.getWorksheet('Employees')!;
    expect(sheet.rowCount).toBe(61);
    const headers = (sheet.getRow(1).values as unknown[]).slice(1);
    expect(headers).toEqual(
      expect.arrayContaining(['National ID', 'Bank Account Number', 'Date of Birth']),
    );
  });

  test('requires at least one column', async ({ page }) => {
    await page.goto('/employees/list');
    await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
    await page.getByRole('button', { name: 'Export' }).click();
    await dialog(page).getByRole('checkbox', { name: 'Select all' }).uncheck();
    await expect(dialog(page).getByRole('button', { name: 'Export to Excel' })).toBeDisabled();
  });
});

test.describe('Excel import', () => {
  test.skip(({ isMobile }) => isMobile, 'Desktop flows');

  test('imports employees through the wizard: template, preview, partial import, history and report', async ({
    page,
  }) => {
    await page.goto('/employees/list');
    await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
    await page.getByRole('link', { name: 'Import' }).click();
    await expect(page).toHaveURL(/\/data-exchange\/import\?module=employees/);
    await expect(page.getByRole('heading', { name: 'Import data', level: 1 })).toBeVisible();

    // Step 1: template
    await expect(page.getByText('Choose what to import')).toBeVisible();
    await expect(page.getByText('Existing records are matched by')).toContainText('Employee Code');
    const { workbook: template, fileName } = await downloadWorkbook(page, () =>
      page.getByRole('button', { name: 'Download template' }).click(),
    );
    expect(fileName).toBe('employees-import-template.xlsx');
    expect(template.worksheets.map((w) => w.name)).toEqual(
      expect.arrayContaining(['Instructions', 'Employees', 'Field guide', 'Organization codes']),
    );
    const data = template.getWorksheet('Employees')!;
    const headers = (data.getRow(1).values as string[]).slice(1);
    const codes = template.getWorksheet('Organization codes')!;
    const reference = {
      company: String(codes.getCell(2, 1).value),
      branch: String(codes.getCell(2, 3).value),
      department: String(codes.getCell(2, 5).value),
      position: String(codes.getCell(2, 7).value),
    };

    // Build a workbook: two valid employees, one with a bad email, one duplicate code
    const rowFor = (code: string, email: string): Record<string, string> => ({
      'Employee Code': code,
      'First Name': 'Test',
      'Last Name': code,
      Gender: 'Female',
      'Date of Birth': '1992-04-18',
      'Marital Status': 'Single',
      Nationality: 'Myanmar',
      'National ID': `NRC-${code}`,
      'Work Email': email,
      'Mobile Phone': '+95 9 123 456 789',
      'Address Line 1': '12 Park Road',
      City: 'Yangon',
      Country: 'Myanmar',
      'Emergency Contact Name': 'Contact Person',
      'Emergency Contact Relationship': 'Spouse',
      'Emergency Contact Phone': '+95 9 987 654 321',
      'Employment Type': 'Full-time',
      'Employment Status': 'Active',
      'Joining Date': '2026-01-05',
      'Company Code': reference.company,
      'Branch Code': reference.branch,
      'Department Code': reference.department,
      'Position Code': reference.position,
      'Bank Name': 'Sample Bank',
      'Bank Account Holder': 'Test Person',
      'Bank Account Number': '1234567890',
    });
    const upload = new (await lib()).Workbook();
    const sheet = upload.addWorksheet('Employees');
    sheet.addRow(headers);
    for (const row of [
      rowFor('EMP501', 'emp501@apexhorizon.example'),
      rowFor('EMP502', 'emp502@apexhorizon.example'),
      rowFor('EMP503', 'not-an-email'),
      rowFor('EMP501', 'emp501b@apexhorizon.example'),
    ]) {
      sheet.addRow(headers.map((h) => row[h] ?? null));
    }
    const buffer = Buffer.from(await upload.xlsx.writeBuffer());

    // Step 2: upload
    await page.getByRole('button', { name: 'Continue to upload' }).click();
    await expect(page.getByText('Upload your Excel file')).toBeVisible();
    await page.locator('input[type=file]').first().setInputFiles({
      name: 'new-employees.xlsx',
      mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      buffer,
    });
    await expect(page.getByText('new-employees.xlsx').first()).toBeVisible();
    await page.getByRole('button', { name: 'Check file' }).click();

    // Step 3: columns match the template automatically
    await expect(page.getByText('All columns match the template')).toBeVisible();
    await page.getByRole('button', { name: 'Validate file' }).click();

    // Step 4: review
    const summary = page.getByRole('region', { name: 'Validation summary' });
    await expect(summary).toContainText('Total rows');
    await expect(summary.getByText('Total rows').locator('..')).toContainText('4');
    await expect(page.getByText('2 row(s) have problems')).toBeVisible();
    await expect(page.getByText(/Work Email is not a valid email/)).toBeVisible();
    await expect(page.getByText(/Duplicate of row 2/)).toBeVisible();
    await page
      .getByRole('radio', { name: /Invalid \(1\)/ })
      .check()
      .catch(() => undefined);
    const report = await downloadWorkbook(page, () =>
      page.getByRole('button', { name: 'Download report' }).first().click(),
    );
    const reportSheet = report.workbook.getWorksheet('Validation report')!;
    expect(reportSheet.rowCount).toBe(3); // header + 2 problems
    expect(String(reportSheet.getCell(2, 1).value)).toMatch(/^[45]$/);
    await page.getByRole('button', { name: 'Continue', exact: true }).click();

    // Step 5: confirm (all-or-nothing is not available because of the problem rows)
    await expect(page.getByText('Confirm the import')).toBeVisible();
    await expect(page.getByText('2 record(s) will be added.', { exact: false })).toBeVisible();
    await expect(page.getByRole('radio', { name: /All or nothing/ })).toBeDisabled();
    await expect(page.getByRole('radio', { name: /Import valid rows only/ })).toBeChecked();
    await page.getByRole('button', { name: 'Import now' }).click();

    // Step 6: result
    await expect(page.getByText('Import completed with some rows not imported')).toBeVisible();
    await expect(page.getByText('2 inserted, 0 updated, 2 skipped.')).toBeVisible();

    // The new employees exist (in-app navigation keeps the data)
    await page.getByRole('link', { name: 'View import history' }).click();
    await expect(page.getByRole('heading', { name: 'Import history', level: 1 })).toBeVisible();
    const historyRow = page.locator('tr.mat-mdc-row').filter({ hasText: 'new-employees.xlsx' });
    await expect(historyRow).toContainText('Employees');
    await expect(historyRow).toContainText('Completed with errors');
    await historyRow.click();
    await expect(dialog(page).getByRole('heading', { name: 'Import details' })).toBeVisible();
    await expect(dialog(page)).toContainText('Recorded problems (2)');
    await dialog(page).getByRole('button', { name: 'Close' }).click();

    // Open the employee list in-app from the sidebar (a page reload would reset the data).
    await searchEmployeesInApp(page, 'EMP50');
    await expect(page.locator('tr.mat-mdc-row')).toHaveCount(2);
    await expect(page.locator('tr.mat-mdc-row').first()).toContainText('EMP501');
  });

  test('rejects files that are not real Excel workbooks', async ({ page }) => {
    await page.goto('/data-exchange/import');
    await expect(page.getByText('Choose what to import')).toBeVisible();
    await page.getByRole('button', { name: 'Continue to upload' }).click();
    // A renamed text file passes the browser's quick checks but not the importer's content check
    await page
      .locator('input[type=file]')
      .first()
      .setInputFiles({
        name: 'fake.xlsx',
        mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        buffer: Buffer.from('this is plain text, not a workbook'),
      });
    await page.getByRole('button', { name: 'Check file' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: /does not match the \.xlsx format/ }),
    ).toBeVisible();
    // Other extensions are stopped straight away
    await page.getByRole('button', { name: 'Remove fake.xlsx' }).click();
    await page
      .locator('input[type=file]')
      .first()
      .setInputFiles({
        name: 'data.csv',
        mimeType: 'text/csv',
        buffer: Buffer.from('a,b'),
      });
    await expect(page.getByText('Only Excel workbooks (.xlsx) can be imported.')).toBeVisible();
  });

  test('asks for column mapping when the file does not follow the template', async ({ page }) => {
    await page.goto('/data-exchange/import');
    await page.getByRole('button', { name: 'Continue to upload' }).click();
    const book = new (await lib()).Workbook();
    const sheet = book.addWorksheet('Staff');
    sheet.addRow(['Code', 'Given', 'Family', 'Sex']);
    sheet.addRow(['EMP600', 'Ada', 'Lovelace', 'Female']);
    await page
      .locator('input[type=file]')
      .first()
      .setInputFiles({
        name: 'staff.xlsx',
        mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        buffer: Buffer.from(await book.xlsx.writeBuffer()),
      });
    await page.getByRole('button', { name: 'Check file' }).click();
    await expect(page.getByText('Some columns need your attention')).toBeVisible();
    await expect(page.getByText(/required field "First Name" is not mapped/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Validate file' })).toBeDisabled();
  });
});

test.describe('Excel screens on mobile', () => {
  test.skip(({ isMobile }) => !isMobile, 'Mobile only');

  test('import wizard and history do not widen the page', async ({ page }) => {
    for (const url of ['/data-exchange/import', '/data-exchange/history']) {
      await page.goto(url);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
      );
      expect(overflow, url).toBe(false);
    }
  });
});
