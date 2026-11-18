using System.Globalization;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>What the submitter writes in a draft. The submitter link itself is <see cref="GrievanceCaseSubmitter"/>.</summary>
public sealed record GrievanceDraftDetails(
    int CategoryId,
    string Subject,
    string Description,
    DateOnly? IncidentDate,
    string? Location,
    string? InvolvedPersonText,
    int? InvolvedDepartmentId,
    bool IsAnonymous)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public GrievanceDraftDetails Normalized() => this with
    {
        Subject = TextNormalization.Clean(Subject ?? string.Empty),
        Description = TextNormalization.Clean(Description ?? string.Empty),
        Location = TextNormalization.CleanOptional(Location),
        InvolvedPersonText = TextNormalization.CleanOptional(InvolvedPersonText),
    };
}

/// <summary>A named submitter's organization placement when the case is submitted (company > branch > department > position).</summary>
public sealed record GrievancePlacement(int CompanyId, int BranchId, int DepartmentId, int PositionId);

/// <summary>
/// One employee grievance (grv.Case). A <see cref="ConfidentialEntity"/>: the row has times and a row version but no user ids,
/// because the submitter writes it and no row may name them. The table is not system-versioned, so draft revisions leave no trace.
/// The submitter is not here (see
/// <see cref="GrievanceCaseSubmitter"/>); an anonymous case keeps no placement. A draft has no case number; submitting gives it one.
/// Every status change is a method of <c>GrievanceCaseWorkflow.cs</c>.
/// </summary>
public sealed partial class GrievanceCase : ConfidentialEntity
{
    public const int SubjectMinLength = 5;
    public const int SubjectMaxLength = 200;
    public const int DescriptionMinLength = 20;
    public const int DescriptionMaxLength = 4000;
    public const int ShortTextMaxLength = 200;
    public const int ReasonMaxLength = GrievanceStatusHistoryEntry.ReasonMaxLength;
    public const int ResolutionSummaryMaxLength = GrievanceResolutionProposal.SummaryMaxLength;
    public const string CaseNoPrefix = "GRV-";

    private GrievanceCase()
    {
    }

    public string? CaseNo { get; private set; }

    public int CategoryId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DateOnly? IncidentDate { get; private set; }

    public string? Location { get; private set; }

    public string? InvolvedPersonText { get; private set; }

    public int? InvolvedDepartmentId { get; private set; }

    /// <summary>The employee the grievance is about, set by HR at triage; excluded from every case-team view of it.</summary>
    public int? SubjectEmployeeId { get; private set; }

    public bool IsAnonymous { get; private set; }

    public GrievanceStatus Status { get; private set; }

    public GrievancePriority? Priority { get; private set; }

    public DateOnly? TargetResolutionDate { get; private set; }

    /// <summary>While <see cref="GrievanceStatus.AwaitingEmployeeResponse"/>: the status the submitter's response returns the case to.</summary>
    public GrievanceStatus? ReturnStatus { get; private set; }

    /// <summary>Set once the case is escalated to the reviewers; kept for reporting.</summary>
    public bool IsEscalated { get; private set; }

    public int? CompanyId { get; private set; }

    public int? BranchId { get; private set; }

    public int? DepartmentId { get; private set; }

    public int? PositionId { get; private set; }

