using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>
/// One employee's KPI plan and results for one evaluation period (kpi.Scorecard). The organization placement (company,
/// branch, department, position) is copied when the scorecard is created, so reports keep the department the employee
/// was in during the period. A scorecard starts in Planning (HR assigns the KPIs); finalizing the plan moves it to Draft. The
/// later steps (submitting, reviewing, scoring) come with the review use cases.
/// </summary>
public sealed partial class Scorecard : AuditedEntity
{
    /// <summary>The KPI weights of a scorecard never add up to more than this many percent, and must add up to exactly this when the plan is finalized.</summary>
    public const decimal MaxTotalWeight = KpiCalculator.WeightTotal;

    private Scorecard()
    {
    }

    public int EmployeeId { get; private set; }

    public int PeriodId { get; private set; }

    public int CompanyId { get; private set; }

    public int BranchId { get; private set; }

    public int DepartmentId { get; private set; }

    public int PositionId { get; private set; }

    /// <summary>The employee's manager when the plan was finalized: the only manager who may review it.</summary>
    public int? ReviewerEmployeeId { get; private set; }

    public ScorecardStatus Status { get; private set; }

    public string? EmployeeComment { get; private set; }

    /// <summary>1 to 5.</summary>
    public decimal? ManagerRating { get; private set; }

    public string? ManagerComment { get; private set; }

    public string? HrComment { get; private set; }

    /// <summary>The official overall score, calculated by the API only.</summary>
    public decimal? OverallScore { get; private set; }

    public string? RatingLabel { get; private set; }

    /// <summary>The <see cref="KpiCalculator.CalculationVersion"/> the score was calculated with.</summary>
    public short? CalculationVersion { get; private set; }

    public DateOnly? PlanFinalizedOn { get; private set; }

    public DateOnly? SubmittedOn { get; private set; }

    public DateOnly? ReviewedOn { get; private set; }

    public DateOnly? FinalizedOn { get; private set; }

    public int? FinalizedByUserId { get; private set; }

    /// <summary>A new scorecard in Planning. An employee never reviews their own scorecard (as CK_Scorecard_NotOwnReviewer).</summary>
    public static Result<Scorecard> Create(
        int employeeId,
        int periodId,
        int companyId,
        int branchId,
        int departmentId,
        int positionId,
        int? reviewerEmployeeId)
    {
        var violations = new Violations()
            .AddIf(employeeId <= 0, "employeeId", "Choose an employee.")
            .AddIf(periodId <= 0, "periodId", "Choose an evaluation period.")
            .AddIf(reviewerEmployeeId == employeeId, "reviewerEmployeeId", "An employee cannot review their own scorecard.");
        if (violations.Any)
        {
            return Result.Failure<Scorecard>(violations.ToError());
        }

        return Result.Success(new Scorecard
        {
            EmployeeId = employeeId,
            PeriodId = periodId,
            CompanyId = companyId,
            BranchId = branchId,
            DepartmentId = departmentId,
            PositionId = positionId,
            ReviewerEmployeeId = reviewerEmployeeId,
            Status = ScorecardStatus.Planning,
        });
    }

    /// <summary>KPIs can only be added, changed or removed, and the scorecard deleted, while it is in Planning (409 otherwise).</summary>
    public Result EnsurePlanning() => Status == ScorecardStatus.Planning
        ? Result.Success()
        : Result.Failure(AppError.Conflict(
            $"The scorecard is {ScorecardStatusText.ToText(Status)}; its KPIs can only be changed while it is in Planning."));

