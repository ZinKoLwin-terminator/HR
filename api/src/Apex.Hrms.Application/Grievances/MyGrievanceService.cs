using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;
using FluentValidation;

namespace Apex.Hrms.Application.Grievances;

/// <summary>
/// The submitter's own grievances. The scope is the caller's own cases (named by employee id, anonymous by pseudonym); anything else is
/// a 404. Rules worth knowing:
/// <list type="bullet">
/// <item>Reads are never written to the access log (a submitter's own views leave no trace, named or anonymous).</item>
/// <item>A draft may be unfinished; submitting assigns the case number (UTC year) and copies a named submitter's placement.</item>
/// <item>A message while the case awaits the submitter is the response (row version checked, the case returns to its previous status);
/// any other message only adds a row (the case and its row version do not change).</item>
/// <item>An upload never changes the case row; <c>markSeen</c> writes grv.CaseSubmitter only.</item>
/// <item>Nothing done to a draft is logged or notified: a deleted draft leaves no trace.</item>
/// </list>
/// Logs never carry the submitter, the subject, the description or a message body.
/// </summary>
internal sealed class MyGrievanceService(
    GrievanceAccess access,
    GrievanceReader reader,
    GrievanceAuditTrail audit,
    GrievanceNotifications notifications,
    GrievanceFiles files,
    GrievanceCaseNumbers numbers,
    IGrievanceSubmitterDirectory submitters,
    IRepository<GrievanceCase> cases,
    IRepository<GrievanceCategory> categories,
    IRepository<Department> departments,
    IRepository<Employee> employees,
    IRepository<GrievanceStatusHistoryEntry> history,
    IRepository<GrievanceCommunication> messages,
    IRepository<GrievanceAttachment> attachments,
    IValidator<GrievanceDraftCreateRequest> createValidator,
    IValidator<GrievanceDraftUpdateRequest> updateValidator,
    IValidator<SubmitGrievanceRequest> submitValidator,
    IValidator<MyGrievanceMessageRequest> messageValidator,
    IReadModel read,
    IUnitOfWork unitOfWork,
    IClock clock,
    GrievanceSettings settings,
    GrievanceUploadQuota quota) : IMyGrievanceService
{
    public const string EmployeesOnly = "Only an employee can raise a grievance.";

    /// <summary>Open drafts one employee may keep (security review M4).</summary>
    public const int MaxOpenDrafts = 5;

    public const string TooManyDrafts = "You already have 5 unfinished drafts. Submit or delete one before you start another.";

    public const string DailyQuotaReached = "You have reached today's upload limit for grievance files. Try again tomorrow.";

    public const string AnonymityFixed = "Start a new draft to change how you report.";

    private static readonly Expression<Func<GrievanceCase, object>> LastUpdate = grievance =>
        grievance.Status == GrievanceStatus.Draft ? grievance.ModifiedAtUtc : (grievance.LastSubmitterUpdateAtUtc ?? grievance.SubmittedAtUtc)!;

    private static readonly Dictionary<string, Expression<Func<GrievanceCase, object>>> SortKeys =
        new Dictionary<string, Expression<Func<GrievanceCase, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["caseNo"] = grievance => grievance.CaseNo!,
            ["subject"] = grievance => grievance.Subject,
            ["status"] = grievance => grievance.Status,
            ["createdAtUtc"] = grievance => grievance.CreatedAtUtc,
            ["submittedAtUtc"] = grievance => grievance.SubmittedAtUtc!,
            ["lastUpdateAtUtc"] = LastUpdate,
        };

    private DateTime UtcNow => clock.UtcNow.UtcDateTime;

    private async Task<IQueryable<GrievanceCase>> OwnAsync(CancellationToken cancellationToken) =>
        read.Query<GrievanceCase>().Where(await access.OwnScopeAsync(cancellationToken));

    public async Task<Result<GrievanceOptionsDto>> OptionsAsync(CancellationToken cancellationToken)
    {
        var categoryRows = await read.ToListAsync(read.Query<GrievanceCategory>().Where(category => category.Status == RecordStatus.Active), cancellationToken);
        var departmentRows = await read.ToListAsync(
            read.Query<Department>().Where(department => department.Status == RecordStatus.Active).Select(department => new { department.Id, department.Name }),
            cancellationToken);
        return Result.Success(new GrievanceOptionsDto(
            [.. categoryRows.OrderBy(category => category.SortOrder).ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(category => new GrievanceCategoryOptionDto(
                    category.Id, category.Code, category.Name, category.Description ?? string.Empty, category.AllowsAnonymous, category.DefaultTargetDays))],
            [.. departmentRows.OrderBy(department => department.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(department => new GrievanceDepartmentOptionDto(department.Id, department.Name))]));
    }

    public async Task<Result<PagedResult<MyGrievanceDto>>> QueryAsync(PageRequest<MyGrievanceFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sortKey = LastUpdate;
        var descending = true;
        if (request.Sort is { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            if (!SortKeys.TryGetValue(active, out var chosen))
            {
                return Result.Failure<PagedResult<MyGrievanceDto>>(AppError.Validation($"Cannot sort by \"{active}\".", "sort", "Choose a sortable field."));
            }

            sortKey = chosen;
            descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<MyGrievanceDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.", "search", $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var own = await OwnAsync(cancellationToken);
        if (request.Filters?.Status is { Length: > 0 } statusText)
        {
            if (!GrievanceText.Status.TryParse(statusText, out var status))
            {
                return Result.Failure<PagedResult<MyGrievanceDto>>(AppError.Validation("Choose a valid status.", "status", "Choose a valid status."));
            }

            own = own.Where(grievance => grievance.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            own = own.Where(Predicates.ContainsAny<GrievanceCase>(request.Search.Trim(), grievance => grievance.CaseNo, grievance => grievance.Subject));
        }

        var total = await read.CountAsync(own, cancellationToken);
        var pageSize = Paging.ClampPageSize(request.PageSize);
        var pageIndex = Paging.ClampPageIndex(Math.Max(0, request.PageIndex), total, pageSize);
        var ordered = (descending ? own.OrderByDescending(sortKey) : own.OrderBy(sortKey)).ThenByDescending(grievance => grievance.Id);
        var pageIds = await read.ToListAsync(ordered.Skip(pageIndex * pageSize).Take(pageSize).Select(grievance => grievance.Id), cancellationToken);
        var rows = await reader.MyRowsAsync(read.Query<GrievanceCase>().Where(grievance => pageIds.Contains(grievance.Id)), cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);
        return Result.Success(new PagedResult<MyGrievanceDto>([.. pageIds.Where(byId.ContainsKey).Select(id => byId[id])], total, pageIndex, pageSize));
    }

    public async Task<Result<MyGrievanceDetailDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var grievance = await reader.FirstAsync(await access.OwnScopeAsync(cancellationToken), id, cancellationToken);
        return grievance is null
            ? Result.Failure<MyGrievanceDetailDto>(GrievanceCaseSteps.CaseNotFound(id))
            : Result.Success(await reader.MyDetailAsync(grievance, cancellationToken));
    }

    public async Task<Result<MyGrievanceDashboardDto>> DashboardAsync(CancellationToken cancellationToken)
    {
        var mine = await reader.MyRowsAsync(await OwnAsync(cancellationToken), cancellationToken);
        static int Count(IEnumerable<MyGrievanceDto> rows, params GrievanceStatus[] statuses) =>
            rows.Count(row => statuses.Contains(GrievanceText.Status.Parse(row.Status)));
        return Result.Success(new MyGrievanceDashboardDto(
            Count(mine, GrievanceStatus.Draft),
            Count(mine, [.. GrievanceCase.OpenStatuses]),
            Count(mine, GrievanceStatus.AwaitingEmployeeResponse),
            Count(mine, GrievanceStatus.Resolved),
            Count(mine, GrievanceStatus.Closed),
            Count(mine, GrievanceStatus.Rejected),
            mine.Count(row => row.HasUnreadUpdate),
            [.. mine.OrderByDescending(row => row.LastUpdateAtUtc).ThenByDescending(row => row.Id).Take(5)]));
    }

    public async Task<Result<EvidenceDownload>> DownloadAttachmentAsync(int attachmentId, CancellationToken cancellationToken)
    {
        var notFound = NotFoundError.For("Attachment", attachmentId);
        var attachment = await read.FirstOrDefaultAsync(
            read.Query<GrievanceAttachment>().Where(file => file.Id == attachmentId
                && file.Visibility == GrievanceAttachmentVisibility.Shared
                && file.RemovedAtUtc == null),
            cancellationToken);
        if (attachment is null || await reader.FirstAsync(await access.OwnScopeAsync(cancellationToken), attachment.CaseId, cancellationToken) is null)
        {
            return Result.Failure<EvidenceDownload>(notFound);
        }

        var content = await files.OpenAsync(attachment.StorageKey, cancellationToken);
        if (content is null)
        {
            files.FileMissing(attachmentId);
            return Result.Failure<EvidenceDownload>(AppError.NotFound("The file of this attachment is not available."));
        }

        var contentType = Domain.Performance.EvidenceFiles.IsStoredContentType(attachment.ContentType) ? attachment.ContentType : "application/octet-stream";
        return Result.Success(new EvidenceDownload(attachment.FileName, contentType, content));
    }

    public async Task<Result<MyGrievanceDetailDto>> CreateDraftAsync(GrievanceDraftCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var valid = await createValidator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(valid.Error);
        }

        if (access.EmployeeId is not { } employeeId)
        {
            return Result.Failure<MyGrievanceDetailDto>(AppError.Forbidden(EmployeesOnly));
        }

        // Best effort: two requests of the same employee at the same moment can both pass the count and leave six drafts. That is
        // acceptable for a guard against clutter (it is not a security boundary), so no lock is taken for it.
        var own = await access.OwnCaseIdsAsync(cancellationToken);
        if (await read.CountAsync(read.Query<GrievanceCase>().Where(grievance => own.Contains(grievance.Id) && grievance.Status == GrievanceStatus.Draft), cancellationToken)
            >= MaxOpenDrafts)
        {
            return Result.Failure<MyGrievanceDetailDto>(AppError.Validation(TooManyDrafts));
        }

        var references = await ReferencesAsync(request, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(references.Error);
        }

        var draft = GrievanceCase.CreateDraft(Details(request), references.Value, clock.Today);
        if (draft.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(draft.Error);
        }

        // The submitter row is keyed by the case id, which the database assigns: two saves in one transaction.
        var transaction = await unitOfWork.BeginTransactionAsync(null, cancellationToken);
        if (transaction.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(transaction.Error);
        }

        await using var scope = transaction.Value;
        cases.Add(draft.Value);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(saved.Error);
        }

        submitters.Add(request.IsAnonymous
            ? GrievanceCaseSubmitter.Anonymous(draft.Value.Id, submitters.PseudonymOf(employeeId))
            : GrievanceCaseSubmitter.Named(draft.Value.Id, employeeId));
        saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(saved.Error);
        }

        await scope.CommitAsync(cancellationToken);
        return Result.Success(await reader.MyDetailAsync(draft.Value, cancellationToken));
    }

    public async Task<Result<MyGrievanceDetailDto>> UpdateDraftAsync(int id, GrievanceDraftUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opened = await OpenOwnAsync(id, request.RowVersion, checkStale: true, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(opened.Error);
        }

        var valid = await updateValidator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(valid.Error);
        }

        var grievance = opened.Value;
        var editable = grievance.EnsureDraft();
        if (editable.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(editable.Error);
        }

        var references = await ReferencesAsync(request, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(references.Error);
        }

        // How the employee reports is fixed when the draft is created (security review L5): switching a draft between named and
        // anonymous would change its submitter row from the employee id to the pseudonym (or back) on the same case, which links them.
        if (request.IsAnonymous != grievance.IsAnonymous)
        {
            return Result.Failure<MyGrievanceDetailDto>(AppError.Validation(AnonymityFixed, "isAnonymous", AnonymityFixed));
        }

        var updated = grievance.UpdateDraft(Details(request), references.Value, clock.Today);
        if (updated.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(updated.Error);
        }

        cases.MarkModified(grievance);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<MyGrievanceDetailDto>(saved.Error) : Result.Success(await reader.MyDetailAsync(grievance, cancellationToken));
    }

    public async Task<Result> DeleteDraftAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var opened = await OpenOwnAsync(id, rowVersion, checkStale: true, cancellationToken);
        if (opened.IsFailure)
        {
            return opened;
        }

        var draft = opened.Value.EnsureDraft();
        if (draft.IsFailure)
        {
            return draft;
        }

        // Under the case's file lock, like an upload: a file added at the same moment is either in this list or waits and then finds
        // the case gone, so no stored file is left without its row.
        var transaction = await unitOfWork.BeginTransactionAsync(GrievanceFiles.LockOf(id), cancellationToken);
        if (transaction.IsFailure)
        {
            return transaction;
        }

        List<string> keys;
        await using (var scope = transaction.Value)
        {
            var stored = await attachments.ListForChangeAsync(attachment => attachment.CaseId == id, cancellationToken);
            keys = [.. stored.Select(attachment => attachment.StorageKey)];
            foreach (var attachment in stored)
            {
                attachments.Remove(attachment);
            }

            cases.Remove(opened.Value);
            var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved;
            }

            await scope.CommitAsync(cancellationToken);
        }

        foreach (var key in keys)
        {
            await files.DiscardAsync(key, id);
        }

        return Result.Success();
    }

    public async Task<Result<MyGrievanceDetailDto>> SubmitAsync(int id, SubmitGrievanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opened = await OpenOwnAsync(id, request.RowVersion, checkStale: true, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(opened.Error);
        }

        var valid = await submitValidator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(valid.Error);
        }

        var grievance = opened.Value;
        var draft = grievance.EnsureDraft();
        if (draft.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(draft.Error);
        }

        var category = await categories.FindAsync(grievance.CategoryId, cancellationToken)
            ?? throw new InvalidOperationException($"The category of grievance case #{id} is missing.");
        GrievancePlacement? placement = null;
        if (!grievance.IsAnonymous && await employees.FindAsync(access.EmployeeId!.Value, cancellationToken) is { } employee)
        {
            placement = new GrievancePlacement(employee.CompanyId, employee.BranchId, employee.DepartmentId, employee.PositionId);
        }

        // Everything the submission checks runs before a case number is consumed; the domain checks again when it submits.
        var submittable = grievance.CheckSubmission(category, request.DeclarationAccepted, clock.Today);
        if (submittable.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(submittable.Error);
        }

        var caseNo = await numbers.NextFreeAsync(cancellationToken);
        if (caseNo.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(caseNo.Error);
        }

        var step = grievance.Submit(category, request.DeclarationAccepted, caseNo.Value, placement, access.SubmitterActor(), clock.Today, UtcNow);
        if (step.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(step.Error);
        }

        history.Add(step.Value.Entry);
        cases.MarkModified(grievance);
        audit.Submitter(grievance, GrievanceAuditAction.StatusChange, GrievanceAuditTrail.Transition(step.Value.Entry.FromStatus, step.Value.Entry.ToStatus));
        var told = await notifications.ToManagementAsync(grievance, "a new grievance was submitted", byCaseTeam: false, cancellationToken);
        if (told.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(told.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<MyGrievanceDetailDto>(saved.Error) : Result.Success(await reader.MyDetailAsync(grievance, cancellationToken));
    }

    public async Task<Result<MyGrievanceDetailDto>> SendMessageAsync(int id, MyGrievanceMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = RowVersionToken.Decode(request.RowVersion);
        if (version.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(version.Error);
        }

        var valid = await messageValidator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(valid.Error);
        }

        var grievance = await cases.FindAsync(id, await access.OwnScopeAsync(cancellationToken), cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<MyGrievanceDetailDto>(GrievanceCaseSteps.CaseNotFound(id));
        }

        return grievance.AwaitsSubmitterResponse
            ? await RespondAsync(grievance, version.Value, request, cancellationToken)
            : await PostMessageAsync(grievance, request, cancellationToken);
    }

    /// <summary>The answer to an information request: the row version must be current; the case returns to its previous status.</summary>
    private async Task<Result<MyGrievanceDetailDto>> RespondAsync(GrievanceCase grievance, byte[] expected, MyGrievanceMessageRequest request, CancellationToken cancellationToken)
    {
        if (!grievance.RowVersion.AsSpan().SequenceEqual(expected))
        {
            return Result.Failure<MyGrievanceDetailDto>(RowVersionToken.Stale);
        }

        cases.ExpectRowVersion(grievance, expected);
        var id = grievance.Id;
        var requests = await read.ToListAsync(
            read.Query<GrievanceCommunication>().Where(message => message.CaseId == id && message.Kind == GrievanceMessageKind.InfoRequest),
            cancellationToken);
        int? inReplyTo;
        if (request.InReplyToId is { } chosen)
        {
            if (requests.All(message => message.Id != chosen))
            {
                return Result.Failure<MyGrievanceDetailDto>(AppError.Validation(
                    "Answer an information request of this case.", "inReplyToId", "Answer an information request of this case."));
            }

            inReplyTo = chosen;
        }
        else
        {
            inReplyTo = requests.OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).FirstOrDefault()?.Id;
        }

        var step = grievance.Respond(request.Body, inReplyTo, access.SubmitterActor(), UtcNow);
        if (step.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(step.Error);
        }

        history.Add(step.Value.Entry);
        messages.Add(step.Value.Message!);
        cases.MarkModified(grievance);
        audit.Submitter(grievance, GrievanceAuditAction.StatusChange, GrievanceAuditTrail.Transition(step.Value.Entry.FromStatus, step.Value.Entry.ToStatus));
        var told = await notifications.ToManagementAsync(grievance, "the employee responded", byCaseTeam: false, cancellationToken);
        if (told.IsSuccess)
        {
            told = await notifications.ToActiveInvestigatorsAsync(grievance, "the employee responded", cancellationToken);
        }

        if (told.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(told.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<MyGrievanceDetailDto>(saved.Error) : Result.Success(await reader.MyDetailAsync(grievance, cancellationToken));
    }

    /// <summary>A plain message: one Communication row; the case row (and its row version) is not touched.</summary>
    private async Task<Result<MyGrievanceDetailDto>> PostMessageAsync(GrievanceCase grievance, MyGrievanceMessageRequest request, CancellationToken cancellationToken)
    {
        var message = grievance.PostSubmitterMessage(request.Body, access.SubmitterActor(), UtcNow);
        if (message.IsFailure)
        {
            return Result.Failure<MyGrievanceDetailDto>(message.Error);
        }

        messages.Add(message.Value);
        audit.Submitter(grievance, GrievanceAuditAction.SendMessage, "from-submitter");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<MyGrievanceDetailDto>(saved.Error) : Result.Success(await reader.MyDetailAsync(grievance, cancellationToken));
    }

    public async Task<Result> PrecheckUploadAsync(int id, CancellationToken cancellationToken)
    {
        var grievance = await reader.FirstAsync(await access.OwnScopeAsync(cancellationToken), id, cancellationToken);
        return grievance is null ? Result.Failure(GrievanceCaseSteps.CaseNotFound(id)) : grievance.EnsureFilesOpen();
    }

    /// <summary>
    /// A submitter's file (always Shared, no user id kept) on their draft or open case. The case row is never changed: the row version is
    /// only checked for its format and returned unchanged. At most 10 submitter files per case (counted under a lock per case, apart from
    /// the case team's), and at most <see cref="GrievanceSettings.SubmitterDailyUploadBytes"/> per account and day. On an ANONYMOUS case the
    /// identifying metadata is removed before the file is stored (<see cref="GrievanceFileScrubber"/>; a PDF is stored as it is).
    /// </summary>
    public async Task<Result<MyGrievanceAttachmentUploadResult>> UploadAttachmentAsync(int id, GrievanceFileUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var version = RowVersionToken.Decode(upload.RowVersion);
        if (version.IsFailure)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(version.Error);
        }

        var file = GrievanceFiles.Inspect(upload);
        if (file.IsFailure)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(file.Error);
        }

        var grievance = await reader.FirstAsync(await access.OwnScopeAsync(cancellationToken), id, cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(GrievanceCaseSteps.CaseNotFound(id));
        }

        var open = grievance.EnsureFilesOpen();
        if (open.IsFailure)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(open.Error);
        }

        var stored = grievance.IsAnonymous ? GrievanceFiles.ForAnonymousSubmitter(file.Value) : file;
        if (stored.IsFailure)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(stored.Error);
        }

        var size = stored.Value.Content.LongLength;
        var account = access.UserId ?? 0;
        var today = clock.Today;
        if (!quota.TryReserve(account, today, size, settings.SubmitterDailyUploadBytes))
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(AppError.Validation(DailyQuotaReached, "file", DailyQuotaReached));
        }

        var reserved = true;
        try
        {
            var result = await StoreSubmitterFileAsync(grievance, stored.Value, cancellationToken);
            reserved = result.IsFailure;
            return result;
        }
        finally
        {
            if (reserved)
            {
                quota.Release(account, today, size);
            }
        }
    }

    private async Task<Result<MyGrievanceAttachmentUploadResult>> StoreSubmitterFileAsync(GrievanceCase grievance, GrievanceCheckedFile file, CancellationToken cancellationToken)
    {
        var id = grievance.Id;

        var transaction = await unitOfWork.BeginTransactionAsync(GrievanceFiles.LockOf(id), cancellationToken);
        if (transaction.IsFailure)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(transaction.Error);
        }

        await using var scope = transaction.Value;
        if (await read.CountAsync(
                read.Query<GrievanceAttachment>().Where(attachment => attachment.CaseId == id
                    && attachment.RemovedAtUtc == null
                    && attachment.UploadedByRole == GrievanceActorRole.Submitter),
                cancellationToken)
            >= GrievanceAttachment.MaxSubmitterFilesPerCase)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(AppError.Conflict(GrievanceFiles.TooManyFromSubmitter));
        }

        var key = IEvidenceStorage.NewKey();
        var created = GrievanceAttachment.Create(
            id,
            null,
            null,
            GrievanceAttachmentVisibility.Shared,
            grievance.RecordedActor(access.SubmitterActor()),
            file.FileName,
            file.ContentType,
            file.Content.LongLength,
            key,
            file.Sha256,
            UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<MyGrievanceAttachmentUploadResult>(created.Error);
        }

        var committed = false;
        try
        {
            await files.SaveAsync(key, file.Content, cancellationToken);
            attachments.Add(created.Value);
            audit.Submitter(grievance, GrievanceAuditAction.UploadAttachment, "visibility:Shared");
            var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<MyGrievanceAttachmentUploadResult>(saved.Error);
            }

            await scope.CommitAsync(cancellationToken);
            committed = true;
            return Result.Success(new MyGrievanceAttachmentUploadResult(GrievanceRedaction.ToMy(created.Value), grievance.RowVersion));
        }
        finally
        {
            if (!committed)
            {
                await files.DiscardAsync(key, id);
            }
        }
    }

    public async Task<Result> MarkSeenAsync(int id, CancellationToken cancellationToken)
    {
        if (await reader.FirstAsync(await access.OwnScopeAsync(cancellationToken), id, cancellationToken) is null)
        {
            return Result.Failure(GrievanceCaseSteps.CaseNotFound(id));
        }

        var link = await submitters.FindForChangeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Grievance case #{id} has no submitter row.");
        link.MarkSeen(UtcNow);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Own case, tracked, for a change: row version 400, scope 404, stale 409 (when asked).</summary>
    private async Task<Result<GrievanceCase>> OpenOwnAsync(int id, string? rowVersion, bool checkStale, CancellationToken cancellationToken)
    {
        var expected = RowVersionToken.Decode(rowVersion);
        if (expected.IsFailure)
        {
            return Result.Failure<GrievanceCase>(expected.Error);
        }

        var grievance = await cases.FindAsync(id, await access.OwnScopeAsync(cancellationToken), cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<GrievanceCase>(GrievanceCaseSteps.CaseNotFound(id));
        }

        if (checkStale)
        {
            if (!grievance.RowVersion.AsSpan().SequenceEqual(expected.Value))
            {
                return Result.Failure<GrievanceCase>(RowVersionToken.Stale);
            }

            cases.ExpectRowVersion(grievance, expected.Value);
        }

        return Result.Success(grievance);
    }

    /// <summary>The chosen category (400 when it does not exist) and involved department (400 when missing or inactive).</summary>
    private Task<Result<GrievanceCategory>> ReferencesAsync(IGrievanceDraftFields fields, CancellationToken cancellationToken) =>
        GrievanceDraftReferences.CheckAsync(categories, departments, fields, cancellationToken);

    private static GrievanceDraftDetails Details(IGrievanceDraftFields fields) => GrievanceDraftReferences.Details(fields);
}

