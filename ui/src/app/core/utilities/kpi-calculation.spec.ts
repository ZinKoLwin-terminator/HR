import { KpiDirection, KpiMeasurementType, KpiScoringMethod } from '../models/kpi.models';
// The single golden file is the contract shared with the C# calculator. It is imported from its
// repository location (not copied): the Angular unit-test builder bundles it like any JSON module.
import goldenCases from '../../../../../docs/test-data/kpi-calculation-cases.json';
import {
  KpiRuleInput,
  kpiAchievement,
  kpiRatingLabel,
  kpiWeightedScore,
  roundHalfAwayFromZero2,
  scoreScorecard,
  sumKpiWeights,
  validateKpiRule,
  validateKpiWeights,
} from './kpi-calculation';

interface GoldenCase {
  name: string;
  rule: {
    measurementType: KpiMeasurementType;
    direction: KpiDirection;
    scoringMethod: KpiScoringMethod;
    target: number;
    rangeMin: number | null;
    rangeMax: number | null;
    maxRating: number | null;
    maxAchievementPct: number | null;
  };
  actual: number;
  expectedAchievement?: number;
  expectedError?: string;
}

const CASES = goldenCases as unknown as GoldenCase[];

const rule = (overrides: Partial<KpiRuleInput> = {}): KpiRuleInput => ({
  measurementType: 'Number',
  direction: 'Higher Is Better',
  scoringMethod: 'Direct Percentage',
  target: 100,
  rangeMin: null,
  rangeMax: null,
  maxRating: null,
  maxAchievementPct: null,
  ...overrides,
});

describe('KPI calculation: shared golden cases', () => {
  it('loads the shared file with unique names', () => {
    expect(CASES.length).toBeGreaterThanOrEqual(30);
    expect(new Set(CASES.map((c) => c.name)).size).toBe(CASES.length);
  });

  for (const golden of CASES) {
    it(golden.name, () => {
      const result = kpiAchievement(golden.rule, golden.actual);
      if (golden.expectedError !== undefined) {
        expect(result.ok).toBe(false);
        if (!result.ok) {
          expect(Object.keys(result.fieldErrors)).toContain(golden.expectedError);
        }
      } else {
        expect(result).toEqual({ ok: true, value: golden.expectedAchievement });
      }
    });
  }
});

describe('KPI calculation: rounding without float drift', () => {
  it('rounds midpoints away from zero', () => {
    expect(roundHalfAwayFromZero2(0.125)).toBe(0.13);
    expect(roundHalfAwayFromZero2(21.875)).toBe(21.88);
    expect(roundHalfAwayFromZero2(-0.125)).toBe(-0.13);
    expect(roundHalfAwayFromZero2(1.005)).toBe(1.01); // 1.005 is below the midpoint as a double
    expect(roundHalfAwayFromZero2(2.675)).toBe(2.68);
    expect(roundHalfAwayFromZero2(0.1 + 0.2)).toBe(0.3);
  });

  it('gives 0.13 for 1 of 800 and 33.33 for 1 of 3', () => {
    expect(kpiAchievement(rule({ target: 800 }), 1)).toEqual({ ok: true, value: 0.13 });
    expect(kpiAchievement(rule({ target: 3 }), 1)).toEqual({ ok: true, value: 33.33 });
  });

  it('rounds a weighted score from the rounded achievement', () => {
    expect(kpiWeightedScore(62.5, 35)).toBe(21.88);
    expect(kpiWeightedScore(50.5, 1)).toBe(0.51);
    expect(kpiWeightedScore(33.33, 0.5)).toBe(0.17);
    expect(kpiWeightedScore(80, 50)).toBe(40);
  });
});

describe('KPI calculation: rule validation', () => {
  it('accepts a consistent rule and refuses each inconsistent one on its field', () => {
    expect(validateKpiRule(rule())).toEqual({});
    expect(validateKpiRule(rule({ target: 0 }))['target']).toBeDefined();
    expect(
      validateKpiRule(rule({ scoringMethod: 'Rating Based', measurementType: 'Rating' }))[
        'maxRating'
      ],
    ).toBeDefined();
    expect(
      validateKpiRule(rule({ scoringMethod: 'Capped Percentage', maxAchievementPct: null }))[
        'maxAchievementPct'
      ],
    ).toBeDefined();
    expect(
      validateKpiRule(rule({ scoringMethod: 'Boolean', measurementType: 'Number' }))[
        'scoringMethod'
      ],
    ).toBeDefined();
    expect(
      validateKpiRule(rule({ direction: 'Lower Is Better', scoringMethod: 'Direct Percentage' }))[
        'maxAchievementPct'
      ],
    ).toContain('Lower is better needs a maximum achievement');
    expect(
      validateKpiRule(
        rule({
          direction: 'Range Based',
          scoringMethod: 'Range Based',
          rangeMin: 105,
          rangeMax: 95,
        }),
      )['rangeMax'],
    ).toBeDefined();
  });

  it('keeps the cap between 1 and 500', () => {
    const capped = (cap: number) =>
      validateKpiRule(rule({ scoringMethod: 'Capped Percentage', maxAchievementPct: cap }));
    expect(capped(0.99)['maxAchievementPct']).toBeDefined();
    expect(capped(1)).toEqual({});
    expect(capped(500)).toEqual({});
    expect(capped(500.01)['maxAchievementPct']).toBeDefined();
  });

  it('does not score with an invalid rule', () => {
    const result = kpiAchievement(rule({ target: 0 }), 50);
    expect(result.ok).toBe(false);
  });
});

