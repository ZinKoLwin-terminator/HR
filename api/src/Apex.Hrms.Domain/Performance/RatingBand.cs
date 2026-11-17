using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>The editable fields of a rating band. Status changes through Activate and Deactivate.</summary>
public sealed record RatingBandDetails(string Label, decimal MinScore, string? Description)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public RatingBandDetails Normalized() => this with
    {
        Label = TextNormalization.Clean(Label),
        Description = TextNormalization.CleanOptional(Description),
    };
}

/// <summary>
/// A band of the overall score (kpi.RatingBand): a score of at least <see cref="MinScore"/> gets the label, up to the
/// next band minimum. It has no code, so it is not a <see cref="CodedRecord"/>.
/// </summary>
public sealed class RatingBand : AuditedEntity
{
    public const decimal MaxMinScore = 500m;

    private RatingBand()
    {
    }

    public string Label { get; private set; } = string.Empty;

    public decimal MinScore { get; private set; }

    public string? Description { get; private set; }

    public RecordStatus Status { get; private set; }

    public bool IsActive => Status == RecordStatus.Active;

    public static Result<RatingBand> Create(RatingBandDetails details, RecordStatus status)
    {
        var band = new RatingBand();
        var result = band.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<RatingBand>(result.Error);
        }

        band.Status = status;
        return Result.Success(band);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(RatingBandDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("label", "Label", normalized.Label)
            .AddIf(
                normalized.MinScore is < 0m or > MaxMinScore,
                "minScore",
                $"Must be between 0 and {MaxMinScore:0}.",
                $"The minimum score must be between 0 and {MaxMinScore:0}.")
            .ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(RatingBandDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        Label = normalized.Label;
        MinScore = normalized.MinScore;
        Description = normalized.Description;
        return Result.Success();
    }

    /// <summary>Makes the band active. Idempotent.</summary>
    public Result Activate()
    {
        Status = RecordStatus.Active;
        return Result.Success();
    }

    /// <summary>Makes the band inactive. Idempotent.</summary>
    public Result Deactivate()
    {
        Status = RecordStatus.Inactive;
        return Result.Success();
    }
}
