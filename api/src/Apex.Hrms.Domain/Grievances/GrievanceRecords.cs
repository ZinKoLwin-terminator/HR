using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>
/// The case team's working record (grv.InvestigationNote): a note, an interview, a follow-up, a finding or a recommendation.
/// Append-only: a correction is a new note. Never shown to the submitter; reviewers see only findings and recommendations.
/// </summary>
public sealed class GrievanceInvestigationNote : Entity
{
    public const int BodyMaxLength = 4000;
    public const int IntervieweeMaxLength = 200;

    private GrievanceInvestigationNote()
    {
    }

    public int CaseId { get; private set; }

    public InvestigationNoteKind Kind { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateOnly? InterviewDate { get; private set; }

    public string? IntervieweeText { get; private set; }

    public int AuthorUserId { get; private set; }

    public int? AuthorEmployeeId { get; private set; }

    public string AuthorName { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>True for the kinds a reviewer may read (D7: findings and recommendations, not raw notes or interviews).</summary>
    public bool IsVisibleToReviewers => Kind is InvestigationNoteKind.Finding or InvestigationNoteKind.Recommendation;

    /// <summary>
    /// A note. An interview needs its date (not in the future); other kinds have no interview fields. The author must be a signed-in
    /// member of the case team (not the submitter). The case's status is checked by <see cref="GrievanceCase.EnsureInvestigationOpen"/>.
    /// </summary>
    public static Result<GrievanceInvestigationNote> Create(
        int caseId,
        InvestigationNoteKind kind,
        string? body,
        DateOnly? interviewDate,
        string? intervieweeText,
        GrievanceActor author,
        DateOnly today,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(author);
        if (author.UserId is not { } userId || author.IsSubmitter)
        {
            throw new ArgumentException("A note is written by a signed-in member of the case team.", nameof(author));
        }

        var cleanBody = TextNormalization.CleanOptional(body);
        var interviewee = TextNormalization.CleanOptional(intervieweeText);
        var isInterview = kind == InvestigationNoteKind.Interview;
        var violations = new Violations()
            .AddIf(cleanBody is null, "body", "Enter the note.")
            .AddIf(cleanBody is { Length: > BodyMaxLength }, "body", $"The note must be at most {BodyMaxLength} characters.")
            .AddIf(isInterview && interviewDate is null, "interviewDate", "Enter the interview date.")
            .AddIf(isInterview && interviewDate > today, "interviewDate", "The interview date cannot be in the future.")
            .AddIf(!isInterview && (interviewDate is not null || interviewee is not null), "kind", "Only an interview has an interview date and interviewee.")
            .AddIf(interviewee is { Length: > IntervieweeMaxLength }, "intervieweeText", $"The interviewee must be at most {IntervieweeMaxLength} characters.");
        if (violations.Any)
        {
            return Result.Failure<GrievanceInvestigationNote>(violations.ToError());
        }

        return Result.Success(new GrievanceInvestigationNote
        {
            CaseId = caseId,
            Kind = kind,
            Body = cleanBody!,
            InterviewDate = isInterview ? interviewDate : null,
            IntervieweeText = isInterview ? interviewee : null,
            AuthorUserId = userId,
            AuthorEmployeeId = author.EmployeeId,
            AuthorName = author.Name.Trim(),
            CreatedAtUtc = createdAtUtc,
        });
    }
}

/// <summary>
/// A file on a case (grv.Attachment): metadata only, the content is in the grievance file storage under <see cref="StorageKey"/>.
/// Written by the submitter too, so it is a <see cref="ConfidentialEntity"/> (times, no user audit columns). A submitter's upload is
/// always Shared and keeps no user id on an anonymous case; a note's file is never Shared. Removing keeps the row.
/// </summary>
public sealed class GrievanceAttachment : ConfidentialEntity
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    /// <summary>Files the submitter may add to one case (security review L7).</summary>
    public const int MaxSubmitterFilesPerCase = 10;

    /// <summary>Files the case team may add to one case, counted apart from the submitter's.</summary>
    public const int MaxCaseTeamFilesPerCase = 20;
    public const int StorageKeyLength = 32;
    public const int Sha256Length = 32;
    public const int FileNameMaxLength = 260;
    public const int ContentTypeMaxLength = 100;

    private GrievanceAttachment()
    {
    }

    public int CaseId { get; private set; }

    public int? CommunicationId { get; private set; }

    public int? NoteId { get; private set; }

    public GrievanceAttachmentVisibility Visibility { get; private set; }

    public GrievanceActorRole UploadedByRole { get; private set; }

    public int? UploadedByUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public byte[] Sha256 { get; private set; } = [];

    public DateTime UploadedAtUtc { get; private set; }

    public DateTime? RemovedAtUtc { get; private set; }

    public bool IsRemoved => RemovedAtUtc is not null;

    /// <summary>True when the key is exactly 32 characters of 0-9 and a-f (as CK_Attachment_StorageKey).</summary>
    public static bool IsValidStorageKey(string? key) =>
        key is { Length: StorageKeyLength } && key.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    /// A file's metadata. The uploader is the actor (a submitter's user id is never kept, named or anonymous; the
    /// case passes the recorded actor); a submitter's file is forced Shared.
    /// </summary>
    public static Result<GrievanceAttachment> Create(
        int caseId,
        int? communicationId,
        int? noteId,
        GrievanceAttachmentVisibility visibility,
        GrievanceActor uploader,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        byte[] sha256,
        DateTime uploadedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(uploader);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(sha256);

        var effectiveVisibility = uploader.IsSubmitter ? GrievanceAttachmentVisibility.Shared : visibility;
        var violations = new Violations()
            .Require("fileName", "File name", fileName)
            .AddIf(fileName.Trim().Length > FileNameMaxLength, "fileName", $"File name must be at most {FileNameMaxLength} characters.")
            .Require("contentType", "Content type", contentType)
            .AddIf(contentType.Trim().Length > ContentTypeMaxLength, "contentType", $"Content type must be at most {ContentTypeMaxLength} characters.")
            .AddIf(sizeBytes is <= 0 or > MaxSizeBytes, "file", "A file must be larger than 0 bytes and at most 10 MB.")
            .AddIf(!IsValidStorageKey(storageKey), "storageKey", "The storage key must be 32 lower-case hexadecimal characters.")
            .AddIf(sha256.Length != Sha256Length, "sha256", "The checksum must be a SHA-256 hash.")
            .AddIf(communicationId is not null && noteId is not null, "noteId", "A file belongs to a message or a note, not both.")
            .AddIf(noteId is not null && effectiveVisibility == GrievanceAttachmentVisibility.Shared, "visibility", "A file of an investigation note cannot be shared with the submitter.");
        if (violations.Any)
        {
            return Result.Failure<GrievanceAttachment>(violations.ToError());
        }

        return Result.Success(new GrievanceAttachment
        {
            CaseId = caseId,
            CommunicationId = communicationId,
            NoteId = noteId,
            Visibility = effectiveVisibility,
            UploadedByRole = uploader.Role,
            UploadedByUserId = uploader.IsSubmitter ? null : uploader.UserId,
            FileName = fileName.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            Sha256 = [.. sha256],
            UploadedAtUtc = uploadedAtUtc,
        });
    }

    /// <summary>Removes the file from view (the row and its key stay). Removing again changes nothing.</summary>
    public void Remove(DateTime nowUtc)
    {
        if (RemovedAtUtc is null)
        {
            RemovedAtUtc = nowUtc < UploadedAtUtc ? UploadedAtUtc : nowUtc;
        }
    }
}

/// <summary>The editable fields of a corrective action.</summary>
public sealed record CorrectiveActionDetails(
    string ActionType,
    string Description,
    string? ResponsibleText,
    DateOnly? DueDate,
    DateOnly? CompletedOn,
    CorrectiveActionStatus Status);

/// <summary>An action HR plans or takes because of a case (grv.CorrectiveAction). HR only; never shown to the submitter.</summary>
public sealed class GrievanceCorrectiveAction : AuditedEntity
{
    public const int ActionTypeMaxLength = 100;
    public const int DescriptionMaxLength = 1000;
    public const int ResponsibleMaxLength = 200;

