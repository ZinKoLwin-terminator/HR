using Apex.Hrms.Domain.Performance;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// Shape rules of the results a screen sends: the row version, comments of at most 1000 characters, each item with an id and an
/// actual value of at most 4 decimals. Whether an item belongs to the scorecard, appears once and the value suits the scoring rule
/// are rules that need the scorecard.
/// </summary>
internal sealed class SaveScorecardResultsRequestValidator : RequestValidator<SaveScorecardResultsRequest>
{
    public const int MaxItems = 50;

    public SaveScorecardResultsRequestValidator()
    {
        RuleForText(x => x.RowVersion).RequiredRowVersion();
        RuleForText(x => x.EmployeeComment, "Comment").OptionalText(Scorecard.CommentMaxLength);
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Send the KPI results in items.")
            .Must(items => items!.Count <= MaxItems).WithMessage($"At most {MaxItems} KPI results can be sent at once.");
        RuleForEach(x => x.Items)
            .NotNull().WithMessage("Each result needs a KPI id.")
            .SetValidator(new ResultItemRequestValidator());
    }
}

internal sealed class ResultItemRequestValidator : RequestValidator<ResultItemRequest>
{
    public ResultItemRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Choose a KPI.");
        RuleFor(x => x.ActualValue)
            .PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage("Use at most 4 decimal places.");
        RuleForText(x => x.EmployeeComment, "Comment").OptionalText(Scorecard.CommentMaxLength);
    }
}

/// <summary>Shape rules of the review draft: ratings from 1 to 5 with at most 2 decimals and comments of at most 1000 characters.</summary>
internal sealed class SaveScorecardReviewRequestValidator : RequestValidator<SaveScorecardReviewRequest>
{
    public SaveScorecardReviewRequestValidator()
    {
        RuleForText(x => x.RowVersion).RequiredRowVersion();
        RuleFor(x => x.ManagerRating).Rating();
        RuleForText(x => x.ManagerComment, "Comment").OptionalText(Scorecard.CommentMaxLength);
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Send the KPI ratings in items.")
            .Must(items => items!.Count <= SaveScorecardResultsRequestValidator.MaxItems)
            .WithMessage($"At most {SaveScorecardResultsRequestValidator.MaxItems} KPI ratings can be sent at once.");
        RuleForEach(x => x.Items)
            .NotNull().WithMessage("Each rating needs a KPI id.")
            .SetValidator(new ReviewItemRequestValidator());
    }
}

internal sealed class ReviewItemRequestValidator : RequestValidator<ReviewItemRequest>
{
    public ReviewItemRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Choose a KPI.");
        RuleFor(x => x.ManagerRating).Rating();
        RuleForText(x => x.ManagerComment, "Comment").OptionalText(Scorecard.CommentMaxLength);
    }
}

internal static class ScorecardRuleExtensions
{
    public static IRuleBuilderOptions<T, decimal?> Rating<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.Must(rating => rating is null or (>= Scorecard.MinRating and <= Scorecard.MaxRating))
            .WithMessage($"The rating must be between {Scorecard.MinRating:0} and {Scorecard.MaxRating:0}.")
            .PrecisionScale(4, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
}

internal sealed class ApproveScorecardCommandValidator : RequestValidator<ApproveScorecardCommand>
{
    public ApproveScorecardCommandValidator() =>
        RuleForText(x => x.Comment, "Comment").OptionalText(Scorecard.CommentMaxLength);
}

internal sealed class FinalizeScorecardCommandValidator : RequestValidator<FinalizeScorecardCommand>
{
    public FinalizeScorecardCommandValidator() =>
        RuleForText(x => x.Comment, "Comment").OptionalText(Scorecard.CommentMaxLength);
}

/// <summary>Returning a scorecard needs a reason (as the database requires): a blank comment is a 400 on <c>comment</c>.</summary>
internal sealed class ReturnScorecardCommandValidator : RequestValidator<ReturnScorecardCommand>
{
    public ReturnScorecardCommandValidator() =>
        RuleForText(x => x.Comment, "Comment").RequiredReason();
}

internal sealed class RejectScorecardCommandValidator : RequestValidator<RejectScorecardCommand>
{
    public RejectScorecardCommandValidator() =>
        RuleForText(x => x.Comment, "Comment").RequiredReason();
}

internal static class ReasonRuleExtensions
{
    public static IRuleBuilderOptions<T, string?> RequiredReason<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(comment => !string.IsNullOrWhiteSpace(comment)).WithMessage("A reason is required.")
            .OptionalText(Scorecard.CommentMaxLength);
}
