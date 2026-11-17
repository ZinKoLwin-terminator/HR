using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>
/// How one KPI is scored. <see cref="Validate"/> enforces the same consistency as the database CHECK constraints, so
/// a rule that gets this far can be calculated without surprises.
/// </summary>
/// <param name="Target">The goal. Zero is allowed only where a zero goal makes sense (lower is better).</param>
/// <param name="RangeMin">Lower end of the acceptable range; set only for Range Based.</param>
/// <param name="RangeMax">Upper end of the acceptable range; set only for Range Based.</param>
/// <param name="MaxRating">Top of the rating scale; set only for Rating Based.</param>
/// <param name="MaxAchievementPct">Cap on the achievement, 1 to 500. Required for Capped Percentage and for Lower Is Better, null for Direct Percentage.</param>
public sealed record KpiRule(
    KpiMeasurementType MeasurementType,
    KpiDirection Direction,
    KpiScoringMethod ScoringMethod,
    decimal Target,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal? MaxAchievementPct)
{
    public const decimal MaxAchievementLimit = 500m;

    public Result Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if ((ScoringMethod == KpiScoringMethod.RatingBased) != (MeasurementType == KpiMeasurementType.Rating))
        {
            errors["scoringMethod"] = ["Rating Based scoring and the Rating measurement type go together."];
        }
        else if ((ScoringMethod == KpiScoringMethod.Boolean) != (MeasurementType == KpiMeasurementType.Boolean))
        {
            errors["scoringMethod"] = ["Boolean scoring and the Boolean measurement type go together."];
        }
        else if ((ScoringMethod == KpiScoringMethod.RangeBased) != (Direction == KpiDirection.RangeBased))
        {
            errors["scoringMethod"] = ["Range Based scoring and the Range Based direction go together."];
        }

        if (ScoringMethod == KpiScoringMethod.RatingBased)
        {
            if (MaxRating is not > 0m)
            {
                errors["maxRating"] = ["The top of the rating scale must be greater than 0."];
            }
        }
        else if (MaxRating is not null)
        {
            errors["maxRating"] = ["The top of the rating scale is only for Rating Based scoring."];
        }

        if (ScoringMethod == KpiScoringMethod.RangeBased)
        {
            if (RangeMin is not > 0m)
            {
                errors["rangeMin"] = ["The range minimum must be greater than 0."];
            }

            if (RangeMax is null || (RangeMin is not null && RangeMax <= RangeMin))
            {
                errors["rangeMax"] = ["The range maximum must be greater than the range minimum."];
            }
        }
        else
        {
            if (RangeMin is not null)
            {
                errors["rangeMin"] = ["The range minimum is only for Range Based scoring."];
            }

            if (RangeMax is not null)
            {
                errors["rangeMax"] = ["The range maximum is only for Range Based scoring."];
            }
        }

        if (ScoringMethod == KpiScoringMethod.CappedPercentage && MaxAchievementPct is null)
        {
            errors["maxAchievementPct"] = ["Capped Percentage needs a maximum achievement."];
        }
        else if (Direction == KpiDirection.LowerIsBetter && MaxAchievementPct is null)
        {
            // Target / actual grows without limit as the actual nears 0, and an actual of 0 cannot be divided by:
            // the cap is what a perfect (zero) result scores, so the score never drops as the result improves.
            errors["maxAchievementPct"] = ["Lower is better needs a maximum achievement (use Capped Percentage): a result of 0 scores it."];
        }
        else if (ScoringMethod == KpiScoringMethod.DirectPercentage && MaxAchievementPct is not null)
        {
            errors["maxAchievementPct"] = ["Direct Percentage has no maximum achievement."];
        }
        else if (MaxAchievementPct is < 1m or > MaxAchievementLimit)
        {
            errors["maxAchievementPct"] = [$"The maximum achievement must be between 1 and {MaxAchievementLimit:0}."];
        }

        if (Target < 0m)
        {
            errors["target"] = ["The target cannot be negative."];
        }
        else if (Target == 0m && Direction is KpiDirection.HigherIsBetter or KpiDirection.ExactTarget)
        {
            errors["target"] = ["The target must be greater than 0 for this direction."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(AppError.Validation("The KPI rule is not valid.", errors));
    }
}