    /// <summary>
    /// The weights on a scorecard never add up to more than 100% while it is planned (they must reach exactly 100% only when the
    /// plan is finalized). A Validation failure on <paramref name="field"/>.
    /// </summary>
    public static Result EnsureWeightTotal(decimal total, string field) => total <= MaxTotalWeight
        ? Result.Success()
        : Result.Failure(AppError.Validation(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"The KPI weights on the scorecard would add up to {total:0.00}%; they cannot exceed {MaxTotalWeight:0}%."),
            field,
            $"The weights cannot add up to more than {MaxTotalWeight:0}%."));

    /// <summary>
    /// Finalizes the plan: Planning to Draft. The plan needs at least one KPI and weights that add up to exactly 100% (as
    /// <see cref="KpiCalculator.ValidateWeights"/>). The reviewer becomes the employee's manager at this moment (null when the
    /// employee has none: then only HR-level reviewers can review it). An employee never reviews their own scorecard (409, as
    /// CK_Scorecard_NotOwnReviewer). Returns the history entry to append.
    /// </summary>
    public Result<ReviewAction> FinalizePlan(
        DateOnly today,
        int? managerEmployeeId,
        IReadOnlyCollection<decimal> weights,
        ReviewActor actor,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(actor);

        var planning = EnsurePlanning();
        if (planning.IsFailure)
        {
            return Result.Failure<ReviewAction>(planning.Error);
        }

        var total = KpiCalculator.ValidateWeights(weights);
        if (total.IsFailure)
        {
            return Result.Failure<ReviewAction>(total.Error);
        }

        if (managerEmployeeId == EmployeeId)
        {
            return Result.Failure<ReviewAction>(AppError.Conflict(
                "The employee is their own manager, so nobody can review this scorecard. Correct the manager on the employee record first."));
        }

        var entry = ReviewAction.Create(
            Id,
            ReviewStep.HR,
            ReviewActionType.FinalizePlan,
            Status,
            ScorecardStatus.Draft,
            actor.UserId,
            actor.EmployeeId,
            actor.Name,
            comment: null,
            overallScore: null,
            occurredAtUtc);
        if (entry.IsFailure)
        {
            return entry;
        }

        ReviewerEmployeeId = managerEmployeeId;
        PlanFinalizedOn = today;
        Status = ScorecardStatus.Draft;
        return entry;
    }
}

/// <summary>Who performed a step of the review history: the account, the employee linked to it (none for the built-in administrator) and the display name.</summary>
public sealed record ReviewActor(int? UserId, int? EmployeeId, string Name);

/// <summary>
/// What an assigned KPI copies from its template (and when): the snapshot that later template edits never change.
/// </summary>
public sealed record ScorecardItemDetails(
    int ScorecardId,
    int TemplateId,
    int CategoryId,
    string KpiCode,
    string KpiName,
    KpiMeasurementType MeasurementType,
    string? Unit,
    KpiDirection Direction,
    KpiScoringMethod ScoringMethod,
    decimal Target,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal Weight,
    decimal? MaxAchievementPct,
    DateOnly StartDate,
    DateOnly EndDate,
    short SortOrder,
    int? AssignedByUserId,
    DateOnly AssignedOn)
{
    /// <summary>The scoring rule the item carries.</summary>
    public KpiRule Rule => new(MeasurementType, Direction, ScoringMethod, Target, RangeMin, RangeMax, MaxRating, MaxAchievementPct);

    /// <summary>
    /// The snapshot of a template: its category, code, name, measurement, direction, scoring method, range, rating scale and cap are
    /// copied; the target and weight are the ones given, or the template's defaults when null.
    /// </summary>
    public static ScorecardItemDetails FromTemplate(
        KpiTemplate template,
        int scorecardId,
        decimal? target,
        decimal? weight,
        DateOnly startDate,
        DateOnly endDate,
        short sortOrder,
        int? assignedByUserId,
        DateOnly assignedOn)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new ScorecardItemDetails(
            scorecardId,
            template.Id,
            template.CategoryId,
            template.Code,
            template.Name,
            template.MeasurementType,
            template.Unit,
            template.Direction,
            template.ScoringMethod,
            target ?? template.DefaultTarget,
            template.RangeMin,
            template.RangeMax,
            template.MaxRating,
            weight ?? template.DefaultWeight,
            template.MaxAchievementPct,
            startDate,
            endDate,
            sortOrder,
            assignedByUserId,
            assignedOn);
    }
}

/// <summary>
/// One assigned KPI of a scorecard with the employee's result (kpi.ScorecardItem). The template's code, name, target,
/// weight and scoring rules are copied into it, so editing the template never changes it.
/// </summary>
public sealed class ScorecardItem : AuditedEntity
{
    private ScorecardItem()
    {
    }

