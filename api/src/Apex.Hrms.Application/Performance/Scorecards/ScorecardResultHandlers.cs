using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The employee saves a draft of the results (<c>PUT /api/kpi/scorecards/{id}/results</c>): only the scorecard's own employee, while
/// it is Draft or Returned and its period is Active (a Returned scorecard also while the period is Completed). Each result is checked against the scoring rule of its KPI and gets its
/// achievement and weighted score; a null result clears them. Nothing is saved unless every item is valid. The scorecard gets no
/// score until it is submitted.
/// </summary>
internal sealed class SaveScorecardResultsHandler(
    IValidator<SaveScorecardResultsRequest> validator,
    ScorecardSteps steps) : IUpdateHandler<SaveScorecardResultsRequest, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(int id, SaveScorecardResultsRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(id, request.RowVersion, StepRole.Owner, ScorecardSteps.NotOwnerOfResults, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var editable = scorecard.EnsureResultsEditable();
        if (editable.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(editable.Error);
        }

        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAcceptsResults(scorecard.Status);
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var ordered = await steps.ItemsOfAsync(id, cancellationToken);
        var requested = request.Items!;
        var matched = ScorecardSteps.Match([.. requested.Select(item => item.Id)], ordered, new Violations());
        if (matched.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(matched.Error);
        }

        var violations = new Violations();
        for (var index = 0; index < requested.Count; index++)
        {
            var checkedResult = matched.Value[index].CheckResult(requested[index].ActualValue, requested[index].EmployeeComment);
            if (checkedResult.IsFailure)
            {
                ScorecardSteps.AddItemErrors(violations, index, checkedResult.Error);
            }
        }

        if (violations.Any)
        {
            return Result.Failure<ScorecardDetailDto>(violations.ToError());
        }

        var saved = scorecard.SaveResults(request.EmployeeComment);
        if (saved.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(saved.Error);
        }

        for (var index = 0; index < requested.Count; index++)
        {
            matched.Value[index].RecordResult(requested[index].ActualValue, requested[index].EmployeeComment);
        }

        return await steps.SaveAsync(scorecard, period, entry: null, ScorecardActivity.ResultsSaved, cancellationToken);
    }
}

/// <summary>
/// The employee submits the scorecard (<c>POST /api/kpi/scorecards/{id}/submit</c>): Draft to Submitted, Returned to Resubmitted.
/// The period must be Active, or Completed for a Returned scorecard. Every KPI needs a result. The API scores the scorecard from the stored results (one authority, <c>KpiCalculator</c>) and stores
/// the per-KPI scores, the overall score, its rating label (over the active bands) and the calculation version. A submission after the
/// period's submission due date is allowed: the due date only informs (reports can flag late ones from <c>submittedOn</c>).
/// </summary>
internal sealed class SubmitScorecardHandler(ScorecardSteps steps) : ICommandHandler<SubmitScorecardCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(SubmitScorecardCommand command, CancellationToken cancellationToken)
    {
        var opened = await steps.OpenAsync(command.Id, command.RowVersion, StepRole.Owner, ScorecardSteps.NotOwnerOfSubmission, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var submittable = scorecard.EnsureCanSubmit();
        if (submittable.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(submittable.Error);
        }

        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAcceptsResults(scorecard.Status);
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var ordered = await steps.ItemsOfAsync(command.Id, cancellationToken);
        var scored = await steps.ScoreAsync(ordered, cancellationToken);
        if (scored.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(scored.Error);
        }

        var entry = scorecard.Submit(steps.Today, scored.Value.Score, scored.Value.Label, steps.Actor, steps.UtcNow);
        if (entry.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(entry.Error);
        }

        ScorecardSteps.ApplyScores(ordered, scored.Value.Score);
        return await steps.SaveAsync(scorecard, period, entry.Value, ScorecardActivity.Submitted, cancellationToken);
    }
}
