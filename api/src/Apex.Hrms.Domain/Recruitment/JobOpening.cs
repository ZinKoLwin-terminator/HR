using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Recruitment;

/// <summary>
/// The fields of a job opening the user controls. The code and the status are not among them: the code is made by the
/// server and the status moves through commands. <see cref="PostedOn"/> is optional: a new opening is posted today
/// unless a date is given, and an update without it keeps the date.
/// </summary>
public sealed record JobOpeningDetails(
    string Title,
    int DepartmentId,
    int HiringManagerId,
    RecruitmentEmploymentType EmploymentType,
    int Vacancies,
    DateOnly? PostedOn,
    DateOnly ClosingDate,
    string? Description)
{
    /// <summary>Trims text and turns a blank description into null. Idempotent.</summary>
    public JobOpeningDetails Normalized() => this with
    {
        Title = TextNormalization.Clean(Title),
        Description = TextNormalization.CleanOptional(Description),
    };
}

/// <summary>
/// A position the company is hiring for (rec.JobOpening). It starts as a Draft and moves through commands:
/// Draft or On Hold to Open, Open to On Hold, anything that is not Filled to Closed, Closed to Open. Opening needs a
/// closing date of today or later and a vacancy left. Filled is never set by hand: it follows the number of selected
/// candidates (see <see cref="SyncFilled"/>). A closed or filled opening cannot be edited.
/// Rules that need other records (department, hiring manager, selected count) are checked by the Application layer.
/// </summary>
public sealed class JobOpening : AuditedEntity
{
    public const int MinVacancies = 1;
    public const int MaxVacancies = 50;

    private static readonly Dictionary<JobOpeningStatus, JobOpeningStatus[]> Transitions =
        new()
        {
            [JobOpeningStatus.Draft] = [JobOpeningStatus.Open, JobOpeningStatus.Closed],
            [JobOpeningStatus.Open] = [JobOpeningStatus.OnHold, JobOpeningStatus.Closed],
            [JobOpeningStatus.OnHold] = [JobOpeningStatus.Open, JobOpeningStatus.Closed],
            [JobOpeningStatus.Closed] = [JobOpeningStatus.Open],
            [JobOpeningStatus.Filled] = [],
        };

    private JobOpening()
    {
    }

    /// <summary>JOB-yyyy-nnn, made by the server from a database sequence.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public int DepartmentId { get; private set; }

    public int HiringManagerEmployeeId { get; private set; }

    public RecruitmentEmploymentType EmploymentType { get; private set; }

    public int Vacancies { get; private set; }

    public DateOnly PostedOn { get; private set; }

    public DateOnly ClosingDate { get; private set; }

    public string? Description { get; private set; }

    public JobOpeningStatus Status { get; private set; }

    /// <summary>Whether applications may be made: only an Open opening accepts them.</summary>
    public bool AcceptsApplications => Status == JobOpeningStatus.Open;

    /// <summary>Whether applications may move forward (Open or On Hold). Rejecting is always possible.</summary>
    public bool AllowsProgress => Status is JobOpeningStatus.Open or JobOpeningStatus.OnHold;

    /// <summary>"Code Title", how the opening is called in messages.</summary>
    public string Label => $"{Code} {Title}";

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(JobOpeningDetails details, DateOnly today, JobOpening? existing = null)
    {
        var normalized = details.Normalized();
        var posted = normalized.PostedOn ?? existing?.PostedOn ?? today;
        return new Violations()
            .Require("title", "Title", normalized.Title)
            .AddIf(
                normalized.Vacancies is < MinVacancies or > MaxVacancies,
                "vacancies",
                "Enter 1 to 50.",
                "Vacancies must be between 1 and 50.")
            .AddIf(
                normalized.ClosingDate < posted,
                "closingDate",
                "Must be on or after the posting date.",
                "The closing date cannot be before the posting date.")
            .ToResult();
    }