    public int ScorecardId { get; private set; }

    public int TemplateId { get; private set; }

    public int CategoryId { get; private set; }

    public string KpiCode { get; private set; } = string.Empty;

    public string KpiName { get; private set; } = string.Empty;

    public KpiMeasurementType MeasurementType { get; private set; }

    public string? Unit { get; private set; }

    public KpiDirection Direction { get; private set; }

    public KpiScoringMethod ScoringMethod { get; private set; }

    public decimal Target { get; private set; }

    public decimal? RangeMin { get; private set; }

    public decimal? RangeMax { get; private set; }

    public decimal? MaxRating { get; private set; }

    public decimal Weight { get; private set; }

    public decimal? MaxAchievementPct { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public short SortOrder { get; private set; }

    public int? AssignedByUserId { get; private set; }

    public DateOnly AssignedOn { get; private set; }

    /// <summary>The employee's result.</summary>
    public decimal? ActualValue { get; private set; }

    public string? EmployeeComment { get; private set; }

    /// <summary>Calculated by the API when results are saved.</summary>
    public decimal? AchievementPct { get; private set; }

    public decimal? WeightedScore { get; private set; }

    /// <summary>The manager's rating of this KPI, 1 to 5.</summary>
    public decimal? ManagerRating { get; private set; }

    public string? ManagerComment { get; private set; }

    /// <summary>The scoring rule of this item.</summary>
    public KpiRule Rule => new(MeasurementType, Direction, ScoringMethod, Target, RangeMin, RangeMax, MaxRating, MaxAchievementPct);

    /// <summary>A new item without a result. The scoring rule must be consistent (<see cref="KpiRule.Validate"/>), as the database checks.</summary>
    public static Result<ScorecardItem> Create(ScorecardItemDetails details)
    {
        var rule = details.Rule.Validate();
        if (rule.IsFailure)
        {
            return Result.Failure<ScorecardItem>(rule.Error);
        }

        var violations = new Violations()
            .Require("kpiName", "KPI name", details.KpiName)
            .Require("kpiCode", "KPI code", details.KpiCode)
            .AddIf(details.Weight is <= 0m or > KpiTemplate.MaxWeight, "weight", $"The weight must be greater than 0 and at most {KpiTemplate.MaxWeight:0}.")
            .AddIf(details.EndDate < details.StartDate, "endDate", "Must be on or after the start date.");
        if (violations.Any)
        {
            return Result.Failure<ScorecardItem>(violations.ToError());
        }

        return Result.Success(new ScorecardItem
        {
            ScorecardId = details.ScorecardId,
            TemplateId = details.TemplateId,
            CategoryId = details.CategoryId,
            KpiCode = details.KpiCode.Trim(),
            KpiName = details.KpiName.Trim(),
            MeasurementType = details.MeasurementType,
            Unit = string.IsNullOrWhiteSpace(details.Unit) ? null : details.Unit.Trim(),
            Direction = details.Direction,
            ScoringMethod = details.ScoringMethod,
            Target = details.Target,
            RangeMin = details.RangeMin,
            RangeMax = details.RangeMax,
            MaxRating = details.MaxRating,
            Weight = details.Weight,
            MaxAchievementPct = details.MaxAchievementPct,
            StartDate = details.StartDate,
            EndDate = details.EndDate,
            SortOrder = details.SortOrder,
            AssignedByUserId = details.AssignedByUserId,
            AssignedOn = details.AssignedOn,
        });
    }

    /// <summary>
    /// Changes what HR plans for this KPI: the target, weight, dates and position. The snapshot of the template (code, name,
    /// scoring rule) never changes, and the rule must stay consistent with the new target (for example a Higher Is Better target
    /// cannot become 0). The caller checks that the scorecard is still in Planning and that the weights stay within 100%.
    /// </summary>
    public Result UpdatePlan(decimal target, decimal weight, DateOnly startDate, DateOnly endDate, short sortOrder)
    {
        var rule = (Rule with { Target = target }).Validate();
        if (rule.IsFailure)
        {
            return rule;
        }

        var violations = new Violations()
            .AddIf(weight is <= 0m or > KpiTemplate.MaxWeight, "weight", $"The weight must be greater than 0 and at most {KpiTemplate.MaxWeight:0}.")
            .AddIf(endDate < startDate, "endDate", "Must be on or after the start date.");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        Target = target;
        Weight = weight;
        StartDate = startDate;
        EndDate = endDate;
        SortOrder = sortOrder;
        return Result.Success();
    }

    /// <summary>
    /// Checks an employee's result without changing anything: the comment fits, and an actual result is valid for the scoring
    /// rule (a Boolean is 0 or 1, a rating lies within its scale, lower is better is not negative). A refusal is a Validation
    /// failure on <c>actualValue</c> or <c>employeeComment</c>. A null result is always valid (it clears the result).
    /// </summary>
    public Result CheckResult(decimal? actual, string? comment)
    {
        var violations = new Violations()
            .AddIf(
                TextNormalization.CleanOptional(comment) is { Length: > Scorecard.CommentMaxLength },
                "employeeComment",
                $"The comment must be at most {Scorecard.CommentMaxLength} characters.");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        return actual is { } value ? KpiCalculator.Achievement(Rule, value) : Result.Success();
    }

    /// <summary>
    /// Stores the employee's result: with an actual value it calculates the achievement and the weighted score
    /// (<see cref="KpiCalculator"/>); with none it clears the result and both figures. Changes nothing when the check fails.
    /// </summary>
    public Result RecordResult(decimal? actual, string? comment)
    {
        var checkedResult = CheckResult(actual, comment);
        if (checkedResult.IsFailure)
        {
            return checkedResult;
        }

        ActualValue = actual;
        EmployeeComment = TextNormalization.CleanOptional(comment);
        if (actual is { } value)
        {
            var achievement = KpiCalculator.Achievement(Rule, value).Value;
            AchievementPct = achievement;
            WeightedScore = KpiCalculator.WeightedScore(achievement, Weight);
        }
        else
        {
            AchievementPct = null;
            WeightedScore = null;
        }

        return Result.Success();
    }

    /// <summary>Stores the score the calculator worked out for this KPI from its stored result.</summary>
    public void ApplyScore(KpiItemScore score)
    {
        ArgumentNullException.ThrowIfNull(score);
        AchievementPct = score.AchievementPct;
        WeightedScore = score.WeightedScore;
    }

    /// <summary>Checks a manager's rating (1 to 5, at most 2 decimals) and comment without changing anything.</summary>
    public static Result CheckReview(decimal? rating, string? comment, string ratingField = "managerRating", string commentField = "managerComment") =>
        new Violations()
            .AddIf(
                rating is { } value && (value < Scorecard.MinRating || value > Scorecard.MaxRating || Math.Round(value, 2) != value),
                ratingField,
                $"The rating must be between {Scorecard.MinRating:0} and {Scorecard.MaxRating:0} with at most 2 decimals.")
            .AddIf(
                TextNormalization.CleanOptional(comment) is { Length: > Scorecard.CommentMaxLength },
                commentField,
                $"The comment must be at most {Scorecard.CommentMaxLength} characters.")
            .ToResult();

    /// <summary>Stores the manager's rating (1 to 5) and comment for this KPI; null clears them.</summary>
    public Result RecordReview(decimal? rating, string? comment)
    {
        var checkedReview = CheckReview(rating, comment);
        if (checkedReview.IsFailure)
        {
            return checkedReview;
        }

        ManagerRating = rating;
        ManagerComment = TextNormalization.CleanOptional(comment);
        return Result.Success();
    }
}

/// <summary>
/// One step of the approval history of a scorecard (kpi.ReviewAction). Append-only: the database user has no UPDATE or
/// DELETE on the table, so the review trail cannot be rewritten. It has no audit columns and no row version.
/// </summary>
public sealed class ReviewAction : Entity
{
    public const int ActorNameMaxLength = 130;
    public const int CommentMaxLength = 1000;

