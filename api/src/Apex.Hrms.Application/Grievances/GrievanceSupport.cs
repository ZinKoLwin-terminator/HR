using System.Globalization;
using System.Security.Cryptography;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Application.Grievances;

/// <summary>
/// The configurable grievance rules: how many years a closed or rejected case is kept (<c>Grievance:RetentionYears</c>, default 7,
/// 1 to 100; set when a case is closed or rejected, cleared when it is reopened), and how many bytes one account may upload as a
/// submitter per day (<c>Grievance:SubmitterDailyUploadBytes</c>, default 100 MB, 1 MB to 10 GB).
/// </summary>
public sealed record GrievanceSettings
{
    public const string RetentionYearsKey = "Grievance:RetentionYears";

    public const int DefaultRetentionYears = 7;

    public const string SubmitterDailyUploadBytesKey = "Grievance:SubmitterDailyUploadBytes";

    public const long DefaultSubmitterDailyUploadBytes = 100L * 1024 * 1024;

    public GrievanceSettings(int retentionYears = DefaultRetentionYears, long submitterDailyUploadBytes = DefaultSubmitterDailyUploadBytes)
    {
        if (retentionYears is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionYears), retentionYears, $"{RetentionYearsKey} must be between 1 and 100 years.");
        }

        if (submitterDailyUploadBytes is < 1024 * 1024 or > 10L * 1024 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(submitterDailyUploadBytes), submitterDailyUploadBytes, $"{SubmitterDailyUploadBytesKey} must be between 1 MB and 10 GB.");
        }

        RetentionYears = retentionYears;
        SubmitterDailyUploadBytes = submitterDailyUploadBytes;
    }

    public int RetentionYears { get; }

    public long SubmitterDailyUploadBytes { get; }
}

/// <summary>
/// The daily byte quota of submitter uploads per account (security review M4), in memory per API instance like the rate limits: it
/// stops one account from filling the grievance folder through drafts and anonymous cases. Only the account id, the business date and
/// a byte count are kept (never a case), and only for today; yesterday's counters are dropped.
/// </summary>
public sealed class GrievanceUploadQuota
{
    private readonly Lock gate = new();
    private readonly Dictionary<(int UserId, DateOnly Day), long> used = [];

    /// <summary>Reserves <paramref name="bytes"/> for the account today; false (nothing reserved) when that would pass <paramref name="limit"/>.</summary>
    public bool TryReserve(int userId, DateOnly day, long bytes, long limit)
    {
        lock (gate)
        {
            foreach (var stale in used.Keys.Where(key => key.Day != day).ToList())
            {
                used.Remove(stale);
            }

            var current = used.GetValueOrDefault((userId, day));
            if (current + bytes > limit)
            {
                return false;
            }

            used[(userId, day)] = current + bytes;
            return true;
        }
    }

    /// <summary>Gives back a reservation whose file was not stored.</summary>
    public void Release(int userId, DateOnly day, long bytes)
    {
        lock (gate)
        {
            if (used.TryGetValue((userId, day), out var current))
            {
                used[(userId, day)] = Math.Max(0, current - bytes);
            }
        }
    }
}

/// <summary>
/// Writes the grievance access log (grv.AuditLog), staged with the unit of work of the request. Details are codes and ids only (a status
/// pair, a field list, an attachment id), never text a person wrote. A submitter's action is logged without a user id (the domain drops
/// it), and a submitter's own reads, and anything done to a draft, are not logged at all: a deleted draft must leave no trace.
/// </summary>
internal sealed class GrievanceAuditTrail(IGrievanceAuditLog log, GrievanceAccess access, IClock clock)
{
    public void Team(int? caseId, GrievanceAuditAction action, GrievanceActorRole role, string? detail) =>
        log.Add(GrievanceAuditEntry.Create(caseId, action, access.TeamActor(role), Clip(detail), clock.UtcNow.UtcDateTime));

    /// <summary>A submitter's action on a submitted case (never on a draft).</summary>
    public void Submitter(GrievanceCase grievance, GrievanceAuditAction action, string? detail)
    {
        ArgumentNullException.ThrowIfNull(grievance);
        if (grievance.Status == GrievanceStatus.Draft)
        {
            return;
        }

        log.Add(GrievanceAuditEntry.Create(grievance.Id, action, grievance.RecordedActor(access.SubmitterActor()), Clip(detail), clock.UtcNow.UtcDateTime));
    }

    public static string Transition(GrievanceStatus from, GrievanceStatus to) =>
        string.Create(CultureInfo.InvariantCulture, $"{GrievanceText.Status.ToText(from)}>{GrievanceText.Status.ToText(to)}");

