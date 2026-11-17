import { KpiDirection, KpiMeasurementType, KpiScoringMethod } from '../models/kpi.models';

/**
 * KPI scoring for the DEMO MOCK ONLY.
 *
 * This repeats the rules of the API's `KpiCalculator` / `KpiRule` (calculation version 1) so the
 * demo build behaves the same way. It is NOT authoritative: with the API on, every official score
 * comes from the server and nothing here is used. The shared golden cases in
 * `docs/test-data/kpi-calculation-cases.json` run against this file and against the C# calculator,
 * so the two cannot drift.
 *
 * No floating point drift: every number is turned into an exact fraction of BigInts (a numerator and
 * a denominator), the formulas run on those fractions, and the only rounding is one explicit step at
 * the end of each figure: two decimals, a midpoint rounds away from zero (0.125 gives 0.13, 21.875
 * gives 21.88).
 */

/** The rule of one KPI (a template, or an item copied from it). */
export interface KpiRuleInput {
  measurementType: KpiMeasurementType;
  direction: KpiDirection;
  scoringMethod: KpiScoringMethod;
  target: number;
  rangeMin: number | null;
  rangeMax: number | null;
  maxRating: number | null;
  maxAchievementPct: number | null;
}

export type KpiCalcResult<T> =
  { ok: true; value: T } | { ok: false; message: string; fieldErrors: Record<string, string> };

/** No achievement exceeds this, whatever the rule. */
export const KPI_SAFETY_CEILING_PCT = 1000;
/** The largest `maxAchievementPct` a rule may have. */
export const KPI_MAX_ACHIEVEMENT_LIMIT = 500;
/** The weights of a plan add up to exactly this. */
export const KPI_WEIGHT_TOTAL = 100;

// ---- Exact decimal arithmetic (fractions of BigInts) ---------------------------------------

/** An exact value `n / d` with `d > 0`. */
interface Fraction {
  n: bigint;
  d: bigint;
}

const ZERO: Fraction = { n: 0n, d: 1n };

/** Exact conversion of a JS number through its shortest decimal text (handles `1e-28` and `1e+21`). */
function exact(value: number): Fraction {
  if (!Number.isFinite(value)) {
    throw new RangeError('A KPI figure must be a finite number.');
  }
  const match = /^(-?)(\d+)(?:\.(\d+))?(?:e([+-]?\d+))?$/i.exec(String(value));
  if (!match) {
    throw new RangeError(`Cannot read "${String(value)}" as a decimal.`);
  }
  const [, sign, whole, fraction = '', exponent = '0'] = match;
  const digits = BigInt(whole + fraction) * (sign === '-' ? -1n : 1n);
  const power = Number(exponent) - fraction.length;
  return power >= 0
    ? { n: digits * 10n ** BigInt(power), d: 1n }
    : { n: digits, d: 10n ** BigInt(-power) };
}

const compare = (a: Fraction, b: Fraction): number => {
  const left = a.n * b.d;
  const right = b.n * a.d;
  return left < right ? -1 : left > right ? 1 : 0;
};
const sign = (a: Fraction): number => (a.n < 0n ? -1 : a.n > 0n ? 1 : 0);
const subtract = (a: Fraction, b: Fraction): Fraction => ({
  n: a.n * b.d - b.n * a.d,
  d: a.d * b.d,
});
const absolute = (a: Fraction): Fraction => ({ n: a.n < 0n ? -a.n : a.n, d: a.d });
const maxOf = (a: Fraction, b: Fraction): Fraction => (compare(a, b) >= 0 ? a : b);
const minOf = (a: Fraction, b: Fraction): Fraction => (compare(a, b) <= 0 ? a : b);
const whole = (value: number): Fraction => ({ n: BigInt(value), d: 1n });

/** numerator / denominator x 100, for a positive denominator. */
function percent(numerator: Fraction, denominator: Fraction): Fraction {
  return { n: numerator.n * denominator.d * 100n, d: numerator.d * denominator.n };
}