    private ReviewAction()
    {
    }

    public int ScorecardId { get; private set; }

    public ReviewStep Step { get; private set; }

    public ReviewActionType Action { get; private set; }

    public ScorecardStatus FromStatus { get; private set; }

    public ScorecardStatus ToStatus { get; private set; }

    public int? ActorUserId { get; private set; }

    /// <summary>Null when the built-in administrator (linked to no employee) acted.</summary>
    public int? ActorEmployeeId { get; private set; }

    /// <summary>Display name of the actor at the time, kept even if the user is later removed.</summary>
    public string ActorName { get; private set; } = string.Empty;

    public string? Comment { get; private set; }

    /// <summary>The overall score at the time of the action, when one was calculated.</summary>
    public decimal? OverallScore { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>A history entry. Rejecting or returning needs a reason (as CK_ReviewAction_Reason).</summary>
    public static Result<ReviewAction> Create(
        int scorecardId,
        ReviewStep step,
        ReviewActionType action,
        ScorecardStatus fromStatus,
        ScorecardStatus toStatus,
        int? actorUserId,
        int? actorEmployeeId,
        string actorName,
        string? comment,
        decimal? overallScore,
        DateTime occurredAtUtc)
    {
        var cleanComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        var violations = new Violations()
            .Require("actorName", "Actor name", actorName)
            .AddIf(
                action is ReviewActionType.Reject or ReviewActionType.Return && cleanComment is null,
                "comment",
                "A reason is required.",
                "Enter a reason for rejecting or returning a scorecard.")
            .AddIf(cleanComment is { Length: > CommentMaxLength }, "comment", $"The comment must be at most {CommentMaxLength} characters.");
        if (violations.Any)
        {
            return Result.Failure<ReviewAction>(violations.ToError());
        }

        return Result.Success(new ReviewAction
        {
            ScorecardId = scorecardId,
            Step = step,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            ActorEmployeeId = actorEmployeeId,
            ActorName = actorName.Trim(),
            Comment = cleanComment,
            OverallScore = overallScore,
            OccurredAtUtc = occurredAtUtc,
        });
    }
}

/// <summary>
/// Evidence for a KPI result (kpi.Attachment). Metadata only: the file lives in the API's file storage under the
/// <see cref="StorageKey"/>, a server-generated key (32 lower-case hexadecimal characters), never a user-supplied path or name.
/// </summary>
public sealed class KpiAttachment : AuditedEntity
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public const int StorageKeyLength = 32;
    public const int FileNameMaxLength = 260;
    public const int ContentTypeMaxLength = 100;