    private static string? Clip(string? detail) =>
        detail is { Length: > GrievanceAuditEntry.DetailMaxLength } ? detail[..GrievanceAuditEntry.DetailMaxLength] : detail;
}

/// <summary>
/// The in-app notifications of the grievance module (D5). The text is the case number and the event only, never the category, the
/// subject or a name. Rules: management on submission; management and the active investigators on the submitter's response; the
/// investigator on assignment; the reviewers
/// on a proposed resolution and on an immediate escalation; the named submitter when the case team does something the submitter sees.
/// Never the anonymous submitter (they have the unread badge), never the employee the case is about. A submitter's own action never
/// excludes the submitter from the recipients: leaving one HR user out of "a case was submitted" would tell the others who submitted it.
/// </summary>
internal sealed class GrievanceNotifications(
    INotificationWriter writer,
    INotificationRecipients recipients,
    IGrievanceSubmitterDirectory submitters,
    IReadModel read,
    GrievanceAccess access)
{
    private const string Icon = "gavel";

    public Task<Result> ToManagementAsync(GrievanceCase grievance, string happened, bool byCaseTeam, CancellationToken cancellationToken) =>
        ToHoldersAsync(grievance, GrievanceAccess.ManagementModule, "View", happened, $"/grievances/cases/{grievance.Id}", byCaseTeam, cancellationToken);

    public Task<Result> ToReviewersAsync(GrievanceCase grievance, string happened, CancellationToken cancellationToken) =>
        ToHoldersAsync(grievance, GrievanceAccess.ReviewModule, "Approve", happened, $"/grievances/reviews/{grievance.Id}", byCaseTeam: true, cancellationToken);

    public async Task<Result> ToInvestigatorAsync(GrievanceCase grievance, int investigatorEmployeeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grievance);
        if (investigatorEmployeeId == grievance.SubjectEmployeeId || investigatorEmployeeId == access.EmployeeId)
        {
            return Result.Success();
        }

        var account = await recipients.UserIdOfEmployeeAsync(investigatorEmployeeId, cancellationToken);
        return account is { } userId
            ? writer.Add([userId], Content(grievance, "you were assigned to investigate it", $"/grievances/investigations/{grievance.Id}"))
            : Result.Success();
    }

    /// <summary>
    /// The investigators actively assigned to the case (never the employee it is about), linking to their investigation screen. Used
    /// for the submitter's response, which the investigation is waiting for.
    /// </summary>
    public async Task<Result> ToActiveInvestigatorsAsync(GrievanceCase grievance, string happened, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grievance);
        var id = grievance.Id;
        var subject = grievance.SubjectEmployeeId;
        var investigators = await read.ToListAsync(
            read.Query<GrievanceAssignment>()
                .Where(assignment => assignment.CaseId == id && assignment.UnassignedAtUtc == null)
                .Select(assignment => assignment.InvestigatorEmployeeId)
                .Distinct(),
            cancellationToken);
        var users = new List<int>();
        foreach (var employeeId in investigators.Where(employeeId => employeeId != subject))
        {
            if (await recipients.UserIdOfEmployeeAsync(employeeId, cancellationToken) is { } userId)
            {
                users.Add(userId);
            }
        }

        return users.Count == 0 ? Result.Success() : writer.Add(users, Content(grievance, happened, $"/grievances/investigations/{grievance.Id}"));
    }

    /// <summary>The named submitter, about a change they may see. Nothing for an anonymous case.</summary>
    public async Task<Result> ToNamedSubmitterAsync(GrievanceCase grievance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grievance);
        if (grievance.IsAnonymous)
        {
            return Result.Success();
        }

        var id = grievance.Id;
        var employee = await read.FirstOrDefaultAsync(
            submitters.Query().Where(submitter => submitter.CaseId == id).Select(submitter => submitter.EmployeeId),
            cancellationToken);
        if (employee is not { } employeeId || employeeId == grievance.SubjectEmployeeId)
        {
            return Result.Success();
        }

        var account = await recipients.UserIdOfEmployeeAsync(employeeId, cancellationToken);
        return account is { } userId
            ? writer.Add([userId], Content(grievance, "there is an update on your grievance", $"/grievances/my/{grievance.Id}"))
            : Result.Success();
    }

    private async Task<Result> ToHoldersAsync(
        GrievanceCase grievance,
        string module,
        string action,
        string happened,
        string link,
        bool byCaseTeam,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grievance);
        var excluded = new List<int>();
        if (grievance.SubjectEmployeeId is { } subject)
        {
            excluded.Add(subject);
        }

        if (byCaseTeam && access.EmployeeId is { } actor)
        {
            excluded.Add(actor);
        }

        var users = await recipients.UserIdsHoldingAsync(module, action, excluded, cancellationToken);
        var others = byCaseTeam && access.UserId is { } self ? users.Where(user => user != self) : users;
        return writer.Add(others, Content(grievance, happened, link));
    }

    private static NotificationContent Content(GrievanceCase grievance, string happened, string link)
    {
        var caseNo = grievance.CaseNo ?? string.Empty;
        return new NotificationContent($"Grievance {caseNo}", $"Grievance {caseNo}: {happened}.", Icon, link);
    }
}

