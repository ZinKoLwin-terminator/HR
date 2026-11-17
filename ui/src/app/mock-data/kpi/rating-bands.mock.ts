import { RatingBand } from '../../core/models/kpi.models';

type BandSeed = [label: string, minScore: number, description: string];

const SEED: BandSeed[] = [
  ['Outstanding', 90, 'Consistently exceeds every target.'],
  ['Exceeds Expectations', 75, 'Exceeds most targets.'],
  ['Meets Expectations', 60, 'Meets the agreed targets.'],
  ['Needs Improvement', 40, 'Misses several targets; a development plan is expected.'],
  ['Unsatisfactory', 0, 'Misses most targets.'],
];

/**
 * The default bands, the same as the database seed (a score of at least `minScore` gets the label).
 * They are configuration, like roles, so the empty-data mode keeps them.
 */
export const DEFAULT_RATING_BANDS: RatingBand[] = SEED.map(([label, minScore, description], i) => ({
  id: i + 1,
  label,
  minScore,
  description,
  status: 'Active',
  rowVersion: '1',
}));