    private GrievanceCorrectiveAction()
    {
    }

    public int CaseId { get; private set; }

    public string ActionType { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? ResponsibleText { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    public CorrectiveActionStatus Status { get; private set; }

    public static Result<GrievanceCorrectiveAction> Create(int caseId, CorrectiveActionDetails details)
    {
        var action = new GrievanceCorrectiveAction { CaseId = caseId };
        var applied = action.Update(details);
        return applied.IsFailure ? Result.Failure<GrievanceCorrectiveAction>(applied.Error) : Result.Success(action);
    }

    /// <summary>Checks the rules (as the CHECK constraints) without changing anything. Implemented exactly when there is a completion date.</summary>
    public static Result Validate(CorrectiveActionDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var actionType = TextNormalization.CleanOptional(details.ActionType);
        var description = TextNormalization.CleanOptional(details.Description);
        var responsible = TextNormalization.CleanOptional(details.ResponsibleText);
        var implemented = details.Status == CorrectiveActionStatus.Implemented;
        return new Violations()
            .AddIf(actionType is null, "actionType", "Action type is required.")
            .AddIf(actionType is { Length: > ActionTypeMaxLength }, "actionType", $"Action type must be at most {ActionTypeMaxLength} characters.")
            .AddIf(description is null, "description", "Description is required.")
            .AddIf(description is { Length: > DescriptionMaxLength }, "description", $"Description must be at most {DescriptionMaxLength} characters.")
            .AddIf(responsible is { Length: > ResponsibleMaxLength }, "responsibleText", $"Responsible must be at most {ResponsibleMaxLength} characters.")
            .AddIf(implemented && details.CompletedOn is null, "completedOn", "Enter the completion date of an implemented action.")
            .AddIf(!implemented && details.CompletedOn is not null, "completedOn", "Only an implemented action has a completion date.")
            .ToResult();
    }

    public Result Update(CorrectiveActionDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        ActionType = details.ActionType.Trim();
        Description = details.Description.Trim();
        ResponsibleText = TextNormalization.CleanOptional(details.ResponsibleText);
        DueDate = details.DueDate;
        CompletedOn = details.CompletedOn;
        Status = details.Status;
        return Result.Success();
    }
}

/// <summary>
/// One entry of the grievance access log (grv.AuditLog, bigint key, append-only): who viewed, downloaded, exported or changed what.
/// <see cref="Detail"/> holds codes and ids only, never case content or personal data. A submitter (named or anonymous) is logged without a
/// user id, and a submitter's own reads are not logged at all.
/// </summary>
public sealed class GrievanceAuditEntry
{
    public const int DetailMaxLength = 500;

    private GrievanceAuditEntry()
    {
    }

    public long Id { get; private set; }

    public int? CaseId { get; private set; }

    public GrievanceAuditAction Action { get; private set; }

    public int? ActorUserId { get; private set; }

    public GrievanceActorRole ActorRole { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string? Detail { get; private set; }

    /// <summary>An entry for the actor (a submitter's user id is never kept, named or anonymous: CK_AuditLog_SubmitterUnnamed).</summary>
    /// <exception cref="ArgumentException">The detail is longer than 500 characters.</exception>
    public static GrievanceAuditEntry Create(int? caseId, GrievanceAuditAction action, GrievanceActor actor, string? detail, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var cleanDetail = TextNormalization.CleanOptional(detail);
        if (cleanDetail is { Length: > DetailMaxLength })
        {
            throw new ArgumentException($"An audit detail is at most {DetailMaxLength} characters of codes.", nameof(detail));
        }

        return new GrievanceAuditEntry
        {
            CaseId = caseId,
            Action = action,
            ActorUserId = actor.IsSubmitter ? null : actor.UserId,
            ActorRole = actor.Role,
            OccurredAtUtc = occurredAtUtc,
            Detail = cleanDetail,
        };
    }
}