describe('KPI calculation: weights', () => {
  it('needs at least one KPI', () => {
    const result = validateKpiWeights([]);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.fieldErrors['weights']).toBe('A scorecard needs at least one KPI.');
    }
  });

  it('accepts exactly 100, however it is split', () => {
    expect(validateKpiWeights([100]).ok).toBe(true);
    expect(validateKpiWeights([40, 35, 25]).ok).toBe(true);
    expect(validateKpiWeights([33.33, 33.33, 33.34]).ok).toBe(true);
    expect(validateKpiWeights([0.1, 0.2, 99.7]).ok).toBe(true);
  });

  it('names the total when it is not 100', () => {
    const low = validateKpiWeights([50, 49.99]);
    expect(low.ok).toBe(false);
    if (!low.ok) {
      expect(low.message).toBe('The KPI weights must add up to 100%. They add up to 99.99%.');
    }
    const high = validateKpiWeights([50, 50.01]);
    expect(high.ok).toBe(false);
    if (!high.ok) {
      expect(high.message).toContain('100.01%');
    }
    const short = validateKpiWeights([40, 35]);
    expect(short.ok).toBe(false);
    if (!short.ok) {
      expect(short.message).toBe('The KPI weights must add up to 100%. They add up to 75.00%.');
    }
  });

  it('refuses a weight at or below 0, above 100, or with more than 2 decimals', () => {
    for (const weights of [[0, 100], [-1, 101], [100.01], [50, 49.999, 0.001]]) {
      expect(validateKpiWeights(weights).ok).toBe(false);
    }
  });

  it('sums weights without float drift', () => {
    expect(sumKpiWeights([0.1, 0.2])).toBe(0.3);
    expect(sumKpiWeights([33.33, 33.33, 33.34])).toBe(100);
  });
});

describe('KPI calculation: overall score and rating label', () => {
  const bands = [
    { label: 'Outstanding', minScore: 90 },
    { label: 'Exceeds Expectations', minScore: 75 },
    { label: 'Meets Expectations', minScore: 60 },
    { label: 'Needs Improvement', minScore: 40 },
    { label: 'Unsatisfactory', minScore: 0 },
  ];

  const exampleCard = (actuals: (number | null)[]) => [
    {
      rule: rule({ scoringMethod: 'Capped Percentage', maxAchievementPct: 120 }),
      weight: 40,
      actual: actuals[0],
    },
    {
      rule: rule({
        direction: 'Lower Is Better',
        scoringMethod: 'Capped Percentage',
        target: 5,
        maxAchievementPct: 100,
      }),
      weight: 35,
      actual: actuals[1],
    },
    {
      rule: rule({
        measurementType: 'Boolean',
        scoringMethod: 'Boolean',
        target: 1,
      }),
      weight: 25,
      actual: actuals[2],
    },
  ];

  it('scores the documented example: 34.00 + 21.88 + 25.00 = 80.88', () => {
    const result = scoreScorecard(exampleCard([85, 8, 1]));
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.value.items).toEqual([
        { achievementPct: 85, weightedScore: 34 },
        { achievementPct: 62.5, weightedScore: 21.88 },
        { achievementPct: 100, weightedScore: 25 },
      ]);
      expect(result.value.overallScore).toBe(80.88);
      expect(kpiRatingLabel(result.value.overallScore, bands)).toBe('Exceeds Expectations');
    }
  });

  it('adds the rounded weighted scores, so the shown figures reconcile', () => {
    const third = {
      rule: rule({ target: 100 }),
      weight: 33.33,
      actual: 33.33,
    };
    const result = scoreScorecard([
      third,
      { ...third, weight: 33.33 },
      { ...third, weight: 33.34 },
    ]);
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.value.items.map((i) => i.weightedScore)).toEqual([11.11, 11.11, 11.11]);
      expect(result.value.overallScore).toBe(33.33);
    }
  });

  it('refuses a missing result on the item and a bad weight total', () => {
    const missing = scoreScorecard(exampleCard([85, null, 1]));
    expect(missing.ok).toBe(false);
    if (!missing.ok) {
      expect(Object.keys(missing.fieldErrors)).toEqual(['items[1].actualValue']);
    }
    const weights = exampleCard([85, 8, 1]);
    weights[0].weight = 39;
    const wrong = scoreScorecard(weights);
    expect(wrong.ok).toBe(false);
    if (!wrong.ok) {
      expect(Object.keys(wrong.fieldErrors)).toContain('weights');
    }
    expect(scoreScorecard([]).ok).toBe(false);
  });

  it('prefixes an invalid result with the item', () => {
    const result = scoreScorecard(exampleCard([85, -1, 1]));
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.fieldErrors)).toEqual(['items[1].actualValue']);
    }
  });

  it('picks the highest band the score reaches', () => {
    expect(kpiRatingLabel(95, bands)).toBe('Outstanding');
    expect(kpiRatingLabel(90, bands)).toBe('Outstanding');
    expect(kpiRatingLabel(89.99, bands)).toBe('Exceeds Expectations');
    expect(kpiRatingLabel(75.88, bands)).toBe('Exceeds Expectations');
    expect(kpiRatingLabel(60, bands)).toBe('Meets Expectations');
    expect(kpiRatingLabel(40, bands)).toBe('Needs Improvement');
    expect(kpiRatingLabel(0, bands)).toBe('Unsatisfactory');
    expect(kpiRatingLabel(120, bands)).toBe('Outstanding');
  });

  it('has no label without bands, or when the score is below every band', () => {
    expect(kpiRatingLabel(80, [])).toBeNull();
    expect(kpiRatingLabel(10, [{ label: 'Only', minScore: 50 }])).toBeNull();
  });
});
