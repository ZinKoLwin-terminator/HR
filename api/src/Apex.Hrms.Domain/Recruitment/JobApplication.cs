using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Recruitment;

/// <summary>
/// A candidate's application for one opening (rec.Application; named JobApplication because "Application" is a layer
/// of this solution). It moves along the pipeline Applied, Screening, Interview, then Selected or Rejected; the
/// transition table is <see cref="CanMove"/>. A rejection needs a reason and the other stages carry none. The opening and
/// the candidate never change after creation. Rules that need other records (the opening's status, vacancies,
/// interviews) are checked by the Application layer.
/// </summary>
public sealed class JobApplication : AuditedEntity
{
    public const int ReasonMaxLength = 200;
    public const int OverrideReasonMaxLength = 300;

    private static readonly Dictionary<ApplicationStage, ApplicationStage[]> Transitions =
        new()
        {
            [ApplicationStage.Applied] = [ApplicationStage.Screening, ApplicationStage.Rejected],
            [ApplicationStage.Screening] = [ApplicationStage.Interview, ApplicationStage.Rejected],
            [ApplicationStage.Interview] = [ApplicationStage.Selected, ApplicationStage.Rejected],
            [ApplicationStage.Selected] = [ApplicationStage.Rejected],
            [ApplicationStage.Rejected] = [],
        };

    private JobApplication()
    {
    }

    public int JobOpeningId { get; private set; }

    public int CandidateId { get; private set; }

    public ApplicationStage Stage { get; private set; }

    public DateOnly AppliedOn { get; private set; }

    /// <summary>Set when the stage is Rejected, null otherwise.</summary>
    public string? RejectionReason { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Set when an HR Administrator approved the hire despite a do-not-rehire match (never shown to recruiters).</summary>
    public string? BlacklistOverrideReason { get; private set; }

    public int? BlacklistOverrideByUserId { get; private set; }

    /// <summary>The stages an application at <paramref name="from"/> may move to.</summary>
    public static IReadOnlyList<ApplicationStage> NextStages(ApplicationStage from) => Transitions[from];

    public static bool CanMove(ApplicationStage from, ApplicationStage to) => Transitions[from].Contains(to);

    /// <summary>Starts a new application at Applied, applied today.</summary>
    public static JobApplication Create(int jobOpeningId, int candidateId, string? notes, DateOnly today) => new()
    {
        JobOpeningId = jobOpeningId,
        CandidateId = candidateId,
        Stage = ApplicationStage.Applied,
        AppliedOn = today,
        Notes = TextNormalization.CleanOptional(notes),
    };

    /// <summary>Changes the notes; nothing else of an application can be edited.</summary>
    public void UpdateNotes(string? notes) => Notes = TextNormalization.CleanOptional(notes);

    /// <summary>The transition rule alone, so callers can check it before the rules that read other records.</summary>
    public Result EnsureCanMoveTo(ApplicationStage stage) =>
        CanMove(Stage, stage)
            ? Result.Success()
            : Result.Failure(AppError.Conflict(
                $"An application at \"{ApplicationStageText.ToText(Stage)}\" cannot move to \"{ApplicationStageText.ToText(stage)}\"."));

    /// <summary>Moves to <paramref name="stage"/>. Rejecting needs a non-blank reason; any other stage clears it.</summary>
    public Result MoveTo(ApplicationStage stage, string? reason)
    {
        var allowed = EnsureCanMoveTo(stage);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var cleaned = TextNormalization.CleanOptional(reason);
        if (stage == ApplicationStage.Rejected && cleaned is null)
        {
            return Result.Failure(AppError.Validation(
                "Enter a reason for rejecting this application.",
                "reason",
                "A reason is required."));
        }

        Stage = stage;
        RejectionReason = stage == ApplicationStage.Rejected ? cleaned : null;
        return Result.Success();
    }

    /// <summary>Keeps who approved the hire despite a do-not-rehire match, and why.</summary>
    public void RecordBlacklistOverride(string reason, int userId)
    {
        BlacklistOverrideReason = reason.Trim();
        BlacklistOverrideByUserId = userId;
    }

    /// <summary>A selected application cannot be deleted: reject it first.</summary>
    public Result EnsureCanDelete() =>
        Stage == ApplicationStage.Selected
            ? Result.Failure(AppError.Conflict("A selected application cannot be deleted. Reject it first."))
            : Result.Success();

    /// <summary>Scheduling an interview moves a screened application to the Interview stage.</summary>
    public bool AdvanceToInterview()
    {
        if (Stage != ApplicationStage.Screening)
        {
            return false;
        }

        Stage = ApplicationStage.Interview;
        return true;
    }
}
