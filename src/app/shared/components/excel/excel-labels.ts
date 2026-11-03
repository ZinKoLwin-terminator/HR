import { ImportBatchStatus } from '../../../core/models/excel.models';

/** Friendly names for batch statuses (the badge colours come from the shared status badge). */
export const BATCH_STATUS_LABELS: Record<ImportBatchStatus, string> = {
  Validated: 'Validated (not imported)',
  Completed: 'Completed',
  CompletedWithErrors: 'Completed with errors',
  Failed: 'Failed (rolled back)',
};
