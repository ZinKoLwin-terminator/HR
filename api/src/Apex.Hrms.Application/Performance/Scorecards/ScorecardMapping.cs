using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>The read-model row of a scorecard list or detail: what the join of scorecard, employee, period and department returns.</summary>
internal sealed class ScorecardRow
{
    public int Id { get; init; }

    public int EmployeeId { get; init; }

    public string EmployeeCode { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public int PeriodId { get; init; }

    public string PeriodName { get; init; } = string.Empty;

    public DateOnly PeriodStartDate { get; init; }

    public int DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public ScorecardStatus Status { get; init; }

    public int ItemCount { get; init; }

    public decimal TotalWeight { get; init; }

    public decimal? OverallScore { get; init; }

    public string? RatingLabel { get; init; }

    public int? ReviewerEmployeeId { get; init; }

    public DateOnly? PlanFinalizedOn { get; init; }

    public DateOnly? SubmittedOn { get; init; }

    public DateOnly? FinalizedOn { get; init; }

    public DateOnly? SubmissionDueDate { get; init; }

    public DateTime ModifiedAtUtc { get; init; }

    public byte[] RowVersion { get; init; } = [];
}

/// <summary>The detail-only columns of a scorecard (comments, review date) and of its period, read in one small query next to the row.</summary>
internal sealed class ScorecardExtras
{
    public DateOnly? ReviewedOn { get; init; }

    public string? EmployeeComment { get; init; }

    public decimal? ManagerRating { get; init; }

    public string? ManagerComment { get; init; }

    public string? HrComment { get; init; }

    public EvaluationPeriodStatus PeriodStatus { get; init; }

    public DateOnly? SubmissionDueDate { get; init; }

    public bool RequiresHrReview { get; init; }
}

internal static class ScorecardMapping
{
    public static string FullName(string firstName, string lastName) => $"{firstName} {lastName}";

    public static ScorecardDto ToDto(ScorecardRow row, IReadOnlyDictionary<int, string> reviewerNames) => new(
        row.Id,
        row.EmployeeId,
        row.EmployeeCode,
        FullName(row.FirstName, row.LastName),
        row.PeriodId,
        row.PeriodName,
        row.DepartmentId,
        row.DepartmentName,
        ScorecardStatusText.ToText(row.Status),
        row.ItemCount,
        row.TotalWeight,
        row.OverallScore,
        row.RatingLabel,
        row.ReviewerEmployeeId,
        row.ReviewerEmployeeId is { } reviewer && reviewerNames.TryGetValue(reviewer, out var name) ? name : null,
        row.PlanFinalizedOn,
        row.SubmittedOn,
        row.FinalizedOn,
        row.SubmissionDueDate,
        row.RowVersion);

    /// <summary>The detail with the review fields <paramref name="visibility"/> allows (the items and history are mapped with the same visibility by the caller).</summary>
    public static ScorecardDetailDto ToDetail(
        ScorecardDto list,
        IReadOnlyList<ScorecardItemDto> items,
        IReadOnlyList<ReviewActionDto> history,
        ScorecardExtras extras,
        ReviewVisibility visibility) => new(
        list.Id,
        list.EmployeeId,
        list.EmployeeCode,
        list.EmployeeName,
        list.PeriodId,
        list.PeriodName,
        list.DepartmentId,
        list.DepartmentName,
        list.Status,
        list.ItemCount,
        list.TotalWeight,
        list.OverallScore,
        list.RatingLabel,
        list.ReviewerEmployeeId,
        list.ReviewerName,
        list.PlanFinalizedOn,
        list.SubmittedOn,
        list.FinalizedOn,
        extras.ReviewedOn,
        EvaluationPeriodStatusText.ToText(extras.PeriodStatus),
        extras.SubmissionDueDate,
        extras.RequiresHrReview,
        extras.EmployeeComment,
        visibility.Rating(extras.ManagerRating),
        visibility.ManagerText(extras.ManagerComment),
        visibility.HrText(extras.HrComment),
        list.RowVersion,
        items,
        history);

    public static AttachmentDto ToDto(KpiAttachment attachment) => new(
        attachment.Id,
        attachment.FileName,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.UploadedOn);

    /// <summary>One KPI with the review fields <paramref name="visibility"/> allows.</summary>
    public static ScorecardItemDto ToItemDto(ScorecardItem item, string categoryName, ReviewVisibility visibility, IReadOnlyList<AttachmentDto>? attachments = null) => new(
        item.Id,
        item.TemplateId,
        item.CategoryId,
        categoryName,
        item.KpiCode,
        item.KpiName,
        KpiMeasurementTypeText.ToText(item.MeasurementType),
        item.Unit,
        KpiDirectionText.ToText(item.Direction),
        KpiScoringMethodText.ToText(item.ScoringMethod),
        item.Target,
        item.RangeMin,
        item.RangeMax,
        item.MaxRating,
        item.Weight,
        item.MaxAchievementPct,
        item.StartDate,
        item.EndDate,
        item.SortOrder,
        item.ActualValue,
        item.EmployeeComment,
        item.AchievementPct,
        item.WeightedScore,
        visibility.Rating(item.ManagerRating),
        visibility.ManagerText(item.ManagerComment),
        item.RowVersion,
        attachments ?? []);

    /// <summary>One history entry; its comment only when <paramref name="visibility"/> allows it for this action.</summary>
    public static ReviewActionDto ToDto(ReviewAction action, ReviewVisibility visibility) => new(
        ReviewStepText.ToText(action.Step),
        ReviewActionTypeText.ToText(action.Action),
        ScorecardStatusText.ToText(action.FromStatus),
        ScorecardStatusText.ToText(action.ToStatus),
        action.ActorUserId,
        action.ActorName,
        visibility.ShowsComment(action.Action) ? action.Comment : null,
        action.OverallScore,
        DateTime.SpecifyKind(action.OccurredAtUtc, DateTimeKind.Utc));
}
