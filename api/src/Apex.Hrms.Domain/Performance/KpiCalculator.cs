using System.Globalization;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>One KPI of a scorecard to be scored: its rule, its weight in percent and the actual result.</summary>
public sealed record KpiScoreInput(KpiRule Rule, decimal Weight, decimal? Actual);

/// <summary>The score of one KPI: its achievement and its contribution to the overall score, both in percent.</summary>
public sealed record KpiItemScore(decimal AchievementPct, decimal WeightedScore);

public sealed record ScorecardScore(IReadOnlyList<KpiItemScore> Items, decimal OverallScore);

/// <summary>
/// The one authority for official KPI scores: only the API calculates them, and only here. Pure and decimal-only, so
/// the same inputs always give the same figures. The frontend demo mock runs the same golden cases
/// (docs/test-data/kpi-calculation-cases.json) so the two cannot drift.
/// <para>
/// Rounding (two decimals, <see cref="MidpointRounding.AwayFromZero"/>) and every formula belong to
/// <see cref="CalculationVersion"/> 1. Changing either means a new version number, so a stored score stays
/// explainable. That is the "rounding configuration": deliberately versioned, not a runtime setting.
/// </para>
/// </summary>
public static class KpiCalculator
{
    /// <summary>Stored with every calculated score. Bump it when any formula or the rounding changes.</summary>
    public const short CalculationVersion = 1;

    /// <summary>
    /// No achievement exceeds this, whatever the rule. It keeps an uncapped Direct Percentage inside the database's
    /// decimal(8,2) and stops one absurd result from dominating a scorecard.
    /// </summary>
    public const decimal SafetyCeilingPct = 1000m;

    public const decimal WeightTotal = 100m;

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Achievement in percent, rounded to two decimals, for one actual result against its rule.</summary>
    public static Result<decimal> Achievement(KpiRule rule, decimal actual)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var valid = rule.Validate();
        if (valid.IsFailure)
        {
            return Result.Failure<decimal>(valid.Error);
        }

        var raw = Raw(rule, actual);
        if (raw.IsFailure)
        {
            return raw;
        }

        var value = Math.Max(0m, raw.Value);
        if (rule.MaxAchievementPct is { } cap)
        {
            value = Math.Min(value, cap);
        }