/// <summary>A file checked by its content: the cleaned name, the detected type, the content and its SHA-256.</summary>
internal sealed record GrievanceCheckedFile(string FileName, string ContentType, Domain.Performance.EvidenceKind Kind, byte[] Content, byte[] Sha256);

/// <summary>
/// Stores grievance files (<see cref="IGrievanceFileStorage"/>): PDF, PNG, JPEG, DOCX or XLSX recognised by content
/// (<see cref="Domain.Performance.EvidenceFiles"/>), at most 10 MB, at most 10 per case. A file is stored under a new random key
/// before its row is saved; if the save fails the file is deleted again. Content and names are never logged, only ids.
/// </summary>
internal sealed partial class GrievanceFiles(IGrievanceFileStorage storage, ILogger<GrievanceFiles> logger)
{
    public const string TooManyFromSubmitter = "You can add at most 10 files to a grievance.";

    public const string TooManyFromCaseTeam = "The case team can add at most 20 files to a case.";

    public static Result<GrievanceCheckedFile> Inspect(GrievanceFileUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var file = Domain.Performance.EvidenceFiles.Inspect(upload.FileName, upload.Content ?? []);
        return file.IsFailure
            ? Result.Failure<GrievanceCheckedFile>(file.Error)
            : Result.Success(new GrievanceCheckedFile(file.Value.FileName, file.Value.ContentType, file.Value.Kind, upload.Content!, SHA256.HashData(upload.Content!)));
    }

    /// <summary>
    /// The file of the submitter of an ANONYMOUS case without its identifying metadata (<see cref="GrievanceFileScrubber"/>), with the
    /// size and hash of the cleaned content. Any other upload is stored as it was sent.
    /// </summary>
    public static Result<GrievanceCheckedFile> ForAnonymousSubmitter(GrievanceCheckedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var cleaned = GrievanceFileScrubber.Scrub(file.Kind, file.Content);
        return cleaned.IsFailure
            ? Result.Failure<GrievanceCheckedFile>(cleaned.Error)
            : Result.Success(file with { Content = cleaned.Value, Sha256 = SHA256.HashData(cleaned.Value) });
    }

    public static ApplicationLock LockOf(int caseId) =>
        new(string.Create(CultureInfo.InvariantCulture, $"grv-files-{caseId}"), "Another file is being added to this case. Try again.");

    public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) => storage.SaveAsync(key, content, cancellationToken);

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken) =>
        GrievanceAttachment.IsValidStorageKey(key) ? storage.OpenReadAsync(key, cancellationToken) : Task.FromResult<Stream?>(null);

    /// <summary>Removes a stored file whose row was not saved, or the file of a deleted draft. Never throws.</summary>
    public async Task DiscardAsync(string key, int caseId)
    {
        try
        {
            if (GrievanceAttachment.IsValidStorageKey(key))
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDiscardFailed(caseId);
        }
    }

    public void FileMissing(int attachmentId) => LogFileMissing(attachmentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A grievance file of case {CaseId} could not be removed from storage.")]
    private partial void LogDiscardFailed(int caseId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The stored file of grievance attachment {AttachmentId} is missing.")]
    private partial void LogFileMissing(int attachmentId);
}

/// <summary>Makes case numbers <c>GRV-yyyy-nnnnn</c>: the UTC year of submission and the next free number of grv.CaseNoSeq.</summary>
internal sealed class GrievanceCaseNumbers(IGrievanceCaseNumberSequence sequence, IRepository<GrievanceCase> cases, IClock clock)
{
    private const int MaxAttempts = 1000;

    public async Task<Result<string>> NextFreeAsync(CancellationToken cancellationToken)
    {
        var year = clock.UtcNow.UtcDateTime.Year;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var caseNo = GrievanceCase.FormatCaseNo(year, await sequence.NextAsync(cancellationToken));
            if (!await cases.AnyAsync(grievance => grievance.CaseNo == caseNo, cancellationToken))
            {
                return Result.Success(caseNo);
            }
        }

        return Result.Failure<string>(AppError.Conflict("No unused case number could be generated. Try again."));
    }
}
