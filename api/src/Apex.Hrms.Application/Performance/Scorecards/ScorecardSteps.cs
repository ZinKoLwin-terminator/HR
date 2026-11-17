using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>Who a step of the scorecard workflow belongs to.</summary>
internal enum StepRole
{
    /// <summary>Only the scorecard's own employee (results, submit).</summary>
    Owner,

    /// <summary>The employee's reviewer or HR (start review, review draft, approve).</summary>
    Reviewer,

    /// <summary>Return and reject: the reviewer or HR in the manager stage, HR only (and not the manager-stage approver) once Approved.</summary>
    ReviewerOrHrByStatus,

    /// <summary>HR only, and not the person who approved it (the final review).</summary>
    Hr,
}

/// <summary>
/// What every submission and review step shares. A step loads the scorecard inside the caller's scope (outside it the scorecard is a
/// 404), decides who may act, checks the row version the client sent and tracks the scorecard so that the save is guarded by it.
/// All the changes of a step (the scorecard, its items, the history entry and the activity line) are written by one save, and the
/// scorecard is always marked modified, so its row version is the one concurrency token of the whole scorecard: of two concurrent
/// actions on the same version only one succeeds, the other gets a 409.
/// </summary>
internal sealed class ScorecardSteps(
    IRepository<Scorecard> scorecards,
    IRepository<ScorecardItem> items,
    IRepository<ReviewAction> history,
    IRepository<RatingBand> bands,
    IAccessScope<Scorecard> scope,
    PlanLoader loader,
    ScorecardReader reader,
    SelfService self,
    ICurrentUser user,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork)
{
    public const string NotOwnerOfResults = "You can only enter results on your own scorecard.";

    public const string NotOwnerOfSubmission = "You can only submit your own scorecard.";

    public const string OwnScorecard = "You cannot review your own scorecard.";

    public const string NotReviewer = "Only the employee's reviewer or HR can review this scorecard.";

    public const string NotHr = "Only HR can give the final review of a scorecard.";

    public const string SameApprover = "The person who approved this scorecard cannot also give the HR final review.";

    public ReviewActor Actor => new(user.UserId, user.EmployeeId, user.DisplayName);

    public DateOnly Today => clock.Today;

    public DateTime UtcNow => clock.UtcNow.UtcDateTime;

    /// <summary>True for the HR tier of the module: <c>kpi:Approve</c> together with <c>employees:Edit</c> (the built-in administrator has both).</summary>
    public bool IsHr => self.TierOf("kpi") == AccessTier.All;

    /// <summary>
    /// Loads the scorecard for a step: the row version must be present and valid (400), the scorecard visible to the caller (404),
    /// the caller allowed to take the step (403, or 409 for a reviewer's own scorecard and for the same approver) and the row
    /// version current (409). The repository checks the row version again in the database when saving.
    /// </summary>
    /// <param name="notFound">
    /// The 404 when the scorecard is missing or outside the scope. Routes whose id is not the scorecard's (an evidence item or
    /// attachment) pass their own resource's message, so the answer never reveals the scorecard id behind it.
    /// </param>
    public async Task<Result<Scorecard>> OpenAsync(
        int id,
        string? rowVersion,
        StepRole role,
        string? ownerMessage,
        CancellationToken cancellationToken,
        AppError? notFound = null)
    {
        var expected = RowVersionToken.Decode(rowVersion);
        if (expected.IsFailure)
        {
            return Result.Failure<Scorecard>(expected.Error);
        }

        var scorecard = await scorecards.FindAsync(id, scope.PredicateFor(user), cancellationToken);
        if (scorecard is null)
        {
            return Result.Failure<Scorecard>(notFound ?? NotFoundError.For("Scorecard", id));
        }

        var allowed = await AuthorizeAsync(scorecard, role, ownerMessage, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<Scorecard>(allowed.Error);
        }

        if (!scorecard.RowVersion.AsSpan().SequenceEqual(expected.Value))
        {
            return Result.Failure<Scorecard>(RowVersionToken.Stale);
        }

        scorecards.ExpectRowVersion(scorecard, expected.Value);
        return Result.Success(scorecard);
    }

    private async Task<Result> AuthorizeAsync(Scorecard scorecard, StepRole role, string? ownerMessage, CancellationToken cancellationToken)
    {
        var own = user.EmployeeId;
        if (role == StepRole.Owner)
        {
            return own is { } employee && employee == scorecard.EmployeeId
                ? Result.Success()
                : Result.Failure(AppError.Forbidden(ownerMessage ?? NotOwnerOfResults));
        }

        if (own is { } caller && caller == scorecard.EmployeeId)
        {
            return Result.Failure(AppError.Conflict(OwnScorecard));
        }

        var hrStage = role == StepRole.Hr || (role == StepRole.ReviewerOrHrByStatus && scorecard.Status == ScorecardStatus.Approved);
        if (hrStage)
        {
            if (!IsHr)
            {
                return Result.Failure(AppError.Forbidden(NotHr));
            }

            return scorecard.Status == ScorecardStatus.Approved && await ApprovedByCallerAsync(scorecard.Id, cancellationToken)
                ? Result.Failure(AppError.Conflict(SameApprover))
                : Result.Success();
        }

        var isReviewer = own is { } reviewer && scorecard.ReviewerEmployeeId == reviewer;
        return isReviewer || IsHr
            ? Result.Success()
            : Result.Failure(AppError.Forbidden(NotReviewer));
    }

    /// <summary>True when the latest manager-stage approval of the scorecard was recorded by the signed-in account.</summary>
    private async Task<bool> ApprovedByCallerAsync(int scorecardId, CancellationToken cancellationToken)
    {
        var approvals = await history.ListAsync(
            action => action.ScorecardId == scorecardId && action.Step == ReviewStep.Manager && action.Action == ReviewActionType.Approve,
            cancellationToken);
        var latest = approvals.OrderByDescending(action => action.Id).FirstOrDefault();
        return latest?.ActorUserId is { } approver && approver == user.UserId;
    }

    public Task<EvaluationPeriod> PeriodOfAsync(Scorecard scorecard, CancellationToken cancellationToken) =>
        loader.PeriodOfAsync(scorecard, cancellationToken);

    /// <summary>The items of the scorecard, tracked, in the order the screens show them (sort order, then id). A score failure names them by this position.</summary>
    public async Task<List<ScorecardItem>> ItemsOfAsync(int scorecardId, CancellationToken cancellationToken) =>
        [.. (await items.ListForChangeAsync(item => item.ScorecardId == scorecardId, cancellationToken))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)];

    /// <summary>
    /// Scores the whole scorecard from the stored results, and the label of the band the score reaches (only active bands count).
    /// Every KPI needs a result (400 on <c>items[i].actualValue</c> for each missing one).
    /// </summary>
    public async Task<Result<(ScorecardScore Score, string? Label)>> ScoreAsync(IReadOnlyList<ScorecardItem> ordered, CancellationToken cancellationToken)
    {
        var score = KpiCalculator.Score([.. ordered.Select(item => new KpiScoreInput(item.Rule, item.Weight, item.ActualValue))]);
        if (score.IsFailure)
        {
            return Result.Failure<(ScorecardScore, string?)>(score.Error);
        }

        var active = await bands.ListAsync(band => band.Status == RecordStatus.Active, cancellationToken);
        var label = KpiCalculator.RatingLabel(score.Value.OverallScore, active.Select(band => (band.MinScore, band.Label)));
        return Result.Success((score.Value, label));
    }

    public static void ApplyScores(IReadOnlyList<ScorecardItem> ordered, ScorecardScore score)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].ApplyScore(score.Items[index]);
        }
    }

    /// <summary>
    /// Finishes a step: appends the history entry, records the activity line (employee code and period name only, never a comment or a
    /// result), saves everything at once and answers with the scorecard as it is now.
    /// </summary>
    public async Task<Result<ScorecardDetailDto>> SaveAsync(
        Scorecard scorecard,
        EvaluationPeriod period,
        ReviewAction? entry,
        Func<string, string, string> activityText,
        CancellationToken cancellationToken)
    {
        var saved = await CommitAsync(scorecard, period, entry, activityText, cancellationToken);
        return saved.IsFailure
            ? Result.Failure<ScorecardDetailDto>(saved.Error)
            : await reader.DetailAsync(reader.Unscoped(), scorecard.Id, cancellationToken);
    }

    /// <summary>The save of <see cref="SaveAsync"/> without the answer: marks the scorecard modified, appends the entry, records the activity line and saves.</summary>
    public async Task<Result> CommitAsync(
        Scorecard scorecard,
        EvaluationPeriod period,
        ReviewAction? entry,
        Func<string, string, string> activityText,
        CancellationToken cancellationToken)
    {
        scorecards.MarkModified(scorecard);
        if (entry is not null)
        {
            history.Add(entry);
        }

        var (code, periodName) = await loader.LabelAsync(scorecard, period, cancellationToken);
        activity.Record(ScorecardActivity.Category, activityText(code, periodName));
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The row version the scorecard has in the database now (after a save).</summary>
    public async Task<byte[]> RowVersionOfAsync(int scorecardId, CancellationToken cancellationToken) =>
        await reader.RowVersionAsync(scorecardId, cancellationToken)
            ?? throw new InvalidOperationException($"Scorecard #{scorecardId} disappeared while it was being changed.");

    public const string NotOwnerOfEvidence = "You can only add or remove evidence on your own scorecard.";

    /// <summary>
    /// Opens the scorecard for adding or removing evidence: only its own employee (404 outside the scope, with <paramref name="notFound"/>,
    /// the message of the route's own resource; 403 for someone else), a current row version (409), the scorecard in Draft or Returned and
    /// its period Active, or Completed for a Returned scorecard (409).
    /// </summary>
    public async Task<Result<(Scorecard Scorecard, EvaluationPeriod Period)>> OpenForEvidenceAsync(
        int scorecardId,
        string? rowVersion,
        AppError notFound,
        CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(scorecardId, rowVersion, StepRole.Owner, NotOwnerOfEvidence, cancellationToken, notFound);
        if (opened.IsFailure)
        {
            return Result.Failure<(Scorecard, EvaluationPeriod)>(opened.Error);
        }

        var accepts = await AcceptsEvidenceAsync(opened.Value, cancellationToken);
        return accepts.IsFailure
            ? Result.Failure<(Scorecard, EvaluationPeriod)>(accepts.Error)
            : Result.Success((opened.Value, accepts.Value));
    }

    /// <summary>
    /// The checks of <see cref="OpenForEvidenceAsync"/> that need no row version (scope 404 with <paramref name="notFound"/>, owner 403,
    /// status and period 409), read-only: the upload route runs them before it reads the file, so a refused upload never buffers a body.
    /// The upload itself runs every check again.
    /// </summary>
    public async Task<Result> PrecheckEvidenceAsync(int scorecardId, AppError notFound, CancellationToken cancellationToken)
    {
        var scorecard = await reader.FirstVisibleAsync(scorecardId, cancellationToken);
        if (scorecard is null)
        {
            return Result.Failure(notFound);
        }

        if (user.EmployeeId is not { } own || own != scorecard.EmployeeId)
        {
            return Result.Failure(AppError.Forbidden(NotOwnerOfEvidence));
        }

        var accepts = await AcceptsEvidenceAsync(scorecard, cancellationToken);
        return accepts.IsFailure ? Result.Failure(accepts.Error) : Result.Success();
    }

    private async Task<Result<EvaluationPeriod>> AcceptsEvidenceAsync(Scorecard scorecard, CancellationToken cancellationToken)
    {
        var editable = scorecard.EnsureEvidenceEditable();
        if (editable.IsFailure)
        {
            return Result.Failure<EvaluationPeriod>(editable.Error);
        }

        var period = await PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAcceptsEvidence(scorecard.Status);
        return open.IsFailure ? Result.Failure<EvaluationPeriod>(open.Error) : Result.Success(period);
    }

    /// <summary>
    /// Checks the item ids of a request against the scorecard: every id must belong to it and appear once (400 on <c>items[i].id</c>).
    /// Returns the matching items in request order, or the violations collected so far (the caller may add more first).
    /// </summary>
    public static Result<List<ScorecardItem>> Match(IReadOnlyList<int> ids, IReadOnlyList<ScorecardItem> ordered, Violations violations)
    {
        var byId = ordered.ToDictionary(item => item.Id);
        var seen = new HashSet<int>();
        var matched = new List<ScorecardItem>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            if (!seen.Add(ids[index]))
            {
                violations.Add($"items[{index}].id", "A KPI can appear only once.", "A KPI appears more than once in the request.");
            }
            else if (byId.TryGetValue(ids[index], out var item))
            {
                matched.Add(item);
            }
            else
            {
                violations.Add($"items[{index}].id", "This KPI is not on the scorecard.", "A KPI in the request is not on this scorecard.");
            }
        }

        return violations.Any ? Result.Failure<List<ScorecardItem>>(violations.ToError()) : Result.Success(matched);
    }

    /// <summary>Carries the field errors of item <paramref name="index"/> into the violations as <c>items[index].field</c>.</summary>
    public static void AddItemErrors(Violations violations, int index, AppError error)
    {
        foreach (var (field, messages) in error.FieldErrors ?? new Dictionary<string, string[]>())
        {
            foreach (var message in messages)
            {
                violations.Add($"items[{index}].{field}", message, error.Message);
            }
        }
    }
}
