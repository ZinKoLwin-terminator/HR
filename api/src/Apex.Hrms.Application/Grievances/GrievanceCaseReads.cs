using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Grievances;

/// <summary>
/// The case-team reads. Every read starts from the caller's scope (<see cref="GrievanceAccess"/>); a case outside it is a 404. Opening a
/// case, its history or a file is written to the access log (with the widest tier the caller holds on the case).
/// </summary>
internal sealed class GrievanceCaseReader(
    GrievanceAccess access,
    GrievanceReader reader,
    GrievanceAuditTrail audit,
    GrievanceFiles files,
    IGrievanceAuditLog log,
    IPermissionHolders holders,
    IReadModel read,
    IUnitOfWork unitOfWork,
    IClock clock) : IGrievanceCaseReader
{
    public async Task<Result<PagedResult<GrievanceCaseDto>>> QueryAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = read.Query<GrievanceCase>().Where(await access.TeamScopeAsync(management: true, investigator: false, reviewer: false, cancellationToken));
        return await reader.TeamPageAsync(source, request, row => row.SubmittedAtUtc!, fallbackDescending: true, cancellationToken);
    }

    public async Task<Result<PagedResult<GrievanceCaseDto>>> InvestigationsAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = read.Query<GrievanceCase>().Where(await access.TeamScopeAsync(management: false, investigator: true, reviewer: false, cancellationToken));
        return await reader.TeamPageAsync(source, NotArchived(request), row => row.SubmittedAtUtc!, fallbackDescending: true, cancellationToken);
    }

    public async Task<Result<PagedResult<GrievanceCaseDto>>> PendingReviewsAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proposals = read.Query<GrievanceResolutionProposal>();
        var source = read.Query<GrievanceCase>()
            .Where(await access.TeamScopeAsync(management: false, investigator: false, reviewer: true, cancellationToken))
            .Where(GrievanceQueries.IsOpen())
            .Where(grievance => grievance.IsEscalated
                || proposals.Any(proposal => proposal.CaseId == grievance.Id && proposal.Decision == ResolutionDecision.Pending));
        return await reader.TeamPageAsync(source, NotArchived(request), row => row.SubmittedAtUtc!, fallbackDescending: false, cancellationToken);
    }

    public async Task<Result<GrievanceCaseDetailDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var grievance = await reader.FirstAsync(await access.CaseTeamScopeAsync(cancellationToken), id, cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<GrievanceCaseDetailDto>(GrievanceCaseSteps.CaseNotFound(id));
        }

        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        var detail = GrievanceRedaction.ToCaseDetail(await reader.BundleAsync(grievance, cancellationToken), roles, clock.Today);
        audit.Team(id, GrievanceAuditAction.ViewCase, roles.ReadRole, null);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<GrievanceCaseDetailDto>(saved.Error) : Result.Success(detail);
    }

    public async Task<Result<IReadOnlyList<GrievanceHistoryEntryDto>>> HistoryAsync(int id, CancellationToken cancellationToken)
    {
        var grievance = await reader.FirstAsync(await access.CaseTeamScopeAsync(cancellationToken), id, cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<IReadOnlyList<GrievanceHistoryEntryDto>>(GrievanceCaseSteps.CaseNotFound(id));
        }

        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        var named = await reader.NamedSubmitterAsync(grievance, cancellationToken);
        var entries = await read.ToListAsync(read.Query<GrievanceStatusHistoryEntry>().Where(entry => entry.CaseId == id), cancellationToken);
        IReadOnlyList<GrievanceHistoryEntryDto> history =
        [
            .. entries.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id)
                .Select(entry => GrievanceRedaction.ToHistory(entry, grievance.IsAnonymous, named, roles)),
        ];
        audit.Team(id, GrievanceAuditAction.ViewHistory, roles.ReadRole, null);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<IReadOnlyList<GrievanceHistoryEntryDto>>(saved.Error) : Result.Success(history);
    }

    public async Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditAsync(int id, PageRequest<GrievanceAuditFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var grievance = await reader.FirstAsync(await access.TeamScopeAsync(management: true, investigator: false, reviewer: false, cancellationToken), id, cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<PagedResult<GrievanceAuditEntryDto>>(GrievanceCaseSteps.CaseNotFound(id));
        }

        return await AuditPageAsync(log.Query().Where(entry => entry.CaseId == id), request, cancellationToken);
    }

    public async Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditLogAsync(PageRequest<GrievanceAuditFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var visible = read.Query<GrievanceCase>()
            .Where(await access.TeamScopeAsync(management: true, investigator: false, reviewer: false, cancellationToken))
            .Select(grievance => (int?)grievance.Id);
        return await AuditPageAsync(log.Query().Where(entry => entry.CaseId == null || visible.Contains(entry.CaseId)), request, cancellationToken);
    }

    /// <summary>One page of access-log entries, newest first, with the action and date filters (business dates, inclusive).</summary>
    private async Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditPageAsync(
        IQueryable<GrievanceAuditEntry> entries,
        PageRequest<GrievanceAuditFilter> request,
        CancellationToken cancellationToken)
    {
        var filter = request.Filters ?? new GrievanceAuditFilter();
        if (filter.Action is { Length: > 0 } actionText)
        {
            if (!GrievanceText.AuditAction.TryParse(actionText, out var action))
            {
                return Result.Failure<PagedResult<GrievanceAuditEntryDto>>(AppError.Validation("Choose a valid action.", "action", "Choose a valid action."));
            }

            entries = entries.Where(entry => entry.Action == action);
        }

        if (filter.From > filter.To)
        {
            return Result.Failure<PagedResult<GrievanceAuditEntryDto>>(AppError.Validation(
                "The start date must not be after the end date.", "from", "The start date must not be after the end date."));
        }

        if (filter.From is { } from)
        {
            var since = clock.StartOfDayUtc(from);
            entries = entries.Where(entry => entry.OccurredAtUtc >= since);
        }

        if (filter.To is { } to)
        {
            var before = clock.StartOfDayUtc(to.AddDays(1));
            entries = entries.Where(entry => entry.OccurredAtUtc < before);
        }

        var total = await read.CountAsync(entries, cancellationToken);
        var pageSize = Paging.ClampPageSize(request.PageSize);
        var pageIndex = Paging.ClampPageIndex(Math.Max(0, request.PageIndex), total, pageSize);
        var page = await read.ToListAsync(
            entries.OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id).Skip(pageIndex * pageSize).Take(pageSize),
            cancellationToken);
        var names = await reader.AccountNamesAsync([.. page.Select(entry => entry.ActorUserId).OfType<int>().Distinct()], cancellationToken);
        int[] caseIds = [.. page.Select(entry => entry.CaseId).OfType<int>().Distinct()];
        var caseNos = caseIds.Length == 0
            ? new Dictionary<int, string?>()
            : (await read.ToListAsync(
                read.Query<GrievanceCase>().Where(grievance => caseIds.Contains(grievance.Id)).Select(grievance => new { grievance.Id, grievance.CaseNo }),
                cancellationToken)).ToDictionary(row => row.Id, row => row.CaseNo);
        return Result.Success(new PagedResult<GrievanceAuditEntryDto>(
            [.. page.Select(entry => new GrievanceAuditEntryDto(
                entry.Id,
                entry.CaseId,
                entry.CaseId is { } caseId ? caseNos.GetValueOrDefault(caseId) : null,
                GrievanceText.AuditAction.ToText(entry.Action),
                entry.ActorUserId,
                entry.ActorUserId is { } user ? names.GetValueOrDefault(user) : null,
                GrievanceText.ActorRole.ToText(entry.ActorRole),
                entry.OccurredAtUtc,
                entry.Detail))],
            total,
            pageIndex,
            pageSize));
    }

    public async Task<Result<IReadOnlyList<GrievanceInvestigatorOptionDto>>> InvestigatorsAsync(string? search, CancellationToken cancellationToken)
    {
        if (search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<IReadOnlyList<GrievanceInvestigatorOptionDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.", "search", $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var eligible = holders.EmployeesHolding(GrievanceAccess.InvestigationModule, "Edit");
        var employees = read.Query<Employee>().Where(employee => eligible.Contains(employee.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            employees = employees.Where(Predicates.ContainsAny<Employee>(search.Trim(), employee => employee.EmployeeCode, employee => employee.FirstName, employee => employee.LastName));
        }

        var openCases = read.Query<GrievanceCase>().Where(GrievanceQueries.IsOpen()).Select(grievance => grievance.Id);
        var assignments = read.Query<GrievanceAssignment>();
        var departments = read.Query<Department>();
        var positions = read.Query<Position>();
        var rows = await read.ToListAsync(
            employees.Select(employee => new
            {
                employee.Id,
                employee.EmployeeCode,
                employee.FirstName,
                employee.LastName,
                DepartmentName = departments.Where(department => department.Id == employee.DepartmentId).Select(department => department.Name).FirstOrDefault(),
                PositionName = positions.Where(position => position.Id == employee.PositionId).Select(position => position.Title).FirstOrDefault(),
                ActiveCaseCount = assignments.Count(assignment => assignment.InvestigatorEmployeeId == employee.Id
                    && assignment.UnassignedAtUtc == null
                    && openCases.Contains(assignment.CaseId)),
            }),
            cancellationToken);
        IReadOnlyList<GrievanceInvestigatorOptionDto> options =
        [
            .. rows.Select(row => new GrievanceInvestigatorOptionDto(
                    row.Id, row.EmployeeCode, $"{row.FirstName} {row.LastName}".Trim(), row.DepartmentName, row.PositionName, row.ActiveCaseCount))
                .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.EmployeeId),
        ];
        return Result.Success(options);
    }

    public const int EmployeeOptionLimit = 20;

    public async Task<Result<IReadOnlyList<GrievanceEmployeeOptionDto>>> EmployeeOptionsAsync(string? search, CancellationToken cancellationToken)
    {
        if (search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<IReadOnlyList<GrievanceEmployeeOptionDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.", "search", $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var employees = read.Query<Employee>().Where(employee => employee.ExitDate == null
            && employee.Status != Domain.Employees.EmploymentStatus.Resigned
            && employee.Status != Domain.Employees.EmploymentStatus.Terminated);
        if (!string.IsNullOrWhiteSpace(search))
        {
            employees = employees.Where(Predicates.ContainsAny<Employee>(search.Trim(), employee => employee.EmployeeCode, employee => employee.FirstName, employee => employee.LastName));
        }

        var departments = read.Query<Department>();
        var positions = read.Query<Position>();
        var rows = await read.ToListAsync(
            employees
                .OrderBy(employee => employee.FirstName).ThenBy(employee => employee.LastName).ThenBy(employee => employee.Id)
                .Take(EmployeeOptionLimit)
                .Select(employee => new
                {
                    employee.Id,
                    employee.EmployeeCode,
                    employee.FirstName,
                    employee.LastName,
                    DepartmentName = departments.Where(department => department.Id == employee.DepartmentId).Select(department => department.Name).FirstOrDefault(),
                    PositionName = positions.Where(position => position.Id == employee.PositionId).Select(position => position.Title).FirstOrDefault(),
                }),
            cancellationToken);
        IReadOnlyList<GrievanceEmployeeOptionDto> options =
            [.. rows.Select(row => new GrievanceEmployeeOptionDto(row.Id, row.EmployeeCode, $"{row.FirstName} {row.LastName}".Trim(), row.DepartmentName, row.PositionName))];
        return Result.Success(options);
    }

    public async Task<Result<EvidenceDownload>> DownloadAttachmentAsync(int attachmentId, CancellationToken cancellationToken)
    {
        var notFound = NotFoundError.For("Attachment", attachmentId);
        var attachment = await read.FirstOrDefaultAsync(
            read.Query<GrievanceAttachment>().Where(file => file.Id == attachmentId && file.RemovedAtUtc == null),
            cancellationToken);
        var grievance = attachment is null ? null : await reader.FirstAsync(await access.CaseTeamScopeAsync(cancellationToken), attachment.CaseId, cancellationToken);
        if (attachment is null || grievance is null)
        {
            return Result.Failure<EvidenceDownload>(notFound);
        }

        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        if (!GrievanceRedaction.MayOpen(roles, attachment.Visibility))
        {
            return Result.Failure<EvidenceDownload>(notFound);
        }

        var content = await files.OpenAsync(attachment.StorageKey, cancellationToken);
        if (content is null)
        {
            files.FileMissing(attachmentId);
            return Result.Failure<EvidenceDownload>(AppError.NotFound("The file of this attachment is not available."));
        }

        audit.Team(grievance.Id, GrievanceAuditAction.DownloadAttachment, roles.ReadRole, string.Create(CultureInfo.InvariantCulture, $"attachment:{attachmentId}"));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            await content.DisposeAsync();
            return Result.Failure<EvidenceDownload>(saved.Error);
        }

        var contentType = Domain.Performance.EvidenceFiles.IsStoredContentType(attachment.ContentType) ? attachment.ContentType : "application/octet-stream";
        return Result.Success(new EvidenceDownload(attachment.FileName, contentType, content));
    }

    /// <summary>Investigators and reviewers never list archived cases (they are out of their scope); the filter is forced off.</summary>
    private static PageRequest<GrievanceCaseFilter> NotArchived(PageRequest<GrievanceCaseFilter> request) =>
        request with { Filters = (request.Filters ?? new GrievanceCaseFilter()) with { Archived = null } };
}