    /// <summary>The status rule of editing: a closed or filled opening cannot be edited.</summary>
    public Result EnsureCanEdit() =>
        Status is JobOpeningStatus.Closed or JobOpeningStatus.Filled
            ? Result.Failure(AppError.Conflict(
                $"A {JobOpeningStatusText.ToText(Status).ToLowerInvariant()} job opening cannot be edited."))
            : Result.Success();

    /// <summary>Starts a new opening as a Draft.</summary>
    public static Result<JobOpening> Create(JobOpeningDetails details, string code, DateOnly today)
    {
        var valid = Validate(details, today);
        if (valid.IsFailure)
        {
            return Result.Failure<JobOpening>(valid.Error);
        }

        var opening = new JobOpening { Code = code, Status = JobOpeningStatus.Draft };
        opening.Apply(details.Normalized(), today);
        return Result.Success(opening);
    }

    /// <summary>
    /// Changes the details of an opening that is not closed or filled. The vacancies cannot drop below the number of
    /// candidates already selected. The status and the code are left untouched.
    /// </summary>
    public Result Update(JobOpeningDetails details, DateOnly today, int selectedCount)
    {
        var editable = EnsureCanEdit();
        if (editable.IsFailure)
        {
            return editable;
        }

        var valid = Validate(details, today, this);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        if (normalized.Vacancies < selectedCount)
        {
            return Result.Failure(AppError.Validation(
                $"{selectedCount} candidate(s) are already selected for this job.",
                "vacancies",
                $"At least {selectedCount} (already selected)."));
        }

        Apply(normalized, today);
        return Result.Success();
    }

    /// <summary>
    /// Moves the opening to <paramref name="target"/> if the transition table allows it. Becoming Open also needs a
    /// closing date of today or later (a past one must be edited first) and a vacancy that is not yet taken.
    /// Filled cannot be requested: it is set by <see cref="SyncFilled"/>.
    /// </summary>
    public Result ChangeStatus(JobOpeningStatus target, DateOnly today, int selectedCount)
    {
        if (!Transitions[Status].Contains(target))
        {
            return Result.Failure(AppError.Conflict(
                $"A {JobOpeningStatusText.ToText(Status).ToLowerInvariant()} job opening cannot be set to {JobOpeningStatusText.ToText(target)}."));
        }

        if (target == JobOpeningStatus.Open)
        {
            if (ClosingDate < today)
            {
                return Result.Failure(AppError.Conflict("The closing date has passed. Edit the opening before reopening it."));
            }

            if (selectedCount >= Vacancies)
            {
                return Result.Failure(AppError.Conflict("All vacancies already have a selected candidate."));
            }
        }

        Status = target;
        return Result.Success();
    }

    /// <summary>
    /// Marks the opening Filled once enough candidates are selected (from Open or On Hold) and Open again when a
    /// selection drops out. Returns what changed: Filled, Open or null.
    /// </summary>
    public JobOpeningStatus? SyncFilled(int selectedCount)
    {
        var full = selectedCount >= Vacancies;
        JobOpeningStatus? next = null;
        if (full && Status is JobOpeningStatus.Open or JobOpeningStatus.OnHold)
        {
            next = JobOpeningStatus.Filled;
        }
        else if (!full && Status == JobOpeningStatus.Filled)
        {
            next = JobOpeningStatus.Open;
        }

        if (next is { } status)
        {
            Status = status;
        }

        return next;
    }

    private void Apply(JobOpeningDetails normalized, DateOnly today)
    {
        Title = normalized.Title;
        DepartmentId = normalized.DepartmentId;
        HiringManagerEmployeeId = normalized.HiringManagerId;
        EmploymentType = normalized.EmploymentType;
        Vacancies = normalized.Vacancies;
        PostedOn = normalized.PostedOn ?? (PostedOn == default ? today : PostedOn);
        ClosingDate = normalized.ClosingDate;
        Description = normalized.Description;
    }
}