    public DateTime? DeclarationAcceptedAtUtc { get; private set; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public DateTime? AcknowledgedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public string? ResolutionSummary { get; private set; }

    public bool ResolutionVisibleToSubmitter { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public DateOnly? RetentionUntil { get; private set; }

    public bool LegalHold { get; private set; }

    /// <summary>The last case-team change the submitter may see (their unread badge; see <see cref="GrievanceCaseSubmitter.HasUnseenUpdate"/>).</summary>
    public DateTime? LastSubmitterUpdateAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    /// <summary>The number of a submitted case: <c>GRV-yyyy-nnnnn</c> (at least five digits; more once the sequence passes 99999).</summary>
    public static string FormatCaseNo(int year, long number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return string.Create(CultureInfo.InvariantCulture, $"{CaseNoPrefix}{year:D4}-{number:D5}");
    }

    /// <summary>True for <c>GRV-yyyy-nnnnn</c> with at least five digits (as CK_Case_CaseNo), at most 20 characters.</summary>
    public static bool IsValidCaseNo(string? caseNo) =>
        caseNo is { Length: >= 14 and <= 20 }
        && caseNo.StartsWith(CaseNoPrefix, StringComparison.Ordinal)
        && caseNo[4..8].All(char.IsAsciiDigit)
        && caseNo[8] == '-'
        && caseNo[9..].All(char.IsAsciiDigit);

    /// <summary>
    /// Checks a draft without changing anything. A draft may be unfinished: it needs a subject (at most 200 characters) and its
    /// description may be empty (at most 4000); the short texts are at most 200, the incident date is not in the future, the category
    /// is active, and anonymity only where the category allows it. Submitting also needs the minimum lengths (subject 5,
    /// description 20), as CK_Case_Subject and CK_Case_Description do for a submitted case.
    /// </summary>
    public static Result ValidateDraft(GrievanceDraftDetails details, GrievanceCategory category, DateOnly today) =>
        DraftViolations(details, category, today, forSubmission: false).ToResult();

    private static Violations DraftViolations(GrievanceDraftDetails details, GrievanceCategory category, DateOnly today, bool forSubmission)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(category);
        if (details.CategoryId != category.Id)
        {
            throw new ArgumentException("The category must be the one the details choose.", nameof(category));
        }

        var normalized = details.Normalized();
        var subjectMin = forSubmission ? SubjectMinLength : 1;
        var descriptionMin = forSubmission ? DescriptionMinLength : 0;
        return new Violations()
            .AddIf(!category.IsActive, "categoryId", "Choose an active category.")
            .AddIf(
                normalized.IsAnonymous && !category.AllowsAnonymous,
                "isAnonymous",
                "This category cannot be raised anonymously.",
                "Grievances of this category cannot be raised anonymously.")
            .AddIf(normalized.Subject.Length == 0, "subject", "Enter a subject.")
            .AddIf(
                normalized.Subject.Length > 0 && (normalized.Subject.Length < subjectMin || normalized.Subject.Length > SubjectMaxLength),
                "subject",
                $"The subject must be {SubjectMinLength} to {SubjectMaxLength} characters.")
            .AddIf(
                normalized.Description.Length < descriptionMin || normalized.Description.Length > DescriptionMaxLength,
                "description",
                forSubmission
                    ? $"The description must be {DescriptionMinLength} to {DescriptionMaxLength} characters."
                    : $"The description must be at most {DescriptionMaxLength} characters.")
            .AddIf(normalized.IncidentDate > today, "incidentDate", "The incident date cannot be in the future.")
            .AddIf(normalized.Location is { Length: > ShortTextMaxLength }, "location", $"The location must be at most {ShortTextMaxLength} characters.")
            .AddIf(
                normalized.InvolvedPersonText is { Length: > ShortTextMaxLength },
                "involvedPersonText",
                $"The person involved must be at most {ShortTextMaxLength} characters.");
    }

    /// <summary>The draft as it is stored, for the checks at submission.</summary>
    private GrievanceDraftDetails CurrentDetails() =>
        new(CategoryId, Subject, Description, IncidentDate, Location, InvolvedPersonText, InvolvedDepartmentId, IsAnonymous);

    /// <summary>A new draft of the signed-in employee. The caller adds its <see cref="GrievanceCaseSubmitter"/> (named or anonymous).</summary>
    public static Result<GrievanceCase> CreateDraft(GrievanceDraftDetails details, GrievanceCategory category, DateOnly today)
    {
        var valid = ValidateDraft(details, category, today);
        if (valid.IsFailure)
        {
            return Result.Failure<GrievanceCase>(valid.Error);
        }

        var draft = new GrievanceCase { Status = GrievanceStatus.Draft };
        draft.Apply(details.Normalized());
        return Result.Success(draft);
    }

    /// <summary>
    /// Changes a draft (409 once it is submitted). When <see cref="IsAnonymous"/> changes, the caller replaces the submitter link
    /// (employee id or pseudonym).
    /// </summary>
    public Result UpdateDraft(GrievanceDraftDetails details, GrievanceCategory category, DateOnly today)
    {
        var draft = EnsureDraft();
        if (draft.IsFailure)
        {
            return draft;
        }

        var valid = ValidateDraft(details, category, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        Apply(details.Normalized());
        return Result.Success();
    }

    /// <summary>Only a draft can be changed or deleted by its submitter (409 otherwise).</summary>
    public Result EnsureDraft() => Status == GrievanceStatus.Draft
        ? Result.Success()
        : Refuse("only a draft can be changed or deleted");

    /// <summary>Open and past its target date (worked out when queried, never stored).</summary>
    public bool IsOverdue(DateOnly today) => IsActive(Status) && TargetResolutionDate is { } target && target < today;

    private void Apply(GrievanceDraftDetails normalized)
    {
        CategoryId = normalized.CategoryId;
        Subject = normalized.Subject;
        Description = normalized.Description;
        IncidentDate = normalized.IncidentDate;
        Location = normalized.Location;
        InvolvedPersonText = normalized.InvolvedPersonText;
        InvolvedDepartmentId = normalized.InvolvedDepartmentId;
        IsAnonymous = normalized.IsAnonymous;
    }
}