        value = Math.Min(value, SafetyCeilingPct);
        return Result.Success(Round2(value));
    }

    /// <summary>Contribution to the overall score, from the already rounded achievement so shown figures reconcile.</summary>
    public static decimal WeightedScore(decimal achievementPct, decimal weight) =>
        Round2(achievementPct * weight / 100m);

    /// <summary>The weights of a scorecard: each above 0 and up to 100 with at most two decimals, adding up to exactly 100.</summary>
    public static Result ValidateWeights(IReadOnlyCollection<decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (weights.Count == 0)
        {
            return WeightError("A scorecard needs at least one KPI.");
        }

        foreach (var weight in weights)
        {
            if (weight is <= 0m or > 100m)
            {
                return WeightError("Each KPI weight must be above 0 and at most 100%.");
            }

            if (Math.Round(weight, 2) != weight)
            {
                return WeightError("Each KPI weight can have at most 2 decimals.");
            }
        }

        var total = weights.Sum();
        return total == WeightTotal
            ? Result.Success()
            : WeightError(string.Create(
                CultureInfo.InvariantCulture,
                $"The KPI weights must add up to 100%. They add up to {total:0.00}%."));
    }

    /// <summary>Scores a whole scorecard: every KPI needs a result, and the weights must add up to 100.</summary>
    public static Result<ScorecardScore> Score(IReadOnlyList<KpiScoreInput> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var weights = ValidateWeights(items.Select(item => item.Weight).ToList());
        if (weights.IsFailure)
        {
            return Result.Failure<ScorecardScore>(weights.Error);
        }

        var missing = new Dictionary<string, string[]>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Actual is null)
            {
                missing[$"items[{i}].actualValue"] = ["The actual result is missing."];
            }
        }

        if (missing.Count > 0)
        {
            return Result.Failure<ScorecardScore>(
                AppError.Validation("Every KPI needs an actual result before it can be scored.", missing));
        }

        var scores = new List<KpiItemScore>(items.Count);
        var overall = 0m;
        for (var i = 0; i < items.Count; i++)
        {
            var achievement = Achievement(items[i].Rule, items[i].Actual!.Value);
            if (achievement.IsFailure)
            {
                return Result.Failure<ScorecardScore>(Prefix(achievement.Error, i));
            }

            var weighted = WeightedScore(achievement.Value, items[i].Weight);
            scores.Add(new KpiItemScore(achievement.Value, weighted));
            overall += weighted;
        }

        return Result.Success(new ScorecardScore(scores, Round2(overall)));
    }

    /// <summary>The label of the band with the highest minimum score that the score reaches, or null if it reaches none.</summary>
    public static string? RatingLabel(decimal overallScore, IEnumerable<(decimal MinScore, string Label)> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        string? label = null;
        decimal? best = null;
        foreach (var (minScore, bandLabel) in bands)
        {
            if (minScore <= overallScore && (best is null || minScore > best))
            {
                best = minScore;
                label = bandLabel;
            }
        }

        return label;
    }

    private static Result<decimal> Raw(KpiRule rule, decimal actual)
    {
        switch (rule.ScoringMethod)
        {
            case KpiScoringMethod.Boolean:
                return actual is 0m or 1m
                    ? Result.Success(actual * 100m)
                    : ActualError("A yes/no result must be 0 or 1.");

            case KpiScoringMethod.RatingBased:
                var maxRating = rule.MaxRating!.Value;
                return actual < 0m || actual > maxRating
                    ? ActualError($"The rating must be between 0 and {maxRating.ToString("0.####", CultureInfo.InvariantCulture)}.")
                    : Result.Success(Percent(actual, maxRating));

            case KpiScoringMethod.RangeBased:
                var min = rule.RangeMin!.Value;
                var max = rule.RangeMax!.Value;
                if (actual >= min && actual <= max)
                {
                    return Result.Success(100m);
                }

                if (actual < min)
                {
                    return Result.Success(actual <= 0m ? 0m : Percent(actual, min));
                }

                return Result.Success(Percent(max, actual));

            default:
                return ByDirection(rule, actual);
        }
    }

    private static Result<decimal> ByDirection(KpiRule rule, decimal actual)
    {
        var target = rule.Target;
        switch (rule.Direction)
        {
            case KpiDirection.HigherIsBetter:
                return Result.Success(actual <= 0m ? 0m : Percent(actual, target));

            case KpiDirection.LowerIsBetter:
                if (actual < 0m)
                {
                    return ActualError("The actual result cannot be negative when lower is better.");
                }

                if (target == 0m)
                {
                    return Result.Success(actual == 0m ? 100m : 0m);
                }

                // A zero result is perfect: it scores the cap, which validation requires for this direction.
                return Result.Success(actual == 0m ? rule.MaxAchievementPct!.Value : Percent(target, actual));

            case KpiDirection.ExactTarget:
                return Result.Success(100m - Percent(Math.Abs(actual - target), target));

            default:
                // Range Based direction is always paired with the Range Based method, which validation guarantees.
                return Result.Failure<decimal>(AppError.Validation(
                    "The KPI rule is not valid.",
                    "scoringMethod",
                    "Range Based scoring and the Range Based direction go together."));
        }
    }

    /// <summary>
    /// numerator / denominator × 100 for a positive denominator, clamped to the safety ceiling. It compares before it
    /// divides, so a huge numerator over a tiny denominator cannot overflow.
    /// </summary>
    private static decimal Percent(decimal numerator, decimal denominator)
    {
        const decimal ceilingRatio = SafetyCeilingPct / 100m;
        if (denominator <= decimal.MaxValue / ceilingRatio && numerator >= denominator * ceilingRatio)
        {
            return SafetyCeilingPct;
        }

        return numerator / denominator * 100m;
    }

    private static Result<decimal> ActualError(string message) =>
        Result.Failure<decimal>(AppError.Validation(message, "actualValue", message));

    private static Result WeightError(string message) =>
        Result.Failure(AppError.Validation(message, "weights", message));

    private static AppError Prefix(AppError error, int index)
    {
        var fields = error.FieldErrors?.ToDictionary(pair => $"items[{index}].{pair.Key}", pair => pair.Value);
        return AppError.Validation(error.Message, fields);
    }
}