/** Rounds to 2 decimals, a midpoint away from zero, and returns the value in hundredths. */
function toHundredths(value: Fraction): bigint {
  const negative = value.n < 0n;
  const magnitude = negative ? -value.n : value.n;
  // floor(|v| x 100 + 1/2), written with integers only.
  const rounded = (magnitude * 200n + value.d) / (2n * value.d);
  return negative ? -rounded : rounded;
}

const fromHundredths = (hundredths: bigint): number => Number(hundredths) / 100;

/** Rounds a number to 2 decimals, a midpoint away from zero, without float drift. */
export function roundHalfAwayFromZero2(value: number): number {
  return fromHundredths(toHundredths(exact(value)));
}

// ---- Rule validation (KpiRule.Validate) ----------------------------------------------------

/**
 * The same consistency rules as the database CHECK constraints and `KpiRule.Validate`.
 * Returns the first error per field, keyed by the field a screen shows it on; empty when valid.
 */
export function validateKpiRule(rule: KpiRuleInput): Record<string, string> {
  const errors: Record<string, string> = {};
  const { measurementType, direction, scoringMethod, maxAchievementPct: cap } = rule;

  if ((scoringMethod === 'Rating Based') !== (measurementType === 'Rating')) {
    errors['scoringMethod'] = 'Rating Based scoring and the Rating measurement type go together.';
  } else if ((scoringMethod === 'Boolean') !== (measurementType === 'Boolean')) {
    errors['scoringMethod'] = 'Boolean scoring and the Boolean measurement type go together.';
  } else if ((scoringMethod === 'Range Based') !== (direction === 'Range Based')) {
    errors['scoringMethod'] = 'Range Based scoring and the Range Based direction go together.';
  }

  if (scoringMethod === 'Rating Based') {
    if (rule.maxRating === null || !(rule.maxRating > 0)) {
      errors['maxRating'] = 'The top of the rating scale must be greater than 0.';
    }
  } else if (rule.maxRating !== null) {
    errors['maxRating'] = 'The top of the rating scale is only for Rating Based scoring.';
  }

  if (scoringMethod === 'Range Based') {
    if (rule.rangeMin === null || !(rule.rangeMin > 0)) {
      errors['rangeMin'] = 'The range minimum must be greater than 0.';
    }
    if (rule.rangeMax === null || (rule.rangeMin !== null && rule.rangeMax <= rule.rangeMin)) {
      errors['rangeMax'] = 'The range maximum must be greater than the range minimum.';
    }
  } else {
    if (rule.rangeMin !== null) {
      errors['rangeMin'] = 'The range minimum is only for Range Based scoring.';
    }
    if (rule.rangeMax !== null) {
      errors['rangeMax'] = 'The range maximum is only for Range Based scoring.';
    }
  }

  if (scoringMethod === 'Capped Percentage' && cap === null) {
    errors['maxAchievementPct'] = 'Capped Percentage needs a maximum achievement.';
  } else if (direction === 'Lower Is Better' && cap === null) {
    // A result of 0 cannot be divided by: the cap is what a perfect (zero) result scores.
    errors['maxAchievementPct'] =
      'Lower is better needs a maximum achievement (use Capped Percentage): a result of 0 scores it.';
  } else if (scoringMethod === 'Direct Percentage' && cap !== null) {
    errors['maxAchievementPct'] = 'Direct Percentage has no maximum achievement.';
  } else if (cap !== null && (cap < 1 || cap > KPI_MAX_ACHIEVEMENT_LIMIT)) {
    errors['maxAchievementPct'] =
      `The maximum achievement must be between 1 and ${KPI_MAX_ACHIEVEMENT_LIMIT}.`;
  }

  if (rule.target < 0) {
    errors['target'] = 'The target cannot be negative.';
  } else if (
    rule.target === 0 &&
    (direction === 'Higher Is Better' || direction === 'Exact Target')
  ) {
    errors['target'] = 'The target must be greater than 0 for this direction.';
  }

  return errors;
}

