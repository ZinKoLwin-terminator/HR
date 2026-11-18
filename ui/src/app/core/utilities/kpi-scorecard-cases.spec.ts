// The shared golden file of whole scorecards, weights, rating labels and dashboard figures: the
// contract with the C# `KpiCalculator` and `KpiFigures` (run there by `KpiScorecardCasesTests`).
// Imported from its repository location (not copied), like kpi-calculation-cases.json.
import scorecardCases from '../../../../../docs/test-data/kpi-scorecard-cases.json';
import {
  KpiCalcResult,
  KpiRuleInput,
  kpiRatingLabel,
  scoreScorecard,
  validateKpiWeights,
} from './kpi-calculation';
import {
  kpiAttentionThreshold,
  kpiAverageOf,
  kpiCompetitionRanks,
  kpiCompletionRate,
} from './kpi-figures';

/** `{ message, fieldErrors: { field: [messages] } }`: the AppError of a 400 in the API. */
interface ExpectedError {
  message: string;
  fieldErrors: Record<string, string[]>;
}

/** One case; `kind` says which of the other fields it uses (see the file's `_comment`). */
interface SharedCase {
  kind: string;
  name: string;
  items?: { rule: KpiRuleInput; weight: number; actual: number | null }[];
  expected?: unknown;
  expectedError?: ExpectedError;
  weights?: number[];
  valid?: boolean;
  overallScore?: number;
  bands?: { minScore: number; label: string }[];
  expectedLabel?: string | null;
  sum?: number | null;
  count?: number;
  finalized?: number;
  evaluated?: number;
  scoresDescending?: number[];
  firstRank?: number;
  firstPosition?: number;
  activeBandMinScores?: number[];
}

const KINDS = [
  'score',
  'validateWeights',
  'ratingLabel',
  'average',
  'completionRate',
  'competitionRanks',
  'attentionThreshold',
];

const CASES = (scorecardCases as unknown as { cases: SharedCase[] }).cases;

/**
 * A failed result against the expected error. The TS calculator keeps one message per field (the
 * API a list), so the expected list is joined the way the C# test joins both sides.
 */
function expectError(result: KpiCalcResult<unknown>, error: ExpectedError): void {
  expect(result.ok).toBe(false);
  if (result.ok) return;
  expect(result.message).toBe(error.message);
  const joined = Object.fromEntries(
    Object.entries(error.fieldErrors).map(([field, messages]) => [field, messages.join(' | ')]),
  );
  expect(result.fieldErrors).toEqual(joined);
}

function run(shared: SharedCase): void {
  switch (shared.kind) {
    case 'score': {
      const result = scoreScorecard(shared.items!);
      if (shared.expectedError) {
        expectError(result, shared.expectedError);
        return;
      }
      expect(result).toEqual({ ok: true, value: shared.expected });
      return;
    }
    case 'validateWeights': {
      const result = validateKpiWeights(shared.weights!);
      if (shared.valid) {
        expect(result.ok, result.ok ? '' : result.message).toBe(true);
      } else {
        expectError(result, shared.expectedError!);
      }
      return;
    }
    case 'ratingLabel':
      expect(kpiRatingLabel(shared.overallScore!, shared.bands!)).toBe(shared.expectedLabel);
      return;
    case 'average':
      expect(kpiAverageOf(shared.sum ?? null, shared.count!)).toBe(shared.expected);
      return;
    case 'completionRate':
      expect(kpiCompletionRate(shared.finalized!, shared.evaluated!)).toBe(shared.expected);
      return;
    case 'competitionRanks':
      expect(
        kpiCompetitionRanks(shared.scoresDescending!, shared.firstRank, shared.firstPosition),
      ).toEqual(shared.expected);
      return;
    case 'attentionThreshold':
      expect(kpiAttentionThreshold(shared.activeBandMinScores!)).toBe(shared.expected);
      return;
    default:
      throw new Error(`Unknown kind "${shared.kind}" in case "${shared.name}".`);
  }
}

describe('KPI scorecard calculation: shared golden cases', () => {
  it('has unique names, known kinds and cases of every kind', () => {
    expect(new Set(CASES.map((c) => c.name)).size).toBe(CASES.length);
    for (const shared of CASES) {
      expect(KINDS, shared.name).toContain(shared.kind);
    }
    for (const kind of KINDS) {
      expect(
        CASES.some((c) => c.kind === kind),
        kind,
      ).toBe(true);
    }
  });

  for (const shared of CASES) {
    it(shared.name, () => run(shared));
  }
});
