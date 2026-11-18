/**
 * What the submission form tells the employee about an anonymous grievance. The design: an anonymous
 * case is linked to its submitter only by a keyed pseudonym; HR sees no name, department or position
 * and the history and messages say "Anonymous submitter". Some details can still identify a person;
 * the form says so plainly instead of promising more than the system can give.
 */

/** What HR will see on an anonymous grievance. */
export const ANONYMOUS_HR_SEES: readonly string[] = [
  'The category, subject, description, incident date and location you write.',
  'The person and department you name as involved, and the files you attach.',
  'Your messages, signed "Anonymous submitter".',
];

/** What HR will not see. */
export const ANONYMOUS_HR_DOES_NOT_SEE: readonly string[] = [
  'Your name, employee code, department or position.',
  'Your user account: the history and messages carry no account or employee id.',
];

/** What can still give an anonymous submitter away. */
export const ANONYMOUS_RESIDUAL_RISKS: readonly string[] = [
  'Timing: when you submit or reply can be matched to who was at work or online.',
  'Writing style and details only you would know can point to you.',
  'File names are visible to HR, and PDF files keep their details (author, software, dates). Details inside Word, Excel, JPEG and PNG files are removed when you upload them. Rename files, and export or print PDFs carefully.',
  'In a small department, the facts alone may narrow it down to a few people.',
];

/**
 * Shown where an anonymous submitter adds files. The server removes the metadata of Word, Excel,
 * JPEG and PNG files; it does NOT clean PDFs, and file names always stay visible to HR.
 */
export const ANONYMOUS_FILE_WARNING =
  'Files can identify you. Details inside Word, Excel, JPEG and PNG files are removed when you upload them, but details inside PDF files are NOT (author, software, dates): export or print PDFs carefully, for example with your name removed from the document properties. File names are visible to HR, so rename files before attaching them.';

/** Shown on an existing draft: the reporting choice is fixed when the draft is created. */
export const ANONYMITY_FIXED_NOTE = 'Start a new draft to change how you report.';

/** Shown when the chosen category does not accept anonymous reports. */
export const ANONYMOUS_NOT_ALLOWED =
  'This category does not accept anonymous grievances: HR needs to know who is concerned to look into it.';
