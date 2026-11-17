import { Scorecard } from '../../../core/models/kpi.models';
import { isLateSubmission } from './scorecard-rules';

/**
 * Whether a row of a scorecard list was submitted late: `submittedOn` after the period's
 * `submissionDueDate`, which every list answer carries (null when the period has none).
 */
export const rowIsLate = (row: Pick<Scorecard, 'submittedOn' | 'submissionDueDate'>): boolean =>
  isLateSubmission(row.submittedOn, row.submissionDueDate);
