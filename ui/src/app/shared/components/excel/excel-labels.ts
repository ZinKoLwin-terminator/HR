import { ImportBatchStatus } from '../../../core/models/excel.models';

/** Friendly names for batch statuses (the badge colours come from the shared status badge). */
export const BATCH_STATUS_LABELS: Record<ImportBatchStatus, string> = {
  Validated: 'Validated (not imported)',
  Completed: 'Completed',
  CompletedWithErrors: 'Completed with errors',
  Failed: 'Failed (rolled back)',
};

/**
 * The module name to show for a batch. The label comes with the batch; when it is missing or is
 * only the module code (an older record), the code is made readable: `job-grades` becomes `Job grades`.
 */
export function moduleDisplayName(moduleCode: string, moduleLabel?: string | null): string {
  const label = moduleLabel?.trim();
  if (label && label !== moduleCode) return label;
  const words = moduleCode.replace(/[-_]+/g, ' ').trim();
  return words ? words.charAt(0).toUpperCase() + words.slice(1) : moduleCode;
}