// ---- Achievement ----------------------------------------------------------------------------

function failure(message: string, field: string): KpiCalcResult<never> {
  return { ok: false, message, fieldErrors: { [field]: message } };
}

type RawResult = { ok: true; value: Fraction } | { ok: false; message: string };

function actualError(message: string): RawResult {
  return { ok: false, message };
}

function rawAchievement(rule: KpiRuleInput, actualValue: number): RawResult {
  const actual = exact(actualValue);
  switch (rule.scoringMethod) {
    case 'Boolean':
      return actualValue === 0 || actualValue === 1
        ? { ok: true, value: whole(actualValue * 100) }
        : actualError('A yes/no result must be 0 or 1.');

    case 'Rating Based': {
      const maxRating = rule.maxRating as number;
      return actualValue < 0 || actualValue > maxRating
        ? actualError(`The rating must be between 0 and ${maxRating}.`)
        : { ok: true, value: percent(actual, exact(maxRating)) };
    }

    case 'Range Based': {
      const min = exact(rule.rangeMin as number);
      const max = exact(rule.rangeMax as number);
      if (compare(actual, min) >= 0 && compare(actual, max) <= 0) {
        return { ok: true, value: whole(100) };
      }
      if (compare(actual, min) < 0) {
        return { ok: true, value: sign(actual) <= 0 ? ZERO : percent(actual, min) };
      }
      return { ok: true, value: percent(max, actual) };
    }

    default:
      return byDirection(rule, actual);
  }
}

function byDirection(rule: KpiRuleInput, actual: Fraction): RawResult {
  const target = exact(rule.target);
  switch (rule.direction) {
    case 'Higher Is Better':
      return { ok: true, value: sign(actual) <= 0 ? ZERO : percent(actual, target) };

    case 'Lower Is Better':
      if (sign(actual) < 0) {
        return actualError('The actual result cannot be negative when lower is better.');
      }
      if (sign(target) === 0) {
        return { ok: true, value: sign(actual) === 0 ? whole(100) : ZERO };
      }
      // A zero result is perfect: it scores the cap, which validation requires for this direction.
      return {
        ok: true,
        value:
          sign(actual) === 0 ? exact(rule.maxAchievementPct as number) : percent(target, actual),
      };

    case 'Exact Target':
      return {
        ok: true,
        value: subtract(whole(100), percent(absolute(subtract(actual, target)), target)),
      };

    default:
      return actualError('Range Based scoring and the Range Based direction go together.');
  }
}

/** Achievement in percent, rounded to 2 decimals, for one actual result against its rule. */
export function kpiAchievement(rule: KpiRuleInput, actual: number): KpiCalcResult<number> {
  const invalid = validateKpiRule(rule);
  if (Object.keys(invalid).length > 0) {
    return { ok: false, message: 'The KPI rule is not valid.', fieldErrors: invalid };
  }
  const raw = rawAchievement(rule, actual);
  if (!raw.ok) {
    return failure(raw.message, 'actualValue');
  }
  let value = maxOf(raw.value, ZERO);
  if (rule.maxAchievementPct !== null) {
    value = minOf(value, exact(rule.maxAchievementPct));
  }
  value = minOf(value, whole(KPI_SAFETY_CEILING_PCT));
  return { ok: true, value: fromHundredths(toHundredths(value)) };
}

/** Contribution to the overall score, from the already rounded achievement so shown figures reconcile. */
export function kpiWeightedScore(achievementPct: number, weight: number): number {
  const product = exact(achievementPct);
  const scaled = { n: product.n * exact(weight).n, d: product.d * exact(weight).d * 100n };
  return fromHundredths(toHundredths(scaled));
}

// ---- Weights --------------------------------------------------------------------------------