/// <summary>What a submitter's draft and HR's intake share: the references the fields point at, and the fields as domain details.</summary>
internal static class GrievanceDraftReferences
{
    /// <summary>The chosen category (400 when it does not exist) and involved department (400 when missing or inactive).</summary>
    public static async Task<Result<GrievanceCategory>> CheckAsync(
        IRepository<GrievanceCategory> categories,
        IRepository<Department> departments,
        IGrievanceDraftFields fields,
        CancellationToken cancellationToken)
    {
        var category = await categories.FindAsync(fields.CategoryId, cancellationToken);
        var violations = new Violations().AddIf(category is null, "categoryId", "Choose an active category.");
        if (fields.InvolvedDepartmentId is { } departmentId)
        {
            var department = await departments.FindAsync(departmentId, cancellationToken);
            violations.AddIf(department is null || !department.IsActive, "involvedDepartmentId", "Choose an active department.");
        }

        return violations.Any ? Result.Failure<GrievanceCategory>(violations.ToError()) : Result.Success(category!);
    }

    public static GrievanceDraftDetails Details(IGrievanceDraftFields fields) => new(
        fields.CategoryId,
        fields.Subject ?? string.Empty,
        fields.Description ?? string.Empty,
        fields.IncidentDate,
        fields.Location,
        fields.InvolvedPersonText,
        fields.InvolvedDepartmentId,
        fields.IsAnonymous);
}