/// <summary>Case-team uploads and corrective actions.</summary>
internal sealed class GrievanceCaseWork(
    GrievanceCaseSteps steps,
    GrievanceAccess access,
    GrievanceReader reader,
    GrievanceAuditTrail audit,
    GrievanceFiles files,
    IRepository<GrievanceAttachment> attachments,
    IRepository<GrievanceCorrectiveAction> actions,
    IRepository<GrievanceCase> cases,
    FluentValidation.IValidator<GrievanceCorrectiveActionCreateRequest> createValidator,
    FluentValidation.IValidator<GrievanceCorrectiveActionUpdateRequest> updateValidator,
    IReadModel read,
    IUnitOfWork unitOfWork) : IGrievanceCaseWork
{
    public async Task<Result> PrecheckUploadAsync(int id, CancellationToken cancellationToken)
    {
        var grievance = await reader.FirstAsync(await access.CaseTeamScopeAsync(cancellationToken), id, cancellationToken);
        if (grievance is null)
        {
            return Result.Failure(GrievanceCaseSteps.CaseNotFound(id));
        }

        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        return roles.CanWorkOnCase ? grievance.EnsureFilesOpen() : Result.Failure(AppError.Forbidden(GrievanceRefusals.CaseWork));
    }

    /// <summary>
    /// A case-team file (management Edit or an assigned investigator with Edit) on an open case. Restricted files are management's. The
    /// count (at most 20 case-team files, apart from the submitter's 10) is checked under a lock per case. A Shared file moves the
    /// submitter's unread badge, so the case row changes and its row version must be current (409 otherwise); a CaseTeam or Restricted
    /// file leaves the case row as it is (like a note) and its row version is only checked for its format. The answer carries the
    /// case's row version either way.
    /// </summary>
    public async Task<Result<GrievanceAttachmentUploadResult>> UploadAttachmentAsync(int id, string? visibility, GrievanceFileUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var version = UseCases.RowVersionToken.Decode(upload.RowVersion);
        if (version.IsFailure)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(version.Error);
        }

        if (!GrievanceText.Visibility.TryParse(visibility, out var chosen))
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(AppError.Validation("Choose who may see the file.", "visibility", "Choose Shared, CaseTeam or Restricted."));
        }

        var file = GrievanceFiles.Inspect(upload);
        if (file.IsFailure)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(file.Error);
        }

        var opened = await steps.OpenAsync(
            id,
            upload.RowVersion,
            (roles, _) => !roles.CanWorkOnCase
                ? GrievanceRefusals.Allow(false, GrievanceRefusals.CaseWork)
                : GrievanceRefusals.Allow(chosen != GrievanceAttachmentVisibility.Restricted || roles.ManagementEdit, GrievanceRefusals.RestrictedFile),
            cancellationToken,
            checkStale: chosen == GrievanceAttachmentVisibility.Shared);
        if (opened.IsFailure)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(opened.Error);
        }

        var grievance = opened.Value.Case;
        var role = opened.Value.Roles.WorkRole;
        var open = grievance.EnsureFilesOpen();
        if (open.IsFailure)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(open.Error);
        }

        var transaction = await unitOfWork.BeginTransactionAsync(GrievanceFiles.LockOf(id), cancellationToken);
        if (transaction.IsFailure)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(transaction.Error);
        }

        await using var scope = transaction.Value;
        if (await read.CountAsync(
                read.Query<GrievanceAttachment>().Where(attachment => attachment.CaseId == id
                    && attachment.RemovedAtUtc == null
                    && attachment.UploadedByRole != GrievanceActorRole.Submitter),
                cancellationToken)
            >= GrievanceAttachment.MaxCaseTeamFilesPerCase)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(AppError.Conflict(GrievanceFiles.TooManyFromCaseTeam));
        }

        var key = Abstractions.IEvidenceStorage.NewKey();
        var created = GrievanceAttachment.Create(
            id, null, null, chosen, steps.Actor(role), file.Value.FileName, file.Value.ContentType, file.Value.Content.LongLength, key, file.Value.Sha256, steps.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<GrievanceAttachmentUploadResult>(created.Error);
        }

        var shared = chosen == GrievanceAttachmentVisibility.Shared;
        if (shared)
        {
            var told = grievance.RecordSubmitterVisibleChange(steps.UtcNow);
            if (told.IsFailure)
            {
                return Result.Failure<GrievanceAttachmentUploadResult>(told.Error);
            }
        }

        var committed = false;
        try
        {
            await files.SaveAsync(key, file.Value.Content, cancellationToken);
            attachments.Add(created.Value);
            var saved = await steps.SaveAsync(
                opened.Value,
                new StepEffects(GrievanceAuditAction.UploadAttachment, "visibility:" + GrievanceText.Visibility.ToText(chosen), role) { TellSubmitter = shared },
                cancellationToken,
                caseChanged: shared);
            if (saved.IsFailure)
            {
                return Result.Failure<GrievanceAttachmentUploadResult>(saved.Error);
            }

            await scope.CommitAsync(cancellationToken);
            committed = true;
            var rowVersion = await reader.RowVersionAsync(id, cancellationToken) ?? grievance.RowVersion;
            return Result.Success(new GrievanceAttachmentUploadResult(GrievanceRedaction.ToTeam(created.Value), rowVersion));
        }
        finally
        {
            if (!committed)
            {
                await files.DiscardAsync(key, id);
            }
        }
    }

    /// <summary>
    /// A corrective action (management Edit) on a submitted case that is not archived; it starts Proposed. The case row does not change
    /// (its row version is only checked for its format).
    /// </summary>
    public async Task<Result<GrievanceCorrectiveActionDto>> AddCorrectiveActionAsync(int id, GrievanceCorrectiveActionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var valid = await createValidator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(
            id, request.RowVersion, (roles, _) => GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly), cancellationToken, checkStale: false);
        if (opened.IsFailure)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(opened.Error);
        }

        if (opened.Value.Case.IsArchived)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(AppError.Conflict("The case is archived and can no longer be changed."));
        }

        var created = GrievanceCorrectiveAction.Create(
            id, new CorrectiveActionDetails(request.ActionType!, request.Description!, request.ResponsibleText, request.DueDate, null, CorrectiveActionStatus.Proposed));
        if (created.IsFailure)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(created.Error);
        }

        actions.Add(created.Value);
        audit.Team(id, GrievanceAuditAction.CorrectiveAction, GrievanceActorRole.Management, "added;status:Proposed");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<GrievanceCorrectiveActionDto>(saved.Error) : Result.Success(GrievanceRedaction.ToDto(created.Value));
    }

    /// <summary>Changes a corrective action (management Edit) with the action's own row version; not on an archived case.</summary>
    public async Task<Result<GrievanceCorrectiveActionDto>> UpdateCorrectiveActionAsync(int actionId, GrievanceCorrectiveActionUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var valid = await updateValidator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(valid.Error);
        }

        var expected = UseCases.RowVersionToken.Decode(request.RowVersion).Value;
        var notFound = UseCases.NotFoundError.For("Corrective action", actionId);
        var action = await actions.FindAsync(actionId, cancellationToken);
        var grievance = action is null ? null : await cases.FindAsync(action.CaseId, await access.CaseTeamScopeAsync(cancellationToken), cancellationToken);
        if (action is null || grievance is null)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(notFound);
        }

        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        if (!roles.ManagementEdit)
        {
            var refused = AppError.Forbidden(GrievanceRefusals.ManagementOnly);
            await steps.RefuseAsync(grievance.Id, roles, refused, cancellationToken);
            return Result.Failure<GrievanceCorrectiveActionDto>(refused);
        }

        if (!action.RowVersion.AsSpan().SequenceEqual(expected))
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(UseCases.RowVersionToken.Stale);
        }

        if (grievance.IsArchived)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(AppError.Conflict("The case is archived and can no longer be changed."));
        }

        actions.ExpectRowVersion(action, expected);
        var status = GrievanceText.CorrectiveActionStatus.Parse(request.Status);
        var updated = action.Update(new CorrectiveActionDetails(request.ActionType!, request.Description!, request.ResponsibleText, request.DueDate, request.CompletedOn, status));
        if (updated.IsFailure)
        {
            return Result.Failure<GrievanceCorrectiveActionDto>(updated.Error);
        }

        audit.Team(grievance.Id, GrievanceAuditAction.CorrectiveAction, GrievanceActorRole.Management,
            string.Create(CultureInfo.InvariantCulture, $"action:{actionId};status:{GrievanceText.CorrectiveActionStatus.ToText(status)}"));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<GrievanceCorrectiveActionDto>(saved.Error) : Result.Success(GrievanceRedaction.ToDto(action));
    }
}