/**
 * The weights of a scorecard: at least one, each above 0 and up to 100 with at most 2 decimals,
 * adding up to exactly 100. On success, `value` is the total.
 */
export function validateKpiWeights(weights: readonly number[]): KpiCalcResult<number> {
  const fail = (message: string) => failure(message, 'weights');
  if (weights.length === 0) {
    return fail('A scorecard needs at least one KPI.');
  }
  let total = ZERO;
  for (const weight of weights) {
    if (weight <= 0 || weight > 100) {
      return fail('Each KPI weight must be above 0 and at most 100%.');
    }
    const fraction = exact(weight);
    if ((fraction.n * 100n) % fraction.d !== 0n) {
      return fail('Each KPI weight can have at most 2 decimals.');
    }
    total = { n: total.n * fraction.d + fraction.n * total.d, d: total.d * fraction.d };
  }
  if (compare(total, whole(KPI_WEIGHT_TOTAL)) !== 0) {
    return fail(
      `The KPI weights must add up to 100%. They add up to ${fromHundredths(toHundredths(total)).toFixed(2)}%.`,
    );
  }
  return { ok: true, value: KPI_WEIGHT_TOTAL };
}

/** The sum of the weights (exact), for display and for the 100% cap while planning. */
export function sumKpiWeights(weights: readonly number[]): number {
  let total = ZERO;
  for (const weight of weights) {
    const fraction = exact(weight);
    total = { n: total.n * fraction.d + fraction.n * total.d, d: total.d * fraction.d };
  }
  return fromHundredths(toHundredths(total));
}

// ---- Whole scorecard ------------------------------------------------------------------------

export interface KpiScoreInput {
  rule: KpiRuleInput;
  /** Percent, 0 to 100. */
  weight: number;
  actual: number | null;
}

export interface KpiItemScore {
  achievementPct: number;
  weightedScore: number;
}

export interface ScorecardScore {
  items: KpiItemScore[];
  overallScore: number;
}

/** Scores a whole scorecard: every KPI needs a result and the weights must add up to 100. */
export function scoreScorecard(items: readonly KpiScoreInput[]): KpiCalcResult<ScorecardScore> {
  const weights = validateKpiWeights(items.map((item) => item.weight));
  if (!weights.ok) {
    return weights;
  }

  const missing: Record<string, string> = {};
  items.forEach((item, index) => {
    if (item.actual === null) {
      missing[`items[${index}].actualValue`] = 'The actual result is missing.';
    }
  });
  if (Object.keys(missing).length > 0) {
    return {
      ok: false,
      message: 'Every KPI needs an actual result before it can be scored.',
      fieldErrors: missing,
    };
  }

  const scores: KpiItemScore[] = [];
  let overall = 0n; // hundredths
  for (const [index, item] of items.entries()) {
    const achievement = kpiAchievement(item.rule, item.actual as number);
    if (!achievement.ok) {
      const fieldErrors = Object.fromEntries(
        Object.entries(achievement.fieldErrors).map(([field, text]) => [
          `items[${index}].${field}`,
          text,
        ]),
      );
      return { ok: false, message: achievement.message, fieldErrors };
    }
    const weightedScore = kpiWeightedScore(achievement.value, item.weight);
    scores.push({ achievementPct: achievement.value, weightedScore });
    overall += BigInt(Math.round(weightedScore * 100));
  }
  return { ok: true, value: { items: scores, overallScore: fromHundredths(overall) } };
}

// ---- Rating label ---------------------------------------------------------------------------

/** The label of the band with the highest minimum score that the score reaches, or null if it reaches none. */
export function kpiRatingLabel(
  overallScore: number,
  bands: readonly { minScore: number; label: string }[],
): string | null {
  let label: string | null = null;
  let best: number | null = null;
  for (const band of bands) {
    if (band.minScore <= overallScore && (best === null || band.minScore > best)) {
      best = band.minScore;
      label = band.label;
    }
  }
  return label;
}