    private KpiAttachment()
    {
    }

    public int ScorecardItemId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public DateOnly UploadedOn { get; private set; }

    /// <summary>True when the key is exactly 32 characters of 0-9 and a-f (as CK_Attachment_StorageKey).</summary>
    public static bool IsValidStorageKey(string? key) =>
        key is { Length: StorageKeyLength } && key.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    public static Result<KpiAttachment> Create(
        int scorecardItemId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        DateOnly uploadedOn)
    {
        var violations = new Violations()
            .Require("fileName", "File name", fileName)
            .AddIf(fileName.Trim().Length > FileNameMaxLength, "fileName", $"File name must be at most {FileNameMaxLength} characters.")
            .Require("contentType", "Content type", contentType)
            .AddIf(contentType.Trim().Length > ContentTypeMaxLength, "contentType", $"Content type must be at most {ContentTypeMaxLength} characters.")
            .AddIf(sizeBytes is <= 0 or > MaxSizeBytes, "sizeBytes", "A file must be larger than 0 bytes and at most 10 MB.")
            .AddIf(!IsValidStorageKey(storageKey), "storageKey", "The storage key must be 32 lower-case hexadecimal characters.");
        if (violations.Any)
        {
            return Result.Failure<KpiAttachment>(violations.ToError());
        }

        return Result.Success(new KpiAttachment
        {
            ScorecardItemId = scorecardItemId,
            FileName = fileName.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedOn = uploadedOn,
        });
    }
}
